using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Properties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    public enum InspectionStatus
    {
        Done = 0,
        Current = 1,
        Fault = 2,
        Idle = -1
    }

    public partial class InspectionService : ObservableObject
    {
        private readonly DeviceInfo _deviceInfo;

        [ObservableProperty]
        private bool routineCycleEnable = false; // 是否開啟巡檢功能

        private int startTime = 10; // 開始巡檢時間
        private int routineTimes = 5; // 總時段
        private int intervalTime = 1; // 巡檢間隔
        private int CurrentRoutine = 0; // 第幾個時段

        public event Action<string, LogLevel>? OnLogEvent;

        [ObservableProperty]
        private bool firstInspectionStatus = false; // 首件狀態
        public ObservableCollection<int> RoutineStatus { get; } = new ObservableCollection<int>(); // 各時段狀態
        public InspectionService(DeviceInfo deviceInfo, int startTime, int routineTimes, int intervalTime)
        {
            this._deviceInfo = deviceInfo;
            setTimesStartTime(startTime, routineTimes, intervalTime);
        }

        public void setTimesStartTime(int startTime, int routineTimes, int intervalTime) // 設定起始時間/次數/間隔
        {
            this.startTime = startTime;
            this.routineTimes = routineTimes;
            this.intervalTime = intervalTime;
            if (DateTime.Now.Hour > this.startTime && DateTime.Now.Hour < this.startTime + this.routineTimes)
                CurrentRoutine = DateTime.Now.Hour - this.startTime - 1;

            RoutineStatus.Clear();
            for (int i = 0; i < this.routineTimes; i++)
                RoutineStatus.Add((int)InspectionStatus.Idle);
        }
        // 首件更新
        public void updateFirstInspection(bool status, string description = "")
        {
            if (FirstInspectionStatus == status) return; // 狀態已更新則不執行

            if (status)
            {
                // SQL

                OnLogEvent?.Invoke($"{_deviceInfo.Name}-完成首件!", LogLevel.Success);
            }
            else
                OnLogEvent?.Invoke($"{_deviceInfo.Name}-產品更新，重新執行首件!", LogLevel.Warning);

            FirstInspectionStatus = status;
        }
        //巡檢更新
        public void updateRoutineStatus(int currentStatus, string description = "")
        {
            if (!RoutineCycleEnable) return;
            if (RoutineStatus[CurrentRoutine] != (int)InspectionStatus.Current) return; // 狀態已更新則不執行

            // SQL 0則OK；2則NG且須加上描述

            RoutineStatus[CurrentRoutine] = currentStatus;

            string okngresult = currentStatus == (int)InspectionStatus.Done ? "OK" : "NG";
            LogLevel levelresult = currentStatus == (int)InspectionStatus.Done ? LogLevel.Success : LogLevel.Error;
            OnLogEvent?.Invoke($"{_deviceInfo.Name}-{startTime + CurrentRoutine * intervalTime}點巡檢完成-" +
                $"檢驗結果:{okngresult}-{description}", levelresult);
        }
        // 巡檢區段檢查 (by time)
        public void checkRoutineTime()
        {
            if (!RoutineCycleEnable) return;
            int currenthour = DateTime.Now.Hour; //(int)((float)DateTime.Now.Second / 3);//

            // 巡檢提示燈號更新 (需小於開始時間)
            if (currenthour == startTime && RoutineStatus[routineTimes - 1] != (int)InspectionStatus.Idle)
            {
                for (int i = 0; i < this.routineTimes; i++)
                    RoutineStatus[i] = (int)InspectionStatus.Idle;
                OnLogEvent?.Invoke($"{_deviceInfo.Name}-巡檢提示燈號更新!", LogLevel.Processing);
            }

            if (currenthour < startTime) return; // 未到巡檢時段
            if (currenthour >= startTime + routineTimes * intervalTime && CurrentRoutine == 0) return; // 超過巡檢時段

            if (currenthour == startTime && RoutineStatus[0] == (int)InspectionStatus.Idle) // 開啟第一區巡檢
            {
                RoutineStatus[0] = (int)InspectionStatus.Current;
                OnLogEvent?.Invoke($"{_deviceInfo.Name}-開始巡檢!", LogLevel.Processing);
            }
            if (currenthour - (startTime + CurrentRoutine * intervalTime) >= intervalTime) // 經過下一區則 1. 是否未巡檢 2. 移至下一區
            {
                updateRoutineStatus((int)InspectionStatus.Fault, "逾時未巡檢");

                CurrentRoutine++;
                if (CurrentRoutine >= routineTimes) // 已完成巡檢
                { 
                    CurrentRoutine = 0;
                    OnLogEvent?.Invoke($"{_deviceInfo.Name}-巡檢時段結束", LogLevel.Success);
                    return; 
                } 
                RoutineStatus[CurrentRoutine] = (int)InspectionStatus.Current;
            }
        }
    }

    public partial class DeviceCardViewModel : ObservableObject
    {
        private DispatcherTimer checkTimer;
        public DeviceInfo Info { get; }
        private readonly LogService _log;

        [ObservableProperty]
        private UserInfo? currentUser;
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

            InspectionStatuses = new InspectionService(Info, 9, 4, 2);
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
            Info.Status++;
            if (Info.Status > 2) Info.Status = -1;

            _log.AddLog($"設備 {Info.Name} 物料已更換", LogLevel.Success);
        } 
        private void FirstArticleInspection() 
        {
            InspectionStatuses.updateFirstInspection(true);
        } 
        private void RoutineInspection()
        {
            InspectionStatuses.updateRoutineStatus((int)InspectionStatus.Done, "巡檢完成");
        }
        private void OperationChange()
        {


            _log.AddLog($"設備 {Info.Name} 設備調適狀態更新:", LogLevel.Processing);
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
