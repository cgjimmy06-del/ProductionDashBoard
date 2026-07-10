using System;
using System.Collections.Generic;
using System.Linq;
using FProductionDashBoard.Dtos;

namespace FProductionDashBoard.Services
{
    /// <summary>
    /// 每個資料集一份欄位目錄：宣告欄位、型別、可否當統計/篩選/快捷/排序與枚舉值（含 rank/狀態色）。
    /// 衍生欄位的實際取值在 ChartViewModel 的 row model 計算，此處只負責宣告。
    /// </summary>
    public static class FieldCatalog
    {
        #region 設備視角欄位 FieldId 常數

        public const string EquipmentName        = "EquipmentName";
        public const string ProductionStatus     = "ProductionStatus";
        public const string LoadLevel            = "LoadLevel";
        public const string TuningStatus         = "TuningStatus";
        public const string ActiveOrderCount     = "ActiveOrderCount";
        public const string TotalPendingQty      = "TotalPendingQty";
        public const string FeasibleProgramCount = "FeasibleProgramCount";
        public const string TotalProgramCount    = "TotalProgramCount";
        public const string CurrentProduct       = "CurrentProduct";
        public const string ProgressCompleted    = "ProgressCompleted";
        public const string ProgressTarget       = "ProgressTarget";

        #endregion

        #region 排程視角欄位 FieldId 常數

        public const string ScheduleId           = "ScheduleId";
        public const string Brand                = "Brand";
        public const string PartNo               = "PartNo";
        public const string Model                = "Model";
        public const string Process              = "Process";
        public const string Quantity             = "Quantity";
        public const string ActualQuantity       = "ActualQuantity";
        public const string ScheduleStatus       = "ScheduleStatus";
        public const string SopType              = "SopType";
        public const string ReceivedAt           = "ReceivedAt";
        public const string WaitingDays          = "WaitingDays";
        public const string ActiveEquipmentCount = "ActiveEquipmentCount";

        #endregion

        #region 設備視角枚舉值常數（衍生欄位）

        public const string ValInProduction = "InProduction";
        public const string ValPendingProd  = "PendingProduction";
        public const string ValIdle         = "Idle";
        public const string ValUnavailable  = "Unavailable";

        public const string ValLoadNone = "None";
        public const string ValLoadLow  = "Low";
        public const string ValLoadMid  = "Mid";
        public const string ValLoadHigh = "High";

        public const string ValTuningNone   = "None";
        public const string ValTuningActive = "Tuning";

        #endregion

        /// <summary>設備視角：一張卡＝一台設備（Equipment＋OrderProduction＋ProgramTuningRecord＋EquipmentProduct 衍生）</summary>
        public static readonly IReadOnlyList<ChartFieldDescriptor> Equipment = new List<ChartFieldDescriptor>
        {
            new(EquipmentName, "ChartFieldEquipmentName", ChartFieldType.Text,
                canFilter: true, canSort: true),

            new(ProductionStatus, "ChartFieldProductionStatus", ChartFieldType.Enum,
                canStat: true, canFilter: true, canQuickFilter: true, canSort: true,
                values: new List<ChartFieldValue>
                {
                    // 色碼比照 ScheduleEquipmentCard 慣例：生產中=Primary、待生產=Warning、閒置(可生產無單)=Success、無可生產=Idle
                    new(ValInProduction, "ChartValInProduction", 1, "PrimaryBrush"),
                    new(ValPendingProd,  "ChartValPendingProd",  2, "WarningBrush"),
                    new(ValIdle,         "ChartValIdle",         3, "SuccessBrush"),
                    new(ValUnavailable,  "ChartValUnavailable",  4, "IdleBrush"),
                }),

            new(LoadLevel, "ChartFieldLoadLevel", ChartFieldType.Enum,
                canStat: true, canFilter: true, canQuickFilter: true, canSort: true,
                values: new List<ChartFieldValue>
                {
                    new(ValLoadNone, "ChartValLoadNone", 0),
                    new(ValLoadLow,  "ChartValLoadLow",  1, "ChipLoadLowBrush"),
                    new(ValLoadMid,  "ChartValLoadMid",  2, "ChipLoadMidBrush"),
                    new(ValLoadHigh, "ChartValLoadHigh", 3, "ChipLoadHighBrush"),
                }),

            new(TuningStatus, "ChartFieldTuningStatus", ChartFieldType.Enum,
                canStat: true, canFilter: true, canQuickFilter: true, canSort: true,
                values: new List<ChartFieldValue>
                {
                    new(ValTuningNone,   "ChartValTuningNone",   0),
                    new(ValTuningActive, "ChartValTuningActive", 1, "WarningBrush"),
                }),

            new(ActiveOrderCount, "ChartFieldActiveOrderCount", ChartFieldType.Number,
                canStat: true, canSort: true),
            new(TotalPendingQty, "ChartFieldTotalPendingQty", ChartFieldType.Number,
                canStat: true, canSort: true),
            new(FeasibleProgramCount, "ChartFieldFeasibleProgramCount", ChartFieldType.Number,
                canSort: true),
            new(TotalProgramCount, "ChartFieldTotalProgramCount", ChartFieldType.Number,
                canSort: true),
            new(CurrentProduct, "ChartFieldCurrentProduct", ChartFieldType.Text,
                canFilter: true),
            new(ProgressCompleted, "ChartFieldProgressCompleted", ChartFieldType.Number,
                canStat: true),
            new(ProgressTarget, "ChartFieldProgressTarget", ChartFieldType.Number,
                canStat: true),
        };

