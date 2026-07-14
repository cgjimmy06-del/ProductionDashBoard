using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using Moq;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class ProgramLibraryViewModelTests
    {
        // LoadAsync 使用 Application.Current.Dispatcher，xUnit 無 WPF Dispatcher。
        // 本測試專注於：RebuildCards / ComputeLight / 統計計算。LoadAsync 由 UI 手動驗證。

        // CardsView / SelectedProgramsView 為 ICollectionView，用 helper 轉換成具型別清單供測試使用。
        private static IReadOnlyList<ProgramDeviceCardViewModel> GetCards(ProgramLibraryViewModel vm)
            => vm.CardsView.Cast<ProgramDeviceCardViewModel>().ToList();

        private static IReadOnlyList<ProgramItemUiModel> GetSelectedPrograms(ProgramLibraryViewModel vm)
            => vm.SelectedProgramsView.Cast<ProgramItemUiModel>().ToList();

        private static EquipmentProduct MakeEp(int eqId, string eqName,
            string partNo, string brand, string modelName, string processName,
            TuningType status) => new()
        {
            EquipmentId = eqId,
            ProductionStatus = status,
            Equipment = new Equipment { Id = eqId, Name = eqName },
            Sop = new SopChecklist
            {
                Product = new Product
                {
                    Part  = new ProductPart { PartNo = partNo, Brand = brand },
                    Model = new ProductModel { Name = modelName }
                },
                Process = new WorkProcess { Name = processName }
            }
        };

        private static ProgramLibraryViewModel CreateVmWithData(
            List<EquipmentProduct> data,
            Mock<IDataService>? mockData = null,
            IDialogService? dialog = null)
        {
            mockData ??= new Mock<IDataService>();
            mockData.Setup(d => d.GetAllEquipmentProductsAsync())
                    .ReturnsAsync(data);
            var log = new LogService();
            var auth = new AuthorizationService();
            var cardReader = new Mock<ICardReaderService>().Object;
            var core = new DashboardCoreServices(log, mockData.Object, auth, cardReader);
            dialog ??= new Mock<IDialogService>().Object;

            var vm = new ProgramLibraryViewModel(core, dialog);
            // 直接注入 _all 繞過非同步 LoadAsync（UI thread 限制）
            vm.InjectDataForTest(data);
            return vm;
        }

        // ── 全域統計 ─────────────────────────────────────────────────────────
        [Fact]
        public void GlobalStats_AreCorrectAfterInject()
        {
            var data = new List<EquipmentProduct>
            {
                MakeEp(1, "A", "P1", "B1", "M1", "Pr1", TuningType.Feasible),
                MakeEp(1, "A", "P2", "B1", "M2", "Pr1", TuningType.Teaching),
                MakeEp(2, "B", "P3", "B2", "M3", "Pr2", TuningType.Offset),
                MakeEp(2, "B", "P4", "B2", "M3", "Pr2", TuningType.Pending),
                MakeEp(3, "C", "P5", "B3", "M4", "Pr3", TuningType.Infeasible),
            };
            var vm = CreateVmWithData(data);

            Assert.Equal(3, vm.GlobalMachineCount);
            Assert.Equal(5, vm.GlobalProgramCount);
            Assert.Equal(1, vm.GlobalFeasibleCount);
            Assert.Equal(1, vm.GlobalTeachingCount);
            Assert.Equal(1, vm.GlobalOffsetCount);
            Assert.Equal(1, vm.GlobalPendingCount);
            Assert.Equal(1, vm.GlobalInfeasibleCount);
        }

        [Fact]
        public void GlobalStats_DoNotChangeAfterFilter()
        {
            var data = new List<EquipmentProduct>
            {
                MakeEp(1, "A", "P1", "B1", "M1", "Pr1", TuningType.Feasible),
                MakeEp(2, "B", "P2", "B2", "M2", "Pr2", TuningType.Teaching),
            };
            var vm = CreateVmWithData(data);

            vm.PartFilter = "P1";  // 應讓 Equipment 2 消失

            Assert.Equal(2, vm.GlobalMachineCount);   // 全域不動
            Assert.Equal(2, vm.GlobalProgramCount);
            Assert.Equal(1, vm.FilteredMachineCount); // 篩選後只剩 1
        }

        // ── 篩選邏輯 ─────────────────────────────────────────────────────────
        [Fact]
        public void Filter_ByPart_HidesNonMatchingCard()
        {
            var data = new List<EquipmentProduct>
            {
                MakeEp(1, "A", "ABC", "B1", "M1", "Pr1", TuningType.Feasible),
                MakeEp(2, "B", "XYZ", "B1", "M1", "Pr1", TuningType.Feasible),
            };
            var vm = CreateVmWithData(data);

            vm.PartFilter = "abc";  // 大小寫不分

            Assert.Single(GetCards(vm));
            Assert.Equal(1, GetCards(vm)[0].EquipmentId);
        }

        [Fact]
        public void Filter_MachineWithZeroMatchingPrograms_IsHidden()
        {
            var data = new List<EquipmentProduct>
            {
                MakeEp(1, "A", "P1", "B1", "M1", "Pr1", TuningType.Feasible),
                MakeEp(2, "B", "P2", "B2", "M2", "Pr2", TuningType.Feasible),
            };
            var vm = CreateVmWithData(data);

            vm.BrandFilter = "B1";

            Assert.Single(GetCards(vm));
        }

        [Fact]
        public void Filter_EmptyString_ShowsAllCards()
        {
            var data = new List<EquipmentProduct>
            {
                MakeEp(1, "A", "P1", "B1", "M1", "Pr1", TuningType.Feasible),
                MakeEp(2, "B", "P2", "B2", "M2", "Pr2", TuningType.Feasible),
                MakeEp(3, "C", "P3", "B3", "M3", "Pr3", TuningType.Feasible),
            };
            var vm = CreateVmWithData(data);

            Assert.Equal(3, GetCards(vm).Count);
        }

        // ── 燈號優先序 ───────────────────────────────────────────────────────
        [Theory]
        [InlineData(TuningType.Teaching,   ProgramLight.Error)]
        [InlineData(TuningType.Infeasible, ProgramLight.Error)]
        [InlineData(TuningType.Offset,     ProgramLight.Offset)]
        [InlineData(TuningType.Pending,    ProgramLight.Pending)]
        [InlineData(TuningType.Feasible,   ProgramLight.Feasible)]
        public void ComputeLight_SingleStatus_ReturnsExpected(TuningType status, ProgramLight expected)
        {
            var progs = new List<EquipmentProduct>
            {
                new() { ProductionStatus = status }
            };
            Assert.Equal(expected, ProgramDeviceCardViewModel.ComputeLight(progs));
        }

        [Fact]
        public void ComputeLight_MixedStatuses_ErrorWins()
        {
            var progs = new List<EquipmentProduct>
            {
                new() { ProductionStatus = TuningType.Feasible },
                new() { ProductionStatus = TuningType.Pending },
                new() { ProductionStatus = TuningType.Offset },
                new() { ProductionStatus = TuningType.Teaching },
            };
            Assert.Equal(ProgramLight.Error, ProgramDeviceCardViewModel.ComputeLight(progs));
        }

        [Fact]
        public void ComputeLight_OffsetBeforePending()
        {
            var progs = new List<EquipmentProduct>
            {
                new() { ProductionStatus = TuningType.Feasible },
                new() { ProductionStatus = TuningType.Pending },
                new() { ProductionStatus = TuningType.Offset },
            };
            Assert.Equal(ProgramLight.Offset, ProgramDeviceCardViewModel.ComputeLight(progs));
        }

        // ── CountLabel ───────────────────────────────────────────────────────
        [Fact]
        public void CardLabels_AreCorrectFormat()
        {
            var card = new ProgramDeviceCardViewModel(1, "A", 5, 12, ProgramLight.Feasible);
            Assert.Equal("5", card.FeasibleLabel);
            Assert.Equal("/12", card.TotalLabel);
        }

        // ── 篩選後統計 ───────────────────────────────────────────────────────
        [Fact]
        public void FilteredStats_UpdateAfterFilter()
        {
            var data = new List<EquipmentProduct>
            {
                MakeEp(1, "A", "P1", "B1", "M1", "Pr1", TuningType.Feasible),
                MakeEp(1, "A", "P2", "B1", "M1", "Pr1", TuningType.Teaching),
                MakeEp(2, "B", "P3", "B2", "M2", "Pr2", TuningType.Feasible),
            };
            var vm = CreateVmWithData(data);

            vm.BrandFilter = "B1";

            Assert.Equal(1, vm.FilteredMachineCount);
            Assert.Equal(2, vm.FilteredProgramCount);
            Assert.Equal(1, vm.FilteredFeasibleCount);
            Assert.Equal(1, vm.FilteredTeachingCount);
        }

        // ── Detail Panel ──────────────────────────────────────────────────────

        private static List<EquipmentProduct> MakeDetailData() =>
        [
            MakeEp(1, "EQ-A", "P1", "B1", "M1", "Pr1", TuningType.Feasible)  .WithSeqNo(1),
            MakeEp(1, "EQ-A", "P2", "B1", "M1", "Pr1", TuningType.Teaching)  .WithSeqNo(3),
            MakeEp(1, "EQ-A", "P3", "B1", "M1", "Pr1", TuningType.Pending)   .WithSeqNo(2),
            MakeEp(2, "EQ-B", "P4", "B2", "M2", "Pr2", TuningType.Offset)    .WithSeqNo(1),
        ];

        [Fact]
        public void SelectedPrograms_UpdatesOnCardSelect()
        {
            var vm = CreateVmWithData(MakeDetailData());
            var card = GetCards(vm).First(c => c.EquipmentId == 1);

            vm.SelectCardCommand.Execute(card);

            Assert.Equal(3, GetSelectedPrograms(vm).Count);
            Assert.True(vm.IsDetailVisible);
        }

        [Fact]
        public void SelectedPrograms_SortedBySeqNo()
        {
            var vm = CreateVmWithData(MakeDetailData());
            vm.SelectCardCommand.Execute(GetCards(vm).First(c => c.EquipmentId == 1));

            var seqs = GetSelectedPrograms(vm).Select(ep => ep.SeqNo).ToList();
            Assert.Equal(seqs.OrderBy(s => s).ToList(), seqs);
        }

        [Fact]
        public void SelectedPrograms_FilterSynced()
        {
            var vm = CreateVmWithData(MakeDetailData());
            vm.SelectCardCommand.Execute(GetCards(vm).First(c => c.EquipmentId == 1));

            // 讓 EQ-A 只剩 Feasible（Brand=B1 全過，再用 PartFilter 限制只看 P1）
            vm.PartFilter = "P1";

            Assert.Single(GetSelectedPrograms(vm));
            Assert.Equal(TuningType.Feasible, GetSelectedPrograms(vm)[0].ProductionStatus);
        }

        [Fact]
        public void SelectedPrograms_ClearsOnDeselect()
        {
            var vm = CreateVmWithData(MakeDetailData());
            vm.SelectCardCommand.Execute(GetCards(vm).First(c => c.EquipmentId == 1));
            Assert.NotEmpty(GetSelectedPrograms(vm));

            vm.CloseDetailCommand.Execute(null);

            Assert.Empty(GetSelectedPrograms(vm));
            Assert.False(vm.IsDetailVisible);
        }

        [Fact]
        public void DetailStats_Correct()
        {
            var vm = CreateVmWithData(MakeDetailData());
            vm.SelectCardCommand.Execute(GetCards(vm).First(c => c.EquipmentId == 1));

            Assert.Equal(1, vm.DetailFeasibleCount);
            Assert.Equal(1, vm.DetailTeachingCount);
            Assert.Equal(0, vm.DetailOffsetCount);
            Assert.Equal(1, vm.DetailPendingCount);
        }

        // ── EditStatus ───────────────────────────────────────────────────────
        [Fact]
        public async Task EditStatus_Confirmed_UpdatesStatusAndStats()
        {
            var ep = MakeEp(1, "EQ-A", "P1", "B1", "M1", "Pr1", TuningType.Feasible);
            ep.EquipmentProductId = 42;
            ep.SeqNo = 1;
            var data = new List<EquipmentProduct> { ep };

            var fixedDate = new DateTime(2026, 6, 5, 10, 30, 0);
            var mockData = new Mock<IDataService>();
            mockData.Setup(d => d.UpdateProductionStatusAsync(42, TuningType.Pending))
                    .ReturnsAsync(fixedDate);

            // 模擬使用者在 Dialog 選「待審核」並確認
            var mockDialog = new Mock<IDialogService>();
            mockDialog.Setup(d => d.ShowDialog(It.IsAny<DialogBaseViewModel<ProgramStatusResult>>()))
                      .Callback<DialogBaseViewModel<ProgramStatusResult>>(v =>
                      {
                          var pvm = (ProgramStatusDialogViewModel)v;
                          pvm.SelectPendingCommand.Execute(null);
                          pvm.ConfirmCommand.Execute(null);
                      })
                      .Returns<DialogBaseViewModel<ProgramStatusResult>>(v => v.Result);

            var vm = CreateVmWithData(data, mockData, mockDialog.Object);
            vm.SelectCardCommand.Execute(GetCards(vm).First(c => c.EquipmentId == 1));
            var item = GetSelectedPrograms(vm).First(i => i.EquipmentProductId == 42);

            await vm.EditStatusCommand.ExecuteAsync(item);

            mockData.Verify(d => d.UpdateProductionStatusAsync(42, TuningType.Pending), Times.Once);
            Assert.Equal(TuningType.Pending, ep.ProductionStatus);
            Assert.Equal(fixedDate, ep.UpdateAt);
            Assert.Equal(0, vm.GlobalFeasibleCount);
            Assert.Equal(1, vm.GlobalPendingCount);
            Assert.Equal(TuningType.Pending, GetSelectedPrograms(vm).First().ProductionStatus);
        }

        // ── 批次狀態（BulkSetTeaching / BulkSetOffset） ───────────────────────
        [Fact]
        public async Task BulkSetTeaching_AllAlreadyTeaching_DoesNotPromptOrCallService()
        {
            var data = new List<EquipmentProduct>
            {
                MakeEp(1, "EQ-A", "P1", "B1", "M1", "Pr1", TuningType.Teaching),
                MakeEp(1, "EQ-A", "P2", "B1", "M1", "Pr1", TuningType.Teaching),
            };
            var mockData = new Mock<IDataService>();
            var mockDialog = new Mock<IDialogService>();
            var vm = CreateVmWithData(data, mockData, mockDialog.Object);
            vm.SelectCardCommand.Execute(GetCards(vm).First(c => c.EquipmentId == 1));

            await vm.BulkSetTeachingCommand.ExecuteAsync(null);

            mockDialog.Verify(d => d.ShowConfirm(It.IsAny<string>()), Times.Never);
            mockData.Verify(d => d.BulkUpdateProductionStatusAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<TuningType>()), Times.Never);
        }

        [Fact]
        public async Task BulkSetTeaching_PartialMatch_ConfirmedUpdatesOnlyNonMatching()
        {
            var ep1 = MakeEp(1, "EQ-A", "P1", "B1", "M1", "Pr1", TuningType.Feasible);
            ep1.EquipmentProductId = 10;
            var ep2 = MakeEp(1, "EQ-A", "P2", "B1", "M1", "Pr1", TuningType.Teaching);
            ep2.EquipmentProductId = 20;
            var ep3 = MakeEp(1, "EQ-A", "P3", "B1", "M1", "Pr1", TuningType.Offset);
            ep3.EquipmentProductId = 30;
            var data = new List<EquipmentProduct> { ep1, ep2, ep3 };

            var fixedDate = new DateTime(2026, 7, 14, 9, 0, 0);
            var mockData = new Mock<IDataService>();
            mockData.Setup(d => d.BulkUpdateProductionStatusAsync(It.IsAny<IEnumerable<int>>(), TuningType.Teaching))
                    .ReturnsAsync(fixedDate);
            var mockDialog = new Mock<IDialogService>();
            mockDialog.Setup(d => d.ShowConfirm(It.IsAny<string>())).Returns(true);

            var vm = CreateVmWithData(data, mockData, mockDialog.Object);
            vm.SelectCardCommand.Execute(GetCards(vm).First(c => c.EquipmentId == 1));

            await vm.BulkSetTeachingCommand.ExecuteAsync(null);

            mockData.Verify(d => d.BulkUpdateProductionStatusAsync(
                It.Is<IEnumerable<int>>(ids => ids.OrderBy(x => x).SequenceEqual(new[] { 10, 30 })),
                TuningType.Teaching), Times.Once);
            Assert.Equal(TuningType.Teaching, ep1.ProductionStatus);
            Assert.Equal(TuningType.Teaching, ep2.ProductionStatus); // 本來就是，維持不變
            Assert.Equal(TuningType.Teaching, ep3.ProductionStatus);
            Assert.Equal(fixedDate, ep1.UpdateAt);
            Assert.Null(ep2.UpdateAt); // 本來就符合狀態，略過未寫入
            Assert.Equal(fixedDate, ep3.UpdateAt);
        }

        [Fact]
        public async Task BulkSetOffset_NotConfirmed_DoesNotCallService()
        {
            var ep = MakeEp(1, "EQ-A", "P1", "B1", "M1", "Pr1", TuningType.Feasible);
            ep.EquipmentProductId = 5;
            var data = new List<EquipmentProduct> { ep };

            var mockData = new Mock<IDataService>();
            var mockDialog = new Mock<IDialogService>();
            mockDialog.Setup(d => d.ShowConfirm(It.IsAny<string>())).Returns(false);

            var vm = CreateVmWithData(data, mockData, mockDialog.Object);
            vm.SelectCardCommand.Execute(GetCards(vm).First(c => c.EquipmentId == 1));

            await vm.BulkSetOffsetCommand.ExecuteAsync(null);

            mockData.Verify(d => d.BulkUpdateProductionStatusAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<TuningType>()), Times.Never);
            Assert.Equal(TuningType.Feasible, ep.ProductionStatus);
        }

        [Fact]
        public async Task BulkSetTeaching_NoSelectedCard_DoesNothing()
        {
            var data = new List<EquipmentProduct>
            {
                MakeEp(1, "EQ-A", "P1", "B1", "M1", "Pr1", TuningType.Feasible),
            };
            var mockData = new Mock<IDataService>();
            var mockDialog = new Mock<IDialogService>();
            var vm = CreateVmWithData(data, mockData, mockDialog.Object);

            await vm.BulkSetTeachingCommand.ExecuteAsync(null);

            mockDialog.Verify(d => d.ShowConfirm(It.IsAny<string>()), Times.Never);
            mockData.Verify(d => d.BulkUpdateProductionStatusAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<TuningType>()), Times.Never);
        }
    }

    internal static class EquipmentProductExtensions
    {
        internal static EquipmentProduct WithSeqNo(this EquipmentProduct ep, int seqNo)
        {
            ep.SeqNo = seqNo;
            return ep;
        }
    }
}
