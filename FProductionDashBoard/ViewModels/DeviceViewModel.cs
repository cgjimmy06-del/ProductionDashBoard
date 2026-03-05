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
    public partial class DeviceInfo : ObservableObject
    { 
        [ObservableProperty]
        private bool lightOn;

        [ObservableProperty]
        private int status;

        public required string DeviceID { get; set; }
        public required string Name { get; set; }
        public required string IP { get; set; }
        public string Description { get; set; } = "";

    }

    public partial class DeviceCardViewModel : ObservableObject
    { 
        public DeviceInfo Info { get; }
        private readonly LogViewModel _log;

        [ObservableProperty]
        private int buttonClickCount = 0;

        public ICommand MaterialsChangeCommand { get; } 
        public ICommand FirstInspectionCommand { get; }
        public ICommand RoutineInspectionCommand { get; }
        public ICommand OperationCommand { get; }

        // 新增一個事件，讓外部可以知道按鈕被點擊
        public event Action? OnButtonClicked;

        public DeviceCardViewModel(DeviceInfo info, LogViewModel log) 
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

            _log.AddLog($"設備 {Info.Name} 物料已更換", LogLevel.Warning);
        } 
        private void FirstArticleInspection() 
        {
            Info.LightOn = !Info.LightOn;

            _log.AddLog($"設備 {Info.Name} 首件已確認", LogLevel.Warning);
        } 
        private void RoutineInspection()
        {


            _log.AddLog($"設備 {Info.Name} 例行巡檢已完成，巡檢時段:", LogLevel.Warning);
        }
        private void OperationChange()
        {


            _log.AddLog($"設備 {Info.Name} 設備調適狀態更新:", LogLevel.Warning);
        }


    }

    public partial class AddDeviceViewModel : ObservableObject
    {
        [ObservableProperty] 
        private string name = string.Empty; 
        [ObservableProperty] 
        private string description = string.Empty; 
        [ObservableProperty] 
        private bool lightOn = false;
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
            var info = new DeviceInfo { DeviceID = "", IP = "", Name = Name, Description = Description, LightOn = LightOn }; 
            OnConfirm?.Invoke(info); 
        } 
        private void Cancel() 
        { 
            OnCancel?.Invoke(); 
        } 

    }


}
