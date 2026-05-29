using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace FProductionDashBoard.ViewModels
{
    public partial class EquipmentSettingViewModel : SettingViewModelBase
    {
        public ObservableCollection<Equipment> EquipmentList { get; } = new();
        public ObservableCollection<EquipmentType> EquipmentTypes { get; } = new();

        [ObservableProperty] private int? editingId;
        [ObservableProperty] private string formCode = "";
        [ObservableProperty] private string formName = "";
        [ObservableProperty] private string formIp = "";
        [ObservableProperty] private int formPort = 0;
        [ObservableProperty] private int? formTypeId;
        [ObservableProperty] private string? formFactory;
        [ObservableProperty] private string? formBuilding;
        [ObservableProperty] private string? formFloor;
        [ObservableProperty] private string? formDepartmentId;
        [ObservableProperty] private string? formDescription;

        public EquipmentSettingViewModel(DashboardCoreServices core, Services.IDialogService dialog)
            : base(core, dialog) { }

        protected override async Task LoadAsync()
        {
            try
            {
                if (!EquipmentTypes.Any())
                {
                    var types = await _core.Data.GetEquipmentTypesAsync();
                    foreach (var t in types) EquipmentTypes.Add(t);
                }
                var list = await _core.Data.GetAllEquipmentAsync();
                EquipmentList.Clear();
                foreach (var e in list) EquipmentList.Add(e);
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"載入設備清單失敗: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void OpenNewForm()
        {
            EditingId = null;
            ClearForm();
            FormTypeId = 1;
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private void Edit(Equipment item)
        {
            EditingId = item.Id;
            FormCode = item.Code;
            FormName = item.Name;
            FormIp = item.Ip;
            FormPort = item.Port;
            FormTypeId = item.TypeId;
            FormFactory = item.Factory;
            FormBuilding = item.Building;
            FormFloor = item.Floor;
            FormDepartmentId = item.DepartmentId;
            FormDescription = item.Description;
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private async Task Delete(Equipment item)
        {
            if (!ShowConfirm($"{Properties.Resources.DialogBaseConfirm} {Properties.Resources.DialogBaseDelete}?"))
                return;
            try
            {
                await _core.Data.DeleteEquipmentAsync(item.Id);
                EquipmentList.Remove(item);
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"刪除設備失敗 (檢查是否有關聯紀錄): {ex.Message}", LogLevel.Error);
            }
        }

        protected override async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(FormCode) || string.IsNullOrWhiteSpace(FormName)
                || string.IsNullOrWhiteSpace(FormIp))
            { FormErrorString = "Code、Name、IP 為必填"; return; }
            if (FormPort is < 0 or > 65535)
            { FormErrorString = "Port 需為 0–65535"; return; }

            try
            {
                var dto = new EquipmentFormDto
                {
                    Id = EditingId,
                    Code = FormCode,
                    Name = FormName,
                    Ip = FormIp,
                    Port = FormPort,
                    TypeId = FormTypeId,
                    Factory = FormFactory,
                    Building = FormBuilding,
                    Floor = FormFloor,
                    DepartmentId = FormDepartmentId,
                    Description = FormDescription
                };

                if (EditingId == null)
                    await _core.Data.AddEquipmentAsync(dto);
                else
                    await _core.Data.UpdateEquipmentAsync(dto);

                FormSuccessString = EditingId == null ? "新增成功" : "更新成功";
                FormErrorString = null;
                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                FormSuccessString = null;
                _core.Log.AddLog($"儲存設備失敗: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void ClearForm()
        {
            EditingId = null;
            FormCode = "";
            FormName = "";
            FormIp = "";
            FormPort = 0;
            FormTypeId = 0;
            FormFactory = null;
            FormBuilding = null;
            FormFloor = null;
            FormDepartmentId = null;
            FormDescription = null;
        }
    }
}
