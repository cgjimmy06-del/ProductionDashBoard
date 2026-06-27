using CommunityToolkit.Mvvm.ComponentModel;
using FProductionDashBoard.Models;
using FProductionDashBoard.Models.Extra;

namespace FProductionDashBoard.UiModels
{
    public enum MesSyncState
    {
        Synced,
        MesOnly,
        LocalOnly,
        DataMismatch,
    }

    public partial class MesSyncItemUiModel : ObservableObject
    {
        public string       DeviceId       { get; }
        public string       Name           { get; }
        public string       Ip             { get; }
        public string       Group          { get; }
        public MesSyncState SyncState      { get; }
        public Equipment?   LocalEquipment { get; }
        public MesDevice?   MesDevice      { get; }
        public bool         IsEnabled      => SyncState != MesSyncState.Synced;

        [ObservableProperty]
        private bool isSelected;

        public MesSyncItemUiModel(string deviceId, string name, string ip, string group,
            MesSyncState syncState, Equipment? localEquipment = null, MesDevice? mesDevice = null)
        {
            DeviceId       = deviceId;
            Name           = name;
            Ip             = ip;
            Group          = group;
            SyncState      = syncState;
            LocalEquipment = localEquipment;
            MesDevice      = mesDevice;
            isSelected     = syncState != MesSyncState.Synced;
        }
    }
}
