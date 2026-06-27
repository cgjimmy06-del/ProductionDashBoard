using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using FProductionDashBoard.Models.Extra;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace FProductionDashBoard.ViewModels
{
    public partial class MesSyncDialogViewModel : DialogBaseViewModel<IEnumerable<MesSyncItemUiModel>?>
    {
        private readonly IDataService _data;
        private readonly ObservableCollection<MesSyncItemUiModel> _allItems = new();

        public ICollectionView FilteredItems { get; }

        [ObservableProperty] private bool onlyUnsynced;
        [ObservableProperty] private string? groupFilter;

        public IReadOnlyList<string> GroupOptions { get; }

        public IAsyncRelayCommand LoadCommand { get; }
        public IRelayCommand SelectAllCommand { get; }
        public IRelayCommand ClearAllCommand { get; }

        public MesSyncDialogViewModel(IDataService data) : base(Properties.Resources.MesSyncDialog_Title)
        {
            _data = data;

            FilteredItems = CollectionViewSource.GetDefaultView(_allItems);
            FilteredItems.Filter = ApplyFilter;

            GroupOptions = new[] { string.Empty, "Robot", "PLC", "AGV", "Meter" };

            LoadCommand    = new AsyncRelayCommand(LoadAsync);
            SelectAllCommand  = new RelayCommand(() => SetAllSelectable(true));
            ClearAllCommand   = new RelayCommand(() => SetAllSelectable(false));
        }

        partial void OnOnlyUnsyncedChanged(bool value) => FilteredItems.Refresh();
        partial void OnGroupFilterChanged(string? value) => FilteredItems.Refresh();

        private bool ApplyFilter(object obj)
        {
            if (obj is not MesSyncItemUiModel item) return false;
            if (OnlyUnsynced && item.SyncState == MesSyncState.Synced) return false;
            if (!string.IsNullOrEmpty(GroupFilter) && item.Group != GroupFilter) return false;
            return true;
        }

        private async Task LoadAsync()
        {
            try
            {
                var mesList   = (await _data.GetAllMesDevicesAsync().ConfigureAwait(false)).ToList();
                var localList = (await _data.GetAllEquipmentAsync().ConfigureAwait(false)).ToList();

                var mesDict   = mesList.ToDictionary(m => m.DeviceId);
                var localDict = localList.ToDictionary(e => e.Code);

                _allItems.Clear();

                var allKeys = mesDict.Keys.Union(localDict.Keys).Distinct();
                foreach (var key in allKeys.OrderBy(k => k))
                {
                    mesDict.TryGetValue(key,   out var mes);
                    localDict.TryGetValue(key, out var local);

                    MesSyncState state;
                    string name, ip, group;

                    if (mes != null && local != null)
                    {
                        state = MesEquipmentMapper.IsSynced(local, mes) ? MesSyncState.Synced : MesSyncState.DataMismatch;
                        name  = local.Name;
                        ip    = local.Ip;
                        group = mes.Group;
                    }
                    else if (mes != null)
                    {
                        state = MesSyncState.MesOnly;
                        name  = mes.Name;
                        ip    = mes.Ip;
                        group = mes.Group;
                    }
                    else
                    {
                        state = MesSyncState.LocalOnly;
                        name  = local!.Name;
                        ip    = local.Ip;
                        group = local.TypeId.HasValue ? GetGroupFromTypeId(local.TypeId.Value) : string.Empty;
                    }

                    _allItems.Add(new MesSyncItemUiModel(key, name, ip, group, state, local, mes));
                }
            }
            catch (Exception ex)
            {
                DialogErrorString = ex.Message;
            }
        }

        private static string GetGroupFromTypeId(int typeId) => typeId switch
        {
            1 => "Robot",
            2 => "PLC",
            3 => "AGV",
            4 => "Meter",
            _ => string.Empty,
        };

        private void SetAllSelectable(bool selected)
        {
            foreach (var item in _allItems.Where(i => i.IsEnabled))
                item.IsSelected = selected;
        }

        protected override void OnConfirm()
        {
            Result = _allItems.Where(i => i.IsSelected && i.IsEnabled).ToList();
            base.OnConfirm();
        }
    }
}