        /// <summary>排程視角：一列＝一張排單（Schedule＋OrderProduction 衍生）</summary>
        public static readonly IReadOnlyList<ChartFieldDescriptor> Schedule = new List<ChartFieldDescriptor>
        {
            new(ScheduleId, "ChartFieldScheduleId", ChartFieldType.Number,
                canSort: true),
            new(Brand, "ChartFieldBrand", ChartFieldType.Text,
                canFilter: true),
            new(PartNo, "ChartFieldPartNo", ChartFieldType.Text,
                canFilter: true, canSort: true),
            new(Model, "ChartFieldModel", ChartFieldType.Text,
                canFilter: true),
            new(Process, "ChartFieldProcess", ChartFieldType.Text,
                canFilter: true),
            new(Quantity, "ChartFieldQuantity", ChartFieldType.Number,
                canStat: true, canSort: true),
            new(ActualQuantity, "ChartFieldActualQuantity", ChartFieldType.Number,
                canStat: true, canSort: true),

            new(ScheduleStatus, "ChartFieldStatus", ChartFieldType.Enum,
                canStat: true, canFilter: true, canQuickFilter: true, canSort: true,
                values: new List<ChartFieldValue>
                {
                    // rank 依 Models ScheduleStatus 業務順序；色碼比照統計卡慣例
                    new("Pending",   "ChartValSchPending",   1, "WarningBrush"),
                    new("Scheduled", "ChartValSchScheduled", 2, "PrimaryBrush"),
                    new("Completed", "ChartValSchCompleted", 3, "SuccessBrush"),
                    new("Released",  "ChartValSchReleased",  4, "SecondaryBrush"),
                    new("Cancelled", "ChartValSchCancelled", 5, "ErrorBrush"),
                }),

            new(SopType, "ChartFieldSopType", ChartFieldType.Enum,
                canFilter: true, canQuickFilter: true, canSort: true,
                values: new List<ChartFieldValue>
                {
                    new("Develop", "ChartValSopDevelop", 1, "AlertBrush"),
                    new("Open",    "ChartValSopOpen",    2),
                    new("Close",   "ChartValSopClose",   3),
                    new("ACME",    "ChartValSopACME",    4),
                    new("TC",      "ChartValSopTC",      5),
                    new("Other",   "ChartValSopOther",   6),
                }),

            new(ReceivedAt, "ChartFieldReceivedAt", ChartFieldType.Date,
                canFilter: true, canSort: true),
            new(WaitingDays, "ChartFieldWaitingDays", ChartFieldType.Number,
                canSort: true),
            new(ActiveEquipmentCount, "ChartFieldActiveEquipmentCount", ChartFieldType.Number,
                canSort: true),
        };

        public static IReadOnlyList<ChartFieldDescriptor> For(ChartDataSet dataSet) => dataSet switch
        {
            ChartDataSet.Equipment => Equipment,
            ChartDataSet.Schedule  => Schedule,
            _ => throw new InvalidOperationException($"[For] 未定義的資料集 {dataSet}")
        };

        public static ChartFieldDescriptor? Find(ChartDataSet dataSet, string fieldId)
            => For(dataSet).FirstOrDefault(f => f.FieldId == fieldId);
    }
}
