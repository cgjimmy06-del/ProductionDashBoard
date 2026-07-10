using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using FProductionDashBoard.ViewModels;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class ChartDesignerViewModelTests
    {
        /// <summary>可觀察 Save/Delete 呼叫的假 store（設計器不經檔案 IO）</summary>
        private class FakeStore : IChartDefinitionStore
        {
            public List<ChartDefinition> Definitions { get; } = new();
            public List<ChartDefinition> SavedCalls { get; } = new();
            public bool ThrowOnSave { get; set; }

            public IReadOnlyList<ChartDefinition> Load() => Definitions.ToList();

            public void Save(ChartDefinition definition)
            {
                if (ThrowOnSave) throw new InvalidOperationException("[Save] 測試用失敗");
                SavedCalls.Add(definition);
            }

            public void Delete(string id) { }
        }

        private static ChartDesignerViewModel CreateDesigner(
            ChartDefinition source, FakeStore store, Action<bool>? onClose = null, bool isNew = false)
        {
            var core = new DashboardCoreServices(
                new LogService(), new Mock<IDataService>().Object,
                new AuthorizationService(), new Mock<ICardReaderService>().Object);
            return new ChartDesignerViewModel(source, isNew, store, core, onClose ?? (_ => { }));
        }

        // --- deep-clone 隔離 ---

        [Fact]
        public void Ctor_DeepClones_EditsDoNotAffectSource()
        {
            var source = DefaultChartDefinitions.CreateScheduleBoard();
            var vm = CreateDesigner(source, new FakeStore());

            vm.WorkingDefinition.Name = "changed";
            vm.WorkingDefinition.FilterRow.FilterFieldIds.Clear();
            vm.WorkingDefinition.Container.Table.ColumnFieldIds.Clear();

            Assert.NotSame(source, vm.WorkingDefinition);
            Assert.NotEqual("changed", source.Name);
            Assert.Equal(3, source.FilterRow.FilterFieldIds.Count);
            Assert.Equal(7, source.Container.Table.ColumnFieldIds.Count);
        }

        // --- 儲存 / 取消 ---

        [Fact]
        public void Cancel_DoesNotSave_ClosesWithFalse()
        {
            var store = new FakeStore();
            bool? closedSaved = null;
            var vm = CreateDesigner(DefaultChartDefinitions.CreateScheduleBoard(), store, s => closedSaved = s);

            vm.CancelCommand.Execute(null);

            Assert.False(closedSaved);
            Assert.Empty(store.SavedCalls);
        }

        [Fact]
        public void Save_PersistsWorkingDefinition_ClosesWithTrue()
        {
            var store = new FakeStore();
            bool? closedSaved = null;
            var vm = CreateDesigner(DefaultChartDefinitions.CreateScheduleBoard(), store, s => closedSaved = s);

            vm.SaveCommand.Execute(null);

            Assert.True(closedSaved);
            var saved = Assert.Single(store.SavedCalls);
            Assert.Same(vm.WorkingDefinition, saved);
        }

        [Fact]
        public void Save_StoreThrows_StaysOpen()
        {
            var store = new FakeStore { ThrowOnSave = true };
            bool? closedSaved = null;
            var vm = CreateDesigner(DefaultChartDefinitions.CreateScheduleBoard(), store, s => closedSaved = s);

            vm.SaveCommand.Execute(null);

            Assert.Null(closedSaved);   // 未關閉，錯誤走 AddLog/AddErrorLog
        }

        // --- 開啟時淨化 ---

        [Fact]
        public void Ctor_SanitizesInvalidReferences_ExposesNotice()
        {
            var source = DefaultChartDefinitions.CreateEquipmentOverview();
            source.Container.Card.Chips.Add(new ChipConfig { FieldId = "Ghost" });
            source.FilterRow.SortOptionFieldIds.Add("Ghost");

            var vm = CreateDesigner(source, new FakeStore());

            Assert.Equal(2, vm.SanitizedCount);
            Assert.True(vm.HasSanitizeNotice);
            Assert.Contains("2", vm.SanitizeNotice);
            Assert.DoesNotContain(vm.WorkingDefinition.Container.Card.Chips, c => c.FieldId == "Ghost");
            // 淨化只作用於 clone，不改寫來源定義
            Assert.Contains(source.Container.Card.Chips, c => c.FieldId == "Ghost");
        }

        [Fact]
        public void Ctor_UnknownDataSet_ResetsAndPreviewStillRenders()
        {
            var source = new ChartDefinition { Id = "x", Name = "X", DataSet = (ChartDataSet)99 };

            var vm = CreateDesigner(source, new FakeStore());

            Assert.Equal(ChartDataSet.Equipment, vm.WorkingDefinition.DataSet);
            Assert.True(vm.HasSanitizeNotice);
            Assert.Equal(3, vm.PreviewRows.Count);
        }

        // --- 左預覽（假資料渲染） ---

        [Fact]
        public void Preview_EquipmentOverview_BuildsCardWallStatsAndFilterHints()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateEquipmentOverview(), new FakeStore());

            Assert.True(vm.IsPreviewCard);
            Assert.False(vm.IsPreviewTable);
            Assert.Equal(3, vm.PreviewRows.Count);
            Assert.Single(vm.PreviewSampleRows);
            Assert.All(vm.PreviewRows, r => Assert.False(string.IsNullOrEmpty(r.Title)));

            Assert.True(vm.IsPreviewStatVisible);
            Assert.Equal(4, vm.PreviewStatItems.Count);
            Assert.Equal("3", vm.PreviewStatItems[0].Value);   // 樣本 3 台設備

            Assert.True(vm.IsPreviewFilterVisible);
            Assert.Equal(2, vm.PreviewFilterLabels.Count);
            Assert.Equal(4, vm.PreviewQuickLabels.Count);
            Assert.NotNull(vm.PreviewSortLabel);
        }

        [Fact]
        public void Preview_ScheduleBoard_BuildsTableColumns()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateScheduleBoard(), new FakeStore());

            Assert.True(vm.IsPreviewTable);
            Assert.False(vm.IsPreviewCard);
            Assert.Equal(7, vm.PreviewTableColumns.Count);
            Assert.Equal(FieldCatalog.ScheduleStatus, vm.PreviewIndicatorFieldId);
            Assert.Equal(3, vm.PreviewRows.Count);
        }
    }
}
