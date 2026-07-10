using System.Collections.Generic;
using FProductionDashBoard.Dtos;

namespace FProductionDashBoard.Services
{
    /// <summary>
    /// 預設圖表定義（種子）：圖表 1「設備總覽」為鎖定預設（IsDefault），
    /// 圖表 2「排程看板」為一般圖表（驗證表格容器與排程視角，可刪）。
    /// </summary>
    public static class DefaultChartDefinitions
    {
        public const string EquipmentOverviewId = "default-equipment-overview";
        public const string ScheduleBoardId     = "default-schedule-board";

        public static List<ChartDefinition> Create() => new()
        {
            CreateEquipmentOverview(),
            CreateScheduleBoard(),
        };

        public static ChartDefinition CreateEquipmentOverview() => new()
        {
            Id = EquipmentOverviewId,
            NameKey = "ChartDefaultEquipmentOverview",
            DataSet = ChartDataSet.Equipment,
            SortOrder = 1,
            IsDefault = true,
            StatRow = new StatRowConfig
            {
                Items =
                {
                    new StatItemConfig { LabelKey = "ChartStatEquipmentTotal" },
                    new StatItemConfig
                    {
                        FieldId = FieldCatalog.ProductionStatus,
                        FilterValue = FieldCatalog.ValInProduction,
                        LabelKey = "ChartValInProduction",
                        ColorKey = "PrimaryBrush",
                    },
                    new StatItemConfig
                    {
                        FieldId = FieldCatalog.TuningStatus,
                        FilterValue = FieldCatalog.ValTuningActive,
                        LabelKey = "ChartValTuningActive",
                        ColorKey = "WarningBrush",
                    },
                    new StatItemConfig
                    {
                        FieldId = FieldCatalog.ProductionStatus,
                        FilterValue = FieldCatalog.ValIdle,
                        LabelKey = "ChartValIdle",
                        ColorKey = "SuccessBrush",
                    },
                },
            },
            FilterRow = new FilterRowConfig
            {
                FilterFieldIds = { FieldCatalog.ProductionStatus, FieldCatalog.EquipmentName },
                QuickButtons =
                {
                    new QuickFilterButtonConfig { FieldId = FieldCatalog.ProductionStatus, Value = FieldCatalog.ValInProduction },
                    new QuickFilterButtonConfig { FieldId = FieldCatalog.ProductionStatus, Value = FieldCatalog.ValIdle },
                    new QuickFilterButtonConfig { FieldId = FieldCatalog.TuningStatus,     Value = FieldCatalog.ValTuningActive },
                    new QuickFilterButtonConfig { FieldId = FieldCatalog.LoadLevel,        Value = FieldCatalog.ValLoadHigh, LabelKey = "ChartBtnHighLoad" },
                },
                SortOptionFieldIds = { FieldCatalog.EquipmentName, FieldCatalog.ProductionStatus, FieldCatalog.LoadLevel },
                DefaultSortFieldId = FieldCatalog.EquipmentName,
                DefaultSortDirection = ChartSortDirection.Ascending,
            },
            Container = new ContainerConfig
            {
                Type = ChartContainerType.Card,
                Card = new CardContainerConfig
                {
                    BorderColorFieldId = FieldCatalog.ProductionStatus,
                    TitleFieldId = FieldCatalog.EquipmentName,
                    Chips =
                    {
                        new ChipConfig { FieldId = FieldCatalog.TuningStatus },
                        new ChipConfig { FieldId = FieldCatalog.LoadLevel },
                    },
                    SecondaryFieldIds = { FieldCatalog.CurrentProduct, FieldCatalog.TotalPendingQty },
                    ProgressNumeratorFieldId = FieldCatalog.ProgressCompleted,
                    ProgressDenominatorFieldId = FieldCatalog.ProgressTarget,
                },
            },
        };

        public static ChartDefinition CreateScheduleBoard() => new()
        {
            Id = ScheduleBoardId,
            NameKey = "ChartDefaultScheduleBoard",
            DataSet = ChartDataSet.Schedule,
            SortOrder = 2,
            IsDefault = false,
            StatRow = new StatRowConfig
            {
                Items =
                {
                    new StatItemConfig { LabelKey = "ChartStatScheduleTotal" },
                    new StatItemConfig
                    {
                        FieldId = FieldCatalog.ScheduleStatus,
                        FilterValue = "Pending",
                        LabelKey = "ChartValSchPending",
                        ColorKey = "WarningBrush",
                    },
                    new StatItemConfig
                    {
                        FieldId = FieldCatalog.ScheduleStatus,
                        FilterValue = "Scheduled",
                        LabelKey = "ChartValSchScheduled",
                        ColorKey = "PrimaryBrush",
                    },
                    new StatItemConfig
                    {
                        FieldId = FieldCatalog.ScheduleStatus,
                        FilterValue = "Completed",
                        LabelKey = "ChartValSchCompleted",
                        ColorKey = "SuccessBrush",
                    },
                },
            },
            FilterRow = new FilterRowConfig
            {
                FilterFieldIds = { FieldCatalog.ScheduleStatus, FieldCatalog.ReceivedAt, FieldCatalog.PartNo },
                QuickButtons =
                {
                    new QuickFilterButtonConfig { FieldId = FieldCatalog.ScheduleStatus, Value = "Pending" },
                    new QuickFilterButtonConfig { FieldId = FieldCatalog.ScheduleStatus, Value = "Scheduled" },
                },
                SortOptionFieldIds = { FieldCatalog.ScheduleId, FieldCatalog.ScheduleStatus, FieldCatalog.WaitingDays },
                DefaultSortFieldId = FieldCatalog.ScheduleId,
                DefaultSortDirection = ChartSortDirection.Descending,
            },
            Container = new ContainerConfig
            {
                Type = ChartContainerType.Table,
                Table = new TableContainerConfig
                {
                    ColumnFieldIds =
                    {
                        FieldCatalog.ScheduleId, FieldCatalog.PartNo, FieldCatalog.Model,
                        FieldCatalog.Process, FieldCatalog.Quantity, FieldCatalog.ScheduleStatus,
                        FieldCatalog.WaitingDays,
                    },
                    IndicatorFieldId = FieldCatalog.ScheduleStatus,
                },
            },
        };
    }
}
