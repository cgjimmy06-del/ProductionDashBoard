using System.Collections.Generic;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using Moq;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    // 只測不碰 Dispatcher / MessageBox 的純切片：QuickOrder 過濾、頁面導航 + IsConfirmVisible、CanConfirm 閘門。
    // 載入訂單/明細（LoadOrdersAsync）與取消/結束（MessageBox）流程不在此涵蓋（需 WPF 環境）。
    public class OrderListDialogViewModelTests
    {
        private static OrderListDialogViewModel CreateVm()
        {
            var core = new DashboardCoreServices(
                new LogService(),
                new Mock<IDataService>().Object,
                new AuthorizationService(),
                new Mock<ICardReaderService>().Object,
                new Mock<IWarehouseService>().Object);
            var eps = new List<EquipmentProduct>
            {
                new() { EquipmentProductId = 10, SopId = 1, SeqNo = 2, ProductionStatus = TuningType.Feasible },
                new() { EquipmentProductId = 11, SopId = 2, SeqNo = 1, ProductionStatus = TuningType.Feasible },
            };
            var user = new UserInfo { UserId = "u1", Name = "n1" };
            return new OrderListDialogViewModel(core, 1, eps, user, "t");
        }

        [Fact]
        public void Ctor_BuildsFilteredProducts_SortedBySeqNo()
        {
            var vm = CreateVm();

            Assert.Equal(2, vm.FilteredEquipmentProducts.Count);
            Assert.True(vm.FilteredEquipmentProducts[0].SeqNo <= vm.FilteredEquipmentProducts[1].SeqNo);
        }

        [Fact]
        public void QuickOrderFilter_NoMatch_EmptiesThenRestores()
        {
            var vm = CreateVm();

            vm.QuickOrderFilterText = "ZZZ_NO_MATCH";
            Assert.Empty(vm.FilteredEquipmentProducts);

            vm.QuickOrderFilterText = "";
            Assert.Equal(2, vm.FilteredEquipmentProducts.Count);
        }

        [Fact]
        public void NavigateToQuickOrder_ShowsConfirm_AndSetsPage()
        {
            var vm = CreateVm();

            vm.NavigateToQuickOrderCommand.Execute(null);

            Assert.Equal(OrderListPage.QuickOrder, vm.CurrentPage);
            Assert.True(vm.IsConfirmVisible);
        }

        [Fact]
        public void CancelNav_FromSubPage_ReturnsToOrderList()
        {
            var vm = CreateVm();
            vm.NavigateToQuickOrderCommand.Execute(null);

            vm.CancelCommand.Execute(null); // 非 OrderList 頁 → 返回列表頁

            Assert.Equal(OrderListPage.OrderList, vm.CurrentPage);
            Assert.False(vm.IsConfirmVisible);
        }

        [Fact]
        public void CanConfirm_OnQuickOrder_RequiresSelection()
        {
            var vm = CreateVm();
            vm.NavigateToQuickOrderCommand.Execute(null); // 清空選取

            Assert.False(vm.ConfirmCommand.CanExecute(null));

            vm.SelectedEquipmentProduct = vm.FilteredEquipmentProducts[0];
            Assert.True(vm.ConfirmCommand.CanExecute(null));
        }
    }
}
