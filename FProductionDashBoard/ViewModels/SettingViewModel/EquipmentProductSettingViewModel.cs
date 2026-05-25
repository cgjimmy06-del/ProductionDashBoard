using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FProductionDashBoard.ViewModels
{
    public partial class EquipmentProductSettingViewModel : SettingViewModelBase
    {
        // ── 左側集合 ────────────────────────────────────────────────
        public ObservableCollection<Equipment> EquipmentList { get; } = new();
        public ObservableCollection<Equipment> FilteredEquipmentList { get; } = new();
        [ObservableProperty] private IReadOnlyList<EquipmentTypeFilterOption> equipmentTypeFilterOptions = Array.Empty<EquipmentTypeFilterOption>();

        // ── 右側品項清單 ─────────────────────────────────────────────
        public ObservableCollection<EquipmentProduct> ProductList { get; } = new();

        // ── SOP 資料（供表單選擇） ────────────────────────────────────
        public ObservableCollection<SopChecklist> SopOptions { get; } = new();
        public ObservableCollection<Product> FormProductOptions { get; } = new();
        public ObservableCollection<WorkProcess> FormProcessOptions { get; } = new();
        public ObservableCollection<SopType> FormSopTypeOptions { get; } = new();

        // ── 左側 filter ──────────────────────────────────────────────
        [ObservableProperty] private string keywordFilter = "";
        [ObservableProperty] private EquipmentType? typeFilter;

        // ── 左側選擇 ─────────────────────────────────────────────────
        [ObservableProperty] private Equipment? selectedEquipment;

        // ── 右側表單欄位 ─────────────────────────────────────────────
        [ObservableProperty] private int? editingProductId;
        [ObservableProperty] private int formSeqNo;
        [ObservableProperty] private string sopKeywordFilter = "";
        [ObservableProperty] private Product? formProduct;
        [ObservableProperty] private WorkProcess? formProcess;
        [ObservableProperty] private SopType? formSopType;
        [ObservableProperty] private bool isSopTypeVisible;
        [ObservableProperty] private bool isProductListLoading;

        public EquipmentProductSettingViewModel(DashboardCoreServices core, Services.IDialogService dialog)
            : base(core, dialog) { }

        // ── 左側 filter partial 方法 ─────────────────────────────────
        partial void OnKeywordFilterChanged(string value) => RecomputeFilteredEquipmentList();
        partial void OnTypeFilterChanged(EquipmentType? value) => RecomputeFilteredEquipmentList();

        private void RecomputeFilteredEquipmentList()
        {
            var prevId = SelectedEquipment?.Id;
            FilteredEquipmentList.Clear();
            foreach (var eq in EquipmentList)
            {
                if (TypeFilter != null && eq.TypeId != TypeFilter.TypeId) continue;
                if (!string.IsNullOrEmpty(KeywordFilter))
                {
                    var hay = string.Join(" ", new[] { eq.Code, eq.Name, eq.Ip }
                        .Where(x => !string.IsNullOrEmpty(x)));
                    if (hay.IndexOf(KeywordFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                }
                FilteredEquipmentList.Add(eq);
            }
            if (prevId != null)
                SelectedEquipment = FilteredEquipmentList.FirstOrDefault(eq => eq.Id == prevId);
        }

        // ── 切換機台 ─────────────────────────────────────────────────
        partial void OnSelectedEquipmentChanged(Equipment? value)
        {
            if (value == null)
            {
                ProductList.Clear();
                CloseForm();
                return;
            }
            _ = LoadProductListAsync(value.Id);
        }

        private async Task LoadProductListAsync(int equipmentId)
        {
            IsProductListLoading = true;
            try
            {
                var list = await _core.Data.GetEquipmentProductsByEquipmentAsync(equipmentId);
                if (SelectedEquipment?.Id != equipmentId) return;
                ProductList.Clear();
                foreach (var ep in list) ProductList.Add(ep);
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"可生產清單 - 載入機台品項失敗: {ex.Message}", LogLevel.Error);
                _core.Log.AddErrorLog($"[LoadProductListAsync] {ex.Message}");
            }
            finally
            {
                IsProductListLoading = false;
            }
        }

        // ── SOP 表單三步驟串聯 ────────────────────────────────────────
        partial void OnSopKeywordFilterChanged(string value)
        {
            RecomputeFormProductOptions();
            RecomputeFormProcessOptions();
        }

        partial void OnFormProductChanged(Product? value)
        {
            FormProcess = null;
            FormSopType = null;
            IsSopTypeVisible = false;
            RecomputeFormProcessOptions();
        }

        partial void OnFormProcessChanged(WorkProcess? value)
        {
            FormSopType = null;
            IsSopTypeVisible = false;
            RecomputeFormSopTypeOptions();
        }

        private void RecomputeFormProductOptions()
        {
            var prevProductId = FormProduct?.ProductId;
            FormProductOptions.Clear();
            foreach (var p in SopOptions
                .Select(s => s.Product)
                .Where(p => p != null)
                .DistinctBy(p => p!.ProductId))
            {
                if (!string.IsNullOrEmpty(SopKeywordFilter))
                {
                    var hay = string.Join(" ", new[] { p!.Part?.PartNo, p.Part?.Brand, p.Model?.Name }
                        .Where(x => !string.IsNullOrEmpty(x)));
                    if (hay.IndexOf(SopKeywordFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                }
                FormProductOptions.Add(p!);
            }
            // 若現有選擇被關鍵字過濾掉則重設（cascade 會清 Process/SopType）
            if (prevProductId != null && !FormProductOptions.Any(p => p.ProductId == prevProductId))
                FormProduct = null;
        }

        private void RecomputeFormProcessOptions()
        {
            if (FormProduct == null)
            {
                FormProcessOptions.Clear();
                return;
            }
            var prevProcessId = FormProcess?.ProcessId;
            FormProcessOptions.Clear();
            foreach (var proc in SopOptions
                .Where(s => s.ProductId == FormProduct.ProductId)
                .Select(s => s.Process)
                .Where(p => p != null)
                .DistinctBy(p => p!.ProcessId))
            {
                if (!string.IsNullOrEmpty(SopKeywordFilter) &&
                    (proc!.Name ?? "").IndexOf(SopKeywordFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                FormProcessOptions.Add(proc!);
            }
            if (prevProcessId != null && !FormProcessOptions.Any(p => p.ProcessId == prevProcessId))
                FormProcess = null;
        }

        private void RecomputeFormSopTypeOptions()
        {
            FormSopTypeOptions.Clear();
            if (FormProduct == null || FormProcess == null) return;

            var types = SopOptions
                .Where(s => s.ProductId == FormProduct.ProductId && s.ProcessId == FormProcess.ProcessId)
                .Select(s => s.SopType)
                .Distinct()
                .ToList();

            foreach (var t in types) FormSopTypeOptions.Add(t);

            if (types.Count == 1)
            {
                FormSopType = types[0];
                IsSopTypeVisible = false;
            }
            else if (types.Count > 1)
            {
                IsSopTypeVisible = true;
            }
            else
            {
                FormErrorString = Properties.Resources.EqprodValidationNoSop;
            }
        }

        private SopChecklist? ResolveMatchedSop()
        {
            if (FormProduct == null || FormProcess == null || FormSopType == null) return null;
            return SopOptions.FirstOrDefault(s =>
                s.ProductId == FormProduct.ProductId &&
                s.ProcessId == FormProcess.ProcessId &&
                s.SopType == FormSopType.Value);
        }

        // ── CRUD Commands ────────────────────────────────────────────
        [RelayCommand]
        private void Edit(EquipmentProduct ep)
        {
            SopKeywordFilter = "";
            RecomputeFormProductOptions();
            EditingProductId = ep.EquipmentProductId;
            FormSeqNo = ep.SeqNo;
            FormProduct = FormProductOptions.FirstOrDefault(p => p.ProductId == ep.Sop?.ProductId);
            // FormProduct 設定後 cascade 重建 FormProcessOptions
            FormProcess = FormProcessOptions.FirstOrDefault(p => p.ProcessId == ep.Sop?.ProcessId);
            // FormProcess 設定後 cascade 重建 FormSopTypeOptions
            FormSopType = ep.Sop?.SopType;
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private async Task DeleteAsync(EquipmentProduct ep)
        {
            if (!ShowConfirm($"{Properties.Resources.DialogBaseConfirm} {Properties.Resources.DialogBaseDelete}?"))
                return;
            try
            {
                await _core.Data.DeleteEquipmentProductAsync(ep.EquipmentProductId);
                if (SelectedEquipment != null)
                    await LoadProductListAsync(SelectedEquipment.Id);
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"可生產清單 - 刪除品項失敗: {ex.Message}", LogLevel.Error);
                _core.Log.AddErrorLog($"[DeleteAsync] {ex.Message}");
            }
        }

        // ── 抽象方法實作 ─────────────────────────────────────────────
        protected override async Task LoadAsync()
        {
            try
            {
                var types = await _core.Data.GetEquipmentTypesAsync();
                EquipmentTypeFilterOptions = new EquipmentTypeFilterOption[] { new(null) }
                    .Concat(types.Select(t => new EquipmentTypeFilterOption(t)))
                    .ToArray();

                var equipments = await _core.Data.GetAllEquipmentAsync();
                EquipmentList.Clear();
                foreach (var eq in equipments) EquipmentList.Add(eq);

                if (SopOptions.Any()) SopOptions.Clear();
                var sops = await _core.Data.GetAllSopChecklistsAsync();
                foreach (var s in sops) SopOptions.Add(s);

                RecomputeFormProductOptions();
                RecomputeFilteredEquipmentList();

                if (SelectedEquipment != null)
                    await LoadProductListAsync(SelectedEquipment.Id);
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"可生產清單 - 載入清單失敗: {ex.Message}", LogLevel.Error);
                _core.Log.AddErrorLog($"[LoadAsync] {ex.Message}");
            }
        }

        protected override void OpenNewForm()
        {
            if (SelectedEquipment == null) return;
            EditingProductId = null;
            FormSeqNo = ProductList.Any() ? ProductList.Max(ep => ep.SeqNo) + 1 : 1;
            SopKeywordFilter = "";
            FormProduct = null;
            FormProcess = null;
            FormSopType = null;
            IsSopTypeVisible = false;
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        protected override async Task SaveAsync()
        {
            if (SelectedEquipment == null)
            { FormErrorString = Properties.Resources.EqprodValidationEquipment; return; }
            if (FormProduct == null)
            { FormErrorString = Properties.Resources.EqprodValidationProduct; return; }
            if (FormProcess == null)
            { FormErrorString = Properties.Resources.EqprodValidationProcess; return; }
            if (FormSeqNo <= 0)
            { FormErrorString = Properties.Resources.EqprodValidationSeqNo; return; }

            bool seqConflict = ProductList.Any(ep =>
                ep.SeqNo == FormSeqNo &&
                ep.EquipmentProductId != (EditingProductId ?? -1));
            if (seqConflict)
            { FormErrorString = Properties.Resources.EqprodValidationSeqNoDuplicate; return; }

            var matched = ResolveMatchedSop();
            if (matched == null)
            { FormErrorString = Properties.Resources.EqprodValidationNoSop; return; }

            try
            {
                var dto = new EquipmentProductFormDto
                {
                    Id = EditingProductId,
                    EquipmentId = SelectedEquipment.Id,
                    SopId = matched.SopId,
                    SeqNo = FormSeqNo
                };

                if (EditingProductId == null)
                    await _core.Data.AddEquipmentProductAsync(dto);
                else
                    await _core.Data.UpdateEquipmentProductAsync(dto);

                bool wasNew = EditingProductId == null;
                await LoadProductListAsync(SelectedEquipment.Id);
                CloseForm();
                FormSuccessString = wasNew ? "新增成功" : "更新成功";
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"可生產清單 - 儲存品項失敗: {ex.Message}", LogLevel.Error);
                _core.Log.AddErrorLog($"[SaveAsync] {ex.Message}");
            }
        }

        protected override void ClearForm()
        {
            EditingProductId = null;
            FormSeqNo = 0;
            SopKeywordFilter = "";
            FormProduct = null;
            FormProcess = null;
            FormSopType = null;
            IsSopTypeVisible = false;
        }
    }

    public sealed record EquipmentTypeFilterOption(EquipmentType? Value);
}
