using System;
using System.Collections.Generic;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.ViewModels;

namespace FProductionDashBoard.Services
{
    /// <summary>
    /// 圖表設計器左預覽的假資料：每資料集寫死 3 筆樣本列（涵蓋各欄位與代表性枚舉值），
    /// 不打 DB。FinishRow 會累加卡片槽位，故每次呼叫都建立全新實例。
    /// </summary>
    internal static class ChartSampleData
    {
        internal static List<ChartRowViewModel> CreateRows(ChartDataSet dataSet) => dataSet switch
        {
            ChartDataSet.Schedule => CreateScheduleRows(),
            _                     => CreateEquipmentRows(),
        };

        private static List<ChartRowViewModel> CreateEquipmentRows() => new()
        {
            EquipmentRow("CNC-01", FieldCatalog.ValInProduction, FieldCatalog.ValLoadHigh,
                FieldCatalog.ValTuningActive, 3, 620, 4, 6, "PN-1001 · CNC", 350, 700),
            EquipmentRow("EDM-02", FieldCatalog.ValPendingProd, FieldCatalog.ValLoadMid,
                FieldCatalog.ValTuningNone, 2, 180, 2, 3, null, 0, 180),
            EquipmentRow("MILL-03", FieldCatalog.ValIdle, FieldCatalog.ValLoadNone,
                FieldCatalog.ValTuningNone, 0, 0, 1, 2, null, 0, 0),
        };

        private static List<ChartRowViewModel> CreateScheduleRows() => new()
        {
            ScheduleRow(1001, "BrandA", "PN-1001", "MD-A", "CNC", 100, 0, "Pending", "Develop", 3, 0),
            ScheduleRow(1002, "BrandB", "PN-2002", "MD-B", "EDM", 250, 80, "Scheduled", "Close", 1, 2),
            ScheduleRow(1003, "BrandC", "PN-3003", "MD-C", "MILL", 60, 60, "Completed", "Other", 7, 1),
        };

        private static ChartRowViewModel EquipmentRow(string name, string status, string load,
            string tuning, int activeOrders, int pendingQty, int feasible, int total,
            string? currentProduct, int completed, int target)
        {
            var row = new ChartRowViewModel();
            row.Values[FieldCatalog.EquipmentName]        = name;
            row.Values[FieldCatalog.ProductionStatus]     = status;
            row.Values[FieldCatalog.LoadLevel]            = load;
            row.Values[FieldCatalog.TuningStatus]         = tuning;
            row.Values[FieldCatalog.ActiveOrderCount]     = activeOrders;
            row.Values[FieldCatalog.TotalPendingQty]      = pendingQty;
            row.Values[FieldCatalog.FeasibleProgramCount] = feasible;
            row.Values[FieldCatalog.TotalProgramCount]    = total;
            row.Values[FieldCatalog.CurrentProduct]       = currentProduct;
            row.Values[FieldCatalog.ProgressCompleted]    = completed;
            row.Values[FieldCatalog.ProgressTarget]       = target;
            return row;
        }

        private static ChartRowViewModel ScheduleRow(int scheduleId, string brand, string partNo,
            string model, string process, int quantity, int actualQuantity, string status,
            string sopType, int waitingDays, int activeEquipments)
        {
            var row = new ChartRowViewModel();
            row.Values[FieldCatalog.ScheduleId]           = scheduleId;
            row.Values[FieldCatalog.Brand]                = brand;
            row.Values[FieldCatalog.PartNo]               = partNo;
            row.Values[FieldCatalog.Model]                = model;
            row.Values[FieldCatalog.Process]              = process;
            row.Values[FieldCatalog.Quantity]             = quantity;
            row.Values[FieldCatalog.ActualQuantity]       = actualQuantity;
            row.Values[FieldCatalog.ScheduleStatus]       = status;
            row.Values[FieldCatalog.SopType]              = sopType;
            row.Values[FieldCatalog.ReceivedAt]           = DateTime.Today.AddDays(-waitingDays);
            row.Values[FieldCatalog.WaitingDays]          = waitingDays;
            row.Values[FieldCatalog.ActiveEquipmentCount] = activeEquipments;
            return row;
        }
    }
}
