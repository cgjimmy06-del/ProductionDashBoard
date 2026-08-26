using FProductionDashBoard.Models;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using System.Collections.Generic;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class ScheduleOperationDialogViewModelTests
    {
        private static ScheduleUiModel MakeSchedule(int? actualQty = null, int quantity = 100) => new()
        {
            ScheduleId     = 1,
            Quantity       = quantity,
            ActualQuantity = actualQty,
            Status         = ScheduleStatus.Scheduled,
        };

        private static OrderProductionInfo MakeOrder(string equipmentName, OrderProductionStatus status, int? qty = 10) => new()
        {
            EquipmentName = equipmentName,
            Status        = status,
            Quantity      = qty,
        };

        private static ScheduleOperationDialogViewModel MakeVerifyVm(IReadOnlyList<OrderProductionInfo> orders)
            => new(ScheduleOperationType.Verify, MakeSchedule(actualQty: 80), string.Empty, orders);

        // ─── AllOrdersCompleted（D1：排除 Cancelled 後不得有 Pending/InProduction） ───

        [Fact]
        public void AllOrdersCompleted_AllCompleted_ReturnsTrue()
        {
            var vm = MakeVerifyVm(new[]
            {
                MakeOrder("CNC-01", OrderProductionStatus.Completed),
                MakeOrder("CNC-02", OrderProductionStatus.Completed),
            });
            Assert.True(vm.AllOrdersCompleted);
        }

        [Fact]
        public void AllOrdersCompleted_CompletedPlusCancelled_ReturnsTrue()
        {
            // D2 邊界：Cancelled 不阻擋
            var vm = MakeVerifyVm(new[]
            {
                MakeOrder("CNC-01", OrderProductionStatus.Completed),
                MakeOrder("CNC-02", OrderProductionStatus.Cancelled),
            });
            Assert.True(vm.AllOrdersCompleted);
        }

        [Fact]
        public void AllOrdersCompleted_HasPending_ReturnsFalse()
        {
            var vm = MakeVerifyVm(new[]
            {
                MakeOrder("CNC-01", OrderProductionStatus.Completed),
                MakeOrder("CNC-02", OrderProductionStatus.Pending),
            });
            Assert.False(vm.AllOrdersCompleted);
        }

        [Fact]
        public void AllOrdersCompleted_HasInProduction_ReturnsFalse()
        {
            var vm = MakeVerifyVm(new[]
            {
                MakeOrder("CNC-01", OrderProductionStatus.InProduction),
            });
            Assert.False(vm.AllOrdersCompleted);
        }

        [Fact]
        public void AllOrdersCompleted_EmptyList_ReturnsTrue()
        {
            var vm = MakeVerifyVm(new List<OrderProductionInfo>());
            Assert.True(vm.AllOrdersCompleted);
        }

        // ─── HasOrderList ───

        [Fact]
        public void HasOrderList_VerifyWithOrders_True()
        {
            var vm = MakeVerifyVm(new[] { MakeOrder("CNC-01", OrderProductionStatus.Completed) });
            Assert.True(vm.HasOrderList);
        }

        [Fact]
        public void HasOrderList_VerifyEmpty_False()
        {
            var vm = MakeVerifyVm(new List<OrderProductionInfo>());
            Assert.False(vm.HasOrderList);
        }

        [Fact]
        public void HasOrderList_NonVerify_False()
        {
            // 其他操作型別即使沒傳清單也不顯示
            var vm = new ScheduleOperationDialogViewModel(
                ScheduleOperationType.Release, MakeSchedule(), string.Empty);
            Assert.False(vm.HasOrderList);
        }

        // ─── IncompleteHint（列舉未完成機台） ───

        [Fact]
        public void IncompleteHint_ListsUnfinishedEquipment()
        {
            var vm = MakeVerifyVm(new[]
            {
                MakeOrder("CNC-01", OrderProductionStatus.Completed),
                MakeOrder("CNC-02", OrderProductionStatus.InProduction),
                MakeOrder("CNC-03", OrderProductionStatus.Pending),
            });
            Assert.NotNull(vm.IncompleteHint);
            Assert.Contains("CNC-02", vm.IncompleteHint);
            Assert.Contains("CNC-03", vm.IncompleteHint);
            Assert.DoesNotContain("CNC-01", vm.IncompleteHint);
        }

        [Fact]
        public void IncompleteHint_AllCompleted_ReturnsNull()
        {
            var vm = MakeVerifyVm(new[] { MakeOrder("CNC-01", OrderProductionStatus.Completed) });
            Assert.Null(vm.IncompleteHint);
        }

        // ─── OnConfirm 閘門（D3 反應式攔截） ───

        [Fact]
        public void Confirm_WhenNotAllCompleted_DoesNotConfirm()
        {
            var vm = MakeVerifyVm(new[]
            {
                MakeOrder("CNC-01", OrderProductionStatus.Pending),
            });
            vm.ConfirmCommand.Execute(null);

            Assert.False(vm.IsConfirmed);
            Assert.Null(vm.Result);
            Assert.False(string.IsNullOrEmpty(vm.DialogErrorString));
        }

        [Fact]
        public void Confirm_WhenAllCompleted_Confirms()
        {
            var vm = MakeVerifyVm(new[]
            {
                MakeOrder("CNC-01", OrderProductionStatus.Completed),
            });
            vm.ConfirmCommand.Execute(null);

            Assert.True(vm.IsConfirmed);
            Assert.NotNull(vm.Result);
            Assert.Equal(80, vm.Result!.ActualQuantity);
        }

        // ─── 實際數量預設值回歸（Q2：維持 schedule.ActualQuantity ?? Quantity） ───

        [Fact]
        public void Verify_DefaultActualQuantity_UsesScheduleActualQuantity()
        {
            var vm = new ScheduleOperationDialogViewModel(
                ScheduleOperationType.Verify, MakeSchedule(actualQty: 80, quantity: 100),
                string.Empty, new[] { MakeOrder("CNC-01", OrderProductionStatus.Completed) });
            Assert.Equal(80, vm.ActualQuantity);
        }

        [Fact]
        public void Verify_DefaultActualQuantity_FallsBackToQuantity_WhenActualNull()
        {
            var vm = new ScheduleOperationDialogViewModel(
                ScheduleOperationType.Verify, MakeSchedule(actualQty: null, quantity: 100),
                string.Empty, new[] { MakeOrder("CNC-01", OrderProductionStatus.Completed) });
            Assert.Equal(100, vm.ActualQuantity);
        }

        // ─── 倉位釋放提示（#3/#4：出料/取消且有現役倉位時顯示） ───

        private static ScheduleUiModel MakeScheduleWithLocation(string? locationCode) => new()
        {
            ScheduleId   = 1,
            Quantity     = 100,
            Status       = ScheduleStatus.Scheduled,
            LocationCode = locationCode,
        };

        [Fact]
        public void IsLocationReleaseVisible_ReleaseWithLocation_TrueAndHintHasCode()
        {
            var vm = new ScheduleOperationDialogViewModel(
                ScheduleOperationType.Release, MakeScheduleWithLocation("A-01"), string.Empty);
            Assert.True(vm.IsLocationReleaseVisible);
            Assert.Contains("A-01", vm.LocationReleaseHint);
        }

        [Fact]
        public void IsLocationReleaseVisible_CancelWithLocation_True()
        {
            var vm = new ScheduleOperationDialogViewModel(
                ScheduleOperationType.Cancel, MakeScheduleWithLocation("B-02"), string.Empty);
            Assert.True(vm.IsLocationReleaseVisible);
        }

        [Fact]
        public void IsLocationReleaseVisible_ReleaseWithoutLocation_False()
        {
            var vm = new ScheduleOperationDialogViewModel(
                ScheduleOperationType.Release, MakeScheduleWithLocation(null), string.Empty);
            Assert.False(vm.IsLocationReleaseVisible);
        }

        [Fact]
        public void IsLocationReleaseVisible_VerifyWithLocation_False()
        {
            // 僅出料/取消顯示；其餘操作即使有倉位也不顯示
            var vm = new ScheduleOperationDialogViewModel(
                ScheduleOperationType.Verify, MakeScheduleWithLocation("A-01"), string.Empty);
            Assert.False(vm.IsLocationReleaseVisible);
        }
    }

    public class OrderProductionInfoTests
    {
        [Fact]
        public void FromEntity_MapsEquipmentName()
        {
            var entity = new OrderProduction
            {
                OrderId     = 1,
                EquipmentId = 5,
                Equipment   = new Equipment { Name = "CNC-01" },
                Status      = OrderProductionStatus.Completed,
                Quantity    = 50,
            };
            var info = OrderProductionInfo.FromEntity(entity);
            Assert.Equal("CNC-01", info.EquipmentName);
        }

        [Fact]
        public void FromEntity_NullEquipment_EquipmentNameEmpty()
        {
            var entity = new OrderProduction
            {
                OrderId     = 1,
                EquipmentId = 5,
                Equipment   = null,
                Status      = OrderProductionStatus.Pending,
            };
            var info = OrderProductionInfo.FromEntity(entity);
            Assert.Equal(string.Empty, info.EquipmentName);
        }
    }
}
