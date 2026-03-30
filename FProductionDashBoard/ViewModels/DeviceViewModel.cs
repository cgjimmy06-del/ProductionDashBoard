using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Properties;
using FProductionDashBoard.UserControls;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace FProductionDashBoard
{
    public enum UserAction
    {
        Producing = 0,
        Tuning = 1,
        Maintaining = 2
    }
    
    public partial class DeviceCardViewModel : ObservableObject
    {
        private DispatcherTimer checkTimer;
        public DeviceInfo Info { get; }
        private readonly LogService _log;

        [ObservableProperty]
        private UserInfo currentUser = new() { ID = "none", Name = "none" };
        [ObservableProperty]
        private ProductInfo currentProduct = new() { ModelCode = "Unknown", TypeCode = "123" };

        public InspectionService InspectionStatuses { get; }
        [ObservableProperty]
        private int currentAction = (int)UserAction.Producing;

        // 介面邏輯
        public ICommand MaterialsChangeCommand { get; }
        public ICommand FirstInspectionCommand { get; }
        public ICommand RoutineInspectionCommand { get; }
        public ICommand OperationCommand { get; }

        public DeviceCardViewModel(DeviceInfo info, UserInfo currentuser, LogService log)
        {
            Info = info;
            _log = log;
            CurrentUser = currentuser;

            InspectionStatuses = new InspectionService(this, 9, 4, 2);
            InspectionStatuses.OnLogEvent += _log.AddLog;

            MaterialsChangeCommand = new RelayCommand(MaterialsChange);
            FirstInspectionCommand = new RelayCommand(FirstArticleInspection);
            RoutineInspectionCommand = new RelayCommand(RoutineInspection);
            OperationCommand = new RelayCommand(OperationChange);

            // 巡檢用計時
            checkTimer = new DispatcherTimer();
            checkTimer.Interval = TimeSpan.FromSeconds(1);
            checkTimer.Tick += (s, e) => { InspectionStatuses.checkRoutineTime(); };
            checkTimer.Start();
        }
        private void MaterialsChange()
        {
            var vm = new MaterialDialogViewModel("物料更換", this);
            var uc = new MaterialsDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                var result = vm.Result ?? new MaterialResult() { Selections = Array.Empty<MaterialInfo>() };
                // SQL
                _log.AddLog($"設備 {Info.Name} 物料更換數量: {result.Selections.Length}", LogLevel.Success);
            }
        }
        private void FirstArticleInspection()
        {
            var vm = new InspectionDialogViewModel("首件", this);
            var uc = new InspectionDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                var result = vm.Result ?? new() { IsNormal = false };
                InspectionStatuses.updateFirstInspection(result.IsNormal, result.Description); // SQL
            }
        }
        private void RoutineInspection()
        {
            var vm = new InspectionDialogViewModel("巡檢", this);
            var uc = new InspectionDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                var result = vm.Result ?? new InspectionResult() { IsNormal = false };
                int statusresult = result.IsNormal ? 0 : 2;
                InspectionStatuses.updateRoutineStatus(statusresult, result.Description); // SQL
            }
        }
        private void OperationChange()
        {
            //var vm = new DialogBaseViewModel<string>("調試");
            //var window = new DialogWindow(vm);
            //window.ShowDialog();

            //if (vm.IsConfirmed)
            //{
            //    var result = vm.Result;
            //}
            Info.Status++;
            if (Info.Status > 2) Info.Status = -1;
            _log.AddLog($"設備 {Info.Name} 設備調試狀態更新:", LogLevel.Processing);
        }


    }

    public partial class AddDeviceViewModel : ObservableObject
    {
        [ObservableProperty]
        private string name = "Default";
        [ObservableProperty]
        private string description = string.Empty;

        public IRelayCommand ConfirmCommand { get; }
        public IRelayCommand CancelCommand { get; }

        public event Action<DeviceInfo>? OnConfirm;
        public event Action? OnCancel;
        public AddDeviceViewModel()
        {
            ConfirmCommand = new RelayCommand(Confirm);
            CancelCommand = new RelayCommand(Cancel);
        }
        private void Confirm()
        {
            var info = new DeviceInfo { DeviceID = "", IP = "", Name = Name, Description = Description };
            OnConfirm?.Invoke(info);
        }
        private void Cancel()
        {
            OnCancel?.Invoke();
        }

    }


}
