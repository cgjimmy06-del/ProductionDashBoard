using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class LocationSettingViewModelTests
    {
        // 註：VM 失敗路徑（catch 後呼叫 _core.Log.AddLog）會走 LogService 內部的
        // Application.Current.Dispatcher.BeginInvoke，在 xUnit 無 WPF Dispatcher 會 NRE。
        // 本測試類專注於 happy path、驗證邏輯、條件啟用與更新保留欄位；
        // BusinessRule 擋刪等失敗路徑由手動 UI 驗證涵蓋。

        private readonly Mock<IDataService> _data = new();
        private readonly Mock<IWarehouseService> _warehouse = new();
        private readonly Mock<IDialogService> _dialog = new();

        public LocationSettingViewModelTests()
        {
            // 預設回傳，讓觸發 LoadAsync 的路徑（含 Save 後的 reload）不 NRE
            _data.Setup(d => d.GetAllEquipmentAsync()).ReturnsAsync(new List<Equipment>());
            _warehouse.Setup(w => w.GetLocationsAsync()).ReturnsAsync(new List<StorageLocation>());
            _warehouse.Setup(w => w.GetOccupancyCountsAsync()).ReturnsAsync(new Dictionary<int, int>());
            _dialog.Setup(d => d.ShowConfirm(It.IsAny<string>())).Returns(true);
        }

        private LocationSettingViewModel CreateVm()
        {
            var log = new LogService();
            var auth = new AuthorizationService();
            var cardReader = new Mock<ICardReaderService>().Object;
            var core = new DashboardCoreServices(log, _data.Object, auth, cardReader, _warehouse.Object);
            return new LocationSettingViewModel(core, _dialog.Object);
        }

        private static Task LoadAsync(LocationSettingViewModel vm)
            => ((IAsyncRelayCommand)vm.LoadCommand).ExecuteAsync(null);

        private static Task SaveAsync(LocationSettingViewModel vm)
            => ((IAsyncRelayCommand)vm.SaveCommand).ExecuteAsync(null);

        // ─── LoadAsync happy path ────────────────────────────────────────────

        [Fact]
        public async Task LoadAsync_BuildsRows_WithOccupancyAndEquipmentName()
        {
            _data.Setup(d => d.GetAllEquipmentAsync()).ReturnsAsync(new List<Equipment>
            {
                new() { Id = 1, Name = "CNC-01" }
            });
            _warehouse.Setup(w => w.GetLocationsAsync()).ReturnsAsync(new List<StorageLocation>
            {
                new() { LocationId = 5, Code = "A-01", LocationType = LocationType.Buffer, EquipmentId = null },
                new() { LocationId = 6, Code = "M-01", LocationType = LocationType.MachineSide, EquipmentId = 1 }
            });
            _warehouse.Setup(w => w.GetOccupancyCountsAsync())
                      .ReturnsAsync(new Dictionary<int, int> { { 5, 3 } });

            var vm = CreateVm();
            await LoadAsync(vm);

            Assert.Equal(2, vm.LocationList.Count);
            var bufferRow = vm.LocationList.Single(r => r.Code == "A-01");
            Assert.Equal(3, bufferRow.OccupancyCount);
            Assert.Null(bufferRow.EquipmentName);
            var machineRow = vm.LocationList.Single(r => r.Code == "M-01");
            Assert.Equal(0, machineRow.OccupancyCount);
            Assert.Equal("CNC-01", machineRow.EquipmentName);
        }

        // ─── OpenNewForm defaults ────────────────────────────────────────────

        [Fact]
        public void OpenNewForm_SetsDefaults()
        {
            var vm = CreateVm();
            vm.NewCommand.Execute(null);

            Assert.Null(vm.EditingId);
            Assert.True(vm.FormIsEnabled);
            Assert.Equal(LocationType.Buffer, vm.FormLocationType);
            Assert.True(vm.IsFormVisible);
        }

        // ─── Edit populates form ─────────────────────────────────────────────

        [Fact]
        public void Edit_PopulatesFormFromSource()
        {
            var vm = CreateVm();
            var source = new StorageLocation
            {
                LocationId = 7,
                Code = "A-07",
                Zone = "A",
                LocationType = LocationType.MachineSide,
                Capacity = 12,
                EquipmentId = 1,
                IsEnabled = false
            };
            var row = new StorageLocationRow(source, 0, "CNC-01");

            vm.EditCommand.Execute(row);

            Assert.Equal(7, vm.EditingId);
            Assert.Equal("A-07", vm.FormCode);
            Assert.Equal("A", vm.FormZone);
            Assert.Equal(LocationType.MachineSide, vm.FormLocationType);
            Assert.Equal(12, vm.FormCapacity);
            Assert.Equal(1, vm.FormEquipmentId);
            Assert.False(vm.FormIsEnabled);
            Assert.True(vm.IsFormVisible);
        }

        // ─── Validation ──────────────────────────────────────────────────────

        [Fact]
        public async Task SaveAsync_BlankCode_SetsError_NoAdd()
        {
            var vm = CreateVm();
            vm.NewCommand.Execute(null);
            vm.FormCode = "   ";

            await SaveAsync(vm);

            Assert.Equal(Properties.Resources.WarehouseValidationCodeRequired, vm.FormErrorString);
            _warehouse.Verify(w => w.AddLocationAsync(It.IsAny<StorageLocation>()), Times.Never);
        }

        // ─── Add happy path ──────────────────────────────────────────────────

        [Fact]
        public async Task SaveAsync_Add_CallsAddLocation()
        {
            var vm = CreateVm();
            vm.NewCommand.Execute(null);
            vm.FormCode = "A-99";
            vm.FormLocationType = LocationType.Buffer;
            vm.FormCapacity = 10;

            await SaveAsync(vm);

            _warehouse.Verify(w => w.AddLocationAsync(It.Is<StorageLocation>(
                l => l.Code == "A-99" && l.Capacity == 10 && l.LocationType == LocationType.Buffer)), Times.Once);
        }

        // ─── Update preserves reserved fields ────────────────────────────────

        [Fact]
        public async Task SaveAsync_Update_PreservesReservedFields()
        {
            var vm = CreateVm();
            var source = new StorageLocation
            {
                LocationId = 7,
                Code = "A-07",
                LocationType = LocationType.Buffer,
                AmrStationCode = "ST-7",   // 預留欄位，不在表單顯示
                MapX = 1.5,
                MapY = 2.5,
                IsEnabled = true
            };
            vm.EditCommand.Execute(new StorageLocationRow(source, 0, null));
            vm.FormCode = "A-07b";
            vm.FormCapacity = 20;

            await SaveAsync(vm);

            _warehouse.Verify(w => w.UpdateLocationAsync(It.Is<StorageLocation>(
                l => l.LocationId == 7
                     && l.Code == "A-07b"
                     && l.Capacity == 20
                     && l.AmrStationCode == "ST-7"   // 保留
                     && l.MapX == 1.5
                     && l.MapY == 2.5)), Times.Once);
        }

        // ─── Delete happy path ───────────────────────────────────────────────

        [Fact]
        public async Task Delete_CallsDeleteLocation_RemovesRow()
        {
            _warehouse.Setup(w => w.GetLocationsAsync()).ReturnsAsync(new List<StorageLocation>
            {
                new() { LocationId = 8, Code = "A-08", LocationType = LocationType.Buffer }
            });
            var vm = CreateVm();
            await LoadAsync(vm);
            var row = vm.LocationList.Single();

            await vm.DeleteCommand.ExecuteAsync(row);

            _warehouse.Verify(w => w.DeleteLocationAsync(8), Times.Once);
            Assert.Empty(vm.LocationList);
        }

        // ─── Conditional equipment binding ───────────────────────────────────

        [Fact]
        public void ChangingTypeAwayFromMachineSide_ClearsEquipmentId()
        {
            var vm = CreateVm();
            vm.FormLocationType = LocationType.MachineSide;
            vm.FormEquipmentId = 1;
            Assert.True(vm.IsMachineSide);

            vm.FormLocationType = LocationType.Buffer;

            Assert.False(vm.IsMachineSide);
            Assert.Null(vm.FormEquipmentId);
        }
    }
}
