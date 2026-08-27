using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FProductionDashBoard.Models;
using FProductionDashBoard.Models.Extra;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using Moq;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class MesSyncDialogViewModelTests
    {
        // 四種對齊情境：E1 一致(Synced)、E2 名稱不符(DataMismatch)、E3 僅本地(LocalOnly)、E4 僅 MES(MesOnly)
        private static (List<MesDevice> mes, List<Equipment> local) SampleData()
        {
            var mes = new List<MesDevice>
            {
                new() { DeviceId = "E1", Name = "Dev1", Ip = "1.1.1.1", SerialNum = "0", Group = "Robot" },
                new() { DeviceId = "E2", Name = "MesName", Ip = "2.2.2.2", SerialNum = "0", Group = "Robot" },
                new() { DeviceId = "E4", Name = "OnlyMes", Ip = "4.4.4.4", SerialNum = "0", Group = "Robot" },
            };
            var local = new List<Equipment>
            {
                new() { Code = "E1", Name = "Dev1", Ip = "1.1.1.1", Port = 0, TypeId = 1 },       // 與 E1 一致
                new() { Code = "E2", Name = "LocalName", Ip = "2.2.2.2", Port = 0, TypeId = 1 },  // 名稱不符
                new() { Code = "E3", Name = "OnlyLocal", Ip = "3.3.3.3", Port = 0, TypeId = 1 },  // 僅本地
            };
            return (mes, local);
        }

        private static MesSyncDialogViewModel CreateVm(List<MesDevice> mes, List<Equipment> local)
        {
            var dataMock = new Mock<IDataService>();
            dataMock.Setup(d => d.GetAllMesDevicesAsync()).ReturnsAsync(mes);
            dataMock.Setup(d => d.GetAllEquipmentAsync()).ReturnsAsync(local);
            return new MesSyncDialogViewModel(dataMock.Object);
        }

        private static List<MesSyncItemUiModel> Items(MesSyncDialogViewModel vm)
            => vm.FilteredItems.Cast<MesSyncItemUiModel>().ToList();

        [Fact]
        public async Task Load_ClassifiesFourSyncStates()
        {
            var (mes, local) = SampleData();
            var vm = CreateVm(mes, local);

            await vm.LoadCommand.ExecuteAsync(null);

            var items = Items(vm);
            Assert.Equal(MesSyncState.Synced,       items.Single(i => i.DeviceId == "E1").SyncState);
            Assert.Equal(MesSyncState.DataMismatch, items.Single(i => i.DeviceId == "E2").SyncState);
            Assert.Equal(MesSyncState.LocalOnly,    items.Single(i => i.DeviceId == "E3").SyncState);
            Assert.Equal(MesSyncState.MesOnly,      items.Single(i => i.DeviceId == "E4").SyncState);
        }

        [Fact]
        public async Task OnlyUnsynced_HidesSyncedItems()
        {
            var (mes, local) = SampleData();
            var vm = CreateVm(mes, local);
            await vm.LoadCommand.ExecuteAsync(null);

            vm.OnlyUnsynced = true;

            var items = Items(vm);
            Assert.Equal(3, items.Count); // E2/E3/E4
            Assert.DoesNotContain(items, i => i.SyncState == MesSyncState.Synced);
        }

        [Fact]
        public async Task GroupFilter_KeepsOnlyMatchingGroup()
        {
            var (mes, local) = SampleData();
            var vm = CreateVm(mes, local);
            await vm.LoadCommand.ExecuteAsync(null);

            vm.GroupFilter = "Robot";

            // E1/E2/E4 group=Robot（E3 LocalOnly 由 TypeId=1 推得 Robot），全部保留
            Assert.All(Items(vm), i => Assert.Equal("Robot", i.Group));
        }

        [Fact]
        public async Task SelectAll_SelectsEnabledItemsOnly()
        {
            var (mes, local) = SampleData();
            var vm = CreateVm(mes, local);
            await vm.LoadCommand.ExecuteAsync(null);

            vm.ClearAllCommand.Execute(null);
            vm.SelectAllCommand.Execute(null);

            var items = Items(vm);
            Assert.True(items.Single(i => i.DeviceId == "E2").IsSelected);  // enabled
            Assert.False(items.Single(i => i.DeviceId == "E1").IsSelected); // Synced → 不可選
        }

        [Fact]
        public async Task Confirm_ReturnsSelectedEnabledItems()
        {
            var (mes, local) = SampleData();
            var vm = CreateVm(mes, local);
            await vm.LoadCommand.ExecuteAsync(null);

            vm.ClearAllCommand.Execute(null);
            vm.SelectAllCommand.Execute(null);
            vm.ConfirmCommand.Execute(null);

            Assert.True(vm.IsConfirmed);
            Assert.NotNull(vm.Result);
            var result = vm.Result!.ToList();
            Assert.Equal(3, result.Count); // E2/E3/E4
            Assert.All(result, i => Assert.NotEqual(MesSyncState.Synced, i.SyncState));
        }
    }
}
