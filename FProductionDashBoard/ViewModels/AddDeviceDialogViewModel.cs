using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace FProductionDashBoard.ViewModels
{
    public class DeviceCardsResult
    {
        public required List<Models.DeviceInfo> Selections;
    }

    public partial class AddDeivceDialogViewModel : DialogBaseViewModel<DeviceCardsResult>
    {
        private List<DeviceInfo> devicesList = new(); // 設備清單 (資料表來源)
        private List<DeviceCardViewModel> existedDevicesList = new(); // 介面已存在設備清單
        private string defaultDevicesFile; // 預設設備檔案

        [ObservableProperty]
        private string? deviceId; // 設備編號
        [ObservableProperty]
        private string? name; // 設備名稱
        [ObservableProperty]
        private string? ip; // 設備IP
        [ObservableProperty]
        private ObservableCollection<DeviceInfo> filteredDevices = new(); // 篩選結果
        [ObservableProperty]
        private ObservableCollection<DeviceInfo> selectedDevices = new(); // 選擇結果
        [ObservableProperty]
        private ObservableCollection<DeviceInfo> selectedFromFiltered = new(); // 篩選多選 (反色項目)
        [ObservableProperty]
        private ObservableCollection<DeviceInfo> selectedFromSelected = new(); // 選擇多選 (反色項目)

        private System.Timers.Timer debounceTimer;

        public ICommand AddToSelectedCommand { get; }
        public ICommand RemoveFromSelectedCommand { get; }
        public ICommand DownloadDeivcesCommand { get; }
        public ICommand UploadDeivcesCommand { get; }

        public AddDeivceDialogViewModel(string dialogstring, string defaultsfile,
            List<DeviceInfo> deviceslist, List<DeviceCardViewModel> existedDevicesList) : base(dialogstring)
        {
            defaultDevicesFile = defaultsfile;
            this.existedDevicesList = existedDevicesList;
            this.devicesList = deviceslist;

            // 初始化 debounce timer
            debounceTimer = new System.Timers.Timer(500); // 500ms 延遲
            debounceTimer.AutoReset = false; // 只觸發一次
            debounceTimer.Elapsed += (s, e) => ApplyFilter();

            AddToSelectedCommand = new RelayCommand(() => AddToSelected());
            RemoveFromSelectedCommand = new RelayCommand(() => RemoveFromSelected());
            DownloadDeivcesCommand = new RelayCommand(() => DownloadDevices());
            UploadDeivcesCommand = new RelayCommand(() => UploadDevices());

            ConfirmCommand = new RelayCommand(() => OnConfirm());
            CancelCommand = new RelayCommand(() => OnCancel());

            ApplyFilter();
        }
        partial void OnDeviceIdChanged(string? value)
        { debounceTimer.Stop(); debounceTimer.Start(); }
        partial void OnNameChanged(string? value)
        { debounceTimer.Stop(); debounceTimer.Start(); }
        partial void OnIpChanged(string? value)
        { debounceTimer.Stop(); debounceTimer.Start(); }
        // 篩選器集合
        public void ApplyFilter()
        {
            var query = devicesList.AsEnumerable();

            if (existedDevicesList.Any())
            {
                var existedIds = existedDevicesList.Select(s => s.Info.DeviceID).ToHashSet();
                query = query.Where(d => !existedIds.Contains(d.DeviceID));
            }

            if (!string.IsNullOrEmpty(DeviceId))
                query = query.Where(d => d.DeviceID.Contains(DeviceId));

            if (!string.IsNullOrEmpty(Name)) // minPrice.HasValue d.Name >= minPrice.Value
                query = query.Where(d => d.Name.Contains(Name));

            if (!string.IsNullOrEmpty(Ip))
                query = query.Where(d => d.IP.Contains(Ip));

            if (SelectedDevices.Any())
            {
                var selectedIds = SelectedDevices.Select(s => s.DeviceID).ToHashSet();
                query = query.Where(d => !selectedIds.Contains(d.DeviceID));
            }

            FilteredDevices = new ObservableCollection<DeviceInfo>(query.ToList());
        }
        // 增減機台事件
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
        // 上下載設備清單 (可供外部快速上下載按鈕)
        public void DownloadDevices()
        {
            DataStorageService.Save(SelectedDevices, defaultDevicesFile);
            DialogErrorString = Properties.Resources.ComStrDownloaded;
        }
        public void UploadDevices()
        {
            SelectedDevices = DataStorageService.Load<ObservableCollection<DeviceInfo>>(defaultDevicesFile);
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
