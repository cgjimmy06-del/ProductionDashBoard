using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Constants;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FProductionDashBoard.ViewModels
{
    public partial class SopChecklistSettingViewModel : SettingViewModelBase
    {
        // ── 集合 ────────────────────────────────────────────────────────
        public ObservableCollection<SopChecklist> SopList { get; } = new();
        public ObservableCollection<SopChecklist> FilteredSopList { get; } = new();
        public ObservableCollection<ProductPart> PartList { get; } = new();
        public ObservableCollection<ProductPart> FilteredPartList { get; } = new();
        public ObservableCollection<ProductModel> ModelList { get; } = new();
        public ObservableCollection<ProductModel> FilteredModelList { get; } = new();
        public ObservableCollection<WorkProcess> ProcessList { get; } = new();
        public ObservableCollection<Material> StationMaterials { get; } = new();
        public ObservableCollection<Material> FixtureMaterials { get; } = new();
        public ObservableCollection<SopChecklistItemFormDto> FormItems { get; } = new();

        public IReadOnlyList<SopType> SopTypes { get; } = Enum.GetValues<SopType>();
        public IReadOnlyList<CheckType> CheckTypes { get; } = Enum.GetValues<CheckType>();
        public IReadOnlyList<int> WorkstationNoOptions { get; } = new[] { 1, 2, 3, 4, 5, 6 };
        public IReadOnlyList<SopType?> SopTypeFilterOptions { get; }

        // ── SOP 左清單篩選 ──────────────────────────────────────────
        [ObservableProperty] private string sopFilter = "";
        [ObservableProperty] private SopType? sopTypeFilter;

        // ── 表頭欄位 ────────────────────────────────────────────────
        [ObservableProperty] private int? editingSopId;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BeginCreatePartCommand))]
        private string partFilter = "";
        [ObservableProperty] private string modelFilter = "";
        [ObservableProperty] private int? formPartId;
        [ObservableProperty] private int? formModelId;
        [ObservableProperty] private int? formProcessId;
        [ObservableProperty] private SopType formSopType = SopType.A;
        [ObservableProperty] private string? formRemark;

        // ── inline Part 新增 ────────────────────────────────────────
        [ObservableProperty] private bool isCreatingPart;
        [ObservableProperty] private string? newPartBrand;
        [ObservableProperty] private string? newPartName;

        // ── 明細子表單欄位 ───────────────────────────────────────────
        [ObservableProperty] private bool isItemFormVisible;
        [ObservableProperty] private int? editingItemIndex;
        [ObservableProperty] private int itemFormSeq = 1;
        [ObservableProperty] private CheckType itemFormCheckType = CheckType.Station;
        [ObservableProperty] private int? itemFormWorkstationNo;
        [ObservableProperty] private int? itemFormMaterialId;
        [ObservableProperty] private int? itemFormQuantity;
        [ObservableProperty] private string? itemFormContent;
        [ObservableProperty] private string? itemFormRemark;

        public SopChecklistSettingViewModel(DashboardCoreServices core, Services.IDialogService dialog)
            : base(core, dialog)
        {
            SopTypeFilterOptions = new SopType?[] { null }
                .Concat(Enum.GetValues<SopType>().Cast<SopType?>())
                .ToList();
        }

        // ── 篩選邏輯 ────────────────────────────────────────────────
        partial void OnSopFilterChanged(string value) => RecomputeFilteredSopList();
        partial void OnSopTypeFilterChanged(SopType? value) => RecomputeFilteredSopList();
        partial void OnPartFilterChanged(string value) => RecomputeFilteredPartList();
        partial void OnModelFilterChanged(string value) => RecomputeFilteredModelList();

        private void RecomputeFilteredSopList()
        {
            FilteredSopList.Clear();
            foreach (var s in SopList)
            {
                if (SopTypeFilter != null && s.SopType != SopTypeFilter.Value) continue;
                if (!string.IsNullOrEmpty(SopFilter))
                {
                    var hay = string.Join(" ", new[] {
                        s.Product?.Part?.PartNo, s.Product?.Part?.Brand, s.Product?.Part?.Name,
                        s.Product?.Model?.Name, s.Process?.Name, s.SopType.ToString(), s.Remark
                    }.Where(x => !string.IsNullOrEmpty(x)));
                    if (hay.IndexOf(SopFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                }
                FilteredSopList.Add(s);
            }
        }

        private void RecomputeFilteredPartList()
        {
            FilteredPartList.Clear();
            foreach (var p in PartList)
            {
                if (!string.IsNullOrEmpty(PartFilter))
                {
                    var hay = string.Join(" ", new[] { p.PartNo, p.Brand, p.Name }
                        .Where(x => !string.IsNullOrEmpty(x)));
                    if (hay.IndexOf(PartFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                }
                FilteredPartList.Add(p);
            }
        }

        private void RecomputeFilteredModelList()
        {
            FilteredModelList.Clear();
            foreach (var m in ModelList)
            {
                if (!string.IsNullOrEmpty(ModelFilter) &&
                    (m.Name ?? "").IndexOf(ModelFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                FilteredModelList.Add(m);
            }
        }

        // ── CheckType 切換時清非當前類型欄位 ─────────────────────────
        partial void OnItemFormCheckTypeChanged(CheckType value)
        {
            switch (value)
            {
                case CheckType.Station:
                    ItemFormQuantity = null;
                    ItemFormContent = null;
                    var stationCountOnSwitch = FormItems.Count(i => i.CheckType == CheckType.Station);
                    ItemFormWorkstationNo = stationCountOnSwitch < WorkstationNoOptions.Count
                        ? WorkstationNoOptions[stationCountOnSwitch]
                        : WorkstationNoOptions[^1];
                    ItemFormMaterialId = StationMaterials.FirstOrDefault()?.MaterialId;
                    break;
                case CheckType.Fixture:
                    ItemFormWorkstationNo = null;
                    ItemFormQuantity = null;
                    ItemFormContent = null;
                    ItemFormMaterialId = FixtureMaterials.FirstOrDefault()?.MaterialId;
                    break;
                case CheckType.Quantity:
                    ItemFormWorkstationNo = null;
                    ItemFormMaterialId = null;
                    ItemFormContent = null;
                    ItemFormQuantity = 0;
                    break;
                case CheckType.Other:
                    ItemFormWorkstationNo = null;
                    ItemFormMaterialId = null;
                    ItemFormQuantity = null;
                    break;
            }
        }

        // ── inline Part 新增 ────────────────────────────────────────
        private bool CanCreatePart() =>
            PartFilter.Length == 8 && !PartList.Any(p => p.PartNo == PartFilter);

        [RelayCommand(CanExecute = nameof(CanCreatePart))]
        private void BeginCreatePart()
        {
            NewPartBrand = null;
            NewPartName = null;
            IsCreatingPart = true;
        }

        [RelayCommand]
        private async Task ConfirmCreatePartAsync()
        {
            try
            {
                var dto = new ProductPartFormDto
                {
                    PartNo = PartFilter,
                    Brand = NewPartBrand,
                    Name = NewPartName
                };
                var newId = await _core.Data.AddProductPartAsync(dto);
                PartList.Add(new ProductPart
                {
                    PartId = newId,
                    PartNo = dto.PartNo,
                    Brand = dto.Brand,
                    Name = dto.Name
                });
                FormPartId = newId;
                IsCreatingPart = false;
                PartFilter = "";
                RecomputeFilteredPartList();
                FormErrorString = null;
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"SOP 管理 - 新增件號失敗: {ex.Message}", LogLevel.Error);
                _core.Log.AddErrorLog($"[ConfirmCreatePartAsync] {ex.Message}");
            }
        }

        [RelayCommand]
        private void CancelCreatePart() => IsCreatingPart = false;

        // ── 明細子表單命令 ───────────────────────────────────────────
        [RelayCommand]
        private void OpenNewItemForm()
        {
            EditingItemIndex = null;
            ItemFormSeq = FormItems.Count + 1;
            ItemFormCheckType = CheckType.Station;
            // Auto-preset for Station (most common)
            var stationCount = FormItems.Count(i => i.CheckType == CheckType.Station);
            ItemFormWorkstationNo = stationCount < WorkstationNoOptions.Count
                ? WorkstationNoOptions[stationCount]
                : WorkstationNoOptions[^1];
            ItemFormMaterialId = StationMaterials.FirstOrDefault()?.MaterialId;
            ItemFormQuantity = 0;
            ItemFormContent = null;
            ItemFormRemark = null;
            IsItemFormVisible = true;
        }

        [RelayCommand]
        private void EditItem(SopChecklistItemFormDto item)
        {
            EditingItemIndex = FormItems.IndexOf(item);
            ItemFormSeq = item.Seq;
            ItemFormCheckType = item.CheckType;
            ItemFormWorkstationNo = item.WorkstationNo;
            ItemFormMaterialId = item.MaterialId;
            ItemFormQuantity = item.Quantity;
            ItemFormContent = item.Content;
            ItemFormRemark = item.Remark;
            IsItemFormVisible = true;
        }

        [RelayCommand]
        private void RemoveItem(SopChecklistItemFormDto item)
        {
            if (!ShowConfirm($"{Properties.Resources.DialogBaseConfirm} {Properties.Resources.DialogBaseDelete}?"))
                return;
            FormItems.Remove(item);
            // Renumber remaining items strictly sequential (no gaps)
            var temp = FormItems.ToList();
            FormItems.Clear();
            for (int i = 0; i < temp.Count; i++) { temp[i].Seq = i + 1; FormItems.Add(temp[i]); }
        }

        [RelayCommand]
        private void SaveItem()
        {
            switch (ItemFormCheckType)
            {
                case CheckType.Station:
                    if (ItemFormWorkstationNo == null || ItemFormMaterialId == null)
                    { FormErrorString = "Station: 工位編號與物料皆必填"; return; }
                    break;
                case CheckType.Fixture:
                    if (ItemFormMaterialId == null)
                    { FormErrorString = "Fixture: 物料必填"; return; }
                    break;
                case CheckType.Quantity:
                    if (ItemFormQuantity == null) ItemFormQuantity = 0;
                    break;
                case CheckType.Other:
                    if (string.IsNullOrWhiteSpace(ItemFormContent))
                    { FormErrorString = "Other: 內容必填"; return; }
                    break;
            }

            var item = new SopChecklistItemFormDto
            {
                Id = EditingItemIndex == null ? null : FormItems[EditingItemIndex.Value].Id,
                Seq = ItemFormSeq,
                CheckType = ItemFormCheckType,
                WorkstationNo = ItemFormCheckType == CheckType.Station ? ItemFormWorkstationNo : null,
                MaterialId = (ItemFormCheckType == CheckType.Station || ItemFormCheckType == CheckType.Fixture) ? ItemFormMaterialId : null,
                Quantity = ItemFormCheckType == CheckType.Quantity ? ItemFormQuantity : null,
                Content = ItemFormCheckType == CheckType.Other ? ItemFormContent : null,
                Remark = ItemFormRemark
            };
            if (EditingItemIndex == null) FormItems.Add(item);
            else FormItems[EditingItemIndex.Value] = item;
            IsItemFormVisible = false;
            FormErrorString = null;
        }

        [RelayCommand]
        private void CancelItem() => IsItemFormVisible = false;

        // ── SOP 編輯/刪除 ────────────────────────────────────────────
        [RelayCommand]
        private async Task EditAsync(SopChecklist sop)
        {
            try
            {
                var detail = await _core.Data.GetSopChecklistWithItemsAsync(sop.SopId);
                if (detail == null)
                { FormErrorString = $"找不到 SOP ID={sop.SopId}"; return; }

                EditingSopId = detail.SopId;
                FormPartId = sop.Product?.PartId;
                FormModelId = sop.Product?.ModelId;
                FormProcessId = detail.ProcessId;
                FormSopType = detail.SopType;
                FormRemark = detail.Remark;
                FormItems.Clear();
                foreach (var i in detail.Items.OrderBy(x => x.Seq))
                {
                    FormItems.Add(new SopChecklistItemFormDto
                    {
                        Id = i.ItemId,
                        Seq = i.Seq,
                        CheckType = i.CheckType,
                        WorkstationNo = i.WorkstationNo,
                        MaterialId = i.MaterialId,
                        Quantity = i.Quantity,
                        Content = i.Content,
                        Remark = i.Remark
                    });
                }
                FormErrorString = null;
                FormSuccessString = null;
                IsItemFormVisible = false;
                IsFormVisible = true;
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"SOP 管理 - 載入 SOP 失敗: {ex.Message}", LogLevel.Error);
                _core.Log.AddErrorLog($"[EditAsync] {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task DeleteAsync(SopChecklist sop)
        {
            if (!ShowConfirm($"{Properties.Resources.DialogBaseConfirm} {Properties.Resources.DialogBaseDelete}?"))
                return;
            try
            {
                await _core.Data.DeleteSopChecklistAsync(sop.SopId);
                SopList.Remove(sop);
                RecomputeFilteredSopList();
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"SOP 管理 - 刪除 SOP 失敗 (檢查是否被機台清單引用): {ex.Message}", LogLevel.Error);
                _core.Log.AddErrorLog($"[DeleteAsync] {ex.Message}");
            }
        }

        // ── 抽象方法實作 ────────────────────────────────────────────
        protected override async Task LoadAsync()
        {
            try
            {
                if (!PartList.Any())
                {
                    var parts = await _core.Data.GetAllProductPartsAsync();
                    foreach (var p in parts) PartList.Add(p);
                }
                if (!ModelList.Any())
                {
                    var models = await _core.Data.GetProductModelsAsync();
                    foreach (var m in models) ModelList.Add(m);
                }
                if (!ProcessList.Any())
                {
                    var processes = await _core.Data.GetWorkProcessesAsync();
                    foreach (var w in processes) ProcessList.Add(w);
                }
                if (!StationMaterials.Any())
                {
                    var mats = await _core.Data.GetMaterialsByTypeAsync(MaterialTypeIds.Station);
                    foreach (var m in mats) StationMaterials.Add(m);
                }
                if (!FixtureMaterials.Any())
                {
                    var mats = await _core.Data.GetMaterialsByTypeAsync(MaterialTypeIds.Fixture);
                    foreach (var m in mats) FixtureMaterials.Add(m);
                }

                var list = await _core.Data.GetAllSopChecklistsAsync();
                SopList.Clear();
                foreach (var s in list) SopList.Add(s);

                RecomputeFilteredSopList();
                RecomputeFilteredPartList();
                RecomputeFilteredModelList();
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"SOP 管理 - 載入清單失敗: {ex.Message}", LogLevel.Error);
                _core.Log.AddErrorLog($"[LoadAsync] {ex.Message}");
            }
        }

        protected override void OpenNewForm()
        {
            EditingSopId = null;
            ClearForm();
            if (ModelList.Any()) FormModelId = ModelList[0].ModelId;
            if (ProcessList.Any()) FormProcessId = ProcessList[0].ProcessId;
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        protected override async Task SaveAsync()
        {
            if (FormPartId == null || FormModelId == null || FormProcessId == null)
            { FormErrorString = "Part / Model / Process 為必填"; return; }
            if (!FormItems.Any())
            { FormErrorString = "至少需新增 1 筆明細"; return; }

            try
            {
                var dto = new SopChecklistFormDto
                {
                    Id = EditingSopId,
                    PartId = FormPartId.Value,
                    ModelId = FormModelId.Value,
                    ProcessId = FormProcessId.Value,
                    SopType = FormSopType,
                    Remark = FormRemark,
                    Items = FormItems.ToList()
                };
                if (EditingSopId == null)
                    await _core.Data.AddSopChecklistAsync(dto);
                else
                    await _core.Data.UpdateSopChecklistAsync(dto);

                FormSuccessString = EditingSopId == null ? "新增成功" : "更新成功";
                FormErrorString = null;
                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                FormSuccessString = null;
                _core.Log.AddLog($"SOP 管理 - 儲存 SOP 失敗: {ex.Message}", LogLevel.Error);
                _core.Log.AddErrorLog($"[SaveAsync] {ex.Message}");
            }
        }

        protected override void ClearForm()
        {
            EditingSopId = null;
            PartFilter = "";
            ModelFilter = "";
            FormPartId = null;
            FormModelId = null;
            FormProcessId = null;
            FormSopType = SopType.A;
            FormRemark = null;
            FormItems.Clear();
            IsCreatingPart = false;
            NewPartBrand = null;
            NewPartName = null;
            IsItemFormVisible = false;
        }
    }
}
