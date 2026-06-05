using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FProductionDashBoard.ViewModels
{
    public partial class ProgramLibraryViewModel : ObservableObject
    {
        private readonly DashboardCoreServices _core;
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
        [ObservableProperty] private string partFilter = string.Empty;
        [ObservableProperty] private string brandFilter = string.Empty;
        [ObservableProperty] private string modelFilter = string.Empty;
        [ObservableProperty] private string processFilter = string.Empty;

        public ObservableCollection<ProgramDeviceCardViewModel> Cards { get; } = new();

        [ObservableProperty] private ProgramDeviceCardViewModel? selectedCard;
        public bool IsDetailVisible => SelectedCard != null;

        partial void OnPartFilterChanged(string value) => RebuildCards();
        partial void OnBrandFilterChanged(string value) => RebuildCards();
        partial void OnModelFilterChanged(string value) => RebuildCards();
        partial void OnProcessFilterChanged(string value) => RebuildCards();

        partial void OnSelectedCardChanged(ProgramDeviceCardViewModel? value)
            => OnPropertyChanged(nameof(IsDetailVisible));

        [RelayCommand]
        private void SelectCard(ProgramDeviceCardViewModel card) => SelectedCard = card;

        public ProgramLibraryViewModel(DashboardCoreServices core)
        {
            _core = core;
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
            Cards.Clear();

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

                Cards.Add(new ProgramDeviceCardViewModel(
                    group.Key, deviceName, feasible, filtered.Count, light));
            }

            // 更新篩選後統計
            FilteredMachineCount  = Cards.Count;
            FilteredProgramCount  = filteredAll.Count;
            FilteredFeasibleCount   = filteredAll.Count(ep => ep.ProductionStatus == TuningType.Feasible);
            FilteredTeachingCount   = filteredAll.Count(ep => ep.ProductionStatus == TuningType.Teaching);
            FilteredOffsetCount     = filteredAll.Count(ep => ep.ProductionStatus == TuningType.Offset);
            FilteredPendingCount    = filteredAll.Count(ep => ep.ProductionStatus == TuningType.Pending);
            FilteredInfeasibleCount = filteredAll.Count(ep => ep.ProductionStatus == TuningType.Infeasible);
        }

        private bool MatchesFilter(EquipmentProduct ep)
        {
            var part    = ep.Sop?.Product?.Part;
            var model   = ep.Sop?.Product?.Model;
            var process = ep.Sop?.Process;

            if (!string.IsNullOrEmpty(PartFilter) &&
                (part?.PartNo?.IndexOf(PartFilter, StringComparison.OrdinalIgnoreCase) < 0))
                return false;
            if (!string.IsNullOrEmpty(BrandFilter) &&
                (part?.Brand?.IndexOf(BrandFilter, StringComparison.OrdinalIgnoreCase) < 0))
                return false;
            if (!string.IsNullOrEmpty(ModelFilter) &&
                (model?.Name?.IndexOf(ModelFilter, StringComparison.OrdinalIgnoreCase) < 0))
                return false;
            if (!string.IsNullOrEmpty(ProcessFilter) &&
                (process?.Name?.IndexOf(ProcessFilter, StringComparison.OrdinalIgnoreCase) < 0))
                return false;
            return true;
        }
    }
}
