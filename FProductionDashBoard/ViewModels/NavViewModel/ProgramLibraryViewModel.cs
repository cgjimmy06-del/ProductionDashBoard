using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace FProductionDashBoard.ViewModels
{
    public partial class ProgramLibraryViewModel : ObservableObject
    {
        private readonly DashboardCoreServices _core;
        private readonly IDialogService _dialog;
        private List<EquipmentProduct> _all = new();

        // 全域統計（載入後算一次，不隨篩選變動）
        [ObservableProperty] private int globalMachineCount;
        [ObservableProperty] private int globalProgramCount;
        [ObservableProperty] private int globalFeasibleCount;
        [ObservableProperty] private int globalTeachingCount;
        [ObservableProperty] private int globalOffsetCount;
        [ObservableProperty] private int globalPendingCount;
        [ObservableProperty] private int globalInfeasibleCount;

        // 篩選後統計
        [ObservableProperty] private int filteredMachineCount;
        [ObservableProperty] private int filteredProgramCount;
        [ObservableProperty] private int filteredFeasibleCount;
        [ObservableProperty] private int filteredTeachingCount;
        [ObservableProperty] private int filteredOffsetCount;
        [ObservableProperty] private int filteredPendingCount;
        [ObservableProperty] private int filteredInfeasibleCount;

        // 篩選條件
        [ObservableProperty] private TuningType? tuningTypeFilter;
        [ObservableProperty] private string partFilter = string.Empty;
        [ObservableProperty] private string brandFilter = string.Empty;
        [ObservableProperty] private string modelFilter = string.Empty;
        [ObservableProperty] private string processFilter = string.Empty;

        public IReadOnlyList<TuningTypeFilterOption> TuningTypeFilterOptions { get; }

        private readonly List<ProgramDeviceCardViewModel> _cards = new();
        private readonly List<ProgramItemUiModel> _programs = new();
        public ICollectionView CardsView { get; }
        public ICollectionView SelectedProgramsView { get; }

        [ObservableProperty] private ProgramDeviceCardViewModel? selectedCard;
        public bool IsDetailVisible => SelectedCard != null;

        // 標題列小統計
        [ObservableProperty] private int detailFeasibleCount;
        [ObservableProperty] private int detailTeachingCount;
        [ObservableProperty] private int detailOffsetCount;
        [ObservableProperty] private int detailPendingCount;

        partial void OnTuningTypeFilterChanged(TuningType? value) => RebuildCards();
        partial void OnPartFilterChanged(string value) => RebuildCards();
        partial void OnBrandFilterChanged(string value) => RebuildCards();
        partial void OnModelFilterChanged(string value) => RebuildCards();
        partial void OnProcessFilterChanged(string value) => RebuildCards();

        partial void OnSelectedCardChanged(ProgramDeviceCardViewModel? value)
        {
            foreach (var card in _cards)
                card.IsSelected = card == value;
            OnPropertyChanged(nameof(IsDetailVisible));
            RebuildDetail();
        }

        [RelayCommand]
        private void SelectCard(ProgramDeviceCardViewModel card) => SelectedCard = card;

        [RelayCommand]
        private void CloseDetail() => SelectedCard = null;

        [RelayCommand]
        private async Task EditStatus(ProgramItemUiModel item)
        {
            var ep = _all.FirstOrDefault(e => e.EquipmentProductId == item.EquipmentProductId);
            if (ep == null) return;

            var vm = new ProgramStatusDialogViewModel(item, Properties.Resources.ProgramStatusDialogTitle);
            _dialog.ShowDialog(vm);
            if (!vm.IsConfirmed || vm.Result == null) return;
            try
            {
                var updatedAt = await _core.Data.UpdateProductionStatusAsync(ep.EquipmentProductId, vm.Result.NewStatus);
                ep.ProductionStatus = vm.Result.NewStatus;
                ep.UpdateAt = updatedAt;
                ComputeGlobalStats();
                RebuildCards();
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[程式庫管理] 更新程式狀態失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[EditStatus] {ex.Message}");
            }
        }

        public ProgramLibraryViewModel(DashboardCoreServices core, IDialogService dialog)
        {
            _core = core;
            _dialog = dialog;
            TuningTypeFilterOptions = new TuningTypeFilterOption[] { new(null) }
                .Concat(Enum.GetValues<TuningType>().Select(t => new TuningTypeFilterOption(t)))
                .ToArray();
            CardsView = CollectionViewSource.GetDefaultView(_cards);
            SelectedProgramsView = CollectionViewSource.GetDefaultView(_programs);
            _ = LoadAsync();
        }

        public void InjectDataForTest(List<EquipmentProduct> data)
        {
            _all = data;
            ComputeGlobalStats();
            RebuildCards();
        }

        private async Task LoadAsync()
        {
            try
            {
                _all = await _core.Data.GetAllEquipmentProductsAsync();
                ComputeGlobalStats();
                RebuildCards();
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[程式庫管理] 載入可生產清單失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[LoadAsync] {ex.Message}");
            }
        }

        private void ComputeGlobalStats()
        {
            GlobalMachineCount  = _all.Select(ep => ep.EquipmentId).Distinct().Count();
            GlobalProgramCount  = _all.Count;
            GlobalFeasibleCount   = _all.Count(ep => ep.ProductionStatus == TuningType.Feasible);
            GlobalTeachingCount   = _all.Count(ep => ep.ProductionStatus == TuningType.Teaching);
            GlobalOffsetCount     = _all.Count(ep => ep.ProductionStatus == TuningType.Offset);
            GlobalPendingCount    = _all.Count(ep => ep.ProductionStatus == TuningType.Pending);
            GlobalInfeasibleCount = _all.Count(ep => ep.ProductionStatus == TuningType.Infeasible);
        }

        private void RebuildCards()
        {
            _cards.Clear();

            var groups = _all.GroupBy(ep => ep.EquipmentId);
            var filteredAll = new List<EquipmentProduct>();

            foreach (var group in groups)
            {
                var filtered = group.Where(MatchesFilter).ToList();
                if (filtered.Count == 0) continue;

                filteredAll.AddRange(filtered);

                var deviceName = group.First().Equipment?.Name ?? group.Key.ToString();
                var feasible = filtered.Count(ep => ep.ProductionStatus == TuningType.Feasible);
                var light = ProgramDeviceCardViewModel.ComputeLight(filtered);

                _cards.Add(new ProgramDeviceCardViewModel(
                    group.Key, deviceName, feasible, filtered.Count, light));
            }

            // 還原選取狀態（SelectedCard 仍指舊實例，以 EquipmentId 識別）
            if (SelectedCard != null)
                foreach (var card in _cards)
                    card.IsSelected = card.EquipmentId == SelectedCard.EquipmentId;

            CardsView.Refresh();

            // 更新篩選後統計
            FilteredMachineCount  = _cards.Count;
            FilteredProgramCount  = filteredAll.Count;
            FilteredFeasibleCount   = filteredAll.Count(ep => ep.ProductionStatus == TuningType.Feasible);
            FilteredTeachingCount   = filteredAll.Count(ep => ep.ProductionStatus == TuningType.Teaching);
            FilteredOffsetCount     = filteredAll.Count(ep => ep.ProductionStatus == TuningType.Offset);
            FilteredPendingCount    = filteredAll.Count(ep => ep.ProductionStatus == TuningType.Pending);
            FilteredInfeasibleCount = filteredAll.Count(ep => ep.ProductionStatus == TuningType.Infeasible);

            RebuildDetail();
        }

        private void RebuildDetail()
        {
            _programs.Clear();
            if (SelectedCard != null)
            {
                var programs = _all
                    .Where(ep => ep.EquipmentId == SelectedCard.EquipmentId && MatchesFilter(ep))
                    .OrderBy(ep => ep.SeqNo);
                foreach (var ep in programs)
                    _programs.Add(ProgramItemUiModel.FromEntity(ep));
            }
            SelectedProgramsView.Refresh();

            DetailFeasibleCount = _programs.Count(item => item.ProductionStatus == TuningType.Feasible);
            DetailTeachingCount = _programs.Count(item => item.ProductionStatus == TuningType.Teaching);
            DetailOffsetCount   = _programs.Count(item => item.ProductionStatus == TuningType.Offset);
            DetailPendingCount  = _programs.Count(item => item.ProductionStatus == TuningType.Pending);
        }

        private bool MatchesFilter(EquipmentProduct ep)
        {
            var part    = ep.Sop?.Product?.Part;
            var model   = ep.Sop?.Product?.Model;
            var process = ep.Sop?.Process;

            if (TuningTypeFilter.HasValue && ep.ProductionStatus != TuningTypeFilter.Value)
                return false;
            if (!string.IsNullOrEmpty(PartFilter) &&
                !(part?.PartNo ?? string.Empty).Contains(PartFilter, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.IsNullOrEmpty(BrandFilter) &&
                !(part?.Brand ?? string.Empty).Contains(BrandFilter, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.IsNullOrEmpty(ModelFilter) &&
                !(model?.Name ?? string.Empty).Contains(ModelFilter, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.IsNullOrEmpty(ProcessFilter) &&
                !(process?.Name ?? string.Empty).Contains(ProcessFilter, StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }
    }

    public sealed record TuningTypeFilterOption(TuningType? Value);
}
