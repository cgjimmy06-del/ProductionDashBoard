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
        public required string Name { get; set; }
        public required string Description { get; set; }

    }

    public partial class DeviceCardViewModel : ObservableObject
    { 
        public DeviceInfo Info { get; }

        [ObservableProperty]
        public int buttonClickCount = 0;

        public ICommand ToggleLightCommand { get; } 
        public ICommand ButtonClickCommand { get; }

        // 新增一個事件，讓外部可以知道按鈕被點擊
        public event Action? OnButtonClicked;

        public DeviceCardViewModel(DeviceInfo info) 
        { 
            Info = info; 
            ToggleLightCommand = new RelayCommand(ToggleLight); 
            ButtonClickCommand = new RelayCommand(OnButtonClick); 
        } 
        private void ToggleLight() { Info.LightOn = !Info.LightOn; } 
        private void OnButtonClick() 
        { 
            ButtonClickCount++;
            OnButtonClicked?.Invoke(); // 通知外部
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
            var info = new DeviceInfo { Name = Name, Description = Description, LightOn = LightOn }; 
            OnConfirm?.Invoke(info); 
        } 
        private void Cancel() 
        { 
            OnCancel?.Invoke(); 
        } 

    }


}
