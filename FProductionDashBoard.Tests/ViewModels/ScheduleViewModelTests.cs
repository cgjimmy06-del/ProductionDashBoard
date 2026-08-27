using FProductionDashBoard.Models;
using FProductionDashBoard.Properties;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class ScheduleViewModelTests
    {
        // LoadAllAsync 使用 IDataService + WPF Dispatcher，xUnit 無 WPF 環境。
        // 本測試透過 InjectDataForTest 繞過非同步載入，專注驗證：
        //   Filter / ComputeStats / DerivedBadge / WaitingDaysText

        private static IReadOnlyList<ScheduleUiModel> GetSchedules(ScheduleViewModel vm)
            => vm.SchedulesView.Cast<ScheduleUiModel>().ToList();

        private static ScheduleViewModel CreateVm()
        {
            var mock = new Mock<IDataService>();
            mock.Setup(d => d.GetAllSchedulesAsync()).ReturnsAsync(new List<Schedule>());
            mock.Setup(d => d.GetAllOrderProductionsAsync()).ReturnsAsync(new List<OrderProduction>());
            var log = new LogService();
            var auth = new AuthorizationService();
            var cardReader = new Mock<ICardReaderService>().Object;
            var core = new DashboardCoreServices(log, mock.Object, auth, cardReader, new Mock<IWarehouseService>().Object);
            var dialog = new Mock<IDialogService>().Object;
            return new ScheduleViewModel(core, dialog, new ListsFromSql());
        }

        private static ScheduleUiModel MakeSched(int id, ScheduleStatus status,
            string partNo = "P1", string modelName = "M1", string processName = "Pr1",
            string? lotNo = null, int quantity = 100, int? actualQty = null,
            DateTime? receivedAt = null)
            => new()
            {
                ScheduleId    = id,
                Status        = status,
                PartNo        = partNo,
                ModelName     = modelName,
                ProcessName   = processName,
                LotNo         = lotNo,
                Quantity      = quantity,
                ActualQuantity = actualQty,
                ReceivedAt    = receivedAt ?? DateTime.Today
            };

        private static OrderProductionInfo MakeOrder(int scheduleId, OrderProductionStatus status, int quantity = 50)
            => new()
            {
                OrderId     = scheduleId * 100,
                ScheduleId  = scheduleId,
                Status      = status,
                Quantity    = quantity
            };

        private static OrderProductionInfo MakeOrderForEquip(int orderId, int equipmentId, OrderProductionStatus status, int quantity = 50)
            => new()
            {
                OrderId     = orderId,
                EquipmentId = equipmentId,
                Status      = status,
                Quantity    = quantity
            };

        private static ScheduleUiModel MakeSchedWithProduct(int id, ScheduleStatus status, int productId, int processId)
            => new()
            {
                ScheduleId  = id,
                Status      = status,
                ProductId   = productId,
                ProcessId   = processId,
                PartNo      = "P1",
                Quantity    = 100,
                ReceivedAt  = DateTime.Today
            };

        private static ScheduleEquipmentCardViewModel MakeFocusCard(int equipmentId, string name, int productId = 1, int processId = 1)
        {
            var ep = new EquipmentProduct
            {
                EquipmentId      = equipmentId,
                ProductionStatus = TuningType.Feasible,
                Sop              = new SopChecklist { ProductId = productId, ProcessId = processId }
            };
            return new ScheduleEquipmentCardViewModel
            {
                EquipmentId          = equipmentId,
                Code                 = $"M{equipmentId:D2}",
                Name                 = name,
                AllEquipmentProducts = new List<EquipmentProduct> { ep }
            };
        }

        private static (ScheduleViewModel vm, Mock<IDataService> dataMock, Mock<IDialogService> dialogMock) CreateVmWithFullMock()
        {
            var dataMock = new Mock<IDataService>();
            dataMock.Setup(d => d.GetAllSchedulesAsync()).ReturnsAsync(new List<Schedule>());
            dataMock.Setup(d => d.GetAllOrderProductionsAsync()).ReturnsAsync(new List<OrderProduction>());
            dataMock.Setup(d => d.GetAllEquipmentProductsAsync()).ReturnsAsync(new List<EquipmentProduct>());
            dataMock.Setup(d => d.GetAllInProgressProgramTuningAsync()).ReturnsAsync(new List<ProgramTuningRecord>());
            var log        = new LogService();
            var auth       = new AuthorizationService();
            var cardReader = new Mock<ICardReaderService>().Object;
            var core       = new DashboardCoreServices(log, dataMock.Object, auth, cardReader, new Mock<IWarehouseService>().Object);
            var dialogMock = new Mock<IDialogService>();
            var vm         = new ScheduleViewModel(core, dialogMock.Object, new ListsFromSql());
            return (vm, dataMock, dialogMock);
        }

        // 調試命令需登入使用者（StartTuningAsync 讀 CurrentUser!）＋ UsersList 含執行人（employees 來源）
        private static (ScheduleViewModel vm, Mock<IDataService> dataMock, Mock<IDialogService> dialogMock) CreateVmForTuning(UserInfo currentUser)
        {
            var dataMock = new Mock<IDataService>();
            dataMock.Setup(d => d.GetAllSchedulesAsync()).ReturnsAsync(new List<Schedule>());
            dataMock.Setup(d => d.GetAllOrderProductionsAsync()).ReturnsAsync(new List<OrderProduction>());
            dataMock.Setup(d => d.GetAllEquipmentProductsAsync()).ReturnsAsync(new List<EquipmentProduct>());
            dataMock.Setup(d => d.GetAllInProgressProgramTuningAsync()).ReturnsAsync(new List<ProgramTuningRecord>());
            var log  = new LogService();
            var auth = new AuthorizationService();
            auth.SetCachedRoles(new List<Role>
            {
                new()
                {
                    RoleId = 1,
                    RolePermissions = new List<RolePermission>
                    {
                        new() { RoleId = 1, PermissionId = PermissionId.OperateTuning },
                        new() { RoleId = 1, PermissionId = PermissionId.Setting },
                    },
                },
            });
            auth.InitializeAsync(currentUser).GetAwaiter().GetResult();
            var cardReader = new Mock<ICardReaderService>().Object;
            var core       = new DashboardCoreServices(log, dataMock.Object, auth, cardReader, new Mock<IWarehouseService>().Object);
            var dialogMock = new Mock<IDialogService>();
            var lists      = new ListsFromSql { UsersList = new List<UserInfo> { currentUser } };
            var vm         = new ScheduleViewModel(core, dialogMock.Object, lists);
            return (vm, dataMock, dialogMock);
        }

        private static UserInfo MakeTuningUser() => new() { Id = 5, UserId = "u5", Name = "Tester", RoleId = 1 };

        // ─── Filter：AllActive ────────────────────────────────────────────────

        [Fact]
        public void Filter_AllActive_ShowsPendingAndScheduled_HidesOthers()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel>
            {
                MakeSched(1, ScheduleStatus.Pending),
                MakeSched(2, ScheduleStatus.Scheduled),
                MakeSched(3, ScheduleStatus.Completed),
                MakeSched(4, ScheduleStatus.Released),
                MakeSched(5, ScheduleStatus.Cancelled),
            };
            vm.InjectDataForTest(schedules, new List<OrderProductionInfo>());
            vm.DateRangeStart = null;
            vm.DateRangeEnd   = null;

            vm.StatusFilter = ScheduleViewFilter.AllActive;

            var visible = GetSchedules(vm);
            Assert.Equal(2, visible.Count);
            Assert.Contains(visible, s => s.ScheduleId == 1);
            Assert.Contains(visible, s => s.ScheduleId == 2);
        }

        // ─── Filter：各狀態篩選 ───────────────────────────────────────────────

        [Theory]
        [InlineData(ScheduleViewFilter.Pending,   ScheduleStatus.Pending)]
        [InlineData(ScheduleViewFilter.Scheduled, ScheduleStatus.Scheduled)]
        [InlineData(ScheduleViewFilter.Completed, ScheduleStatus.Completed)]
        [InlineData(ScheduleViewFilter.Released,  ScheduleStatus.Released)]
        [InlineData(ScheduleViewFilter.Cancelled, ScheduleStatus.Cancelled)]
        public void Filter_ByStatus_ShowsOnlyMatchingStatus(ScheduleViewFilter filter, ScheduleStatus expected)
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel>
            {
                MakeSched(1, ScheduleStatus.Pending),
                MakeSched(2, ScheduleStatus.Scheduled),
                MakeSched(3, ScheduleStatus.Completed),
                MakeSched(4, ScheduleStatus.Released),
                MakeSched(5, ScheduleStatus.Cancelled),
            };
            vm.InjectDataForTest(schedules, new List<OrderProductionInfo>());
            vm.DateRangeStart = null;
            vm.DateRangeEnd   = null;

            vm.StatusFilter = filter;

            var visible = GetSchedules(vm);
            Assert.Single(visible);
            Assert.Equal(expected, visible[0].Status);
        }

        // ─── Filter：ProductionDone ───────────────────────────────────────────

        [Fact]
        public void Filter_ProductionDone_ShowsOnlyScheduledWithCompletedBadge()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel>
            {
                MakeSched(1, ScheduleStatus.Scheduled, quantity: 100, actualQty: null),
                MakeSched(2, ScheduleStatus.Scheduled, quantity: 50,  actualQty: null),
                MakeSched(3, ScheduleStatus.Pending),
            };
            var orders = new List<OrderProductionInfo>
            {
                MakeOrder(2, OrderProductionStatus.Completed, quantity: 50),  // badge = "✓ 生產完成"
            };
            vm.InjectDataForTest(schedules, orders);
            vm.DateRangeStart = null;
            vm.DateRangeEnd   = null;

            vm.StatusFilter = ScheduleViewFilter.ProductionDone;

            var visible = GetSchedules(vm);
            Assert.Single(visible);
            Assert.Equal(2, visible[0].ScheduleId);
        }

        // ─── Filter：SearchText ───────────────────────────────────────────────

        [Fact]
        public void Filter_SearchText_MatchesPartNoModelNameLotNo_CaseInsensitive()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel>
            {
                MakeSched(1, ScheduleStatus.Pending, partNo: "ABC-001"),
                MakeSched(2, ScheduleStatus.Pending, modelName: "XYZ-MODEL"),
                MakeSched(3, ScheduleStatus.Pending, lotNo: "LOT-999"),
                MakeSched(4, ScheduleStatus.Pending, partNo: "OTHER"),
            };
            vm.InjectDataForTest(schedules, new List<OrderProductionInfo>());
            vm.DateRangeStart = null;
            vm.DateRangeEnd   = null;
            vm.StatusFilter   = ScheduleViewFilter.AllActive;

            vm.SearchText = "abc";

            var visible = GetSchedules(vm);
            Assert.Single(visible);
            Assert.Equal(1, visible[0].ScheduleId);
        }

        [Fact]
        public void Filter_SearchText_MatchesLotNo()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel>
            {
                MakeSched(1, ScheduleStatus.Pending, lotNo: "LOT-999"),
                MakeSched(2, ScheduleStatus.Pending),
            };
            vm.InjectDataForTest(schedules, new List<OrderProductionInfo>());
            vm.DateRangeStart = null;
            vm.DateRangeEnd   = null;
            vm.StatusFilter   = ScheduleViewFilter.AllActive;

            vm.SearchText = "lot";

            var visible = GetSchedules(vm);
            Assert.Single(visible);
            Assert.Equal(1, visible[0].ScheduleId);
        }

        // ─── Filter：ProcessFilter ────────────────────────────────────────────

        [Fact]
        public void Filter_Process_ExcludesNonMatchingProcess()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel>
            {
                MakeSched(1, ScheduleStatus.Pending, processName: "Welding"),
                MakeSched(2, ScheduleStatus.Pending, processName: "Assembly"),
            };
            vm.InjectDataForTest(schedules, new List<OrderProductionInfo>());
            vm.DateRangeStart = null;
            vm.DateRangeEnd   = null;
            vm.StatusFilter   = ScheduleViewFilter.AllActive;

            vm.ProcessFilter = "weld";

            var visible = GetSchedules(vm);
            Assert.Single(visible);
            Assert.Equal(1, visible[0].ScheduleId);
        }

        // ─── Filter：DateRange ────────────────────────────────────────────────

        [Fact]
        public void Filter_DateRange_ExcludesOutOfRangeItems()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel>
            {
                MakeSched(1, ScheduleStatus.Pending, receivedAt: new DateTime(2026, 1, 1)),
                MakeSched(2, ScheduleStatus.Pending, receivedAt: new DateTime(2026, 6, 1)),
            };
            vm.InjectDataForTest(schedules, new List<OrderProductionInfo>());
            vm.StatusFilter = ScheduleViewFilter.AllActive;

            vm.DateRangeStart = new DateTime(2026, 5, 1);
            vm.DateRangeEnd   = new DateTime(2026, 6, 30);

            var visible = GetSchedules(vm);
            Assert.Single(visible);
            Assert.Equal(2, visible[0].ScheduleId);
        }

        // ─── ComputeStats ────────────────────────────────────────────────────

        [Fact]
        public void ComputeStats_CountsCorrectly()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel>
            {
                MakeSched(1, ScheduleStatus.Pending,   quantity: 100),
                MakeSched(2, ScheduleStatus.Pending,   quantity: 200),
                MakeSched(3, ScheduleStatus.Scheduled, quantity: 300, actualQty: 150),
                MakeSched(4, ScheduleStatus.Scheduled, quantity: 400, actualQty: 400),
                MakeSched(5, ScheduleStatus.Completed, quantity: 50),
            };
            vm.InjectDataForTest(schedules, new List<OrderProductionInfo>());
            vm.DateRangeStart = null;
            vm.DateRangeEnd   = null;

            // 5 筆全部非 Cancelled → StatTotal=5, qty=100+200+300+400+50=1050
            Assert.Equal(5, vm.StatTotal);
            Assert.Equal(1050, vm.StatTotalQty);
            Assert.Equal(2, vm.StatPending);
            Assert.Equal(300, vm.StatPendingQty);
            Assert.Equal(2, vm.StatScheduled);
            Assert.Equal(550, vm.StatScheduledActualQty); // 150+400
        }

        [Fact]
        public void ComputeStats_UnderScheduled_CountsCorrectly()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel>
            {
                MakeSched(1, ScheduleStatus.Scheduled, quantity: 100, actualQty: 80),  // 20 件缺口
                MakeSched(2, ScheduleStatus.Scheduled, quantity: 200, actualQty: 200), // 足額
                MakeSched(3, ScheduleStatus.Scheduled, quantity: 150, actualQty: 100), // 50 件缺口
            };
            vm.InjectDataForTest(schedules, new List<OrderProductionInfo>());
            vm.DateRangeStart = null;
            vm.DateRangeEnd   = null;

            Assert.Equal(2, vm.StatUnderScheduledCount);
            Assert.Equal(70, vm.StatUnderScheduledQtyDeficit); // 20+50
        }

        [Fact]
        public void ComputeStats_IsIndependentOfSearchFilter()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel>
            {
                MakeSched(1, ScheduleStatus.Pending, partNo: "ABC"),
                MakeSched(2, ScheduleStatus.Pending, partNo: "XYZ"),
            };
            vm.InjectDataForTest(schedules, new List<OrderProductionInfo>());
            vm.DateRangeStart = null;
            vm.DateRangeEnd   = null;

            var statBeforeFilter = vm.StatPending;
            vm.SearchText = "ABC"; // 篩選後只顯示 1 筆

            Assert.Equal(statBeforeFilter, vm.StatPending); // 統計不受 SearchText 影響
            Assert.Equal(2, vm.StatPending);
        }

        // ─── DerivedBadge ────────────────────────────────────────────────────

        [Fact]
        public void DerivedBadge_NoOrdersAndNoSopType_ReturnsNewProductBadge()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel> { MakeSched(1, ScheduleStatus.Scheduled, quantity: 100) };
            vm.InjectDataForTest(schedules, new List<OrderProductionInfo>());

            Assert.Equal(Resources.SchDerivedBadgeNewProduct, schedules[0].DerivedBadge);
            Assert.Equal(DerivedBadgeKeys.NewProduct, schedules[0].DerivedBadgeKey);
        }

        [Fact]
        public void DerivedBadge_HasInProductionOrder_ReturnsInProductionBadge()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel> { MakeSched(1, ScheduleStatus.Scheduled, quantity: 100) };
            var orders = new List<OrderProductionInfo>
            {
                MakeOrder(1, OrderProductionStatus.InProduction, 50),
                MakeOrder(1, OrderProductionStatus.Pending, 50),
            };
            vm.InjectDataForTest(schedules, orders);

            Assert.Equal(Resources.SchDerivedBadgeInProduction, schedules[0].DerivedBadge);
            Assert.Equal(DerivedBadgeKeys.InProduction, schedules[0].DerivedBadgeKey);
        }

        [Fact]
        public void DerivedBadge_AllCompleted_SufficientQty_ReturnsProductionDone()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel> { MakeSched(1, ScheduleStatus.Scheduled, quantity: 100) };
            var orders = new List<OrderProductionInfo>
            {
                MakeOrder(1, OrderProductionStatus.Completed, 100),
            };
            vm.InjectDataForTest(schedules, orders);

            Assert.Equal(Resources.SchDerivedBadgeComplete, schedules[0].DerivedBadge);
            Assert.Equal(DerivedBadgeKeys.Complete, schedules[0].DerivedBadgeKey);
        }

        [Fact]
        public void DerivedBadge_AllCompleted_InsufficientQty_ReturnsPartialDone()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel> { MakeSched(1, ScheduleStatus.Scheduled, quantity: 100) };
            var orders = new List<OrderProductionInfo>
            {
                MakeOrder(1, OrderProductionStatus.Completed, 60),
            };
            vm.InjectDataForTest(schedules, orders);

            Assert.Equal(Resources.SchDerivedBadgePartial, schedules[0].DerivedBadge);
            Assert.Equal(DerivedBadgeKeys.Partial, schedules[0].DerivedBadgeKey);
        }

        [Fact]
        public void DerivedBadge_AllCancelled_ReturnsCancelWarning()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel> { MakeSched(1, ScheduleStatus.Scheduled, quantity: 100) };
            var orders = new List<OrderProductionInfo>
            {
                MakeOrder(1, OrderProductionStatus.Cancelled, 50),
                MakeOrder(1, OrderProductionStatus.Cancelled, 50),
            };
            vm.InjectDataForTest(schedules, orders);

            Assert.Equal(Resources.SchDerivedBadgeCancelNotice, schedules[0].DerivedBadge);
            Assert.Equal(DerivedBadgeKeys.CancelNotice, schedules[0].DerivedBadgeKey);
        }

        [Fact]
        public void DerivedBadge_SomeCancelledSomeCompleted_CountsOnlyNonCancelled()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel> { MakeSched(1, ScheduleStatus.Scheduled, quantity: 100) };
            var orders = new List<OrderProductionInfo>
            {
                MakeOrder(1, OrderProductionStatus.Completed,  80),
                MakeOrder(1, OrderProductionStatus.Cancelled,  50), // 不計入
            };
            vm.InjectDataForTest(schedules, orders);

            // nonCancelledQty = 80 < 100 → 部分完成
            Assert.Equal(Resources.SchDerivedBadgePartial, schedules[0].DerivedBadge);
            Assert.Equal(DerivedBadgeKeys.Partial, schedules[0].DerivedBadgeKey);
        }

        // ─── WaitingDaysText ─────────────────────────────────────────────────

        [Fact]
        public void WaitingDaysText_PendingAndToday_IsNull()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel>
            {
                MakeSched(1, ScheduleStatus.Pending, receivedAt: DateTime.Today)
            };
            vm.InjectDataForTest(schedules, new List<OrderProductionInfo>());

            Assert.Null(schedules[0].WaitingDaysText);
        }

        [Fact]
        public void WaitingDaysText_PendingAnd3DaysAgo_ShowsWaitingText()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel>
            {
                MakeSched(1, ScheduleStatus.Pending, receivedAt: DateTime.Today.AddDays(-3))
            };
            vm.InjectDataForTest(schedules, new List<OrderProductionInfo>());

            Assert.Equal($"{Resources.WaitedDaysForSchedule} 3 {Resources.ComStrDay}", schedules[0].WaitingDaysText);
        }

        [Fact]
        public void WaitingDaysText_PendingAndOver5Days_ShowsWarningIcon()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel>
            {
                MakeSched(1, ScheduleStatus.Pending, receivedAt: DateTime.Today.AddDays(-7))
            };
            vm.InjectDataForTest(schedules, new List<OrderProductionInfo>());

            Assert.Equal($"⚠ {Resources.WaitedDaysForSchedule} 7 {Resources.ComStrDay}", schedules[0].WaitingDaysText);
        }

        [Fact]
        public void WaitingDaysText_NonPendingStatus_IsNull()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel>
            {
                MakeSched(1, ScheduleStatus.Scheduled, receivedAt: DateTime.Today.AddDays(-10))
            };
            vm.InjectDataForTest(schedules, new List<OrderProductionInfo>());

            Assert.Null(schedules[0].WaitingDaysText);
        }

        // ─── PR3：設備焦點模式 ───────────────────────────────────────────────────

        [Fact]
        public void SelectEquipmentCard_SetsIsSelectedAndPopulatesOrders()
        {
            var vm = CreateVm();
            var orders = new List<OrderProductionInfo>
            {
                MakeOrderForEquip(101, equipmentId: 1, OrderProductionStatus.Pending, 50),
                MakeOrderForEquip(102, equipmentId: 2, OrderProductionStatus.Pending, 30),
            };
            vm.InjectDataForTest(new List<ScheduleUiModel>(), orders);

            var card = MakeFocusCard(1, "Machine-01");
            vm.SelectEquipmentCardCommand.Execute(card);

            Assert.Same(card, vm.SelectedEquipmentCard);
            Assert.True(card.IsSelected);
            Assert.True(vm.IsEquipmentDetailVisible);
            Assert.False(vm.IsCardWallVisible);
            Assert.Single(vm.SelectedEquipmentOrders);
            Assert.Equal(101, vm.SelectedEquipmentOrders[0].OrderId);
        }

        [Fact]
        public void SelectEquipmentCard_ClearsSelectedSchedule()
        {
            var vm = CreateVm();
            var schedule = MakeSched(1, ScheduleStatus.Pending);
            vm.InjectDataForTest(new List<ScheduleUiModel> { schedule }, new List<OrderProductionInfo>());
            vm.DateRangeStart = null;
            vm.DateRangeEnd   = null;
            vm.SelectedSchedule = schedule;

            var card = MakeFocusCard(1, "Machine-01");
            vm.SelectEquipmentCardCommand.Execute(card);

            Assert.Null(vm.SelectedSchedule);
        }

        [Fact]
        public void DeselectEquipmentCard_ResetsToLayer1()
        {
            var vm = CreateVm();
            vm.InjectDataForTest(new List<ScheduleUiModel>(), new List<OrderProductionInfo>());

            var card = MakeFocusCard(1, "Machine-01");
            vm.SelectEquipmentCardCommand.Execute(card);
            Assert.True(vm.IsEquipmentDetailVisible);

            vm.DeselectEquipmentCardCommand.Execute(null);

            Assert.Null(vm.SelectedEquipmentCard);
            Assert.False(card.IsSelected);
            Assert.True(vm.IsCardWallVisible);
            Assert.False(vm.IsEquipmentDetailVisible);
            Assert.Empty(vm.SelectedEquipmentOrders);
        }

        [Fact]
        public void SelectSchedule_InFocusMode_DoesNotClearSelectedEquipmentCard()
        {
            var vm = CreateVm();
            var schedule = MakeSched(1, ScheduleStatus.Pending);
            vm.InjectDataForTest(new List<ScheduleUiModel> { schedule }, new List<OrderProductionInfo>());
            vm.DateRangeStart = null;
            vm.DateRangeEnd   = null;

            var card = MakeFocusCard(1, "Machine-01");
            vm.SelectEquipmentCardCommand.Execute(card);
            Assert.NotNull(vm.SelectedEquipmentCard);

            vm.SelectedSchedule = schedule;

            Assert.Same(card, vm.SelectedEquipmentCard);
            Assert.True(card.IsSelected);
            Assert.Same(schedule, vm.SelectedSchedule);
        }

        [Fact]
        public void FilterSchedule_FocusMode_OnlyShowsCompatibleSchedules()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel>
            {
                MakeSchedWithProduct(1, ScheduleStatus.Pending, productId: 1, processId: 1),
                MakeSchedWithProduct(2, ScheduleStatus.Pending, productId: 2, processId: 1),
            };
            vm.InjectDataForTest(schedules, new List<OrderProductionInfo>());
            vm.DateRangeStart = null;
            vm.DateRangeEnd   = null;

            var card = MakeFocusCard(1, "Machine-01", productId: 1, processId: 1);
            vm.SelectEquipmentCardCommand.Execute(card);

            var visible = vm.SchedulesView.Cast<ScheduleUiModel>().ToList();
            Assert.Single(visible);
            Assert.Equal(1, visible[0].ScheduleId);
        }

        [Fact]
        public void UpdateSelectedEquipmentOrders_ComputesStatsCorrectly()
        {
            var vm = CreateVm();
            var orders = new List<OrderProductionInfo>
            {
                MakeOrderForEquip(101, 1, OrderProductionStatus.Pending,      50),
                MakeOrderForEquip(102, 1, OrderProductionStatus.Pending,      30),
                MakeOrderForEquip(103, 1, OrderProductionStatus.InProduction, 80),
            };
            vm.InjectDataForTest(new List<ScheduleUiModel>(), orders);

            var card = MakeFocusCard(1, "Machine-01");
            vm.SelectEquipmentCardCommand.Execute(card);

            Assert.Equal(2, vm.EquipDetailPendingCount);
            Assert.Equal(80, vm.EquipDetailPendingQty);
            Assert.Equal(1, vm.EquipDetailInProductionCount);
            Assert.Equal(80, vm.EquipDetailInProductionQty);
        }

        [Fact]
        public async Task StartOrderProduction_CallsServiceAndReloads()
        {
            var (vm, dataMock, _) = CreateVmWithFullMock();
            dataMock.Setup(d => d.StartProductionAsync(It.IsAny<int>(), It.IsAny<int>()))
                    .Returns(Task.CompletedTask);
            var order = MakeOrderForEquip(101, 1, OrderProductionStatus.Pending);

            await vm.StartOrderProductionCommand.ExecuteAsync(order);

            dataMock.Verify(d => d.StartProductionAsync(101, It.IsAny<int>()), Times.Once);
            dataMock.Verify(d => d.GetAllSchedulesAsync(), Times.AtLeast(1));
        }

        [Fact]
        public async Task EndOrderProduction_CallsServiceAndReloads()
        {
            var (vm, dataMock, _) = CreateVmWithFullMock();
            dataMock.Setup(d => d.EndProductionAsync(It.IsAny<int>()))
                    .Returns(Task.CompletedTask);
            var order = MakeOrderForEquip(102, 1, OrderProductionStatus.InProduction);

            await vm.EndOrderProductionCommand.ExecuteAsync(order);

            dataMock.Verify(d => d.EndProductionAsync(102), Times.Once);
            dataMock.Verify(d => d.GetAllSchedulesAsync(), Times.AtLeast(1));
        }

        [Fact]
        public async Task CancelOrderProduction_ConfirmedWithDescription_CallsService()
        {
            var (vm, dataMock, dialogMock) = CreateVmWithFullMock();
            dataMock.Setup(d => d.CancelOrderAsync(It.IsAny<int>(), It.IsAny<string?>()))
                    .Returns(Task.CompletedTask);
            dialogMock.Setup(d => d.ShowDialog(It.IsAny<CancelOrderConfirmationDialogViewModel>()))
                      .Callback<DialogBaseViewModel<CancelOrderResult>>(raw =>
                      {
                          var dvm = (CancelOrderConfirmationDialogViewModel)raw;
                          dvm.Description = "test reason";
                          dvm.ConfirmCommand.Execute(null);
                      })
                      .Returns<DialogBaseViewModel<CancelOrderResult>>(raw => ((CancelOrderConfirmationDialogViewModel)raw).Result);
            var order = MakeOrderForEquip(103, 1, OrderProductionStatus.Pending);

            await vm.CancelOrderProductionCommand.ExecuteAsync(order);

            dataMock.Verify(d => d.CancelOrderAsync(103, "test reason"), Times.Once);
        }

        [Fact]
        public async Task CancelOrderProduction_Dismissed_DoesNotCallService()
        {
            var (vm, dataMock, dialogMock) = CreateVmWithFullMock();
            dialogMock.Setup(d => d.ShowDialog(It.IsAny<CancelOrderConfirmationDialogViewModel>()))
                      .Returns<CancelOrderConfirmationDialogViewModel>(dvm => null);
            var order = MakeOrderForEquip(103, 1, OrderProductionStatus.Pending);

            await vm.CancelOrderProductionCommand.ExecuteAsync(order);

            dataMock.Verify(d => d.CancelOrderAsync(It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
        }

        // ─── 安排調試命令（Arrange 分派 / Start / End）──────────────────────────
        // ArrangeTuning 依 card.ActiveTuning 分派：非 null → End、null → Start。
        // 對話框讀 vm.IsConfirmed/Result（非回傳值），故確認一律用 Callback 就地觸發 ConfirmCommand。

        [Fact]
        public async Task ArrangeTuning_EndBranch_Confirmed_CallsEndAndReloads()
        {
            var (vm, dataMock, dialogMock) = CreateVmForTuning(MakeTuningUser());
            dataMock.Setup(d => d.EndProgramTuningAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<string?>()))
                    .Returns(Task.CompletedTask);

            var card = MakeFocusCard(1, "Machine-01");
            card.ActiveTuning = new ProgramTuningRecord
            {
                ProgramTuningId = 42, EquipmentId = 1,
                TuningType = TuningType.Teaching, StartedAt = new DateTime(2026, 3, 10, 8, 0, 0)
            };
            vm.SelectEquipmentCardCommand.Execute(card);

            // End 確認為無守衛 DialogBaseViewModel<bool>，Callback 就地觸發確認
            dialogMock.Setup(d => d.ShowDialog(It.IsAny<DialogBaseViewModel<bool>>()))
                      .Callback<DialogBaseViewModel<bool>>(raw => raw.ConfirmCommand.Execute(null))
                      .Returns<DialogBaseViewModel<bool>>(raw => raw.IsConfirmed);

            await vm.ArrangeTuningCommand.ExecuteAsync(null);

            dataMock.Verify(d => d.EndProgramTuningAsync(42, It.IsAny<DateTime>(), It.IsAny<string?>()), Times.Once);
            dataMock.Verify(d => d.GetAllSchedulesAsync(), Times.AtLeast(1));
        }

        [Fact]
        public async Task ArrangeTuning_EndBranch_Dismissed_DoesNotCallEnd()
        {
            var (vm, dataMock, _) = CreateVmForTuning(MakeTuningUser());

            var card = MakeFocusCard(1, "Machine-01");
            card.ActiveTuning = new ProgramTuningRecord
            {
                ProgramTuningId = 42, EquipmentId = 1,
                TuningType = TuningType.Teaching, StartedAt = new DateTime(2026, 3, 10, 8, 0, 0)
            };
            vm.SelectEquipmentCardCommand.Execute(card);

            // 未觸發 ConfirmCommand → confirmVm.IsConfirmed 保持 false → 生產碼 return
            await vm.ArrangeTuningCommand.ExecuteAsync(null);

            dataMock.Verify(d => d.EndProgramTuningAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task ArrangeTuning_EndBranch_ServiceThrows_DoesNotCrash()
        {
            var (vm, dataMock, dialogMock) = CreateVmForTuning(MakeTuningUser());
            dataMock.Setup(d => d.EndProgramTuningAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<string?>()))
                    .ThrowsAsync(new Exception("db down"));

            var card = MakeFocusCard(1, "Machine-01");
            card.ActiveTuning = new ProgramTuningRecord
            {
                ProgramTuningId = 42, EquipmentId = 1,
                TuningType = TuningType.Teaching, StartedAt = new DateTime(2026, 3, 10, 8, 0, 0)
            };
            vm.SelectEquipmentCardCommand.Execute(card);
            dialogMock.Setup(d => d.ShowDialog(It.IsAny<DialogBaseViewModel<bool>>()))
                      .Callback<DialogBaseViewModel<bool>>(raw => raw.ConfirmCommand.Execute(null))
                      .Returns<DialogBaseViewModel<bool>>(raw => raw.IsConfirmed);

            // 失敗保留可重試：catch 內只記 log，例外被吞、不逸出崩潰（本專案無全域例外處理器）
            await vm.ArrangeTuningCommand.ExecuteAsync(null);

            dataMock.Verify(d => d.EndProgramTuningAsync(42, It.IsAny<DateTime>(), It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task ArrangeTuning_StartBranch_Confirmed_CallsStartAndReloads()
        {
            var user = MakeTuningUser();
            var (vm, dataMock, dialogMock) = CreateVmForTuning(user);
            var teachingEp = new EquipmentProduct
            {
                EquipmentProductId = 100, SeqNo = 1, SopId = 1, ProductionStatus = TuningType.Teaching
            };
            dataMock.Setup(d => d.GetEquipmentProductsByEquipmentAsync(1))
                    .ReturnsAsync(new List<EquipmentProduct> { teachingEp });
            dataMock.Setup(d => d.GetLastCompletedTeachingNamesByEquipmentAsync(1))
                    .ReturnsAsync(new Dictionary<int, string>());
            dataMock.Setup(d => d.StartProgramTuningAsync(
                        It.IsAny<int>(), It.IsAny<int>(), It.IsAny<TuningType>(),
                        It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
                    .ReturnsAsync(1);

            var card = MakeFocusCard(1, "Machine-01"); // ActiveTuning=null → Start 分支
            vm.SelectEquipmentCardCommand.Execute(card);

            // TuningDialogViewModel.ConfirmCommand 有守衛，需先選品項（執行人已預設當前使用者）
            dialogMock.Setup(d => d.ShowDialog(It.IsAny<TuningDialogViewModel>()))
                      .Callback<DialogBaseViewModel<TuningResult>>(raw =>
                      {
                          var dvm = (TuningDialogViewModel)raw;
                          dvm.SelectedEquipmentProduct = dvm.FilteredEquipmentProducts.First();
                          dvm.ConfirmCommand.Execute(null);
                      })
                      .Returns<DialogBaseViewModel<TuningResult>>(raw => ((TuningDialogViewModel)raw).Result);

            await vm.ArrangeTuningCommand.ExecuteAsync(null);

            // startedBy=Executor.Id、managedBy=currentUser.Id，兩者皆為當前使用者(5)；forceArrange=false
            dataMock.Verify(d => d.StartProgramTuningAsync(
                1, 100, TuningType.Teaching, 5, 5, It.IsAny<DateTime>(), false), Times.Once);
            dataMock.Verify(d => d.GetAllSchedulesAsync(), Times.AtLeast(1));
        }

        [Fact]
        public async Task ArrangeTuning_StartBranch_Dismissed_DoesNotCallStart()
        {
            var (vm, dataMock, _) = CreateVmForTuning(MakeTuningUser());
            // 讓對話框能正常建構（非走載入失敗路徑），再驗證「取消」不寫入
            dataMock.Setup(d => d.GetEquipmentProductsByEquipmentAsync(1))
                    .ReturnsAsync(new List<EquipmentProduct>
                    {
                        new() { EquipmentProductId = 100, SeqNo = 1, SopId = 1, ProductionStatus = TuningType.Teaching }
                    });
            dataMock.Setup(d => d.GetLastCompletedTeachingNamesByEquipmentAsync(1))
                    .ReturnsAsync(new Dictionary<int, string>());

            var card = MakeFocusCard(1, "Machine-01");
            vm.SelectEquipmentCardCommand.Execute(card);

            // dialog 未確認 → vm.IsConfirmed=false / Result=null → 生產碼 return
            await vm.ArrangeTuningCommand.ExecuteAsync(null);

            dataMock.Verify(d => d.StartProgramTuningAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<TuningType>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public void ComputeBadges_SetsActiveOrderCountAndEquipmentCount()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel>
            {
                MakeSched(1, ScheduleStatus.Scheduled, quantity: 100),
            };
            var orders = new List<OrderProductionInfo>
            {
                new() { OrderId = 101, ScheduleId = 1, EquipmentId = 1, Status = OrderProductionStatus.Pending,      Quantity = 30 },
                new() { OrderId = 102, ScheduleId = 1, EquipmentId = 2, Status = OrderProductionStatus.InProduction, Quantity = 50 },
                new() { OrderId = 103, ScheduleId = 1, EquipmentId = 1, Status = OrderProductionStatus.Completed,    Quantity = 20 },
            };
            vm.InjectDataForTest(schedules, orders);

            // ActiveOrderCount currently tracks all non-cancelled orders for the schedule.
            Assert.Equal(3, schedules[0].ActiveOrderCount);
            Assert.Equal(2, schedules[0].ActiveEquipmentCount);
            Assert.True(schedules[0].HasActiveOrders);
        }

        // ─── 焦點模式返回後卡片狀態重置（Medium-1 回歸）────────────────────────

        [Fact]
        public void DeselectEquipmentCard_AfterLayer2_ResetsAllCardCompatibility()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel>
            {
                MakeSchedWithProduct(1, ScheduleStatus.Pending, productId: 1, processId: 1),
            };
            vm.InjectDataForTest(schedules, new List<OrderProductionInfo>());

            var compatibleEp = new EquipmentProduct
            {
                EquipmentId      = 10,
                ProductionStatus = TuningType.Feasible,
                Sop              = new SopChecklist { ProductId = 1, ProcessId = 1 }
            };
            var otherEp = new EquipmentProduct
            {
                EquipmentId      = 20,
                ProductionStatus = TuningType.Feasible,
                Sop              = new SopChecklist { ProductId = 2, ProcessId = 2 }
            };
            vm.InjectCardsForTest(new List<EquipmentProduct> { compatibleEp, otherEp });

            // Layer 2：選取排單，otherCard 應標記為不相容
            vm.SelectedSchedule = schedules[0];
            var cards = vm.EquipmentCardsView.Cast<ScheduleEquipmentCardViewModel>().ToList();
            var allCards = new[] { compatibleEp, otherEp }
                .Select(ep => vm.EquipmentCardsView.Cast<ScheduleEquipmentCardViewModel>()
                    .FirstOrDefault(c => c.EquipmentId == ep.EquipmentId))
                .Where(c => c != null).ToList();

            // 進入焦點模式（點設備卡片）
            var cardToFocus = MakeFocusCard(10, "M10", productId: 1, processId: 1);
            vm.SelectEquipmentCardCommand.Execute(cardToFocus);
            Assert.NotNull(vm.SelectedEquipmentCard);

            // 返回：呼叫 DeselectEquipmentCard
            vm.DeselectEquipmentCardCommand.Execute(null);

            // 所有卡片的相容性旗標應已重置
            var allVisible = vm.EquipmentCardsView.Cast<ScheduleEquipmentCardViewModel>().ToList();
            Assert.All(allVisible, c =>
            {
                Assert.True(c.HasMatchingProgram);
                Assert.True(c.IsCompatibleWithSelectedSchedule);
            });
            Assert.Null(vm.SelectedEquipmentCard);
            Assert.Null(vm.SelectedSchedule);
        }
    }
}
