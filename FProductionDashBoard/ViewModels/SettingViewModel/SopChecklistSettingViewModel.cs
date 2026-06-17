using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        public IReadOnlyList<int> WorkstationNoOptions { get; } = Enumerable.Range(1, 20).ToList();
        public IReadOnlyList<SopTypeFilterOption> SopTypeFilterOptions { get; }

        // ── 表單標題（computed from EditingSopId） ──────────────────
        public string FormTitle => EditingSopId == null
            ? Properties.Resources.SopTitleNew
            : string.Format(Properties.Resources.SopTitleEdit, EditingSopId);

        // ── SOP 左清單篩選 ──────────────────────────────────────────
        [ObservableProperty] private string sopFilter = "";
        [ObservableProperty] private SopType? sopTypeFilter;

        // ── 表頭欄位 ────────────────────────────────────────────────
        [ObservableProperty] private int? editingSopId;
        partial void OnEditingSopIdChanged(int? value) => OnPropertyChanged(nameof(FormTitle));
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BeginCreatePartCommand))]
        private string partFilter = "";
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BeginCreateModelCommand))]
        private string modelFilter = "";
        [ObservableProperty] private int? formPartId;
        [ObservableProperty] private int? formModelId;
        [ObservableProperty] private int? formProcessId;
        [ObservableProperty] private SopType formSopType = SopType.Develop;
        [ObservableProperty] private string? formRemark;

        // ── inline Part 新增 ────────────────────────────────────────
        [ObservableProperty] private bool isCreatingPart;
        [ObservableProperty] private string? newPartBrand;
        [ObservableProperty] private string? newPartName;

        // ── inline Model 新增 ───────────────────────────────────────
        [ObservableProperty] private bool isCreatingModel;
        [ObservableProperty] private string? newModelRemark;

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
            SopTypeFilterOptions = new SopTypeFilterOption[] { new(null) }
                .Concat(Enum.GetValues<SopType>().Select(t => new SopTypeFilterOption(t)))
                .ToArray();
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

        private int AssignNextStationNo()
        {
            var stationCount = FormItems.Count(i => i.CheckType == CheckType.Station);
            return stationCount < WorkstationNoOptions.Count
                ? WorkstationNoOptions[stationCount]
                : WorkstationNoOptions[^1];
        }

        private Material? FindMaterialById(int? id) =>
            StationMaterials.FirstOrDefault(m => m.MaterialId == id)
            ?? FixtureMaterials.FirstOrDefault(m => m.MaterialId == id);

        // ── CheckType 切換時清非當前類型欄位 ─────────────────────────
        partial void OnItemFormCheckTypeChanged(CheckType value)
        {
            switch (value)
            {
                case CheckType.Station:
                    ItemFormQuantity = null;
                    ItemFormContent = null;
                    ItemFormWorkstationNo = AssignNextStationNo();
                    ItemFormMaterialId = StationMaterials.FirstOrDefault()?.MaterialId;
                    break;
                case CheckType.Fixture:
                    ItemFormWorkstationNo = null;
                    ItemFormQuantity = null;
                    ItemFormContent = null;
                    ItemFormMaterialId = FixtureMaterials.FirstOrDefault()?.MaterialId;
                    break;
                case CheckType.Quantity:
                case CheckType.ManHour:
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
        private async Task BeginCreatePartAsync()
        {
            NewPartBrand = null;
            NewPartName = null;
            IsCreatingPart = true;
            try
            {
                var customer = await _core.Data.GetCustomerByCodeAsync(PartFilter);
                if (customer != null) NewPartBrand = customer;
            }
            catch (Exception ex)
            {
                _core.Log.AddErrorLog($"[BeginCreatePartAsync] {ex.Message}");
            }
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

        // ── inline Model 新增 ────────────────────────────────────────
        private bool CanCreateModel() =>
            ModelFilter?.Length == 3 && !FilteredModelList.Any();

        [RelayCommand(CanExecute = nameof(CanCreateModel))]
        private void BeginCreateModel()
        {
            NewModelRemark = null;
            IsCreatingModel = true;
        }

        [RelayCommand]
        private async Task ConfirmCreateModelAsync()
        {
            try
            {
                var dto = new ProductModelFormDto { Name = ModelFilter, Remark = NewModelRemark };
                var newId = await _core.Data.AddProductModelAsync(dto);
                ModelList.Add(new ProductModel { ModelId = newId, Name = dto.Name, Remark = dto.Remark });
                FormModelId = newId;
                IsCreatingModel = false;
                ModelFilter = "";
                RecomputeFilteredModelList();
                FormErrorString = null;
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"SOP 管理 - 新增型號失敗: {ex.Message}", LogLevel.Error);
                _core.Log.AddErrorLog($"[ConfirmCreateModelAsync] {ex.Message}");
            }
        }

        [RelayCommand]
        private void CancelCreateModel() => IsCreatingModel = false;

        // ── 明細子表單命令 ───────────────────────────────────────────
        [RelayCommand]
        private void OpenNewItemForm()
        {
            EditingItemIndex = null;
            ItemFormSeq = FormItems.Count + 1;
            ItemFormCheckType = CheckType.Station;
            // Auto-preset for Station (most common)
            ItemFormWorkstationNo = AssignNextStationNo();
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
            for (int i = 0; i < FormItems.Count; i++)
                FormItems[i].Seq = i + 1;
        }

        [RelayCommand]
        private void SaveItem()
        {
            switch (ItemFormCheckType)
            {
                case CheckType.Station:
                    if (ItemFormWorkstationNo == null || ItemFormMaterialId == null)
                    { FormErrorString = Properties.Resources.SopValidationStation; return; }
                    break;
                case CheckType.Fixture:
                    if (ItemFormMaterialId == null)
                    { FormErrorString = Properties.Resources.SopValidationFixture; return; }
                    break;
                case CheckType.Quantity:
                case CheckType.ManHour:
                    if (ItemFormQuantity == null) ItemFormQuantity = 0;
                    break;
                case CheckType.Other:
                    if (string.IsNullOrWhiteSpace(ItemFormContent))
                    { FormErrorString = Properties.Resources.SopValidationOther; return; }
                    break;
            }

            var mat = (ItemFormCheckType == CheckType.Station || ItemFormCheckType == CheckType.Fixture)
                ? FindMaterialById(ItemFormMaterialId)
                : null;
            var item = new SopChecklistItemFormDto
            {
                Id = EditingItemIndex == null ? null : FormItems[EditingItemIndex.Value].Id,
                Seq = ItemFormSeq,
                CheckType = ItemFormCheckType,
                WorkstationNo = ItemFormCheckType == CheckType.Station ? ItemFormWorkstationNo : null,
                MaterialId = (ItemFormCheckType == CheckType.Station || ItemFormCheckType == CheckType.Fixture) ? ItemFormMaterialId : null,
                MaterialName = mat?.Name,
                Quantity = (ItemFormCheckType == CheckType.Quantity || ItemFormCheckType == CheckType.ManHour) ? ItemFormQuantity : null,
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
                    var mat = FindMaterialById(i.MaterialId);
                    FormItems.Add(new SopChecklistItemFormDto
                    {
                        Id = i.ItemId,
                        Seq = i.Seq,
                        CheckType = i.CheckType,
                        WorkstationNo = i.WorkstationNo,
                        MaterialId = i.MaterialId,
                        MaterialName = mat?.Name,
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
                if (PartList.Any()) PartList.Clear();
                var parts = await _core.Data.GetAllProductPartsAsync();
                foreach (var p in parts) PartList.Add(p);

                if (ModelList.Any()) ModelList.Clear();
                var models = await _core.Data.GetProductModelsAsync();
                foreach (var m in models) ModelList.Add(m);

                if (ProcessList.Any()) ProcessList.Clear();
                var processes = await _core.Data.GetWorkProcessesAsync();
                foreach (var w in processes) ProcessList.Add(w);

                if (StationMaterials.Any()) StationMaterials.Clear();
                var stationMats = await _core.Data.GetMaterialsByTypeAsync(MaterialTypeIds.Station);
                foreach (var m in stationMats) StationMaterials.Add(m);

                if (FixtureMaterials.Any()) FixtureMaterials.Clear();
                var fixtureMats = await _core.Data.GetMaterialsByTypeAsync(MaterialTypeIds.Fixture);
                foreach (var m in fixtureMats) FixtureMaterials.Add(m);

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
            if (ProcessList.Any()) FormProcessId = ProcessList[0].ProcessId;
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        protected override async Task SaveAsync()
        {
            if (FormPartId == null || FormModelId == null || FormProcessId == null)
            { FormErrorString = Properties.Resources.SopValidationRequiredFields; return; }
            if (!FormItems.Any())
            { FormErrorString = Properties.Resources.SopValidationMinItems; return; }

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

                FormSuccessString = EditingSopId == null ? Properties.Resources.SopSuccessAdd : Properties.Resources.SopSuccessUpdate;
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
            FormSopType = SopType.Develop;
            FormRemark = null;
            FormItems.Clear();
            IsCreatingPart = false;
            NewPartBrand = null;
            NewPartName = null;
            IsCreatingModel = false;
            NewModelRemark = null;
            IsItemFormVisible = false;
        }
    }

    public sealed record SopTypeFilterOption(SopType? Value);
}
