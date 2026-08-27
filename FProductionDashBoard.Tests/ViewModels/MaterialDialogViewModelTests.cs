using System.Collections.Generic;
using FProductionDashBoard.Properties;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class MaterialDialogViewModelTests
    {
        private static List<MaterialInfo> Materials() => new()
        {
            new() { Code = "M1", Name = "Mat1" },
            new() { Code = "M2", Name = "Mat2" },
        };

        private static MaterialDialogViewModel CreateVm(List<MaterialInfo> mats)
            => new("t", DeviceCardTestFactory.Create(), mats);

        [Fact]
        public void MaterialButtonClick_SelectMode_TogglesSelection()
        {
            var mats = Materials();
            var vm = CreateVm(mats);
            var m = mats[0];

            vm.MaterialButtonClickCommand.Execute(m);
            Assert.True(m.IsSelected);
            Assert.Equal(1, m.SelectedCount);

            vm.MaterialButtonClickCommand.Execute(m);
            Assert.False(m.IsSelected);
            Assert.Equal(0, m.SelectedCount);
        }

        [Fact]
        public void MaterialButtonClick_CountMode_Accumulates()
        {
            var mats = Materials();
            var vm = CreateVm(mats);
            vm.ToggleModeCommand.Execute(null); // 進入計數模式
            var m = mats[0];

            vm.MaterialButtonClickCommand.Execute(m);
            vm.MaterialButtonClickCommand.Execute(m);

            Assert.True(m.IsSelected);
            Assert.Equal(2, m.SelectedCount);
        }

        [Fact]
        public void ToggleMode_ResetsSelectedCountOfSelectedToOne()
        {
            var mats = Materials();
            var vm = CreateVm(mats);
            var m = mats[0];

            vm.ToggleModeCommand.Execute(null); // 計數模式
            vm.MaterialButtonClickCommand.Execute(m);
            vm.MaterialButtonClickCommand.Execute(m);
            vm.MaterialButtonClickCommand.Execute(m); // SelectedCount = 3

            vm.ToggleModeCommand.Execute(null); // 切模式 → 已選項 SelectedCount 重置為 1

            Assert.Equal(1, m.SelectedCount);
        }

        [Fact]
        public void ClearSelection_ClearsAll()
        {
            var mats = Materials();
            var vm = CreateVm(mats);
            vm.MaterialButtonClickCommand.Execute(mats[0]);
            vm.MaterialButtonClickCommand.Execute(mats[1]);

            vm.ClearSelectionCommand.Execute(null);

            Assert.All(mats, m => Assert.False(m.IsSelected));
            Assert.All(mats, m => Assert.Equal(0, m.SelectedCount));
        }

        [Fact]
        public void Confirm_NoSelection_SetsError()
        {
            var vm = CreateVm(Materials());

            vm.ConfirmCommand.Execute(null);

            Assert.False(vm.IsConfirmed);
            Assert.Equal(Resources.MaterialNonSelectionError, vm.DialogErrorString);
        }

        [Fact]
        public void Confirm_WithSelection_ProducesResult()
        {
            var mats = Materials();
            var vm = CreateVm(mats);
            vm.MaterialButtonClickCommand.Execute(mats[0]);

            vm.ConfirmCommand.Execute(null);

            Assert.True(vm.IsConfirmed);
            Assert.NotNull(vm.Result);
            Assert.Single(vm.Result!.Selections);
            Assert.Equal("M1", vm.Result.Selections[0].Code);
        }
    }
}
