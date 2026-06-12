using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using Moq;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class ScheduleEquipmentCardViewModelTests
    {
        // ─── 建構輔助 ─────────────────────────────────────────────────────────

        private static EquipmentProduct MakeEp(int productId, int processId, TuningType status)
            => new()
            {
                ProductionStatus = status,
                Sop = new SopChecklist { ProductId = productId, ProcessId = processId }
            };

        private static ScheduleEquipmentCardViewModel MakeCard(
            List<EquipmentProduct> eps,
            int pendingCount = 0,
            int inProdCount  = 0,
            int pendingQty   = 0)
        {
            var card = new ScheduleEquipmentCardViewModel
            {
                EquipmentId          = 1,
                Code                 = "M01",
                Name                 = "Machine-01",
                AllEquipmentProducts = eps,
            };
            card.PendingOrderCount      = pendingCount;
            card.InProductionOrderCount = inProdCount;
            card.TotalPendingQty        = pendingQty;
            return card;
        }

        // ─── 色碼計算 ──────────────────────────────────────────────────────────

        [Fact]
        public void CardColor_NoFeasibleEp_Gray()
        {
            var card = MakeCard(new List<EquipmentProduct>
            {
                MakeEp(1, 1, TuningType.Pending),
            });

            Assert.Equal(ScheduleCardColor.Gray, card.CardColor);
        }

        [Fact]
        public void CardColor_CompatibleIdle_Green()
        {
            var card = MakeCard(new List<EquipmentProduct>
            {
                MakeEp(1, 1, TuningType.Feasible),
            });

            Assert.Equal(ScheduleCardColor.Green, card.CardColor);
        }

        [Fact]
        public void CardColor_CompatibleWithPendingOrders_Orange()
        {
            var card = MakeCard(new List<EquipmentProduct>
            {
                MakeEp(1, 1, TuningType.Feasible),
            }, pendingCount: 2);

            Assert.Equal(ScheduleCardColor.Orange, card.CardColor);
        }

        [Fact]
        public void CardColor_CompatibleWithInProductionOrders_Blue()
        {
            var card = MakeCard(new List<EquipmentProduct>
            {
                MakeEp(1, 1, TuningType.Feasible),
            }, pendingCount: 1, inProdCount: 1);

            Assert.Equal(ScheduleCardColor.Blue, card.CardColor);
        }

        [Fact]
        public void CardColor_FeasibleExistsButNotCompatible_Gray()
        {
            var card = MakeCard(new List<EquipmentProduct>
            {
                MakeEp(1, 1, TuningType.Feasible),
            });
            card.IsCompatibleWithSelectedSchedule = false;

            Assert.Equal(ScheduleCardColor.Gray, card.CardColor);
        }

        // ─── HasAnyFeasibleEp ──────────────────────────────────────────────────

        [Fact]
        public void HasAnyFeasibleEp_WithFeasibleEp_True()
        {
            var card = MakeCard(new List<EquipmentProduct>
            {
                MakeEp(1, 1, TuningType.Pending),
                MakeEp(2, 1, TuningType.Feasible),
            });

            Assert.True(card.HasAnyFeasibleEp);
        }

        [Fact]
        public void HasAnyFeasibleEp_NoFeasibleEp_False()
        {
            var card = MakeCard(new List<EquipmentProduct>
            {
                MakeEp(1, 1, TuningType.Pending),
                MakeEp(2, 1, TuningType.Offset),
            });

            Assert.False(card.HasAnyFeasibleEp);
        }

        [Fact]
        public void HasAnyFeasibleEp_EmptyList_False()
        {
            var card = MakeCard(new List<EquipmentProduct>());

            Assert.False(card.HasAnyFeasibleEp);
        }

        // ─── HasAnyFeasibleEpForKeys ──────────────────────────────────────────

        [Fact]
        public void HasAnyFeasibleEpForKeys_MatchingFeasibleEp_True()
        {
            var card = MakeCard(new List<EquipmentProduct>
            {
                MakeEp(productId: 10, processId: 20, TuningType.Feasible),
            });
            var keys = new HashSet<(int ProductId, int ProcessId)> { (10, 20) };

            Assert.True(card.HasAnyFeasibleEpForKeys(keys));
        }

        [Fact]
        public void HasAnyFeasibleEpForKeys_MatchingButNotFeasible_False()
        {
            var card = MakeCard(new List<EquipmentProduct>
            {
                MakeEp(productId: 10, processId: 20, TuningType.Pending),
            });
            var keys = new HashSet<(int ProductId, int ProcessId)> { (10, 20) };

            Assert.False(card.HasAnyFeasibleEpForKeys(keys));
        }

        [Fact]
        public void HasAnyFeasibleEpForKeys_FeasibleButDifferentProcess_False()
        {
            var card = MakeCard(new List<EquipmentProduct>
            {
                MakeEp(productId: 10, processId: 99, TuningType.Feasible),
            });
            var keys = new HashSet<(int ProductId, int ProcessId)> { (10, 20) };

            Assert.False(card.HasAnyFeasibleEpForKeys(keys));
        }

        [Fact]
        public void HasAnyFeasibleEpForKeys_MultipleKeys_MatchesAny()
        {
            var card = MakeCard(new List<EquipmentProduct>
            {
                MakeEp(productId: 10, processId: 20, TuningType.Feasible),
            });
            var keys = new HashSet<(int ProductId, int ProcessId)>
            {
                (99, 99),
                (10, 20),
            };

            Assert.True(card.HasAnyFeasibleEpForKeys(keys));
        }

        // ─── HasAnyCompatibleEpForKeys ────────────────────────────────────────

        [Fact]
        public void HasAnyCompatibleEpForKeys_MatchingFeasibleEp_True()
        {
            var card = MakeCard(new List<EquipmentProduct>
            {
                MakeEp(productId: 10, processId: 20, TuningType.Feasible),
            });
            var keys = new HashSet<(int ProductId, int ProcessId)> { (10, 20) };

            Assert.True(card.HasAnyCompatibleEpForKeys(keys));
        }

        [Fact]
        public void HasAnyCompatibleEpForKeys_MatchingNonFeasibleEp_True()
        {
            var card = MakeCard(new List<EquipmentProduct>
            {
                MakeEp(productId: 10, processId: 20, TuningType.Pending),
            });
            var keys = new HashSet<(int ProductId, int ProcessId)> { (10, 20) };

            Assert.True(card.HasAnyCompatibleEpForKeys(keys));
        }

        [Fact]
        public void HasAnyCompatibleEpForKeys_NoMatchingKey_False()
        {
            var card = MakeCard(new List<EquipmentProduct>
            {
                MakeEp(productId: 10, processId: 20, TuningType.Feasible),
            });
            var keys = new HashSet<(int ProductId, int ProcessId)> { (99, 99) };

            Assert.False(card.HasAnyCompatibleEpForKeys(keys));
        }

        // ─── ActiveOrderCount ─────────────────────────────────────────────────

        [Fact]
        public void ActiveOrderCount_PendingPlusInProduction()
        {
            var card = MakeCard(new List<EquipmentProduct>(), pendingCount: 2, inProdCount: 3);

            Assert.Equal(5, card.ActiveOrderCount);
        }

        [Fact]
        public void ActiveOrderCount_NeitherPendingNorInProduction_Zero()
        {
            var card = MakeCard(new List<EquipmentProduct>());

            Assert.Equal(0, card.ActiveOrderCount);
        }

        // ─── FeasibleMatchingCount / TotalMatchingCount ───────────────────────

        [Fact]
        public void Counts_SetDirectly_ReflectCorrectly()
        {
            var card = MakeCard(new List<EquipmentProduct>());
            card.FeasibleMatchingCount = 2;
            card.TotalMatchingCount    = 3;

            Assert.Equal(2, card.FeasibleMatchingCount);
            Assert.Equal(3, card.TotalMatchingCount);
        }

        // ─── LoadLevel ────────────────────────────────────────────────────────

        [Theory]
        [InlineData(0,   ScheduleCardLoadLevel.None)]
        [InlineData(1,   ScheduleCardLoadLevel.Low)]
        [InlineData(99,  ScheduleCardLoadLevel.Low)]
        [InlineData(100, ScheduleCardLoadLevel.Mid)]
        [InlineData(500, ScheduleCardLoadLevel.Mid)]
        [InlineData(501, ScheduleCardLoadLevel.High)]
        [InlineData(999, ScheduleCardLoadLevel.High)]
        public void LoadLevel_Boundary_CorrectLevel(int qty, ScheduleCardLoadLevel expected)
        {
            var card = MakeCard(new List<EquipmentProduct>());
            card.TotalPendingQty = qty;

            Assert.Equal(expected, card.LoadLevel);
        }

        // ─── SortOrder ─────────────────────────────────────────────────────────

        [Fact]
        public void SortOrder_GreenLessThanOrange()
        {
            var green = MakeCard(new List<EquipmentProduct> { MakeEp(1, 1, TuningType.Feasible) });
            var orange = MakeCard(new List<EquipmentProduct> { MakeEp(1, 1, TuningType.Feasible) }, pendingCount: 1);

            Assert.True(green.SortOrder < orange.SortOrder);
        }

        [Fact]
        public void SortOrder_OrangeLessThanBlue()
        {
            var orange = MakeCard(new List<EquipmentProduct> { MakeEp(1, 1, TuningType.Feasible) }, pendingCount: 1);
            var blue   = MakeCard(new List<EquipmentProduct> { MakeEp(1, 1, TuningType.Feasible) }, inProdCount: 1);

            Assert.True(orange.SortOrder < blue.SortOrder);
        }

        [Fact]
        public void SortOrder_BlueLessThanGray()
        {
            var blue = MakeCard(new List<EquipmentProduct> { MakeEp(1, 1, TuningType.Feasible) }, inProdCount: 1);
            var gray = MakeCard(new List<EquipmentProduct> { MakeEp(1, 1, TuningType.Pending) });

            Assert.True(blue.SortOrder < gray.SortOrder);
        }

        // ─── ResetForLayer1 ────────────────────────────────────────────────────

        [Fact]
        public void ResetForLayer1_ResetsAllLayer2State()
        {
            var card = MakeCard(new List<EquipmentProduct> { MakeEp(1, 1, TuningType.Feasible) });
            card.IsCompatibleWithSelectedSchedule = false;
            card.HasMatchingProgram               = false;
            card.FeasibleMatchingCount            = 5;
            card.TotalMatchingCount               = 10;
            card.CompatibleEquipmentProducts      = new List<EquipmentProduct> { MakeEp(1, 1, TuningType.Feasible) };

            card.ResetForLayer1();

            Assert.True(card.IsCompatibleWithSelectedSchedule);
            Assert.True(card.HasMatchingProgram);
            Assert.Equal(0, card.FeasibleMatchingCount);
            Assert.Equal(0, card.TotalMatchingCount);
            Assert.Empty(card.CompatibleEquipmentProducts);
        }
    }

    // ─── UpdateCardCompatibility 整合測試（透過 ScheduleViewModel）─────────────

    public class ScheduleViewModelCardCompatibilityTests
    {
        private static ScheduleViewModel CreateVm()
        {
            var mock = new Mock<IDataService>();
            mock.Setup(d => d.GetAllSchedulesAsync()).ReturnsAsync(new List<Schedule>());
            mock.Setup(d => d.GetAllOrderProductionsAsync()).ReturnsAsync(new List<OrderProduction>());
            mock.Setup(d => d.GetAllEquipmentProductsAsync()).ReturnsAsync(new List<EquipmentProduct>());
            mock.Setup(d => d.GetAllInProgressProgramTuningAsync()).ReturnsAsync(new List<ProgramTuningRecord>());
            var log  = new LogService();
            var auth = new AuthorizationService();
            var cr   = new Mock<ICardReaderService>().Object;
            var dialog = new Mock<IDialogService>().Object;
            return new ScheduleViewModel(new DashboardCoreServices(log, mock.Object, auth, cr), dialog);
        }

        private static EquipmentProduct MakeEp(int productId, int processId, TuningType status, int eqId = 1)
            => new()
            {
                EquipmentId      = eqId,
                ProductionStatus = status,
                Equipment        = new Equipment { Id = eqId, Code = $"M0{eqId}", Name = $"Machine-0{eqId}" },
                Sop              = new SopChecklist { ProductId = productId, ProcessId = processId }
            };

        private static IReadOnlyList<ScheduleEquipmentCardViewModel> GetCards(ScheduleViewModel vm)
            => vm.EquipmentCardsView.Cast<ScheduleEquipmentCardViewModel>().ToList();

        // ─── UpdateCardCompatibility：HasMatchingProgram ─────────────────────

        [Fact]
        public void UpdateCardCompatibility_NoMatchingEp_HasMatchingProgramFalse()
        {
            var vm = CreateVm();
            vm.InjectDataForTest(new List<ScheduleUiModel>(), new List<OrderProductionInfo>());
            vm.InjectCardsForTest(new List<EquipmentProduct>
            {
                MakeEp(productId: 10, processId: 20, TuningType.Feasible, eqId: 1),
            });

            var schedule = new ScheduleUiModel { ScheduleId = 1, ProductId = 99, ProcessId = 99 };
            vm.SelectedSchedule = schedule;

            var cards = GetCards(vm);
            Assert.Empty(cards); // HasMatchingProgram=false → filtered out
        }

        [Fact]
        public void UpdateCardCompatibility_MatchingFeasibleEp_CompatibleAndVisible()
        {
            var vm = CreateVm();
            vm.InjectDataForTest(new List<ScheduleUiModel>(), new List<OrderProductionInfo>());
            vm.InjectCardsForTest(new List<EquipmentProduct>
            {
                MakeEp(productId: 10, processId: 20, TuningType.Feasible, eqId: 1),
            });

            var schedule = new ScheduleUiModel { ScheduleId = 1, ProductId = 10, ProcessId = 20 };
            vm.SelectedSchedule = schedule;

            var cards = GetCards(vm);
            Assert.Single(cards);
            Assert.True(cards[0].IsCompatibleWithSelectedSchedule);
            Assert.True(cards[0].HasMatchingProgram);
            Assert.Equal(1, cards[0].FeasibleMatchingCount);
            Assert.Equal(1, cards[0].TotalMatchingCount);
        }

        [Fact]
        public void UpdateCardCompatibility_MatchingButNotFeasible_GrayAndVisible()
        {
            var vm = CreateVm();
            vm.InjectDataForTest(new List<ScheduleUiModel>(), new List<OrderProductionInfo>());
            vm.InjectCardsForTest(new List<EquipmentProduct>
            {
                MakeEp(productId: 10, processId: 20, TuningType.Pending, eqId: 1),
            });

            var schedule = new ScheduleUiModel { ScheduleId = 1, ProductId = 10, ProcessId = 20 };
            vm.SelectedSchedule = schedule;

            var cards = GetCards(vm);
            Assert.Single(cards); // HasMatchingProgram=true → visible
            Assert.False(cards[0].IsCompatibleWithSelectedSchedule);
            Assert.Equal(ScheduleCardColor.Gray, cards[0].CardColor);
            Assert.Equal(0, cards[0].FeasibleMatchingCount);
            Assert.Equal(1, cards[0].TotalMatchingCount);
        }

        [Fact]
        public void UpdateCardCompatibility_ResetOnDeselect_Layer1Restored()
        {
            var vm = CreateVm();
            vm.InjectDataForTest(new List<ScheduleUiModel>(), new List<OrderProductionInfo>());
            vm.InjectCardsForTest(new List<EquipmentProduct>
            {
                MakeEp(productId: 10, processId: 20, TuningType.Feasible, eqId: 1),
            });

            var schedule = new ScheduleUiModel { ScheduleId = 1, ProductId = 99, ProcessId = 99 };
            vm.SelectedSchedule = schedule;
            Assert.Empty(GetCards(vm)); // hidden

            vm.SelectedSchedule = null;
            Assert.Single(GetCards(vm)); // restored
        }

        // ─── IsStatsPanelVisible ──────────────────────────────────────────────

        [Fact]
        public void IsStatsPanelVisible_NoSelection_False()
        {
            var vm = CreateVm();
            Assert.False(vm.IsStatsPanelVisible);
        }

        [Fact]
        public void IsStatsPanelVisible_WithSelection_True()
        {
            var vm = CreateVm();
            vm.InjectDataForTest(new List<ScheduleUiModel>(), new List<OrderProductionInfo>());
            vm.SelectedSchedule = new ScheduleUiModel { ScheduleId = 1 };

            Assert.True(vm.IsStatsPanelVisible);
        }

        // ─── UpdateSelectedScheduleStats ──────────────────────────────────────

        [Fact]
        public void SelectedScheduleStats_CalculatesCorrectly()
        {
            var vm = CreateVm();
            var orders = new List<OrderProductionInfo>
            {
                new() { ScheduleId = 1, EquipmentId = 1, Status = OrderProductionStatus.Pending,      Quantity = 50 },
                new() { ScheduleId = 1, EquipmentId = 2, Status = OrderProductionStatus.InProduction, Quantity = 30 },
                new() { ScheduleId = 1, EquipmentId = 1, Status = OrderProductionStatus.Completed,    Quantity = 20 },
                new() { ScheduleId = 1, EquipmentId = 3, Status = OrderProductionStatus.Cancelled,    Quantity = 10 },
            };
            vm.InjectDataForTest(new List<ScheduleUiModel>(), orders);
            vm.InjectCardsForTest(new List<EquipmentProduct>
            {
                MakeEp(1, 1, TuningType.Feasible, eqId: 1),
                MakeEp(1, 1, TuningType.Feasible, eqId: 2),
            });

            vm.SelectedSchedule = new ScheduleUiModel { ScheduleId = 1, ProductId = 1, ProcessId = 1 };

            Assert.Equal(3, vm.SelectedScheduleOrderTotal);       // 非 Cancelled
            Assert.Equal(2, vm.SelectedScheduleOrderIncomplete);   // Pending + InProduction
            Assert.Contains("Machine-01", vm.SelectedScheduleEquipments);
            Assert.Contains("Machine-02", vm.SelectedScheduleEquipments);
        }
    }
}
