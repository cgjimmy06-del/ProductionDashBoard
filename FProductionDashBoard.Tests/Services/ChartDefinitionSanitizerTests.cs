using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using Xunit;

namespace FProductionDashBoard.Tests.Services
{
    public class ChartDefinitionSanitizerTests
    {
        [Fact]
        public void Sanitize_DefaultDefinitions_RemovesNothing()
        {
            Assert.Equal(0, ChartDefinitionSanitizer.Sanitize(DefaultChartDefinitions.CreateEquipmentOverview()));
            Assert.Equal(0, ChartDefinitionSanitizer.Sanitize(DefaultChartDefinitions.CreateScheduleBoard()));
        }

        [Fact]
        public void Sanitize_RemovesAllInvalidReferences_ReturnsCount()
        {
            var def = new ChartDefinition
            {
                Id = "x",
                Name = "X",
                DataSet = ChartDataSet.Equipment,
                StatRow = new StatRowConfig
                {
                    Items =
                    {
                        new StatItemConfig(),                                    // FieldId 空＝全列計數，保留
                        new StatItemConfig { FieldId = "Ghost" },                                                  // 1
                        new StatItemConfig { FieldId = FieldCatalog.ProductionStatus, FilterValue = "Nope" },      // 2
                        new StatItemConfig { FieldId = FieldCatalog.ProductionStatus, FilterValue = FieldCatalog.ValIdle },
                    },
                },
                FilterRow = new FilterRowConfig
                {
                    FilterFieldIds = { FieldCatalog.EquipmentName, "Ghost" },                                      // 3
                    QuickButtons =
                    {
                        new QuickFilterButtonConfig { FieldId = FieldCatalog.ProductionStatus, Value = FieldCatalog.ValIdle },
                        new QuickFilterButtonConfig { FieldId = "Ghost", Value = "x" },                            // 4
                        new QuickFilterButtonConfig { FieldId = FieldCatalog.EquipmentName, Value = "x" },         // 5（不可快捷）
                        new QuickFilterButtonConfig { FieldId = FieldCatalog.ProductionStatus, Value = "Nope" },   // 6（值不存在）
                    },
                    SortOptionFieldIds = { FieldCatalog.EquipmentName, "Ghost" },                                  // 7
                    DefaultSortFieldId = "Ghost",                                                                  // 8
                },
                Container = new ContainerConfig
                {
                    Type = ChartContainerType.Card,
                    Card = new CardContainerConfig
                    {
                        BorderColorFieldId = "Ghost",                                                              // 9
                        TitleFieldId = "Ghost",                                                                    // 10
                        Chips =
                        {
                            new ChipConfig { FieldId = FieldCatalog.LoadLevel },
                            new ChipConfig { FieldId = "Ghost" },                                                  // 11
                        },
                        SecondaryFieldIds = { FieldCatalog.CurrentProduct, "Ghost" },                              // 12
                        ProgressNumeratorFieldId = "Ghost",                                                        // 13
                        ProgressDenominatorFieldId = FieldCatalog.ProgressTarget,
                    },
                    Table = new TableContainerConfig
                    {
                        ColumnFieldIds = { FieldCatalog.EquipmentName, "Ghost" },                                  // 14
                        IndicatorFieldId = "Ghost",                                                                // 15
                    },
                },
            };

            var removed = ChartDefinitionSanitizer.Sanitize(def);

            Assert.Equal(15, removed);
            Assert.Equal(2, def.StatRow.Items.Count);
            Assert.Equal(new[] { FieldCatalog.EquipmentName }, def.FilterRow.FilterFieldIds);
            Assert.Single(def.FilterRow.QuickButtons);
            Assert.Equal(new[] { FieldCatalog.EquipmentName }, def.FilterRow.SortOptionFieldIds);
            Assert.Equal("", def.FilterRow.DefaultSortFieldId);
            Assert.Equal("", def.Container.Card.BorderColorFieldId);
            Assert.Equal("", def.Container.Card.TitleFieldId);
            Assert.Single(def.Container.Card.Chips);
            Assert.Equal(new[] { FieldCatalog.CurrentProduct }, def.Container.Card.SecondaryFieldIds);
            Assert.Null(def.Container.Card.ProgressNumeratorFieldId);
            Assert.Equal(FieldCatalog.ProgressTarget, def.Container.Card.ProgressDenominatorFieldId);
            Assert.Equal(new[] { FieldCatalog.EquipmentName }, def.Container.Table.ColumnFieldIds);
            Assert.Null(def.Container.Table.IndicatorFieldId);
        }

        [Fact]
        public void Sanitize_UnknownDataSet_ResetsToEquipment_AndDropsForeignFields()
        {
            var def = new ChartDefinition
            {
                Id = "x",
                Name = "X",
                DataSet = (ChartDataSet)99,
                FilterRow = new FilterRowConfig
                {
                    // 排程視角欄位在 Equipment 目錄不存在，重設後一併剔除
                    FilterFieldIds = { FieldCatalog.ScheduleStatus },
                },
            };

            var removed = ChartDefinitionSanitizer.Sanitize(def);

            Assert.Equal(ChartDataSet.Equipment, def.DataSet);
            Assert.Empty(def.FilterRow.FilterFieldIds);
            Assert.Equal(2, removed);   // DataSet 重設 + 1 個失效篩選欄位
        }

        [Fact]
        public void Sanitize_UnknownContainerType_ResetsToCard()
        {
            var def = new ChartDefinition
            {
                Id = "x",
                Name = "X",
                DataSet = ChartDataSet.Equipment,
                Container = new ContainerConfig { Type = (ChartContainerType)99 },
            };

            var removed = ChartDefinitionSanitizer.Sanitize(def);

            Assert.Equal(ChartContainerType.Card, def.Container.Type);
            Assert.Equal(1, removed);
        }
    }
}
