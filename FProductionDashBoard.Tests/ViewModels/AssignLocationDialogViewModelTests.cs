using FProductionDashBoard.Models;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using System.Collections.Generic;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class AssignLocationDialogViewModelTests
    {
        private static ScheduleUiModel MakeSchedule(int? locationId = null, string? locationCode = null) => new()
        {
            ScheduleId   = 7,
            PartNo       = "P1",
            ModelName    = "M1",
            ProcessName  = "Pr1",
            LocationId   = locationId,
            LocationCode = locationCode,
        };

        private static IReadOnlyList<PioLocationChoice> MakeChoices()
        {
            var loc = new StorageLocation { LocationId = 10, Code = "A-01", LocationType = LocationType.Buffer };
            return new List<PioLocationChoice>
            {
                new(null, null),                                   // 首項 (無)
                new(10, new StorageLocationRow(loc, 0, null)),
            };
        }

        [Fact]
        public void Ctor_PreselectsCurrentLocation()
        {
            var vm = new AssignLocationDialogViewModel(MakeSchedule(locationId: 10, locationCode: "A-01"), MakeChoices());
            Assert.Equal(10, vm.SelectedLocationId);
            Assert.Equal("A-01", vm.CurrentLocationCode);
        }

        [Fact]
        public void Ctor_Unassigned_PreselectsNone()
        {
            var vm = new AssignLocationDialogViewModel(MakeSchedule(locationId: null), MakeChoices());
            Assert.Null(vm.SelectedLocationId);   // 預選「(無)」
        }

        [Fact]
        public void Confirm_SelectNone_ResultLocationIdNull()
        {
            var vm = new AssignLocationDialogViewModel(MakeSchedule(locationId: 10, locationCode: "A-01"), MakeChoices());
            vm.SelectedLocationId = null;   // 改選「(無)」＝取消指派
            vm.ConfirmCommand.Execute(null);

            Assert.True(vm.IsConfirmed);
            Assert.NotNull(vm.Result);
            Assert.Null(vm.Result!.LocationId);
        }

        [Fact]
        public void Confirm_SelectLocation_ResultHasId()
        {
            var vm = new AssignLocationDialogViewModel(MakeSchedule(locationId: null), MakeChoices());
            vm.SelectedLocationId = 10;
            vm.ConfirmCommand.Execute(null);

            Assert.True(vm.IsConfirmed);
            Assert.Equal(10, vm.Result!.LocationId);
        }
    }
}
