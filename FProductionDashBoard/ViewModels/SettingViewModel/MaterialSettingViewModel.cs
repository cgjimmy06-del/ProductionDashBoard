using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace FProductionDashBoard.ViewModels
{
    public partial class MaterialSettingViewModel : SettingViewModelBase
    {
        public ObservableCollection<Material> MaterialList { get; } = new();
        public ObservableCollection<MaterialType> MaterialTypes { get; } = new();

        [ObservableProperty] private int? editingId;
        [ObservableProperty] private string formMaterialCode = "";
        [ObservableProperty] private string formName = "";
        [ObservableProperty] private string? formBrand;
        [ObservableProperty] private string? formSpecification;
        [ObservableProperty] private int? formTypeId;
        [ObservableProperty] private string? formDescription;
        [ObservableProperty] private int formMinimumStock = 0;
        [ObservableProperty] private int formQuantityInStock = 0;

        public MaterialSettingViewModel(LogService log, IDataService dataService)
            : base(log, dataService) { }

        protected override async Task LoadAsync()
        {
            try
            {
                if (!MaterialTypes.Any())
                {
                    var types = await _dataService.GetMaterialTypesAsync();
                    foreach (var t in types) MaterialTypes.Add(t);
                }
                var list = await _dataService.GetAllMaterialsAsync();
                MaterialList.Clear();
                foreach (var m in list) MaterialList.Add(m);
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _log.AddLog($"載入材料清單失敗: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void OpenNewForm()
        {
            EditingId = null;
            ClearForm();
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private void Edit(Material item)
        {
            EditingId = item.MaterialId;
            FormMaterialCode = item.MaterialCode;
            FormName = item.Name;
            FormBrand = item.Brand;
            FormSpecification = item.Specification;
            FormTypeId = item.TypeId;
            FormDescription = item.Description;
            FormMinimumStock = item.MinimumStock;
            FormQuantityInStock = item.QuantityInStock;
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private async Task Delete(Material item)
        {
            try
            {
                await _dataService.DeleteMaterialAsync(item.MaterialId);
                MaterialList.Remove(item);
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _log.AddLog($"刪除材料失敗: {ex.Message}", LogLevel.Error);
            }
        }

        protected override async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(FormMaterialCode) || string.IsNullOrWhiteSpace(FormName))
            { FormErrorString = "MaterialCode、Name 為必填"; return; }

            try
            {
                var dto = new MaterialFormDto
                {
                    Id = EditingId,
                    MaterialCode = FormMaterialCode,
                    Name = FormName,
                    Brand = FormBrand,
                    Specification = FormSpecification,
                    TypeId = FormTypeId,
                    Description = FormDescription,
                    MinimumStock = FormMinimumStock,
                    QuantityInStock = FormQuantityInStock
                };

                if (EditingId == null)
                    await _dataService.AddMaterialAsync(dto);
                else
                    await _dataService.UpdateMaterialAsync(dto);

                FormSuccessString = EditingId == null ? "新增成功" : "更新成功";
                FormErrorString = null;
                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                FormSuccessString = null;
                _log.AddLog($"儲存材料失敗: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void ClearForm()
        {
            EditingId = null;
            FormMaterialCode = "";
            FormName = "";
            FormBrand = null;
            FormSpecification = null;
            FormTypeId = 0;
            FormDescription = null;
            FormMinimumStock = 0;
            FormQuantityInStock = 0;
        }
    }
}
