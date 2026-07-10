using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FProductionDashBoard.Tests.Services
{
    /// <summary>
    /// 欄位目錄與預設圖表定義的完整性檢查：
    /// 目錄宣告（標籤 key / rank / 型別旗標）與預設定義引用的一致性，避免改目錄漏改預設圖表。
    /// </summary>
    public class FieldCatalogTests
    {
        private static readonly ChartDataSet[] AllDataSets = { ChartDataSet.Equipment, ChartDataSet.Schedule };

        private static string? GetResource(string key)
            => FProductionDashBoard.Properties.Resources.ResourceManager.GetString(key);

        // --- 目錄完整性 ---

        [Fact]
        public void AllDataSets_FieldIds_AreUnique()
        {
            foreach (var ds in AllDataSets)
            {
                var ids = FieldCatalog.For(ds).Select(f => f.FieldId).ToList();
                Assert.Equal(ids.Count, ids.Distinct().Count());
            }
        }

        [Fact]
        public void AllFields_LabelKeys_ExistInResources()
        {
            foreach (var field in AllDataSets.SelectMany(FieldCatalog.For))
                Assert.False(string.IsNullOrEmpty(GetResource(field.LabelKey)),
                    $"欄位 {field.FieldId} 的標籤 key {field.LabelKey} 不存在於 Resources.resx");
        }

        [Fact]
        public void EnumFields_HaveValues_WithUniqueRanksAndExistingLabelKeys()
        {
            foreach (var field in AllDataSets.SelectMany(FieldCatalog.For)
                                             .Where(f => f.Type == ChartFieldType.Enum))
            {
                Assert.True(field.Values.Count > 0, $"Enum 欄位 {field.FieldId} 未宣告值清單");

                var ranks = field.Values.Select(v => v.Rank).ToList();
                Assert.Equal(ranks.Count, ranks.Distinct().Count());

                foreach (var value in field.Values)
                    Assert.False(string.IsNullOrEmpty(GetResource(value.LabelKey)),
                        $"欄位 {field.FieldId} 值 {value.Value} 的標籤 key {value.LabelKey} 不存在");
            }
        }

        [Fact]
        public void NonEnumFields_HaveNoValues()
        {
            foreach (var field in AllDataSets.SelectMany(FieldCatalog.For)
                                             .Where(f => f.Type != ChartFieldType.Enum))
                Assert.Empty(field.Values);
        }

        [Fact]
        public void QuickFilterFields_AreEnumType()
        {
            foreach (var field in AllDataSets.SelectMany(FieldCatalog.For).Where(f => f.CanQuickFilter))
                Assert.Equal(ChartFieldType.Enum, field.Type);
        }

        // --- 預設圖表定義一致性 ---

        public static IEnumerable<object[]> DefaultDefinitions()
            => DefaultChartDefinitions.Create().Select(d => new object[] { d });

        [Theory]
        [MemberData(nameof(DefaultDefinitions))]
        public void Defaults_ReferenceExistingFieldIds(ChartDefinition def)
        {
            void AssertField(string fieldId, string where)
            {
                if (string.IsNullOrEmpty(fieldId)) return; // Count 全部列可為空
                Assert.True(FieldCatalog.Find(def.DataSet, fieldId) != null,
                    $"{def.Id} 的 {where} 引用不存在的欄位 {fieldId}");
            }

            foreach (var s in def.StatRow.Items) AssertField(s.FieldId, "統計列");
            foreach (var f in def.FilterRow.FilterFieldIds) AssertField(f, "篩選欄位");
            foreach (var s in def.FilterRow.SortOptionFieldIds) AssertField(s, "排序選項");
            AssertField(def.FilterRow.DefaultSortFieldId, "預設排序");

            foreach (var b in def.FilterRow.QuickButtons)
            {
                var field = FieldCatalog.Find(def.DataSet, b.FieldId);
                Assert.True(field?.CanQuickFilter == true,
                    $"{def.Id} 快捷按鈕欄位 {b.FieldId} 不存在或不可作快捷");
                Assert.Contains(field!.Values, v => v.Value == b.Value);
            }

            if (def.Container.Type == ChartContainerType.Card)
            {
                var card = def.Container.Card;
                AssertField(card.BorderColorFieldId, "卡片邊框色");
                AssertField(card.TitleFieldId, "卡片主名稱");
                foreach (var c in card.Chips) AssertField(c.FieldId, "卡片 chip");
                foreach (var s in card.SecondaryFieldIds) AssertField(s, "卡片次要資訊");
                if (card.ProgressNumeratorFieldId != null) AssertField(card.ProgressNumeratorFieldId, "進度分子");
                if (card.ProgressDenominatorFieldId != null) AssertField(card.ProgressDenominatorFieldId, "進度分母");
            }
            else if (def.Container.Type == ChartContainerType.Table)
            {
                foreach (var c in def.Container.Table.ColumnFieldIds) AssertField(c, "表格欄位");
                if (def.Container.Table.IndicatorFieldId != null)
                    AssertField(def.Container.Table.IndicatorFieldId, "表格燈號");
            }
        }

        [Theory]
        [MemberData(nameof(DefaultDefinitions))]
        public void Defaults_RespectLimits(ChartDefinition def)
        {
            Assert.True(def.FilterRow.FilterFieldIds.Count <= ChartConstants.MaxFilterFields);
            Assert.True(def.FilterRow.QuickButtons.Count <= ChartConstants.MaxQuickFilterButtons);
            Assert.True(def.FilterRow.SortOptionFieldIds.Count <= ChartConstants.MaxSortOptions);
            Assert.True(def.Container.Card.Chips.Count <= ChartConstants.MaxChips);
            Assert.True(def.Container.Card.SecondaryFieldIds.Count <= ChartConstants.MaxSecondaryInfos);
        }

        [Theory]
        [MemberData(nameof(DefaultDefinitions))]
        public void Defaults_LabelKeys_ExistInResources(ChartDefinition def)
        {
            Assert.False(string.IsNullOrEmpty(def.NameKey));
            Assert.False(string.IsNullOrEmpty(GetResource(def.NameKey!)));

            foreach (var s in def.StatRow.Items.Where(i => i.LabelKey != null))
                Assert.False(string.IsNullOrEmpty(GetResource(s.LabelKey!)),
                    $"{def.Id} 統計標籤 key {s.LabelKey} 不存在");

            foreach (var b in def.FilterRow.QuickButtons.Where(x => x.LabelKey != null))
                Assert.False(string.IsNullOrEmpty(GetResource(b.LabelKey!)),
                    $"{def.Id} 快捷按鈕標籤 key {b.LabelKey} 不存在");
        }

        [Fact]
        public void Defaults_OnlyEquipmentOverview_IsLockedDefault()
        {
            var defaults = DefaultChartDefinitions.Create();

            Assert.True(defaults.Single(d => d.Id == DefaultChartDefinitions.EquipmentOverviewId).IsDefault);
            Assert.False(defaults.Single(d => d.Id == DefaultChartDefinitions.ScheduleBoardId).IsDefault);
        }
    }
}
