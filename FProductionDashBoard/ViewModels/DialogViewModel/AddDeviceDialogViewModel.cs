using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using FProductionDashBoard.UiModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace FProductionDashBoard.ViewModels
{
    public class DeviceCardsResult
    {
        public List<DeviceInfo> Selections { get; set; } = new();
    }

    public partial class AddDeviceDialogViewModel : DialogBaseViewModel<DeviceCardsResult>
    {
        private readonly List<DeviceInfo> _allDevices;
        private readonly List<DeviceCardViewModel> existedDevicesList;
        private readonly string defaultDevicesFile;

        [ObservableProperty] private string keywordFilter = "";
        [ObservableProperty] private EquipmentType? typeFilter;
        [ObservableProperty] private string? buildingFilter = "";

        [ObservableProperty] private ObservableCollection<DeviceInfo> filteredDevices = new();
        [ObservableProperty] private ObservableCollection<DeviceInfo> selectedDevices = new();
        [ObservableProperty] private ObservableCollection<DeviceInfo> selectedFromFiltered = new();
        [ObservableProperty] private ObservableCollection<DeviceInfo> selectedFromSelected = new();

        public IReadOnlyList<EquipmentTypeFilterOption> TypeFilterOptions { get; }
        public IReadOnlyList<string> BuildingOptions { get; }

        public ICommand AddToSelectedCommand { get; }
        public ICommand RemoveFromSelectedCommand { get; }
        public ICommand DownloadDevicesCommand { get; }
        public ICommand UploadDevicesCommand { get; }

        public AddDeviceDialogViewModel(string dialogstring, string defaultsfile,
            List<DeviceInfo> deviceslist, List<DeviceCardViewModel> existedDevicesList,
            IEnumerable<EquipmentTypeFilterOption> typeOptions) : base(dialogstring)
        {
            defaultDevicesFile = defaultsfile;
            this.existedDevicesList = existedDevicesList;
            _allDevices = deviceslist;

            TypeFilterOptions = typeOptions.ToArray();

            var buildings = _allDevices
                .Select(d => d.Building)
                .Where(b => !string.IsNullOrEmpty(b))
                .Select(b => b!)
                .Distinct()
                .Order();
            BuildingOptions = new[] { "" }.Concat(buildings).ToArray();

            AddToSelectedCommand = new RelayCommand(() => AddToSelected());
            RemoveFromSelectedCommand = new RelayCommand(() => RemoveFromSelected());
            DownloadDevicesCommand = new RelayCommand(() => DownloadDevices());
            UploadDevicesCommand = new RelayCommand(() => UploadDevices());

            ConfirmCommand = new RelayCommand(() => OnConfirm());
            CancelCommand = new RelayCommand(() => OnCancel());

            ApplyFilter();
        }

        partial void OnKeywordFilterChanged(string value) => ApplyFilter();
        partial void OnTypeFilterChanged(EquipmentType? value) => ApplyFilter();
        partial void OnBuildingFilterChanged(string? value) => ApplyFilter();

        public void ApplyFilter()
        {
            var excludedIds = existedDevicesList.Select(s => s.Info.DeviceID)
                .Concat(SelectedDevices.Select(s => s.DeviceID))
                .ToHashSet();

            FilteredDevices.Clear();
            foreach (var device in _allDevices)
            {
                if (excludedIds.Contains(device.DeviceID)) continue;
                if (TypeFilter != null && device.TypeId != TypeFilter.TypeId) continue;
                if (!string.IsNullOrEmpty(BuildingFilter) && device.Building != BuildingFilter) continue;
                if (!string.IsNullOrEmpty(KeywordFilter))
                {
                    var hay = string.Join(" ", new[] { device.DeviceID, device.Name, device.IP }
                        .Where(x => !string.IsNullOrEmpty(x)));
                    if (hay.IndexOf(KeywordFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                }
                FilteredDevices.Add(device);
            }
        }

        public void AddToSelected()
        {
            foreach (var device in SelectedFromFiltered.ToList())
                if (!SelectedDevices.Contains(device))
                    SelectedDevices.Add(device);
            ApplyFilter();
        }

        public void RemoveFromSelected()
        {
            foreach (var device in SelectedFromSelected.ToList())
                SelectedDevices.Remove(device);
            ApplyFilter();
        }

        public void DownloadDevices()
        {
            try
            {
                Services.JsonDataService.Save(SelectedDevices, defaultDevicesFile);
                DialogErrorString = Properties.Resources.ComStrDownloaded;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DownloadDevices] {ex.Message}");
                DialogErrorString = "設備清單儲存失敗";
            }
        }

        public void UploadDevices()
        {
            SelectedDevices = Services.JsonDataService.Load<ObservableCollection<DeviceInfo>>(defaultDevicesFile);
            ApplyFilter();
        }

        protected override void OnConfirm()
        {
            if (!SelectedDevices.Any())
            { DialogErrorString = Properties.Resources.AddDeviceNonSelectionError; return; }

            Result = new DeviceCardsResult() { Selections = SelectedDevices.ToList() };

            base.OnConfirm();
        }
    }
}
