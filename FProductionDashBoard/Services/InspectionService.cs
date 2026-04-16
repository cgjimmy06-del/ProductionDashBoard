using CommunityToolkit.Mvvm.ComponentModel;
using FProductionDashBoard.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Services
{
    public enum InspectionStatus
    {
        Done = 0,
        Current = 1,
        Fault = 2,
        Idle = -1
    }
    public partial class InspectionService : ObservableObject
    {
        private readonly DeviceCardViewModel currentDevice;

        [ObservableProperty]
        private bool routineCycleEnable = false; // 是否開啟巡檢功能

        public int StartTime = 10; // 開始巡檢時間
        public int routineTimes = 5; // 總時段
        public int intervalTime = 1; // 巡檢間隔
        public int CurrentRoutine = 0; // 第幾個時段

        public event Action<string, LogLevel>? OnLogEvent; // 多此一舉，當練習用
        //public event Action<string>? OnErrorLogEvent;

        [ObservableProperty]
        private bool firstInspectionStatus = false; // 首件狀態
        public ObservableCollection<int> RoutineStatus { get; } = new ObservableCollection<int>(); // 各時段狀態
        public InspectionService(DeviceCardViewModel deviceInfo, int startTime, int routineTimes, int intervalTime)
        {
            currentDevice = deviceInfo;
            setTimesStartTime(startTime, routineTimes, intervalTime);
        }

        public void setTimesStartTime(int startTime, int routineTimes, int intervalTime) // 設定起始時間/次數/間隔
        {
            this.StartTime = startTime;
            this.routineTimes = routineTimes;
            this.intervalTime = intervalTime;
            if (DateTime.Now.Hour > this.StartTime && DateTime.Now.Hour < this.StartTime + this.routineTimes)
                CurrentRoutine = DateTime.Now.Hour - this.StartTime - 1;

            RoutineStatus.Clear();
            for (int i = 0; i < this.routineTimes; i++)
                RoutineStatus.Add((int)InspectionStatus.Idle);
        }
        // 首件更新 (未加入SQL)
        public void updateFirstInspection(bool status, string? errorcode = "", string? description = "")
        {
            if (status)
            {
                if (FirstInspectionStatus) return;

                // SQL 機台 人員 產品 有無異常 描述 日 時 完整時間
                // _sql
                OnLogEvent?.Invoke($"{currentDevice.Info.Name}-{Properties.Resources.InsFirstSuccess}", LogLevel.Success);
            }
            else
            {
                if (FirstInspectionStatus)
                    OnLogEvent?.Invoke($"{currentDevice.Info.Name}-{Properties.Resources.InsFirstProductUpdate}", LogLevel.Warning);
                else
                {
                    // SQL 機台 人員 產品 有無異常 描述 日 時 完整時間
                    // _sql
                    OnLogEvent?.Invoke($"{currentDevice.Info.Name}:{Properties.Resources.InsFirstAbnormal} - " +
                        $"{errorcode}:{description}", LogLevel.Error);
                }
            }
            FirstInspectionStatus = status;
        }
        //巡檢更新 (未加入SQL)
        public void updateRoutineStatus(int currentStatus, string? errorcode = "", string? description = "")
        {
            if (!RoutineCycleEnable) return;
            if (RoutineStatus[CurrentRoutine] != (int)InspectionStatus.Current) return; // 狀態已更新則不執行

            // SQL 機台 人員 時段 有無異常 描述 日 時 完整時間
            // 0則OK；2則NG且須加上描述
            // _sql

            RoutineStatus[CurrentRoutine] = currentStatus;

            string okngresult = currentStatus == (int)InspectionStatus.Done ? "OK" : "NG";
            LogLevel levelresult = currentStatus == (int)InspectionStatus.Done ? LogLevel.Success : LogLevel.Error;
            OnLogEvent?.Invoke($"{currentDevice.Info.Name}-[{StartTime + CurrentRoutine * intervalTime}:00]" +
                $"{Properties.Resources.InsRoutineSuccess}:{okngresult}-{errorcode}:{description}", levelresult);
        }
        // 巡檢區段檢查 (by time)
        public void checkRoutineTime()
        {
            if (!RoutineCycleEnable) return;
            int currenthour = DateTime.Now.Hour; //(int)((float)DateTime.Now.Second / 3);//

            // 巡檢提示燈號更新 (需小於開始時間)
            if (currenthour == StartTime && RoutineStatus[routineTimes - 1] != (int)InspectionStatus.Idle)
            {
                for (int i = 0; i < this.routineTimes; i++)
                    RoutineStatus[i] = (int)InspectionStatus.Idle;
                OnLogEvent?.Invoke($"{currentDevice.Info.Name}-{Properties.Resources.InsRoutineUpdateLight}", LogLevel.Processing);
            }

            if (currenthour < StartTime) return; // 未到巡檢時段
            if (currenthour >= StartTime + routineTimes * intervalTime && CurrentRoutine == 0) return; // 超過巡檢時段

            if (currenthour == StartTime && RoutineStatus[0] == (int)InspectionStatus.Idle) // 開啟第一區巡檢
            {
                RoutineStatus[0] = (int)InspectionStatus.Current;
                OnLogEvent?.Invoke($"{currentDevice.Info.Name}-{Properties.Resources.InsRoutineStart}", LogLevel.Processing);
            }
            if (currenthour - (StartTime + CurrentRoutine * intervalTime) >= intervalTime) // 經過下一區則 1. 是否未巡檢 2. 移至下一區
            {
                updateRoutineStatus((int)InspectionStatus.Fault, null, Properties.Resources.InsRoutineOverdue);

                CurrentRoutine++;
                if (CurrentRoutine >= routineTimes) // 已完成巡檢
                {
                    CurrentRoutine = 0;
                    OnLogEvent?.Invoke($"{currentDevice.Info.Name}-{Properties.Resources.InsRoutineEnd}", LogLevel.Success);
                    return;
                }
                RoutineStatus[CurrentRoutine] = (int)InspectionStatus.Current;
            }
        }
    }
}
