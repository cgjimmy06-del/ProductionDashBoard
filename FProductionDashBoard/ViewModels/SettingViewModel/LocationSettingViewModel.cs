using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FProductionDashBoard.ViewModels
{
    public partial class LocationSettingViewModel : SettingViewModelBase
    {
        public ObservableCollection<StorageLocationRow> LocationList { get; } = new();
        public ObservableCollection<LocationType> LocationTypes { get; } = new();
        public ObservableCollection<Equipment> EquipmentList { get; } = new();

        [ObservableProperty] private int? editingId;
        [ObservableProperty] private string formCode = "";
        [ObservableProperty] private string? formZone;
        [ObservableProperty] private LocationType formLocationType = LocationType.Buffer;
        [ObservableProperty] private int? formCapacity;
        [ObservableProperty] private int? formEquipmentId;
        [ObservableProperty] private bool formIsEnabled = true;

        // 更新時保留來源實體，回寫未在表單顯示的預留欄位（AmrStationCode/MapX/MapY）
        private StorageLocation? _editingSource;

        /// <summary>型別為機邊時才可綁定設備（供 XAML 條件啟用設備下拉）。</summary>
        public bool IsMachineSide => FormLocationType == LocationType.MachineSide;

        public LocationSettingViewModel(DashboardCoreServices core, Services.IDialogService dialog)
            : base(core, dialog) { }

        partial void OnFormLocationTypeChanged(LocationType value)
        {
            OnPropertyChanged(nameof(IsMachineSide));
            if (value != LocationType.MachineSide)
                FormEquipmentId = null;   // 非機邊倉位不綁設備
        }

        protected override async Task LoadAsync()
        {
            try
            {
                if (!LocationTypes.Any())
                    foreach (LocationType t in Enum.GetValues(typeof(LocationType)))
                        LocationTypes.Add(t);

                if (!EquipmentList.Any())
                {
                    var equipments = await _core.Data.GetAllEquipmentAsync();
                    foreach (var e in equipments) EquipmentList.Add(e);
                }

                var locations = await _core.Warehouse.GetLocationsAsync();
                var occupancy = await _core.Warehouse.GetOccupancyCountsAsync();
                var equipmentNames = EquipmentList.ToDictionary(e => e.Id, e => e.Name);

                LocationList.Clear();
                foreach (var loc in locations)
                {
                    occupancy.TryGetValue(loc.LocationId, out var count);
                    string? eqName = loc.EquipmentId is int eid && equipmentNames.TryGetValue(eid, out var n) ? n : null;
                    LocationList.Add(new StorageLocationRow(loc, count, eqName));
                }
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog("[倉位] 載入倉位清單失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[LoadAsync] {ex.Message}");
            }
        }

        protected override void OpenNewForm()
        {
            ClearForm();
            FormLocationType = LocationType.Buffer;
            FormIsEnabled = true;
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private void Edit(StorageLocationRow row)
        {
            var loc = row.Source;
            _editingSource = loc;
            EditingId = loc.LocationId;
            FormCode = loc.Code;
            FormZone = loc.Zone;
            FormLocationType = loc.LocationType;
            FormCapacity = loc.Capacity;
            FormEquipmentId = loc.EquipmentId;
            FormIsEnabled = loc.IsEnabled;
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private async Task Delete(StorageLocationRow row)
        {
            if (!ShowConfirm($"{Properties.Resources.DialogBaseConfirm} {Properties.Resources.DialogBaseDelete}?"))
                return;
            try
            {
                await _core.Warehouse.DeleteLocationAsync(row.Source.LocationId);
                LocationList.Remove(row);
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog("[倉位] 刪除倉位失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[Delete] {ex.Message}");
            }
        }

        protected override async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(FormCode))
            { FormErrorString = Properties.Resources.WarehouseValidationCodeRequired; return; }

            try
            {
                if (EditingId == null || _editingSource == null)
                {
                    var location = new StorageLocation();
                    ApplyFormTo(location);
                    await _core.Warehouse.AddLocationAsync(location);
                    FormSuccessString = Properties.Resources.SettingSuccessAdd;
                }
                else
                {
                    ApplyFormTo(_editingSource);   // 只覆蓋表單欄位，保留 AmrStationCode/MapX/MapY
                    await _core.Warehouse.UpdateLocationAsync(_editingSource);
                    FormSuccessString = Properties.Resources.SettingSuccessUpdate;
                }
                FormErrorString = null;
                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                FormSuccessString = null;
                _core.Log.AddLog("[倉位] 儲存倉位失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[SaveAsync] {ex.Message}");
            }
        }

        private void ApplyFormTo(StorageLocation location)
        {
            location.Code = FormCode.Trim();
            location.Zone = FormZone;
            location.LocationType = FormLocationType;
            location.Capacity = FormCapacity;
            location.EquipmentId = IsMachineSide ? FormEquipmentId : null;
            location.IsEnabled = FormIsEnabled;
        }

        protected override void ClearForm()
        {
            EditingId = null;
            _editingSource = null;
            FormCode = "";
            FormZone = null;
            FormLocationType = LocationType.Buffer;
            FormCapacity = null;
            FormEquipmentId = null;
            FormIsEnabled = true;
        }
    }
}
