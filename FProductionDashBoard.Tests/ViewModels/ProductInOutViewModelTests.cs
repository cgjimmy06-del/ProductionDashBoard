using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class ProductInOutViewModelTests
    {
        // 建立 VM 並回傳可驗證的 mock；建構子 fire-and-forget LoadAsync 全走空 mock，不影響倉儲命令驗證
        private static (ProductInOutViewModel vm, Mock<IWarehouseService> wh, Mock<IDataService> data, Mock<IDialogService> dialog)
            CreateVm()
        {
            var data = new Mock<IDataService>();
            data.Setup(d => d.GetAllSchedulesAsync()).ReturnsAsync(new List<Schedule>());
            data.Setup(d => d.GetAllProductsAsync()).ReturnsAsync(new List<Product>());
            data.Setup(d => d.GetWorkProcessesAsync()).ReturnsAsync(new List<WorkProcess>());

            var wh = new Mock<IWarehouseService>();
            SetupWarehouseEmpty(wh);

            var core = new DashboardCoreServices(new LogService(), data.Object, new AuthorizationService(),
                new Mock<ICardReaderService>().Object, wh.Object);
            var dialog = new Mock<IDialogService>();
            var vm = new ProductInOutViewModel(core, dialog.Object);
            return (vm, wh, data, dialog);
        }

        private static void SetupWarehouseEmpty(Mock<IWarehouseService> wh)
        {
            wh.Setup(w => w.GetLocationsAsync()).ReturnsAsync(new List<StorageLocation>());
            wh.Setup(w => w.GetOccupancyCountsAsync()).ReturnsAsync(new Dictionary<int, int>());
            wh.Setup(w => w.GetActiveAssignmentMapAsync()).ReturnsAsync(new Dictionary<int, StorageLocation>());
        }

        private static ScheduleUiModel Sched(int id, ScheduleStatus status, int? locationId = null,
            string? locationCode = null, int quantity = 100, int? actualQty = null) => new()
        {
            ScheduleId     = id,
            Status         = status,
            LocationId     = locationId,
            LocationCode   = locationCode,
            Quantity       = quantity,
            ActualQuantity = actualQty,
        };

        // 模擬改倉 Dialog：選定倉位（null = (無)）後確認
        private static void SetupAssignDialog(Mock<IDialogService> dialog, int? chosenLocationId)
        {
            dialog.Setup(d => d.ShowDialog(It.IsAny<AssignLocationDialogViewModel>()))
                  .Callback<DialogBaseViewModel<AssignLocationResult>>(raw =>
                  {
                      var dvm = (AssignLocationDialogViewModel)raw;
                      dvm.SelectedLocationId = chosenLocationId;
                      dvm.ConfirmCommand.Execute(null);
                  });
        }

        // 模擬出料/取消/拆單共用 Dialog：直接確認
        private static void SetupScheduleOpDialog(Mock<IDialogService> dialog)
        {
            dialog.Setup(d => d.ShowDialog(It.IsAny<ScheduleOperationDialogViewModel>()))
                  .Callback<DialogBaseViewModel<ScheduleOperationResult>>(raw =>
                  {
                      var dvm = (ScheduleOperationDialogViewModel)raw;
                      dvm.ConfirmCommand.Execute(null);
                  });
        }

        // ─── 改倉 Dialog 動作矩陣 ─────────────────────────────────────────────────

        [Fact]
        public async Task AssignLocation_Unassigned_SelectLocation_CallsAssign()
        {
            var (vm, wh, _, dialog) = CreateVm();
            SetupAssignDialog(dialog, chosenLocationId: 10);

            await vm.AssignLocationCommand.ExecuteAsync(Sched(5, ScheduleStatus.Scheduled, locationId: null));

            wh.Verify(w => w.AssignAsync(10, 5, It.IsAny<int>()), Times.Once);
            wh.Verify(w => w.ReassignAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            wh.Verify(w => w.ReleaseAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task AssignLocation_HasLocation_SelectDifferent_CallsReassign()
        {
            var (vm, wh, _, dialog) = CreateVm();
            SetupAssignDialog(dialog, chosenLocationId: 20);

            await vm.AssignLocationCommand.ExecuteAsync(Sched(5, ScheduleStatus.Scheduled, locationId: 10, locationCode: "A-01"));

            wh.Verify(w => w.ReassignAsync(5, 20, It.IsAny<int>()), Times.Once);
            wh.Verify(w => w.AssignAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            wh.Verify(w => w.ReleaseAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task AssignLocation_HasLocation_SelectNone_CallsRelease()
        {
            var (vm, wh, _, dialog) = CreateVm();
            SetupAssignDialog(dialog, chosenLocationId: null);   // 選「(無)」= 取消指派

            await vm.AssignLocationCommand.ExecuteAsync(Sched(5, ScheduleStatus.Scheduled, locationId: 10, locationCode: "A-01"));

            wh.Verify(w => w.ReleaseAsync(5, It.IsAny<int>()), Times.Once);
            wh.Verify(w => w.AssignAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            wh.Verify(w => w.ReassignAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task AssignLocation_SameLocation_NoOp()
        {
            var (vm, wh, _, dialog) = CreateVm();
            SetupAssignDialog(dialog, chosenLocationId: 10);

            await vm.AssignLocationCommand.ExecuteAsync(Sched(5, ScheduleStatus.Scheduled, locationId: 10, locationCode: "A-01"));

            wh.Verify(w => w.AssignAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            wh.Verify(w => w.ReassignAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            wh.Verify(w => w.ReleaseAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        // ─── 出料 / 取消連動釋放 ─────────────────────────────────────────────────

        [Fact]
        public async Task Release_WithLocation_ReleasesWarehouse()
        {
            var (vm, wh, data, dialog) = CreateVm();
            SetupScheduleOpDialog(dialog);

            await vm.ReleaseCommand.ExecuteAsync(Sched(5, ScheduleStatus.Completed, locationId: 10, locationCode: "A-01"));

            data.Verify(d => d.MarkReleasedAsync(5, It.IsAny<int>(), It.IsAny<string>()), Times.Once);
            wh.Verify(w => w.ReleaseAsync(5, It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task Release_WithoutLocation_SkipsWarehouseRelease()
        {
            var (vm, wh, _, dialog) = CreateVm();
            SetupScheduleOpDialog(dialog);

            await vm.ReleaseCommand.ExecuteAsync(Sched(5, ScheduleStatus.Completed, locationId: null));

            wh.Verify(w => w.ReleaseAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Cancel_WithLocation_ReleasesWarehouse()
        {
            var (vm, wh, data, dialog) = CreateVm();
            SetupScheduleOpDialog(dialog);

            await vm.CancelScheduleCommand.ExecuteAsync(Sched(5, ScheduleStatus.Scheduled, locationId: 10, locationCode: "A-01"));

            data.Verify(d => d.CancelScheduleAsync(5, It.IsAny<string>()), Times.Once);
            wh.Verify(w => w.ReleaseAsync(5, It.IsAny<int>()), Times.Once);
        }

        // ─── 拆單：原單釋放、子單沿用原倉位 ──────────────────────────────────────

        [Fact]
        public async Task Split_WithLocation_ReleasesOriginalAndAssignsChild()
        {
            var (vm, wh, data, dialog) = CreateVm();
            data.Setup(d => d.SplitScheduleAsync(It.IsAny<ScheduleSplitDto>())).ReturnsAsync(77);   // child id
            SetupScheduleOpDialog(dialog);

            await vm.SplitCommand.ExecuteAsync(Sched(5, ScheduleStatus.Completed, locationId: 10, locationCode: "A-01",
                quantity: 100, actualQty: 60));

            wh.Verify(w => w.ReleaseAsync(5, It.IsAny<int>()), Times.Once);       // 原單釋放
            wh.Verify(w => w.AssignAsync(10, 77, It.IsAny<int>()), Times.Once);   // 子單沿用原倉位
        }

        [Fact]
        public async Task Split_WithoutLocation_NoWarehouseCalls()
        {
            var (vm, wh, data, dialog) = CreateVm();
            data.Setup(d => d.SplitScheduleAsync(It.IsAny<ScheduleSplitDto>())).ReturnsAsync(77);
            SetupScheduleOpDialog(dialog);

            await vm.SplitCommand.ExecuteAsync(Sched(5, ScheduleStatus.Completed, locationId: null,
                quantity: 100, actualQty: 60));

            wh.Verify(w => w.ReleaseAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            wh.Verify(w => w.AssignAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        // ─── 入料帶倉位：建立後上架 ──────────────────────────────────────────────

        [Fact]
        public async Task SubmitForm_WithLocation_AssignsAfterCreate()
        {
            var product = new Product { ProductId = 3 };
            var process = new WorkProcess { ProcessId = 4 };
            var data = new Mock<IDataService>();
            data.Setup(d => d.GetAllSchedulesAsync()).ReturnsAsync(new List<Schedule>());
            data.Setup(d => d.GetAllProductsAsync()).ReturnsAsync(new List<Product> { product });
            data.Setup(d => d.GetWorkProcessesAsync()).ReturnsAsync(new List<WorkProcess> { process });
            data.Setup(d => d.AddScheduleAsync(It.IsAny<ScheduleCreateDto>())).ReturnsAsync(999);

            var wh = new Mock<IWarehouseService>();
            SetupWarehouseEmpty(wh);

            var core = new DashboardCoreServices(new LogService(), data.Object, new AuthorizationService(),
                new Mock<ICardReaderService>().Object, wh.Object);
            var vm = new ProductInOutViewModel(core, new Mock<IDialogService>().Object);

            vm.SelectedProduct = product;
            vm.SelectedProcess = process;
            vm.FormQuantity    = 5;
            vm.FormLocationId  = 7;

            await vm.SubmitFormCommand.ExecuteAsync(null);

            data.Verify(d => d.AddScheduleAsync(It.Is<ScheduleCreateDto>(
                dto => dto.ProductId == 3 && dto.ProcessId == 4 && dto.Quantity == 5)), Times.Once);
            wh.Verify(w => w.AssignAsync(7, 999, It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task SubmitForm_WithoutLocation_NoAssign()
        {
            var product = new Product { ProductId = 3 };
            var process = new WorkProcess { ProcessId = 4 };
            var data = new Mock<IDataService>();
            data.Setup(d => d.GetAllSchedulesAsync()).ReturnsAsync(new List<Schedule>());
            data.Setup(d => d.GetAllProductsAsync()).ReturnsAsync(new List<Product> { product });
            data.Setup(d => d.GetWorkProcessesAsync()).ReturnsAsync(new List<WorkProcess> { process });
            data.Setup(d => d.AddScheduleAsync(It.IsAny<ScheduleCreateDto>())).ReturnsAsync(999);

            var wh = new Mock<IWarehouseService>();
            SetupWarehouseEmpty(wh);

            var core = new DashboardCoreServices(new LogService(), data.Object, new AuthorizationService(),
                new Mock<ICardReaderService>().Object, wh.Object);
            var vm = new ProductInOutViewModel(core, new Mock<IDialogService>().Object);

            vm.SelectedProduct = product;
            vm.SelectedProcess = process;
            vm.FormQuantity    = 5;
            vm.FormLocationId  = null;   // 不指派

            await vm.SubmitFormCommand.ExecuteAsync(null);

            wh.Verify(w => w.AssignAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }
    }
}
