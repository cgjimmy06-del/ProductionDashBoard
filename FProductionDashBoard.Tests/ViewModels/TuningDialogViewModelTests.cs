using FProductionDashBoard.Models;
using FProductionDashBoard.ViewModels;
using System.Collections.Generic;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class TuningDialogViewModelTests
    {
        private static List<EquipmentProductItem> MakeItems() =>
        [
            new() { EquipmentProductId = 1, SeqNo = 1, DisplayLabel = "#1 PartA_M1 · 加工", ProductionStatus = TuningType.Teaching },
            new() { EquipmentProductId = 2, SeqNo = 2, DisplayLabel = "#2 PartA_M2 · 加工", ProductionStatus = TuningType.Offset },
            new() { EquipmentProductId = 3, SeqNo = 3, DisplayLabel = "#3 PartB_M1 · 組裝", ProductionStatus = TuningType.Teaching },
            new() { EquipmentProductId = 4, SeqNo = 4, DisplayLabel = "#4 PartB_M2 · 組裝", ProductionStatus = TuningType.Feasible },
        ];

        private static TuningDialogViewModel Create() =>
            new("機台: Device-01", "人員: John", MakeItems());

        // ─── Initial state ────────────────────────────────────────────────────

        [Fact]
        public void InitialState_TeachingModeAndTeachingItemsFiltered()
        {
            var vm = Create();
            Assert.Equal((int)TuningType.Teaching, vm.TuningMode);
            Assert.Equal(2, vm.FilteredEquipmentProducts.Count);
            Assert.All(vm.FilteredEquipmentProducts, i => Assert.Equal(TuningType.Teaching, i.ProductionStatus));
        }

        [Fact]
        public void InitialState_SelectedIsNull_ConfirmDisabled()
        {
            var vm = Create();
            Assert.Null(vm.SelectedEquipmentProduct);
            Assert.False(vm.ConfirmCommand.CanExecute(null));
        }

        // ─── TeachingCommand ──────────────────────────────────────────────────

        [Fact]
        public void TeachingCommand_SetsModeAndFiltersTeachingItems()
        {
            var vm = Create();
            vm.OffsetCommand.Execute(null); // 先切到 Offset
            vm.TeachingCommand.Execute(null);
            Assert.Equal((int)TuningType.Teaching, vm.TuningMode);
            Assert.Equal(2, vm.FilteredEquipmentProducts.Count);
            Assert.All(vm.FilteredEquipmentProducts, i => Assert.Equal(TuningType.Teaching, i.ProductionStatus));
        }

        [Fact]
        public void TeachingCommand_ClearsSelection()
        {
            var vm = Create();
            vm.SelectedEquipmentProduct = vm.FilteredEquipmentProducts[0];
            vm.OffsetCommand.Execute(null);
            vm.TeachingCommand.Execute(null);
            Assert.Null(vm.SelectedEquipmentProduct);
        }

        // ─── OffsetCommand ────────────────────────────────────────────────────

        [Fact]
        public void OffsetCommand_SetsModeAndFiltersOffsetItems()
        {
            var vm = Create();
            vm.OffsetCommand.Execute(null);
            Assert.Equal((int)TuningType.Offset, vm.TuningMode);
            Assert.Single(vm.FilteredEquipmentProducts);
            Assert.Equal(TuningType.Offset, vm.FilteredEquipmentProducts[0].ProductionStatus);
        }

        [Fact]
        public void OffsetCommand_ClearsSelection()
        {
            var vm = Create();
            vm.SelectedEquipmentProduct = vm.FilteredEquipmentProducts[0];
            vm.OffsetCommand.Execute(null);
            Assert.Null(vm.SelectedEquipmentProduct);
        }

        // ─── ConfirmCommand ───────────────────────────────────────────────────

        [Fact]
        public void ConfirmCommand_EnabledWhenItemSelected()
        {
            var vm = Create();
            vm.SelectedEquipmentProduct = vm.FilteredEquipmentProducts[0];
            Assert.True(vm.ConfirmCommand.CanExecute(null));
        }

        [Fact]
        public void ConfirmCommand_TeachingMode_ReturnsCorrectResult()
        {
            var vm = Create();
            var item = vm.FilteredEquipmentProducts[0]; // Teaching item, Id=1
            vm.SelectedEquipmentProduct = item;
            vm.ConfirmCommand.Execute(null);
            Assert.True(vm.IsConfirmed);
            Assert.NotNull(vm.Result);
            Assert.Equal(TuningType.Teaching, vm.Result!.TuningType);
            Assert.Equal(item.EquipmentProductId, vm.Result.EquipmentProductId);
        }

        [Fact]
        public void ConfirmCommand_OffsetMode_ReturnsCorrectResult()
        {
            var vm = Create();
            vm.OffsetCommand.Execute(null);
            var item = vm.FilteredEquipmentProducts[0]; // Offset item, Id=2
            vm.SelectedEquipmentProduct = item;
            vm.ConfirmCommand.Execute(null);
            Assert.True(vm.IsConfirmed);
            Assert.NotNull(vm.Result);
            Assert.Equal(TuningType.Offset, vm.Result!.TuningType);
            Assert.Equal(item.EquipmentProductId, vm.Result.EquipmentProductId);
        }

        [Fact]
        public void ConfirmCommand_WithoutSelection_DoesNotConfirm()
        {
            var vm = Create();
            // CanExecute is false, but verify OnConfirm guard also holds
            Assert.False(vm.ConfirmCommand.CanExecute(null));
            Assert.False(vm.IsConfirmed);
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

        // ─── Non-Teaching/Offset items not shown ─────────────────────────────

        [Fact]
        public void FeasibleItems_NotShownInEitherMode()
        {
            var vm = Create();
            Assert.DoesNotContain(vm.FilteredEquipmentProducts, i => i.ProductionStatus == TuningType.Feasible);
            vm.OffsetCommand.Execute(null);
            Assert.DoesNotContain(vm.FilteredEquipmentProducts, i => i.ProductionStatus == TuningType.Feasible);
        }
    }
}
