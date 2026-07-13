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
            ChartDefinition source, FakeStore store, Action<bool>? onClose = null, bool isNew = false,
            Mock<IDialogService>? dialogMock = null)
        {
            var core = new DashboardCoreServices(
                new LogService(), new Mock<IDataService>().Object,
                new AuthorizationService(), new Mock<ICardReaderService>().Object);
            var dialog = dialogMock ?? new Mock<IDialogService>();
            return new ChartDesignerViewModel(source, isNew, store, core, dialog.Object, onClose ?? (_ => { }));
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

        // --- C2：基本區 ---

        [Fact]
        public void ChartName_Edit_WritesNameAndClearsNameKey()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateScheduleBoard(), new FakeStore());

            vm.ChartName = "我的看板";

            Assert.Equal("我的看板", vm.WorkingDefinition.Name);
            Assert.Null(vm.WorkingDefinition.NameKey);
            Assert.Equal("我的看板", vm.DisplayName);
        }

        [Fact]
        public void SortOrder_Edit_WritesThrough()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateScheduleBoard(), new FakeStore());

            vm.SortOrder = 7;

            Assert.Equal(7, vm.WorkingDefinition.SortOrder);
        }

        [Fact]
        public void DataSetSwitch_Confirmed_ResetsSelections_AndPrefillsTitleField()
        {
            var dialog = new Mock<IDialogService>();
            dialog.Setup(d => d.ShowConfirm(It.IsAny<string>())).Returns(true);
            var vm = CreateDesigner(DefaultChartDefinitions.CreateEquipmentOverview(), new FakeStore(),
                dialogMock: dialog);

            vm.SelectedDataSet = vm.DataSetOptions.First(o => o.Value == ChartDataSet.Schedule);

            var def = vm.WorkingDefinition;
            Assert.Equal(ChartDataSet.Schedule, def.DataSet);
            Assert.Empty(def.StatRow.Items);
            Assert.Empty(def.FilterRow.FilterFieldIds);
            Assert.Empty(def.FilterRow.QuickButtons);
            Assert.Empty(def.FilterRow.SortOptionFieldIds);
            Assert.Equal("", def.FilterRow.DefaultSortFieldId);
            Assert.Equal(FieldCatalog.ScheduleId, def.Container.Card.TitleFieldId);   // 新資料集第一欄
            Assert.Empty(def.Container.Card.Chips);
            Assert.Null(def.Container.Card.ProgressNumeratorFieldId);
            Assert.Empty(def.Container.Table.ColumnFieldIds);
            Assert.Empty(vm.StatItemEditors);
            Assert.Equal(3, vm.PreviewRows.Count);   // 預覽改用排程樣本
        }

        [Fact]
        public void DataSetSwitch_Declined_RevertsSelection_KeepsDefinition()
        {
            var dialog = new Mock<IDialogService>();
            dialog.Setup(d => d.ShowConfirm(It.IsAny<string>())).Returns(false);
            var vm = CreateDesigner(DefaultChartDefinitions.CreateEquipmentOverview(), new FakeStore(),
                dialogMock: dialog);

            vm.SelectedDataSet = vm.DataSetOptions.First(o => o.Value == ChartDataSet.Schedule);

            Assert.Equal(ChartDataSet.Equipment, vm.SelectedDataSet?.Value);   // 已還原
            Assert.Equal(ChartDataSet.Equipment, vm.WorkingDefinition.DataSet);
            Assert.NotEmpty(vm.WorkingDefinition.StatRow.Items);               // 定義未被重置
        }

        // --- C2：統計列 ---

        [Fact]
        public void AddStatItem_UpToLimit_ThenDisabled()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateEquipmentOverview(), new FakeStore());
            Assert.Equal(4, vm.StatItemEditors.Count);   // 種子 4 項

            while (vm.CanAddStatItem)
                vm.AddStatItemCommand.Execute(null);

            Assert.Equal(ChartConstants.MaxStatItems, vm.StatItemEditors.Count);
            Assert.Equal(ChartConstants.MaxStatItems, vm.WorkingDefinition.StatRow.Items.Count);
            Assert.False(vm.AddStatItemCommand.CanExecute(null));

            vm.RemoveStatItemCommand.Execute(vm.StatItemEditors[^1]);
            Assert.True(vm.AddStatItemCommand.CanExecute(null));
            Assert.Equal(ChartConstants.MaxStatItems - 1, vm.WorkingDefinition.StatRow.Items.Count);
        }

        [Fact]
        public void StatItemEditor_LabelEdit_ClearsLabelKey()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateEquipmentOverview(), new FakeStore());
            var editor = vm.StatItemEditors[0];   // 種子第一項使用 LabelKey

            editor.Label = "自訂標籤";

            Assert.Equal("自訂標籤", editor.Config.Label);
            Assert.Null(editor.Config.LabelKey);
        }

        [Fact]
        public void StatItemEditor_SumAggregate_RestrictsToNumberFields()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateEquipmentOverview(), new FakeStore());
            var editor = vm.StatItemEditors[1];   // 生產狀態（Enum）計數項

            editor.SelectedAggregate = editor.AggregateOptions.First(o => o.Value == ChartAggregateType.Sum);

            Assert.All(editor.FieldOptions, o =>
                Assert.Equal(ChartFieldType.Number,
                    FieldCatalog.Find(ChartDataSet.Equipment, o.FieldId)!.Type));
            Assert.Equal(ChartAggregateType.Sum, editor.Config.Aggregate);
            // 原 Enum 欄位不在 Sum 選項內 → 自動落到第一個 Number 欄位
            Assert.Contains(editor.FieldOptions, o => o.FieldId == editor.Config.FieldId);
        }

        [Fact]
        public void StatRowToggle_Off_HidesPreviewStats()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateEquipmentOverview(), new FakeStore());
            Assert.True(vm.IsPreviewStatVisible);

            vm.IsStatRowEnabled = false;

            Assert.False(vm.WorkingDefinition.StatRow.Enabled);
            Assert.False(vm.IsPreviewStatVisible);
        }

        // --- C2：篩選列三清單 ---

        [Fact]
        public void AddFilterField_UpToLimit_ThenDisabled_AndSyncsDefinition()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateEquipmentOverview(), new FakeStore());
            Assert.Equal(2, vm.FilterFieldEditors.Count);   // 種子 2 項

            vm.AddFilterFieldCommand.Execute(null);

            Assert.Equal(3, vm.FilterFieldEditors.Count);
            Assert.Equal(3, vm.WorkingDefinition.FilterRow.FilterFieldIds.Count);
            Assert.False(vm.AddFilterFieldCommand.CanExecute(null));

            vm.RemoveFilterFieldCommand.Execute(vm.FilterFieldEditors[0]);
            Assert.Equal(2, vm.WorkingDefinition.FilterRow.FilterFieldIds.Count);
            Assert.True(vm.AddFilterFieldCommand.CanExecute(null));
        }

        [Fact]
        public void QuickButtonEditor_FieldChange_ResetsValueToFirstOfNewField()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateEquipmentOverview(), new FakeStore());
            var editor = vm.QuickButtonEditors[0];   // 生產狀態=生產中

            editor.SelectedField = editor.FieldOptions.First(o => o.FieldId == FieldCatalog.LoadLevel);

            Assert.Equal(FieldCatalog.LoadLevel, editor.Config.FieldId);
            Assert.Equal(FieldCatalog.ValLoadNone, editor.Config.Value);   // 新欄位第一個值
            Assert.All(editor.ValueOptions, o => Assert.NotNull(o.Value)); // 快捷按鈕無「無」選項
        }

        [Fact]
        public void AddQuickButton_UpToLimit_ThenDisabled()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateEquipmentOverview(), new FakeStore());
            Assert.Equal(4, vm.QuickButtonEditors.Count);   // 種子 4 顆

            vm.AddQuickButtonCommand.Execute(null);

            Assert.Equal(5, vm.QuickButtonEditors.Count);
            Assert.Equal(5, vm.WorkingDefinition.FilterRow.QuickButtons.Count);
            Assert.False(vm.AddQuickButtonCommand.CanExecute(null));
        }

        [Fact]
        public void RemoveSortOption_ThatIsDefaultSort_FallsBackToTitle()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateEquipmentOverview(), new FakeStore());
            // 種子預設排序＝設備名稱
            Assert.Equal(FieldCatalog.EquipmentName, vm.SelectedDefaultSort?.FieldId);

            var target = vm.SortOptionEditors.First(e => e.SelectedField?.FieldId == FieldCatalog.EquipmentName);
            vm.RemoveSortOptionCommand.Execute(target);

            Assert.Equal("", vm.WorkingDefinition.FilterRow.DefaultSortFieldId);   // 落回主名稱
            Assert.Equal("", vm.SelectedDefaultSort?.FieldId);
            Assert.Equal(2, vm.WorkingDefinition.FilterRow.SortOptionFieldIds.Count);
        }

        [Fact]
        public void DefaultSortDirection_Toggle_WritesThrough()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateEquipmentOverview(), new FakeStore());

            vm.IsDefaultSortDescending = true;

            Assert.Equal(ChartSortDirection.Descending, vm.WorkingDefinition.FilterRow.DefaultSortDirection);
        }

        // --- C3：儲存驗證（行內錯誤，不寫 store） ---

        private static ChartDefinition CustomCardDef(string name = "自訂圖表") => new()
        {
            Id = "custom-1",
            Name = name,
            DataSet = ChartDataSet.Equipment,
            Container = new ContainerConfig
            {
                Type = ChartContainerType.Card,
                Card = new CardContainerConfig { TitleFieldId = FieldCatalog.EquipmentName },
            },
        };

        [Fact]
        public void Save_EmptyName_SetsValidationError_NotSaved()
        {
            var store = new FakeStore();
            bool? closed = null;
            var vm = CreateDesigner(CustomCardDef(name: "   "), store, s => closed = s);

            vm.SaveCommand.Execute(null);

            Assert.NotNull(vm.ValidationError);
            Assert.Null(closed);
            Assert.Empty(store.SavedCalls);
        }

        [Fact]
        public void Save_DuplicateName_SetsValidationError()
        {
            var store = new FakeStore();
            store.Definitions.Add(new ChartDefinition { Id = "other", Name = "戰情室" });
            var vm = CreateDesigner(CustomCardDef(name: "戰情室"), store);

            vm.SaveCommand.Execute(null);

            Assert.NotNull(vm.ValidationError);
            Assert.Empty(store.SavedCalls);
        }

        [Fact]
        public void Save_CardWithoutTitleField_SetsValidationError()
        {
            var def = CustomCardDef();
            def.Container.Card.TitleFieldId = "";
            var store = new FakeStore();
            var vm = CreateDesigner(def, store);

            vm.SaveCommand.Execute(null);

            Assert.NotNull(vm.ValidationError);
            Assert.Empty(store.SavedCalls);
        }

        [Fact]
        public void Save_TableWithoutColumns_SetsValidationError()
        {
            var def = CustomCardDef();
            def.Container.Type = ChartContainerType.Table;
            var store = new FakeStore();
            var vm = CreateDesigner(def, store);

            vm.SaveCommand.Execute(null);

            Assert.NotNull(vm.ValidationError);
            Assert.Empty(store.SavedCalls);
        }

        [Fact]
        public void Save_Valid_TrimsName_ClearsErrorAndCloses()
        {
            var store = new FakeStore();
            bool? closed = null;
            var vm = CreateDesigner(CustomCardDef(name: "  我的圖表  "), store, s => closed = s);

            vm.SaveCommand.Execute(null);

            Assert.Null(vm.ValidationError);
            Assert.True(closed);
            Assert.Equal("我的圖表", Assert.Single(store.SavedCalls).Name);
        }

        // --- C3：容器編輯 ---

        [Fact]
        public void ContainerTypeSwitch_ToTable_SwitchesPreview_KeepsCardConfig()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateEquipmentOverview(), new FakeStore());
            Assert.True(vm.IsPreviewCard);

            vm.SelectedContainerType = vm.ContainerTypeOptions.First(o => o.Value == ChartContainerType.Table);

            Assert.Equal(ChartContainerType.Table, vm.WorkingDefinition.Container.Type);
            Assert.True(vm.IsPreviewTable);
            Assert.False(vm.IsPreviewCard);
            // 切換不清空卡片設定
            Assert.Equal(FieldCatalog.EquipmentName, vm.WorkingDefinition.Container.Card.TitleFieldId);
            Assert.NotEmpty(vm.WorkingDefinition.Container.Card.Chips);
        }

        [Fact]
        public void AddChip_UpToLimit_ThenDisabled_WritesThrough()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateEquipmentOverview(), new FakeStore());
            Assert.Equal(2, vm.ChipEditors.Count);   // 種子 2 個 chip

            vm.AddChipCommand.Execute(null);

            Assert.Equal(3, vm.ChipEditors.Count);
            Assert.Equal(3, vm.WorkingDefinition.Container.Card.Chips.Count);
            Assert.False(vm.AddChipCommand.CanExecute(null));

            var editor = vm.ChipEditors[^1];
            editor.ShowOnlyWhenHasValue = false;
            Assert.False(editor.Config.ShowOnlyWhenHasValue);

            vm.RemoveChipCommand.Execute(editor);
            Assert.Equal(2, vm.WorkingDefinition.Container.Card.Chips.Count);
            Assert.True(vm.AddChipCommand.CanExecute(null));
        }

        [Fact]
        public void AddSecondary_UpToLimit_SyncsDefinition()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateEquipmentOverview(), new FakeStore());
            Assert.Equal(2, vm.SecondaryEditors.Count);   // 種子 2 項

            vm.AddSecondaryCommand.Execute(null);

            Assert.Equal(3, vm.WorkingDefinition.Container.Card.SecondaryFieldIds.Count);
            Assert.False(vm.AddSecondaryCommand.CanExecute(null));

            vm.RemoveSecondaryCommand.Execute(vm.SecondaryEditors[0]);
            Assert.Equal(2, vm.WorkingDefinition.Container.Card.SecondaryFieldIds.Count);
        }

        [Fact]
        public void MoveTableColumn_UpAndDown_ReordersDefinition()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateScheduleBoard(), new FakeStore());
            var original = vm.WorkingDefinition.Container.Table.ColumnFieldIds.ToList();
            var second = vm.TableColumnEditors[1];

            vm.MoveTableColumnUpCommand.Execute(second);
            Assert.Equal(original[1], vm.WorkingDefinition.Container.Table.ColumnFieldIds[0]);
            Assert.Equal(original[0], vm.WorkingDefinition.Container.Table.ColumnFieldIds[1]);

            vm.MoveTableColumnDownCommand.Execute(second);
            Assert.Equal(original, vm.WorkingDefinition.Container.Table.ColumnFieldIds);

            // 邊界：第一列上移不動作
            vm.MoveTableColumnUpCommand.Execute(vm.TableColumnEditors[0]);
            Assert.Equal(original, vm.WorkingDefinition.Container.Table.ColumnFieldIds);
        }

        [Fact]
        public void Indicator_SetNone_WritesNull()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateScheduleBoard(), new FakeStore());
            Assert.Equal(FieldCatalog.ScheduleStatus, vm.WorkingDefinition.Container.Table.IndicatorFieldId);

            vm.SelectedIndicator = vm.IndicatorOptions.First(o => o.FieldId == "");

            Assert.Null(vm.WorkingDefinition.Container.Table.IndicatorFieldId);
        }

        [Fact]
        public void ProgressFields_SetNone_WritesNull_HidesPreviewProgress()
        {
            var vm = CreateDesigner(DefaultChartDefinitions.CreateEquipmentOverview(), new FakeStore());
            Assert.True(vm.PreviewRows[0].HasProgress);

            vm.SelectedProgressNumerator = vm.ProgressFieldOptions.First(o => o.FieldId == "");

            Assert.Null(vm.WorkingDefinition.Container.Card.ProgressNumeratorFieldId);
            Assert.False(vm.PreviewRows[0].HasProgress);   // 分子清空＝不顯示進度條
        }
    }
}
