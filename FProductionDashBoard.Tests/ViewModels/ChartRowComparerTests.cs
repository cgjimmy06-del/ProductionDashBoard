using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class ChartRowComparerTests
    {
        private static ChartRowViewModel Row(string fieldId, object? value, string title)
        {
            var row = new ChartRowViewModel { Title = title };
            row.Values[fieldId] = value;
            return row;
        }

        [Fact]
        public void Compare_NumberFieldWithNonNumericValue_TolerantlyFallsBackToTieBreak()
        {
            // 型別誤配（如欄位新增時型別填錯）：容錯視為無法比較，回退主名稱排序，不擲例外
            var comparer = ChartRowComparer.Create(
                ChartDataSet.Equipment, FieldCatalog.ActiveOrderCount, ChartSortDirection.Ascending);
            var a = Row(FieldCatalog.ActiveOrderCount, "not-a-number", "A");
            var b = Row(FieldCatalog.ActiveOrderCount, "also-not-a-number", "B");

            var result = comparer.Compare(a, b);

            Assert.True(result < 0);   // TieBreak：依 Title 字母序 A < B
        }

        [Fact]
        public void Compare_DateFieldWithNonDateValue_TolerantlyFallsBackToTieBreak()
        {
            var comparer = ChartRowComparer.Create(
                ChartDataSet.Schedule, FieldCatalog.ReceivedAt, ChartSortDirection.Ascending);
            var a = Row(FieldCatalog.ReceivedAt, 12345, "A");
            var b = Row(FieldCatalog.ReceivedAt, 67890, "B");

            var result = comparer.Compare(a, b);

            Assert.True(result < 0);
        }
    }
}
