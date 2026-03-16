using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FProductionDashBoard
{
    public partial class DeviceCardViewModel : ObservableObject
    { 
        public DeviceInfo Info { get; }
        private readonly LogService _log;

        [ObservableProperty]
        private int buttonClickCount = 0;

        public ICommand MaterialsChangeCommand { get; } 
        public ICommand FirstInspectionCommand { get; }
        public ICommand RoutineInspectionCommand { get; }
        public ICommand OperationCommand { get; }

        // 新增一個事件，讓外部可以知道按鈕被點擊
        public event Action? OnButtonClicked;

        public DeviceCardViewModel(DeviceInfo info, LogService log) 
        { 
            Info = info;
            _log = log;

            MaterialsChangeCommand = new RelayCommand(MaterialsChange);
            FirstInspectionCommand = new RelayCommand(FirstArticleInspection);
            RoutineInspectionCommand = new RelayCommand(RoutineInspection);
            OperationCommand = new RelayCommand(OperationChange);
        } 
        private void MaterialsChange() 
        { 
            ButtonClickCount++;
            OnButtonClicked?.Invoke(); // 通知外部

            _log.AddLog($"設備 {Info.Name} 物料已更換");
        } 
        private void FirstArticleInspection() 
        {
            Info.Status++;
            if (Info.Status > 2) Info.Status = -1;

            _log.AddLog($"設備 {Info.Name} 首件已確認");
        } 
        private void RoutineInspection()
        {


            _log.AddLog($"設備 {Info.Name} 例行巡檢已完成，巡檢時段:");
        }
        private void OperationChange()
        {


            _log.AddLog($"設備 {Info.Name} 設備調適狀態更新:");
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
