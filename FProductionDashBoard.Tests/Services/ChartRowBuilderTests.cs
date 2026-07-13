using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FProductionDashBoard.Tests.Services
{
    public class ChartRowBuilderTests
    {
        private static ChartDefinition EquipmentDef(params StatItemConfig[] items) => new()
        {
            Id = "x",
            Name = "X",
            DataSet = ChartDataSet.Equipment,
            StatRow = new StatRowConfig { Items = items.ToList() },
        };

        private static ChartRowViewModel Row(string name, string status)
        {
            var row = new ChartRowViewModel();
            row.Values[FieldCatalog.EquipmentName] = name;
            row.Values[FieldCatalog.ProductionStatus] = status;
            return row;
        }

        [Fact]
        public void BuildStatItems_MoreThanMax_TakesMaxStatItems()
        {
            var items = Enumerable.Range(0, ChartConstants.MaxStatItems + 3)
                .Select(i => new StatItemConfig { Label = $"S{i}" })
                .ToArray();
            var rows = new List<ChartRowViewModel> { Row("A", FieldCatalog.ValIdle) };

            var stats = ChartRowBuilder.BuildStatItems(EquipmentDef(items), rows);

            Assert.Equal(ChartConstants.MaxStatItems, stats.Count);
        }

        [Fact]
        public void BuildStatItems_SumOnEnumField_ToleratesAsZero()
        {
            // 舊定義誤配 Sum＋Enum 欄位：ToDouble 容忍為 0，不擲例外
            var def = EquipmentDef(new StatItemConfig
            {
                Aggregate = ChartAggregateType.Sum,
                FieldId = FieldCatalog.ProductionStatus,
                Label = "S",
            });
            var rows = new List<ChartRowViewModel>
            {
                Row("A", FieldCatalog.ValInProduction),
                Row("B", FieldCatalog.ValIdle),
            };

            var stats = ChartRowBuilder.BuildStatItems(def, rows);

            Assert.Equal("0", Assert.Single(stats).Value);
        }

        [Fact]
        public void FormatValue_NumberFieldWithNonNumericRaw_ToleratesAsRawString()
        {
            // 型別誤配（如欄位新增時型別填錯）：容錯回退為原始值字串，不擲例外
            var field = new ChartFieldDescriptor("Test", "TestKey", ChartFieldType.Number);

            var formatted = ChartRowBuilder.FormatValue(field, "not-a-number");

            Assert.Equal("not-a-number", formatted);
        }

        [Fact]
        public void FormatValue_DateFieldWithNonDateRaw_ToleratesAsRawString()
        {
            var field = new ChartFieldDescriptor("Test", "TestKey", ChartFieldType.Date);

            var formatted = ChartRowBuilder.FormatValue(field, 12345);

            Assert.Equal("12345", formatted);
        }
    }
}
