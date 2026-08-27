using System.Collections.Generic;
using System.Linq;
using FProductionDashBoard.Models;
using FProductionDashBoard.Properties;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class AddDeviceDialogViewModelTests
    {
        private static DeviceInfo MakeDevice(string id, string name = "N", string ip = "1.1.1.1",
            int? typeId = 1, string? building = "A")
            => new() { DeviceID = id, Name = name, IP = ip, TypeId = typeId, Building = building };

        private static AddDeviceDialogViewModel CreateVm(params DeviceInfo[] devices)
            => new("title", "defaults.json", devices.ToList(), new List<DeviceCardViewModel>(),
                   new[] { new EquipmentTypeFilterOption(null) });

        [Fact]
        public void BuildingOptions_AreDistinctSortedWithEmptyFirst()
        {
            var vm = CreateVm(
                MakeDevice("D1", building: "B"),
                MakeDevice("D2", building: "A"),
                MakeDevice("D3", building: "A"));

            Assert.Equal(new[] { "", "A", "B" }, vm.BuildingOptions);
        }

        [Fact]
        public void Ctor_FiltersToAllDevicesInitially()
        {
            var vm = CreateVm(MakeDevice("D1"), MakeDevice("D2"));
            Assert.Equal(2, vm.FilteredDevices.Count);
        }

        [Fact]
        public void KeywordFilter_MatchesNameCaseInsensitive()
        {
            var vm = CreateVm(
                MakeDevice("AAA", name: "Xyz"),
                MakeDevice("BBB", name: "Other"));

            vm.KeywordFilter = "xyz";

            Assert.Single(vm.FilteredDevices);
            Assert.Equal("AAA", vm.FilteredDevices[0].DeviceID);
        }

        [Fact]
        public void TypeFilter_FiltersByTypeId()
        {
            var vm = CreateVm(
                MakeDevice("D1", typeId: 1),
                MakeDevice("D2", typeId: 2));

            vm.TypeFilter = new EquipmentType { TypeId = 2 };

            Assert.Single(vm.FilteredDevices);
            Assert.Equal("D2", vm.FilteredDevices[0].DeviceID);
        }

        [Fact]
        public void AddToSelected_MovesDeviceAndExcludesFromFiltered()
        {
            var d1 = MakeDevice("D1");
            var vm = CreateVm(d1, MakeDevice("D2"));

            vm.SelectedFromFiltered.Add(d1);
            vm.AddToSelected();

            Assert.Contains(d1, vm.SelectedDevices);
            Assert.DoesNotContain(d1, vm.FilteredDevices); // 已選項應被排除
        }

        [Fact]
        public void RemoveFromSelected_MovesDeviceBackToFiltered()
        {
            var d1 = MakeDevice("D1");
            var vm = CreateVm(d1);

            vm.SelectedFromFiltered.Add(d1);
            vm.AddToSelected();
            vm.SelectedFromSelected.Add(d1);
            vm.RemoveFromSelected();

            Assert.DoesNotContain(d1, vm.SelectedDevices);
            Assert.Contains(d1, vm.FilteredDevices);
        }

        [Fact]
        public void Confirm_NoSelection_SetsError()
        {
            var vm = CreateVm(MakeDevice("D1"));
            vm.ConfirmCommand.Execute(null);

            Assert.False(vm.IsConfirmed);
            Assert.Equal(Resources.AddDeviceNonSelectionError, vm.DialogErrorString);
        }

        [Fact]
        public void Confirm_WithSelection_ProducesResult()
        {
            var d1 = MakeDevice("D1");
            var vm = CreateVm(d1);

            vm.SelectedFromFiltered.Add(d1);
            vm.AddToSelected();
            vm.ConfirmCommand.Execute(null);

            Assert.True(vm.IsConfirmed);
            Assert.NotNull(vm.Result);
            Assert.Single(vm.Result!.Selections);
            Assert.Equal("D1", vm.Result.Selections[0].DeviceID);
        }
    }
}
