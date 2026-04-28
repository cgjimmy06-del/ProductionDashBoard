using FProductionDashBoard.Models;
using FProductionDashBoard.ViewModels;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class TuningDialogViewModelTests
    {
        private static TuningDialogViewModel Create() =>
            new("機台: Device-01", "人員: John", "產品: ProductA");

        // ─── TeachingCommand ──────────────────────────────────────────────────
        [Fact]
        public void TeachingCommand_SetsConfirmedWithTeachingResult()
        {
            var vm = Create();
            vm.TeachingCommand.Execute(null);
            Assert.True(vm.IsConfirmed);
            Assert.NotNull(vm.Result);
            Assert.Equal(TuningType.Teaching, vm.Result!.TuningType);
        }

        // ─── OffsetCommand ────────────────────────────────────────────────────
        [Fact]
        public void OffsetCommand_SetsConfirmedWithOffsetResult()
        {
            var vm = Create();
            vm.OffsetCommand.Execute(null);
            Assert.True(vm.IsConfirmed);
            Assert.NotNull(vm.Result);
            Assert.Equal(TuningType.Offset, vm.Result!.TuningType);
        }

        // ─── CancelCommand ────────────────────────────────────────────────────
        [Fact]
        public void CancelCommand_SetsNotConfirmedNullResult()
        {
            var vm = Create();
            vm.CancelCommand.Execute(null);
            Assert.False(vm.IsConfirmed);
            Assert.Null(vm.Result);
        }
    }
}
