using FProductionDashBoard.Models;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using System.Collections.Generic;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class TuningDialogViewModelTests
    {
        private const int CurrentUserId = 100;

        private static List<EquipmentProductItem> MakeItems() =>
        [
            new() { EquipmentProductId = 1, SeqNo = 1, DisplayLabel = "#1 PartA_M1 · 加工", ProductionStatus = TuningType.Teaching, Brand = "ACME", PartNo = "PA-001", Model = "M1", Process = "加工" },
            new() { EquipmentProductId = 2, SeqNo = 2, DisplayLabel = "#2 PartA_M2 · 加工", ProductionStatus = TuningType.Offset,   Brand = "ACME", PartNo = "PA-002", Model = "M2", Process = "加工" },
            new() { EquipmentProductId = 3, SeqNo = 3, DisplayLabel = "#3 PartB_M1 · 組裝", ProductionStatus = TuningType.Teaching, Brand = "TC",   PartNo = "PB-001", Model = "M1", Process = "組裝" },
            new() { EquipmentProductId = 4, SeqNo = 4, DisplayLabel = "#4 PartB_M2 · 組裝", ProductionStatus = TuningType.Feasible, Brand = "TC",   PartNo = "PB-002", Model = "M2", Process = "組裝" },
        ];

        private static List<UserInfo> MakeEmployees() =>
        [
            new() { Id = CurrentUserId, UserId = "u100", Name = "John" },
            new() { Id = 200, UserId = "u200", Name = "Mary" },
        ];

        private static TuningDialogViewModel Create(
            IReadOnlyDictionary<int, string>? lastTeachingMap = null,
            bool canForceArrange = false) =>
            new("機台: Device-01", "人員: John", MakeItems(), MakeEmployees(), CurrentUserId,
                lastTeachingMap ?? new Dictionary<int, string>(), canForceArrange);

        // ─── Initial state ────────────────────────────────────────────────────

        [Fact]
        public void InitialState_TeachingModeAndTeachingItemsFiltered()
        {
            var vm = Create();
            Assert.Equal((int)TuningType.Teaching, vm.TuningMode);
            Assert.Equal(2, vm.FilteredEquipmentProducts.Count);
            Assert.All(vm.FilteredEquipmentProducts, i => Assert.Equal(TuningType.Teaching, i.ProductionStatus));
        }

        [Fact]
        public void InitialState_SelectedIsNull_ConfirmDisabled()
        {
            var vm = Create();
            Assert.Null(vm.SelectedEquipmentProduct);
            Assert.False(vm.ConfirmCommand.CanExecute(null));
        }

        // ─── TeachingCommand ──────────────────────────────────────────────────

        [Fact]
        public void TeachingCommand_SetsModeAndFiltersTeachingItems()
        {
            var vm = Create();
            vm.OffsetCommand.Execute(null); // 先切到 Offset
            vm.TeachingCommand.Execute(null);
            Assert.Equal((int)TuningType.Teaching, vm.TuningMode);
            Assert.Equal(2, vm.FilteredEquipmentProducts.Count);
            Assert.All(vm.FilteredEquipmentProducts, i => Assert.Equal(TuningType.Teaching, i.ProductionStatus));
        }

        [Fact]
        public void TeachingCommand_ClearsSelection()
        {
            var vm = Create();
            vm.SelectedEquipmentProduct = vm.FilteredEquipmentProducts[0];
            vm.OffsetCommand.Execute(null);
            vm.TeachingCommand.Execute(null);
            Assert.Null(vm.SelectedEquipmentProduct);
        }

        // ─── OffsetCommand ────────────────────────────────────────────────────

        [Fact]
        public void OffsetCommand_SetsModeAndFiltersOffsetItems()
        {
            var vm = Create();
            vm.OffsetCommand.Execute(null);
            Assert.Equal((int)TuningType.Offset, vm.TuningMode);
            Assert.Single(vm.FilteredEquipmentProducts);
            Assert.Equal(TuningType.Offset, vm.FilteredEquipmentProducts[0].ProductionStatus);
        }

        [Fact]
        public void OffsetCommand_ClearsSelection()
        {
            var vm = Create();
            vm.SelectedEquipmentProduct = vm.FilteredEquipmentProducts[0];
            vm.OffsetCommand.Execute(null);
            Assert.Null(vm.SelectedEquipmentProduct);
        }

        // ─── ConfirmCommand ───────────────────────────────────────────────────

        [Fact]
        public void ConfirmCommand_EnabledWhenItemSelected()
        {
            var vm = Create();
            vm.SelectedEquipmentProduct = vm.FilteredEquipmentProducts[0];
            Assert.True(vm.ConfirmCommand.CanExecute(null));
        }

        [Fact]
        public void ConfirmCommand_TeachingMode_ReturnsCorrectResult()
        {
            var vm = Create();
            var item = vm.FilteredEquipmentProducts[0]; // Teaching item, Id=1
            vm.SelectedEquipmentProduct = item;
            vm.ConfirmCommand.Execute(null);
            Assert.True(vm.IsConfirmed);
            Assert.NotNull(vm.Result);
            Assert.Equal(TuningType.Teaching, vm.Result!.TuningType);
            Assert.Equal(item.EquipmentProductId, vm.Result.EquipmentProductId);
        }

        [Fact]
        public void ConfirmCommand_OffsetMode_ReturnsCorrectResult()
        {
            var vm = Create();
            vm.OffsetCommand.Execute(null);
            var item = vm.FilteredEquipmentProducts[0]; // Offset item, Id=2
            vm.SelectedEquipmentProduct = item;
            vm.ConfirmCommand.Execute(null);
            Assert.True(vm.IsConfirmed);
            Assert.NotNull(vm.Result);
            Assert.Equal(TuningType.Offset, vm.Result!.TuningType);
            Assert.Equal(item.EquipmentProductId, vm.Result.EquipmentProductId);
        }

        [Fact]
        public void ConfirmCommand_WithoutSelection_DoesNotConfirm()
        {
            var vm = Create();
            // CanExecute is false, but verify OnConfirm guard also holds
            Assert.False(vm.ConfirmCommand.CanExecute(null));
            Assert.False(vm.IsConfirmed);
        }

        // ─── CancelCommand ────────────────────────────────────────────────────

        [Fact]
        public void CancelCommand_SetsNotConfirmedNullResult()
        {
            var vm = Create();
            vm.CancelCommand.Execute(null);
            Assert.False(vm.IsConfirmed);
            Assert.Null(vm.Result);
        }

        // ─── Non-Teaching/Offset items not shown ─────────────────────────────

        [Fact]
        public void FeasibleItems_NotShownInEitherMode()
        {
            var vm = Create();
            Assert.DoesNotContain(vm.FilteredEquipmentProducts, i => i.ProductionStatus == TuningType.Feasible);
            vm.OffsetCommand.Execute(null);
            Assert.DoesNotContain(vm.FilteredEquipmentProducts, i => i.ProductionStatus == TuningType.Feasible);
        }

        // ─── Executor（執行人員下拉）─────────────────────────────────────────

        [Fact]
        public void Executor_DefaultsToCurrentUser()
        {
            var vm = Create();
            Assert.NotNull(vm.SelectedEmployee);
            Assert.Equal(CurrentUserId, vm.SelectedEmployee!.Id);
            Assert.Equal(2, vm.FilteredEmployees.Count);
        }

        [Fact]
        public void ConfirmDisabled_WhenNoEmployeeSelected()
        {
            var vm = Create();
            vm.SelectedEquipmentProduct = vm.FilteredEquipmentProducts[0];
            vm.SelectedEmployee = null;
            Assert.False(vm.ConfirmCommand.CanExecute(null));
        }

        [Fact]
        public void EmployeeFilter_FiltersByName()
        {
            var vm = Create();
            vm.EmployeeFilterText = "mar";
            Assert.Single(vm.FilteredEmployees);
            Assert.Equal("Mary", vm.FilteredEmployees[0].Name);
        }

        [Fact]
        public void EmployeeFilter_FiltersByUserId()
        {
            var vm = Create();
            vm.EmployeeFilterText = "u200";
            Assert.Single(vm.FilteredEmployees);
            Assert.Equal(200, vm.FilteredEmployees[0].Id);
        }

        [Fact]
        public void EmployeeFilter_Cleared_RestoresAll()
        {
            var vm = Create();
            vm.EmployeeFilterText = "mary";
            vm.EmployeeFilterText = string.Empty;
            Assert.Equal(2, vm.FilteredEmployees.Count);
        }

        [Fact]
        public void EmployeeFilter_WhenSelectionDropsOut_SelectsFirstMatch()
        {
            var vm = Create(); // 預設選 John(100)
            vm.EmployeeFilterText = "mary"; // John 被濾掉
            Assert.NotNull(vm.SelectedEmployee);
            Assert.Equal(200, vm.SelectedEmployee!.Id);
        }

        [Fact]
        public void EmployeeFilter_NoMatch_ClearsSelection()
        {
            var vm = Create();
            vm.EmployeeFilterText = "zzz";
            Assert.Empty(vm.FilteredEmployees);
            Assert.Null(vm.SelectedEmployee);
        }

        [Fact]
        public void Executor_WhenCurrentUserNotInList_DefaultsToFirst()
        {
            var vm = new TuningDialogViewModel("機台: Device-01", "人員: X", MakeItems(), MakeEmployees(),
                currentUserId: 999, new Dictionary<int, string>(), canForceArrange: false);
            Assert.NotNull(vm.SelectedEmployee);
            Assert.Equal(CurrentUserId, vm.SelectedEmployee!.Id); // 第一項 John
        }

        // ─── TeachedUserName（上次帶點人員）─────────────────────────────────

        [Fact]
        public void TeachedUserName_WhenSelectedHasHistory_ShowsName()
        {
            var vm = Create(new Dictionary<int, string> { [1] = "李阿姨" });
            vm.SelectedEquipmentProduct = vm.FilteredEquipmentProducts.First(i => i.EquipmentProductId == 1);
            Assert.Equal("李阿姨", vm.TeachedUserName);
        }

        [Fact]
        public void TeachedUserName_WhenNoHistory_ShowsPlaceholder()
        {
            var vm = Create(new Dictionary<int, string> { [1] = "李阿姨" });
            vm.SelectedEquipmentProduct = vm.FilteredEquipmentProducts.First(i => i.EquipmentProductId == 3);
            Assert.Equal("—", vm.TeachedUserName);
        }

        // ─── Confirm 輸出 StartedBy ──────────────────────────────────────────

        [Fact]
        public void Confirm_OutputsSelectedExecutorAsStartedBy()
        {
            var vm = Create();
            vm.SelectedEquipmentProduct = vm.FilteredEquipmentProducts[0];
            vm.SelectedEmployee = vm.FilteredEmployees.First(e => e.Id == 200);
            vm.ConfirmCommand.Execute(null);
            Assert.NotNull(vm.Result);
            Assert.Equal(200, vm.Result!.StartedBy);
            Assert.False(vm.Result.IsForceArrange);
        }

        [Fact]
        public void CanForceArrange_ReflectsConstructorArg()
        {
            Assert.False(Create(canForceArrange: false).CanForceArrange);
            Assert.True(Create(canForceArrange: true).CanForceArrange);
        }

        // ─── ForceArrange（強制安排模式）─────────────────────────────────────

        [Fact]
        public void ForceArrange_On_ShowsAllItems()
        {
            var vm = Create(canForceArrange: true);
            vm.ToggleForceArrangeCommand.Execute(null);
            Assert.True(vm.IsForceArrange);
            Assert.Equal(4, vm.FilteredEquipmentProducts.Count);
        }

        [Fact]
        public void ForceArrange_Off_RestoresModeFiltering()
        {
            var vm = Create(canForceArrange: true);
            vm.ToggleForceArrangeCommand.Execute(null);
            vm.ToggleForceArrangeCommand.Execute(null);
            Assert.False(vm.IsForceArrange);
            Assert.Equal(2, vm.FilteredEquipmentProducts.Count);
            Assert.All(vm.FilteredEquipmentProducts, i => Assert.Equal(TuningType.Teaching, i.ProductionStatus));
        }

        [Theory]
        [InlineData("acme", new[] { 1, 2 })]   // Brand（不分大小寫）
        [InlineData("PB-", new[] { 3, 4 })]    // PartNo
        [InlineData("M1", new[] { 1, 3 })]     // Model
        [InlineData("組裝", new[] { 3, 4 })]   // Process
        public void ForceArrange_KeywordFilter_MatchesAnyField(string keyword, int[] expectedIds)
        {
            var vm = Create(canForceArrange: true);
            vm.ToggleForceArrangeCommand.Execute(null);
            vm.KeywordFilter = keyword;
            Assert.Equal(expectedIds, vm.FilteredEquipmentProducts.Select(i => i.EquipmentProductId).ToArray());
        }

        [Fact]
        public void ForceArrange_StatusFilter_FiltersByStatus()
        {
            var vm = Create(canForceArrange: true);
            vm.ToggleForceArrangeCommand.Execute(null);
            vm.StatusFilter = TuningType.Feasible;
            Assert.Single(vm.FilteredEquipmentProducts);
            Assert.Equal(4, vm.FilteredEquipmentProducts[0].EquipmentProductId);
        }

        [Fact]
        public void ForceArrange_KeywordAndStatus_Combined()
        {
            var vm = Create(canForceArrange: true);
            vm.ToggleForceArrangeCommand.Execute(null);
            vm.KeywordFilter = "TC";
            vm.StatusFilter = TuningType.Teaching;
            Assert.Single(vm.FilteredEquipmentProducts);
            Assert.Equal(3, vm.FilteredEquipmentProducts[0].EquipmentProductId);
        }

        [Fact]
        public void ForceArrange_ModeSwitch_KeepsListAndSelection()
        {
            var vm = Create(canForceArrange: true);
            vm.ToggleForceArrangeCommand.Execute(null);
            var item = vm.FilteredEquipmentProducts.First(i => i.EquipmentProductId == 4); // Feasible
            vm.SelectedEquipmentProduct = item;
            vm.OffsetCommand.Execute(null); // 強制模式下切換模式不影響清單與選取
            Assert.Equal(4, vm.FilteredEquipmentProducts.Count);
            Assert.Same(item, vm.SelectedEquipmentProduct);
        }

        [Fact]
        public void ForceArrange_Confirm_OutputsIsForceArrangeAndMode()
        {
            var vm = Create(canForceArrange: true);
            vm.ToggleForceArrangeCommand.Execute(null);
            vm.OffsetCommand.Execute(null);
            vm.SelectedEquipmentProduct = vm.FilteredEquipmentProducts.First(i => i.EquipmentProductId == 4);
            vm.ConfirmCommand.Execute(null);
            Assert.NotNull(vm.Result);
            Assert.True(vm.Result!.IsForceArrange);
            Assert.Equal(TuningType.Offset, vm.Result.TuningType);
            Assert.Equal(4, vm.Result.EquipmentProductId);
        }
    }
}
