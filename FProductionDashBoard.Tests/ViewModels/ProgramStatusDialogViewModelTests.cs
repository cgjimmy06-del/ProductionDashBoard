using FProductionDashBoard.Models;
using FProductionDashBoard.ViewModels;
using System.Windows;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class ProgramStatusDialogViewModelTests
    {
        private static EquipmentProduct MakeEp(TuningType status) => new()
        {
            EquipmentProductId = 1,
            ProductionStatus = status,
            Sop = new SopChecklist
            {
                Product = new Product
                {
                    Part  = new ProductPart { PartNo = "TEST-001", Brand = "B1" },
                    Model = new ProductModel { Name = "M1" }
                },
                Process = new WorkProcess { Name = "P1" }
            }
        };

        private static ProgramStatusDialogViewModel Create(TuningType status)
            => new(MakeEp(status), "Test Title");

        // ── Visibility 規則 ─────────────────────────────────────────────────

        [Fact]
        public void Visibility_Feasible_ExcludesSelf()
        {
            var vm = Create(TuningType.Feasible);

            Assert.Equal(Visibility.Collapsed, vm.FeasibleVisibility);
            Assert.Equal(Visibility.Visible,   vm.TeachingVisibility);
            Assert.Equal(Visibility.Visible,   vm.OffsetVisibility);
            Assert.Equal(Visibility.Visible,   vm.InfeasibleVisibility);
            Assert.Equal(Visibility.Visible,   vm.PendingVisibility);
        }

        [Fact]
        public void Visibility_Infeasible_OnlyTeachingAndOffset()
        {
            var vm = Create(TuningType.Infeasible);

            Assert.Equal(Visibility.Visible,   vm.TeachingVisibility);
            Assert.Equal(Visibility.Visible,   vm.OffsetVisibility);
            Assert.Equal(Visibility.Collapsed, vm.FeasibleVisibility);
            Assert.Equal(Visibility.Collapsed, vm.InfeasibleVisibility);
            Assert.Equal(Visibility.Collapsed, vm.PendingVisibility);
        }

        [Fact]
        public void Visibility_Teaching_ExcludesFeasibleAndSelf()
        {
            var vm = Create(TuningType.Teaching);

            Assert.Equal(Visibility.Collapsed, vm.TeachingVisibility);
            Assert.Equal(Visibility.Collapsed, vm.FeasibleVisibility);
            Assert.Equal(Visibility.Visible,   vm.OffsetVisibility);
            Assert.Equal(Visibility.Visible,   vm.InfeasibleVisibility);
            Assert.Equal(Visibility.Visible,   vm.PendingVisibility);
        }

        [Fact]
        public void Visibility_Offset_ExcludesFeasibleAndSelf()
        {
            var vm = Create(TuningType.Offset);

            Assert.Equal(Visibility.Collapsed, vm.OffsetVisibility);
            Assert.Equal(Visibility.Collapsed, vm.FeasibleVisibility);
            Assert.Equal(Visibility.Visible,   vm.TeachingVisibility);
            Assert.Equal(Visibility.Visible,   vm.InfeasibleVisibility);
            Assert.Equal(Visibility.Visible,   vm.PendingVisibility);
        }

        // ── 選擇指令 ────────────────────────────────────────────────────────

        [Fact]
        public void SelectCommand_UpdatesSelectedStatus()
        {
            var vm = Create(TuningType.Feasible);

            vm.SelectOffsetCommand.Execute(null);

            Assert.Equal(TuningType.Offset, vm.SelectedStatus);
            Assert.False(vm.IsConfirmed);
        }

        // ── 確認 / 取消 ──────────────────────────────────────────────────────

        [Fact]
        public void Confirm_WithoutSelection_ShowsError()
        {
            var vm = Create(TuningType.Feasible);

            vm.ConfirmCommand.Execute(null);

            Assert.False(vm.IsConfirmed);
            Assert.False(string.IsNullOrEmpty(vm.DialogErrorString));
        }

        [Fact]
        public void Confirm_WithSelection_SetsResult()
        {
            var vm = Create(TuningType.Feasible);
            vm.SelectPendingCommand.Execute(null);

            vm.ConfirmCommand.Execute(null);

            Assert.True(vm.IsConfirmed);
            Assert.NotNull(vm.Result);
            Assert.Equal(TuningType.Pending, vm.Result!.NewStatus);
        }

        [Fact]
        public void Cancel_IsNotConfirmed()
        {
            var vm = Create(TuningType.Feasible);
            vm.SelectPendingCommand.Execute(null);

            vm.CancelCommand.Execute(null);

            Assert.False(vm.IsConfirmed);
            Assert.Null(vm.Result);
        }
    }
}
