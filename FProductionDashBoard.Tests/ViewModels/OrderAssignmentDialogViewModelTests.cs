using FProductionDashBoard.Models;
using FProductionDashBoard.Properties;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class OrderAssignmentDialogViewModelTests
    {
        private static ScheduleUiModel MakeSchedule()
            => new() { ScheduleId = 7, PartNo = "P1", ModelName = "M1", ProcessName = "Pr1", LotNo = "L1", Quantity = 100 };

        private static EquipmentProduct MakeEp(int equipmentId, int epId)
            => new() { EquipmentId = equipmentId, EquipmentProductId = epId };

        private static OrderAssignmentDialogViewModel CreateVm(int remainingQty, params EquipmentProduct[] options)
            => new(MakeSchedule(), options, remainingQty, "Machine-01");

        [Fact]
        public void Ctor_PreselectsFirstOption_AndDefaultsQuantityToRemaining()
        {
            var ep1 = MakeEp(1, 10);
            var ep2 = MakeEp(1, 11);
            var vm = CreateVm(30, ep1, ep2);

            Assert.Same(ep1, vm.SelectedEquipmentProduct);
            Assert.Equal(30, vm.Quantity);
        }

        [Fact]
        public void Ctor_NoRemainingQty_LeavesQuantityNull()
        {
            var vm = CreateVm(0, MakeEp(1, 10));
            Assert.Null(vm.Quantity);
        }

        [Fact]
        public void Confirm_NoSelection_SetsErrorAndDoesNotConfirm()
        {
            var vm = CreateVm(30); // 無 options → SelectedEquipmentProduct 為 null
            vm.ConfirmCommand.Execute(null);

            Assert.False(vm.IsConfirmed);
            Assert.Null(vm.Result);
            Assert.Equal(Resources.SchAssignNoSopSelected, vm.DialogErrorString);
        }

        [Fact]
        public void Confirm_QuantityZeroOrNegative_SetsError()
        {
            var vm = CreateVm(30, MakeEp(1, 10));
            vm.Quantity = 0;
            vm.ConfirmCommand.Execute(null);

            Assert.False(vm.IsConfirmed);
            Assert.Equal(Resources.SchAssignQtyRequired, vm.DialogErrorString);
        }

        [Fact]
        public void Confirm_QuantityExceedsMax_SetsError()
        {
            var vm = CreateVm(30, MakeEp(1, 10));
            vm.Quantity = 31;
            vm.ConfirmCommand.Execute(null);

            Assert.False(vm.IsConfirmed);
            Assert.Equal(Resources.SchAssignQtyExceeded, vm.DialogErrorString);
        }

        [Fact]
        public void Confirm_Valid_ProducesResultAndConfirms()
        {
            var ep = MakeEp(1, 10);
            var vm = CreateVm(30, ep);
            vm.Quantity = 25;
            vm.ConfirmCommand.Execute(null);

            Assert.True(vm.IsConfirmed);
            Assert.NotNull(vm.Result);
            Assert.Equal(1, vm.Result!.EquipmentId);
            Assert.Equal(10, vm.Result.EquipmentProductId);
            Assert.Equal(25, vm.Result.Quantity);
            Assert.Equal(7, vm.Result.ScheduleId);
        }
    }
}
