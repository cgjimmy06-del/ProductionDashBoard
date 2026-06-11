using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
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
            var core = new DashboardCoreServices(log, mock.Object, auth, cardReader);
            var dialog = new Mock<IDialogService>().Object;
            return new ScheduleViewModel(core, dialog);
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
        public void DerivedBadge_NoOrders_ReturnsNull()
        {
            var vm = CreateVm();
            var schedules = new List<ScheduleUiModel> { MakeSched(1, ScheduleStatus.Scheduled, quantity: 100) };
            vm.InjectDataForTest(schedules, new List<OrderProductionInfo>());

            Assert.Null(schedules[0].DerivedBadge);
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

            Assert.Equal("⬟ 生產中", schedules[0].DerivedBadge);
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

            Assert.Equal("✓ 生產完成", schedules[0].DerivedBadge);
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

            Assert.Equal("⚠ 部分完成", schedules[0].DerivedBadge);
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

            Assert.Equal("⚠ 取消注意", schedules[0].DerivedBadge);
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
            Assert.Equal("⚠ 部分完成", schedules[0].DerivedBadge);
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

            Assert.Equal("已等待 3 天", schedules[0].WaitingDaysText);
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

            Assert.Equal("⚠ 已等待 7 天", schedules[0].WaitingDaysText);
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
    }
}
