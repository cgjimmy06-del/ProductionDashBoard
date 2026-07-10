using FProductionDashBoard.Dtos;
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
    public class ChartViewModelTests
    {
        // LoadAsync 使用 IDataService + WPF Dispatcher，xUnit 無 WPF 環境。
        // 透過 InjectDataForTest 繞過非同步載入，驗證：
        //   頁籤建構 / 設備與排程列衍生欄位 / 統計計算 / 容器切換 / 預設排序

        private static ChartViewModel CreateVm()
        {
            var mock = new Mock<IDataService>();
            var log = new LogService();
            var auth = new AuthorizationService();
            var cardReader = new Mock<ICardReaderService>().Object;
            var core = new DashboardCoreServices(log, mock.Object, auth, cardReader);
            var store = new JsonChartDefinitionStore(
                _ => new List<ChartDefinition>(),
                (_, _) => { });
            return new ChartViewModel(core, store);
        }

        private static Equipment MakeEquipment(int id, string name)
            => new() { Id = id, Code = $"E{id:00}", Name = name };

        private static EquipmentProduct MakeEp(Equipment eq, TuningType status)
            => new() { EquipmentId = eq.Id, Equipment = eq, ProductionStatus = status };

        private static OrderProductionInfo MakeOrder(int orderId, int equipmentId,
            OrderProductionStatus status, int qty, int? scheduleId = null)
            => new() { OrderId = orderId, EquipmentId = equipmentId, Status = status, Quantity = qty, ScheduleId = scheduleId };

        private static Schedule MakeSchedule(int id, ScheduleStatus status, int quantity = 100, DateTime? receivedAt = null)
            => new() { ScheduleId = id, Status = status, Quantity = quantity, ReceivedAt = receivedAt };

        private static List<ChartRowViewModel> GetRows(ChartViewModel vm)
            => vm.RowsView.Cast<ChartRowViewModel>().ToList();

        private static void Inject(ChartViewModel vm,
            List<Schedule>? schedules = null, List<OrderProductionInfo>? orders = null,
            List<EquipmentProduct>? eps = null, List<ProgramTuningRecord>? tunings = null)
            => vm.InjectDataForTest(schedules ?? new(), orders ?? new(), eps ?? new(), tunings ?? new());

        // --- 頁籤 ---

        [Fact]
        public void Inject_BuildsTabs_SelectsLockedDefaultFirst()
        {
            var vm = CreateVm();

            Inject(vm);

            Assert.Equal(2, vm.Tabs.Count);
            Assert.Equal(DefaultChartDefinitions.EquipmentOverviewId, vm.SelectedTab?.Definition.Id);
            Assert.True(vm.Tabs[0].IsDefault);
            Assert.True(vm.IsCardContainer);
            Assert.False(vm.IsTableContainer);
        }

        // --- 設備視角列 ---

        [Fact]
        public void EquipmentRow_InProduction_DerivesStatusLoadAndProgress()
        {
            var vm = CreateVm();
            var eq = MakeEquipment(1, "CNC-01");
            var orders = new List<OrderProductionInfo>
            {
                MakeOrder(1, 1, OrderProductionStatus.InProduction, 30),
                MakeOrder(2, 1, OrderProductionStatus.Pending, 20),
                MakeOrder(3, 1, OrderProductionStatus.Completed, 50),
            };

            Inject(vm, orders: orders, eps: new() { MakeEp(eq, TuningType.Feasible) });

            var row = Assert.Single(GetRows(vm));
            Assert.Equal(FieldCatalog.ValInProduction, row.Values[FieldCatalog.ProductionStatus]);
            Assert.Equal(50, row.Values[FieldCatalog.TotalPendingQty]);
            Assert.Equal(FieldCatalog.ValLoadLow, row.Values[FieldCatalog.LoadLevel]);
            Assert.Equal(2, row.Values[FieldCatalog.ActiveOrderCount]);
            Assert.Equal(50, row.Values[FieldCatalog.ProgressCompleted]);
            Assert.Equal(100, row.Values[FieldCatalog.ProgressTarget]);
            Assert.Equal("CNC-01", row.Title);
            Assert.True(row.HasProgress);
            Assert.Equal(50, row.ProgressPercent);
            Assert.Equal("PrimaryBrush", row.BorderBrushKey);
        }

        [Fact]
        public void EquipmentRow_NoFeasibleEp_IsUnavailable_WithoutChips()
        {
            var vm = CreateVm();
            var eq = MakeEquipment(1, "EDM-03");

            Inject(vm, eps: new() { MakeEp(eq, TuningType.Pending) });

            var row = Assert.Single(GetRows(vm));
            Assert.Equal(FieldCatalog.ValUnavailable, row.Values[FieldCatalog.ProductionStatus]);
            Assert.Empty(row.Chips);   // 調試 None / 負載 None（rank 0）皆不出 chip
            Assert.Equal("IdleBrush", row.BorderBrushKey);
        }

        [Fact]
        public void EquipmentRow_ActiveTuning_ShowsWarningChip()
        {
            var vm = CreateVm();
            var eq = MakeEquipment(1, "CNC-05");

            Inject(vm,
                eps: new() { MakeEp(eq, TuningType.Feasible) },
                tunings: new() { new ProgramTuningRecord { EquipmentId = 1 } });

            var row = Assert.Single(GetRows(vm));
            Assert.Equal(FieldCatalog.ValTuningActive, row.Values[FieldCatalog.TuningStatus]);
            Assert.Contains(row.Chips, c => c.BrushKey == "WarningBrush");
        }

        [Fact]
        public void EquipmentRows_DefaultSort_ByNameAscending()
        {
            var vm = CreateVm();
            var eqB = MakeEquipment(1, "M-200");
            var eqA = MakeEquipment(2, "M-100");

            Inject(vm, eps: new()
            {
                MakeEp(eqB, TuningType.Feasible),
                MakeEp(eqA, TuningType.Feasible),
            });

            var rows = GetRows(vm);
            Assert.Equal(new[] { "M-100", "M-200" }, rows.Select(r => r.Title).ToArray());
        }

        // --- 統計列 ---

        [Fact]
        public void EquipmentStats_CountTotalAndFilteredValues()
        {
            var vm = CreateVm();
            var eq1 = MakeEquipment(1, "A");
            var eq2 = MakeEquipment(2, "B");

            Inject(vm,
                orders: new() { MakeOrder(1, 1, OrderProductionStatus.InProduction, 10) },
                eps: new() { MakeEp(eq1, TuningType.Feasible), MakeEp(eq2, TuningType.Feasible) });

            // 預設圖表統計順序：設備總數 / 生產中 / 調試中 / 閒置
            Assert.Equal(4, vm.StatItems.Count);
            Assert.Equal("2", vm.StatItems[0].Value);
            Assert.Equal("1", vm.StatItems[1].Value);
            Assert.Equal("0", vm.StatItems[2].Value);
            Assert.Equal("1", vm.StatItems[3].Value);
            Assert.True(vm.IsStatRowVisible);
        }

        // --- 排程視角（表格容器） ---

        [Fact]
        public void SwitchTab_ScheduleBoard_BuildsTableRowsStatsAndColumns()
        {
            var vm = CreateVm();
            Inject(vm, schedules: new()
            {
                MakeSchedule(1, ScheduleStatus.Pending, receivedAt: DateTime.Today.AddDays(-3)),
                MakeSchedule(2, ScheduleStatus.Scheduled),
            });

            vm.SelectedTab = vm.Tabs.First(t => t.Definition.Id == DefaultChartDefinitions.ScheduleBoardId);

            Assert.True(vm.IsTableContainer);
            Assert.False(vm.IsCardContainer);
            Assert.Equal(7, vm.TableColumns.Count);
            Assert.Equal(FieldCatalog.ScheduleStatus, vm.IndicatorFieldId);

            var rows = GetRows(vm);
            Assert.Equal(2, rows.Count);
            // 預設排序：單號遞減
            Assert.Equal(2, rows[0].Values[FieldCatalog.ScheduleId]);
            Assert.Equal("PrimaryBrush", rows[0].IndicatorBrushKey);   // Scheduled
            Assert.Equal(3, rows[1].Values[FieldCatalog.WaitingDays]);

            // 統計：全部 / 待排單 / 已排單 / 已完成
            Assert.Equal("2", vm.StatItems[0].Value);
            Assert.Equal("1", vm.StatItems[1].Value);
            Assert.Equal("1", vm.StatItems[2].Value);
            Assert.Equal("0", vm.StatItems[3].Value);
        }

        [Fact]
        public void SwitchTab_BackToEquipment_RebuildsCardContainer()
        {
            var vm = CreateVm();
            var eq = MakeEquipment(1, "A");
            Inject(vm,
                schedules: new() { MakeSchedule(1, ScheduleStatus.Pending) },
                eps: new() { MakeEp(eq, TuningType.Feasible) });

            vm.SelectedTab = vm.Tabs[1];
            vm.SelectedTab = vm.Tabs[0];

            Assert.True(vm.IsCardContainer);
            Assert.Empty(vm.TableColumns);
            Assert.Single(GetRows(vm));
        }
    }
}
