using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class ChartViewModelTests
    {
        // LoadAsync 使用 IDataService + WPF Dispatcher，xUnit 無 WPF 環境。
        // 透過 InjectDataForTest 繞過非同步載入，驗證：
        //   頁籤建構 / 設備與排程列衍生欄位 / 統計計算 / 容器切換 / 預設排序

        private static ChartViewModel CreateVm() => CreateVm(out _);

        private static ChartViewModel CreateVm(out Mock<IDataService> dataMock,
            List<ChartDefinition>? definitions = null, bool hasEditPermission = true,
            Mock<IDialogService>? dialogMock = null)
        {
            dataMock = new Mock<IDataService>();
            var log = new LogService();

            // 操作列六命令綁 PermissionId.Edit，預設授權以免干擾其餘測試
            var auth = new AuthorizationService();
            if (hasEditPermission)
            {
                auth.SetCachedRoles(new List<Role>
                {
                    new()
                    {
                        RoleId = 1,
                        RolePermissions = new List<RolePermission>
                        {
                            new() { RoleId = 1, PermissionId = PermissionId.Edit },
                        },
                    },
                });
                auth.InitializeAsync(new UserInfo { UserId = "u1", Name = "n1", RoleId = 1 })
                    .GetAwaiter().GetResult();
            }

            var cardReader = new Mock<ICardReaderService>().Object;
            var core = new DashboardCoreServices(log, dataMock.Object, auth, cardReader);
            var store = new JsonChartDefinitionStore(
                _ => definitions ?? new List<ChartDefinition>(),
                (_, _) => { });
            return new ChartViewModel(core, store, (dialogMock ?? new Mock<IDialogService>()).Object);
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

        private static void Refresh(ChartViewModel vm,
            List<Schedule>? schedules = null, List<OrderProductionInfo>? orders = null,
            List<EquipmentProduct>? eps = null, List<ProgramTuningRecord>? tunings = null)
            => vm.ApplyRefreshedData(schedules ?? new(), orders ?? new(), eps ?? new(), tunings ?? new());

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

        // --- 篩選列：篩選欄位 ---

        private static ChartViewModel CreateVmWithTwoEquipments(out Equipment inProd, out Equipment idle)
        {
            var vm = CreateVm();
            inProd = MakeEquipment(1, "CNC-01");
            idle   = MakeEquipment(2, "EDM-02");
            Inject(vm,
                orders: new() { MakeOrder(1, 1, OrderProductionStatus.InProduction, 30) },
                eps: new() { MakeEp(inProd, TuningType.Feasible), MakeEp(idle, TuningType.Feasible) });
            return vm;
        }

        [Fact]
        public void EnumFilter_SelectValue_FiltersRows()
        {
            var vm = CreateVmWithTwoEquipments(out _, out _);
            Assert.True(vm.IsFilterRowVisible);

            var filter = vm.FilterFields.First(f => f.FieldId == FieldCatalog.ProductionStatus);
            filter.SelectedOption = filter.Options.First(o => o.Value == FieldCatalog.ValInProduction);

            var row = Assert.Single(GetRows(vm));
            Assert.Equal("CNC-01", row.Title);
        }

        [Fact]
        public void TextFilter_Keyword_FiltersCaseInsensitive()
        {
            var vm = CreateVmWithTwoEquipments(out _, out _);

            var filter = vm.FilterFields.First(f => f.FieldId == FieldCatalog.EquipmentName);
            filter.Keyword = "cnc";

            var row = Assert.Single(GetRows(vm));
            Assert.Equal("CNC-01", row.Title);
        }

        [Fact]
        public void DateFilter_Range_FiltersScheduleRows()
        {
            var vm = CreateVm();
            Inject(vm, schedules: new()
            {
                MakeSchedule(1, ScheduleStatus.Pending, receivedAt: DateTime.Today.AddDays(-10)),
                MakeSchedule(2, ScheduleStatus.Pending, receivedAt: DateTime.Today.AddDays(-1)),
            });
            vm.SelectedTab = vm.Tabs.First(t => t.Definition.Id == DefaultChartDefinitions.ScheduleBoardId);

            var filter = vm.FilterFields.First(f => f.FieldId == FieldCatalog.ReceivedAt);
            filter.DateStart = DateTime.Today.AddDays(-5);

            var row = Assert.Single(GetRows(vm));
            Assert.Equal(2, row.Values[FieldCatalog.ScheduleId]);
        }

        // --- 篩選列：快捷按鈕 ---

        [Fact]
        public void QuickButton_Toggle_FiltersAndSecondPressReleases()
        {
            var vm = CreateVmWithTwoEquipments(out _, out _);

            var btn = vm.QuickButtons.First(b => b.Value == FieldCatalog.ValInProduction);
            btn.IsActive = true;
            Assert.Single(GetRows(vm));

            btn.IsActive = false;
            Assert.Equal(2, GetRows(vm).Count);
        }

        [Fact]
        public void QuickButtons_SameField_MutuallyExclusive()
        {
            var vm = CreateVmWithTwoEquipments(out _, out _);

            var inProdBtn = vm.QuickButtons.First(b => b.Value == FieldCatalog.ValInProduction);
            var idleBtn   = vm.QuickButtons.First(b => b.Value == FieldCatalog.ValIdle);

            inProdBtn.IsActive = true;
            idleBtn.IsActive = true;   // 後按覆蓋前按

            Assert.False(inProdBtn.IsActive);
            Assert.True(idleBtn.IsActive);
            var row = Assert.Single(GetRows(vm));
            Assert.Equal("EDM-02", row.Title);
        }

        [Fact]
        public void QuickButtons_DifferentFields_CombineWithAnd()
        {
            var vm = CreateVm();
            var eq1 = MakeEquipment(1, "A");   // 生產中＋調試中
            var eq2 = MakeEquipment(2, "B");   // 生產中
            Inject(vm,
                orders: new()
                {
                    MakeOrder(1, 1, OrderProductionStatus.InProduction, 30),
                    MakeOrder(2, 2, OrderProductionStatus.InProduction, 30),
                },
                eps: new() { MakeEp(eq1, TuningType.Feasible), MakeEp(eq2, TuningType.Feasible) },
                tunings: new() { new ProgramTuningRecord { EquipmentId = 1 } });

            vm.QuickButtons.First(b => b.Value == FieldCatalog.ValInProduction).IsActive = true;
            Assert.Equal(2, GetRows(vm).Count);

            vm.QuickButtons.First(b => b.FieldId == FieldCatalog.TuningStatus).IsActive = true;
            var row = Assert.Single(GetRows(vm));
            Assert.Equal("A", row.Title);
        }

        // --- 篩選列：排序切換 ---

        [Fact]
        public void SortOption_ChangeToLoadLevelDescending_OrdersByRank()
        {
            var vm = CreateVm();
            var eqLow  = MakeEquipment(1, "A-Low");
            var eqHigh = MakeEquipment(2, "B-High");
            Inject(vm,
                orders: new()
                {
                    MakeOrder(1, 1, OrderProductionStatus.Pending, 50),    // Low
                    MakeOrder(2, 2, OrderProductionStatus.Pending, 600),   // High
                },
                eps: new() { MakeEp(eqLow, TuningType.Feasible), MakeEp(eqHigh, TuningType.Feasible) });

            vm.SelectedSortOption = vm.SortOptions.First(o => o.FieldId == FieldCatalog.LoadLevel);
            vm.IsSortDescending = true;

            var rows = GetRows(vm);
            Assert.Equal(new[] { "B-High", "A-Low" }, rows.Select(r => r.Title).ToArray());
        }

        // --- 恢復預設 ---

        [Fact]
        public void RestoreDefaults_ResetsFiltersButtonsAndSort()
        {
            var vm = CreateVmWithTwoEquipments(out _, out _);

            var filter = vm.FilterFields.First(f => f.FieldId == FieldCatalog.ProductionStatus);
            filter.SelectedOption = filter.Options.First(o => o.Value == FieldCatalog.ValIdle);
            vm.QuickButtons.First(b => b.Value == FieldCatalog.ValIdle).IsActive = true;
            vm.SelectedSortOption = vm.SortOptions.First(o => o.FieldId == FieldCatalog.LoadLevel);
            vm.IsSortDescending = true;

            vm.RestoreDefaultsCommand.Execute(null);

            Assert.Equal(2, GetRows(vm).Count);
            Assert.Null(filter.SelectedOption?.Value);
            Assert.All(vm.QuickButtons, b => Assert.False(b.IsActive));
            Assert.Equal(FieldCatalog.EquipmentName, vm.SelectedSortOption?.FieldId);
            Assert.False(vm.IsSortDescending);
            Assert.Equal(new[] { "CNC-01", "EDM-02" }, GetRows(vm).Select(r => r.Title).ToArray());
        }

        // --- 定期刷新（資料-only） ---

        [Fact]
        public void ApplyRefreshedData_RebuildsRowsAndStats_WithNewData()
        {
            var vm = CreateVm();
            var eq1 = MakeEquipment(1, "A");
            Inject(vm, eps: new() { MakeEp(eq1, TuningType.Feasible) });
            Assert.Single(GetRows(vm));

            var eq2 = MakeEquipment(2, "B");
            var eq3 = MakeEquipment(3, "C");
            Refresh(vm, eps: new() { MakeEp(eq2, TuningType.Feasible), MakeEp(eq3, TuningType.Feasible) });

            Assert.Equal(2, GetRows(vm).Count);
            Assert.Equal("2", vm.StatItems[0].Value);   // 設備總數統計反映新資料
        }

        [Fact]
        public void ApplyRefreshedData_PreservesFilterSortSelection_AppliesToNewData()
        {
            var vm = CreateVmWithTwoEquipments(out _, out _);   // CNC-01 生產中 / EDM-02 閒置
            var filter = vm.FilterFields.First(f => f.FieldId == FieldCatalog.ProductionStatus);
            filter.SelectedOption = filter.Options.First(o => o.Value == FieldCatalog.ValInProduction);
            vm.SelectedSortOption = vm.SortOptions.First(o => o.FieldId == FieldCatalog.LoadLevel);
            vm.IsSortDescending = true;
            Assert.Single(GetRows(vm));

            // 新資料：兩台生產中（負載 High/Low）＋一台閒置
            var high = MakeEquipment(10, "P-10");
            var low  = MakeEquipment(11, "P-11");
            var idle = MakeEquipment(12, "Z-12");
            Refresh(vm,
                orders: new()
                {
                    MakeOrder(1, 10, OrderProductionStatus.InProduction, 600),   // High
                    MakeOrder(2, 11, OrderProductionStatus.InProduction, 50),    // Low
                },
                eps: new()
                {
                    MakeEp(high, TuningType.Feasible),
                    MakeEp(low, TuningType.Feasible),
                    MakeEp(idle, TuningType.Feasible),
                });

            // 篩選 VM 未被重建（同一實例）、選值保留
            Assert.Same(filter, vm.FilterFields.First(f => f.FieldId == FieldCatalog.ProductionStatus));
            Assert.Equal(FieldCatalog.ValInProduction, filter.SelectedOption?.Value);
            // 生產中篩選濾掉閒置台；負載遞減排序仍生效 → High 在 Low 前
            var rows = GetRows(vm);
            Assert.Equal(new[] { "P-10", "P-11" }, rows.Select(r => r.Title).ToArray());
        }

        [Fact]
        public void ApplyRefreshedData_KeepsTabsAndSelectedTabInstances()
        {
            var vm = CreateVm();
            Inject(vm);
            var selectedBefore = vm.SelectedTab;
            var tabsBefore = vm.Tabs.ToArray();

            var eq = MakeEquipment(1, "A");
            Refresh(vm, eps: new() { MakeEp(eq, TuningType.Feasible) });

            Assert.Same(selectedBefore, vm.SelectedTab);
            Assert.Equal(tabsBefore, vm.Tabs.ToArray());   // 頁籤實例不變
        }

        [Fact]
        public void ApplyRefreshedData_DesignerOpen_DoesNotApply()
        {
            var vm = CreateVm();
            var eq = MakeEquipment(1, "A");
            Inject(vm, eps: new() { MakeEp(eq, TuningType.Feasible) });
            Assert.Single(GetRows(vm));

            vm.IsDesignerOpen = true;
            var eq2 = MakeEquipment(2, "B");
            var eq3 = MakeEquipment(3, "C");
            Refresh(vm, eps: new() { MakeEp(eq2, TuningType.Feasible), MakeEp(eq3, TuningType.Feasible) });

            Assert.Single(GetRows(vm));   // 套用前 guard：本輪丟棄，維持前次 1 列
        }

        [Fact]
        public async Task RefreshDataAsync_DesignerOpen_DoesNotQueryDb()
        {
            var vm = CreateVm(out var dataMock);
            var eq = MakeEquipment(1, "A");
            Inject(vm, eps: new() { MakeEp(eq, TuningType.Feasible) });

            vm.IsDesignerOpen = true;
            await vm.RefreshDataAsync();

            dataMock.Verify(d => d.GetAllSchedulesAsync(), Times.Never);
            dataMock.Verify(d => d.GetAllEquipmentProductsAsync(), Times.Never);
        }

        [Fact]
        public async Task RefreshDataAsync_DbFailure_KeepsPreviousRowsWithoutThrowing()
        {
            var vm = CreateVm(out var dataMock);
            var eq1 = MakeEquipment(1, "A");
            var eq2 = MakeEquipment(2, "B");
            Inject(vm, eps: new() { MakeEp(eq1, TuningType.Feasible), MakeEp(eq2, TuningType.Feasible) });
            Assert.Equal(2, GetRows(vm).Count);

            dataMock.Setup(d => d.GetAllSchedulesAsync())
                .ThrowsAsync(new InvalidOperationException("db down"));

            await vm.RefreshDataAsync();   // 週期路徑吞例外、維持前次資料

            Assert.Equal(2, GetRows(vm).Count);
        }

        // --- 縮放 ---

        [Fact]
        public void Zoom_StepsThroughLevels_AndClampsAtBounds()
        {
            FProductionDashBoard.Properties.Settings.Default.ChartZoomPercent = 100;
            var vm = CreateVm();

            Assert.Equal(100, vm.ZoomPercent);
            Assert.True(vm.CanZoomIn);
            Assert.True(vm.CanZoomOut);

            vm.ZoomOutCommand.Execute(null);
            Assert.Equal(75, vm.ZoomPercent);
            Assert.False(vm.CanZoomOut);
            Assert.Equal(0.75, vm.ZoomScale, 3);

            vm.ZoomInCommand.Execute(null);
            vm.ZoomInCommand.Execute(null);
            vm.ZoomInCommand.Execute(null);
            Assert.Equal(150, vm.ZoomPercent);
            Assert.False(vm.CanZoomIn);
        }

        // --- 設計器切換（C1：編輯入口 / 關閉回呼 / LoadAsync guard） ---

        [Fact]
        public void EditChart_NonDefaultTab_OpensDesigner_CancelCloses()
        {
            var vm = CreateVm();
            Inject(vm);
            vm.SelectedTab = vm.Tabs.First(t => !t.IsDefault);

            Assert.True(vm.EditChartCommand.CanExecute(null));
            vm.EditChartCommand.Execute(null);

            Assert.True(vm.IsDesignerOpen);
            Assert.False(vm.IsViewerVisible);
            Assert.NotNull(vm.Designer);

            vm.Designer!.CancelCommand.Execute(null);

            Assert.False(vm.IsDesignerOpen);
            Assert.True(vm.IsViewerVisible);
            Assert.Null(vm.Designer);
        }

        [Fact]
        public void EditChart_DefaultTab_CannotExecute()
        {
            var vm = CreateVm();
            Inject(vm);

            vm.SelectedTab = vm.Tabs.First(t => t.IsDefault);

            Assert.False(vm.EditChartCommand.CanExecute(null));
        }

        [Fact]
        public void EditChart_Save_ReloadsTabs_WithUpdatedDefinition()
        {
            var vm = CreateVm();
            Inject(vm);
            vm.SelectedTab = vm.Tabs.First(t => !t.IsDefault);

            vm.EditChartCommand.Execute(null);
            vm.Designer!.WorkingDefinition.Name = "MyChart";
            vm.Designer.WorkingDefinition.NameKey = null;
            vm.Designer.SaveCommand.Execute(null);

            Assert.False(vm.IsDesignerOpen);
            Assert.Null(vm.Designer);
            Assert.Equal("MyChart", vm.SelectedTab?.DisplayName);   // 依 previousId 還原選中頁籤
        }

        [Fact]
        public async Task LoadCommand_WhileDesignerOpen_SkipsReload()
        {
            var vm = CreateVm(out var dataMock);
            Inject(vm);
            vm.SelectedTab = vm.Tabs.First(t => !t.IsDefault);
            vm.EditChartCommand.Execute(null);
            var tabBefore = vm.SelectedTab;

            await vm.LoadCommand.ExecuteAsync(null);

            dataMock.Verify(d => d.GetAllSchedulesAsync(), Times.Never);
            Assert.Same(tabBefore, vm.SelectedTab);
            Assert.True(vm.IsDesignerOpen);
        }

        // --- DB 不可用時的骨架渲染（A-2：首次空資料、其後保留前次資料） ---

        [Fact]
        public async Task LoadCommand_DataServiceThrows_StillBuildsTabsWithEmptyData()
        {
            var vm = CreateVm(out var dataMock);
            dataMock.Setup(d => d.GetAllSchedulesAsync())
                    .ThrowsAsync(new InvalidOperationException("no db"));

            await vm.LoadCommand.ExecuteAsync(null);

            Assert.Equal(2, vm.Tabs.Count);           // 兩張預設圖表頁籤仍建立
            Assert.NotNull(vm.SelectedTab);
            Assert.Empty(GetRows(vm));                // 無資料列
            Assert.True(vm.IsStatRowVisible);
            Assert.Equal("0", vm.StatItems[0].Value); // 統計顯示 0
        }

        [Fact]
        public async Task LoadCommand_ThrowsAfterSuccessfulLoad_KeepsPreviousData()
        {
            var vm = CreateVm(out var dataMock);
            var eq = MakeEquipment(1, "CNC-01");
            Inject(vm, eps: new() { MakeEp(eq, TuningType.Feasible) });
            Assert.Single(GetRows(vm));

            dataMock.Setup(d => d.GetAllSchedulesAsync())
                    .ThrowsAsync(new InvalidOperationException("db down"));
            await vm.LoadCommand.ExecuteAsync(null);

            Assert.Equal(2, vm.Tabs.Count);
            Assert.Single(GetRows(vm));               // 前次資料未被清空
            Assert.Equal("CNC-01", GetRows(vm)[0].Title);
        }

        // --- 檢視端失效引用防護（唯讀容忍，不改寫檔案） ---

        [Fact]
        public void SelectTab_UnknownDataSet_NoCrash_ShowsEmpty()
        {
            var badDef = new ChartDefinition { Id = "bad", Name = "Bad", DataSet = (ChartDataSet)99 };
            var vm = CreateVm(out _, new List<ChartDefinition>
            {
                DefaultChartDefinitions.CreateEquipmentOverview(),
                badDef,
            });
            // 需有資料列才會進 FinishRow 觸發 FieldCatalog.For 的 throw 路徑
            Inject(vm, schedules: new() { MakeSchedule(1, ScheduleStatus.Pending) });

            vm.SelectedTab = vm.Tabs.First(t => t.Definition.Id == "bad");

            Assert.Empty(GetRows(vm));
            Assert.False(vm.IsCardContainer);
            Assert.False(vm.IsTableContainer);
            Assert.False(vm.IsStatRowVisible);
            Assert.False(vm.IsFilterRowVisible);
        }

        [Fact]
        public void SelectTab_InvalidTableColumnsAndSort_FilteredWithoutCrash()
        {
            var tableDef = DefaultChartDefinitions.CreateScheduleBoard();
            tableDef.Container.Table.ColumnFieldIds.Add("Ghost");
            tableDef.FilterRow.SortOptionFieldIds.Clear();          // 無排序選項
            tableDef.FilterRow.DefaultSortFieldId = "Ghost";        // 預設排序失效 → TieBreak 落回主名稱
            var vm = CreateVm(out _, new List<ChartDefinition>
            {
                DefaultChartDefinitions.CreateEquipmentOverview(),
                tableDef,
            });
            Inject(vm, schedules: new() { MakeSchedule(1, ScheduleStatus.Pending) });

            vm.SelectedTab = vm.Tabs.First(t => t.Definition.Id == DefaultChartDefinitions.ScheduleBoardId);

            Assert.Equal(7, vm.TableColumns.Count);   // Ghost 欄不渲染
            Assert.Single(GetRows(vm));               // 排序失效不擲例外
        }

        // --- C4：操作列命令權限與反灰 ---

        [Fact]
        public void ChartCommands_WithoutEditPermission_AllDisabled()
        {
            var vm = CreateVm(out _, hasEditPermission: false);
            Inject(vm);
            vm.SelectedTab = vm.Tabs.First(t => !t.IsDefault);

            Assert.False(vm.AddChartCommand.CanExecute(null));
            Assert.False(vm.EditChartCommand.CanExecute(null));
            Assert.False(vm.CopyChartCommand.CanExecute(null));
            Assert.False(vm.DeleteChartCommand.CanExecute(null));
            Assert.False(vm.ExportChartCommand.CanExecute(null));
            Assert.False(vm.ImportChartCommand.CanExecute(null));
        }

        [Fact]
        public void DeleteChart_DefaultTab_CannotExecute()
        {
            var vm = CreateVm();
            Inject(vm);

            vm.SelectedTab = vm.Tabs.First(t => t.IsDefault);

            Assert.False(vm.DeleteChartCommand.CanExecute(null));
            Assert.True(vm.CopyChartCommand.CanExecute(null));     // 預設圖表可複製
            Assert.True(vm.ExportChartCommand.CanExecute(null));   // 預設圖表可匯出
        }

        private static List<ChartDefinition> TenCharts()
        {
            var defs = new List<ChartDefinition> { DefaultChartDefinitions.CreateEquipmentOverview() };
            for (var i = 2; i <= 10; i++)
                defs.Add(new ChartDefinition
                {
                    Id = $"c{i}",
                    Name = $"Chart{i}",
                    DataSet = ChartDataSet.Equipment,
                    SortOrder = i,
                });
            return defs;
        }

        [Fact]
        public void ChartCommands_AtMaxCharts_AddCopyImportDisabled()
        {
            var vm = CreateVm(out _, TenCharts());
            Inject(vm);
            vm.SelectedTab = vm.Tabs.First(t => !t.IsDefault);

            Assert.False(vm.AddChartCommand.CanExecute(null));
            Assert.False(vm.CopyChartCommand.CanExecute(null));
            Assert.False(vm.ImportChartCommand.CanExecute(null));
            Assert.True(vm.EditChartCommand.CanExecute(null));
            Assert.True(vm.DeleteChartCommand.CanExecute(null));
        }

        // --- C4：新增／複製 ---

        [Fact]
        public void AddChart_OpensDesigner_WithDefaultContent()
        {
            var vm = CreateVm();
            Inject(vm);

            vm.AddChartCommand.Execute(null);

            Assert.True(vm.IsDesignerOpen);
            Assert.True(vm.Designer!.IsNew);
            var def = vm.Designer.WorkingDefinition;
            Assert.Equal("", def.Name);
            Assert.Equal(ChartDataSet.Equipment, def.DataSet);
            Assert.Equal(3, def.SortOrder);                        // 種子 1、2 → 新圖表排最後
            Assert.False(def.IsDefault);
            Assert.True(def.StatRow.Enabled);
            Assert.Empty(def.StatRow.Items);
            Assert.True(def.FilterRow.Enabled);
            Assert.Empty(def.FilterRow.FilterFieldIds);
            Assert.Equal(ChartContainerType.Card, def.Container.Type);
            Assert.Equal(FieldCatalog.EquipmentName, def.Container.Card.TitleFieldId);   // 預填第一欄
        }

        [Fact]
        public void CopyChart_ClonesWithNewId_DedupedName_NotDefault()
        {
            var vm = CreateVm();
            Inject(vm);
            vm.SelectedTab = vm.Tabs.First(t => t.IsDefault);   // 複製鎖定預設圖表

            vm.CopyChartCommand.Execute(null);

            Assert.True(vm.IsDesignerOpen);
            Assert.True(vm.Designer!.IsNew);
            var clone = vm.Designer.WorkingDefinition;
            var source = DefaultChartDefinitions.CreateEquipmentOverview();
            Assert.NotEqual(source.Id, clone.Id);
            Assert.False(clone.IsDefault);
            Assert.Null(clone.NameKey);
            var resolved = FProductionDashBoard.Properties.Resources.ChartDefaultEquipmentOverview;
            Assert.Equal($"{resolved} (2)", clone.Name);       // 與來源同名 → 加 (2)
            Assert.Equal(source.Container.Card.TitleFieldId, clone.Container.Card.TitleFieldId);
        }

        // --- C4：刪除 ---

        [Fact]
        public void DeleteChart_Confirmed_RemovesAndReloads()
        {
            var dialog = new Mock<IDialogService>();
            dialog.Setup(d => d.ShowConfirm(It.IsAny<string>())).Returns(true);
            var vm = CreateVm(out _, dialogMock: dialog);
            Inject(vm);
            vm.SelectedTab = vm.Tabs.First(t => !t.IsDefault);

            vm.DeleteChartCommand.Execute(null);

            Assert.Single(vm.Tabs);
            Assert.True(vm.Tabs[0].IsDefault);
            Assert.Same(vm.Tabs[0], vm.SelectedTab);
        }

        [Fact]
        public void DeleteChart_Declined_KeepsTabs()
        {
            var dialog = new Mock<IDialogService>();
            dialog.Setup(d => d.ShowConfirm(It.IsAny<string>())).Returns(false);
            var vm = CreateVm(out _, dialogMock: dialog);
            Inject(vm);
            vm.SelectedTab = vm.Tabs.First(t => !t.IsDefault);

            vm.DeleteChartCommand.Execute(null);

            Assert.Equal(2, vm.Tabs.Count);
        }

        // --- C4：匯入／匯出 ---

        private static string SerializeDef(ChartDefinition def)
            => System.Text.Json.JsonSerializer.Serialize(def);

        [Fact]
        public void ImportDefinitionJson_Valid_AddsSelectsAndForcesNonDefault()
        {
            var vm = CreateVm();
            Inject(vm);
            var import = new ChartDefinition
            {
                Id = "foreign-id",
                Name = "外部圖表",
                DataSet = ChartDataSet.Equipment,
                IsDefault = true,   // 匯入強制轉為一般圖表
                Container = new ContainerConfig
                {
                    Type = ChartContainerType.Card,
                    Card = new CardContainerConfig { TitleFieldId = FieldCatalog.EquipmentName },
                },
            };

            Assert.True(vm.ImportDefinitionJson(SerializeDef(import)));

            Assert.Equal(3, vm.Tabs.Count);
            var imported = vm.SelectedTab!;
            Assert.Equal("外部圖表", imported.DisplayName);
            Assert.NotEqual("foreign-id", imported.Definition.Id);
            Assert.False(imported.Definition.IsDefault);
        }

        [Fact]
        public void ImportDefinitionJson_NameConflict_AppendsSuffix()
        {
            var vm = CreateVm();
            Inject(vm);
            var import = new ChartDefinition { Id = "x", Name = "MyChart", DataSet = ChartDataSet.Equipment };
            Assert.True(vm.ImportDefinitionJson(SerializeDef(import)));

            Assert.True(vm.ImportDefinitionJson(SerializeDef(import)));   // 同名再匯入一次

            Assert.Contains(vm.Tabs, t => t.DisplayName == "MyChart");
            Assert.Contains(vm.Tabs, t => t.DisplayName == "MyChart (2)");
        }

        [Fact]
        public void ImportDefinitionJson_InvalidJsonOrNameless_ReturnsFalse()
        {
            var vm = CreateVm();
            Inject(vm);

            Assert.False(vm.ImportDefinitionJson("not-json"));
            Assert.False(vm.ImportDefinitionJson("{}"));   // 缺名稱

            Assert.Equal(2, vm.Tabs.Count);
        }

        [Fact]
        public void ImportDefinitionJson_AtMaxCharts_ReturnsFalse()
        {
            var vm = CreateVm(out _, TenCharts());
            Inject(vm);
            var import = new ChartDefinition { Id = "x", Name = "溢出圖表", DataSet = ChartDataSet.Equipment };

            Assert.False(vm.ImportDefinitionJson(SerializeDef(import)));   // store 上限防線 throw → false

            Assert.Equal(10, vm.Tabs.Count);
        }

        [Fact]
        public void ExportThenImport_RoundTripsDefinition()
        {
            var vm = CreateVm();
            Inject(vm);
            var source = vm.Tabs.First(t => !t.IsDefault).Definition;
            var path = Path.Combine(Path.GetTempPath(), $"chart-export-{Guid.NewGuid():N}.json");

            try
            {
                vm.ExportDefinitionTo(source, path);
                Assert.True(vm.ImportDefinitionJson(File.ReadAllText(path)));

                var imported = vm.SelectedTab!.Definition;
                Assert.NotEqual(source.Id, imported.Id);
                Assert.Equal(source.DataSet, imported.DataSet);
                Assert.Equal(source.Container.Table.ColumnFieldIds, imported.Container.Table.ColumnFieldIds);
                // 與來源同名 → 去衝突後綴
                Assert.EndsWith("(2)", vm.SelectedTab.DisplayName);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
