using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace FProductionDashBoard.ViewModels
{
    public class TuningResult
    {
        public TuningType TuningType { get; set; }
        public int EquipmentProductId { get; set; }
    }

    public partial class TuningDialogViewModel : DialogBaseViewModel<TuningResult>
    {
        public string CurrentDevice { get; }
        public string CurrentUser { get; }

        private readonly IList<EquipmentProductItem> _allItems;

        public ObservableCollection<EquipmentProductItem> FilteredEquipmentProducts { get; } = new();

        [ObservableProperty] private EquipmentProductItem? selectedEquipmentProduct;
        [ObservableProperty] private int tuningMode = (int)TuningType.Teaching;

        public ICommand TeachingCommand { get; }
        public ICommand OffsetCommand { get; }

        public TuningDialogViewModel(string device, string user, IList<EquipmentProductItem> items)
            : base(Properties.Resources.TuningDialogTitle)
        {
            CurrentDevice = device;
            CurrentUser = user;
            _allItems = items;

            TeachingCommand = new RelayCommand(SelectTeaching);
            OffsetCommand = new RelayCommand(SelectOffset);
            ConfirmCommand = new RelayCommand(() => OnConfirm(), () => SelectedEquipmentProduct != null);

            RefilterProducts();
        }

        partial void OnSelectedEquipmentProductChanged(EquipmentProductItem? value)
            => ((RelayCommand)ConfirmCommand).NotifyCanExecuteChanged();

        partial void OnTuningModeChanged(int value) => RefilterProducts();

        private void RefilterProducts()
        {
            SelectedEquipmentProduct = null;
            FilteredEquipmentProducts.Clear();
            var targetStatus = TuningMode == (int)TuningType.Teaching
                ? TuningType.Teaching
                : TuningType.Offset;
            foreach (var item in _allItems.Where(i => i.ProductionStatus == targetStatus))
                FilteredEquipmentProducts.Add(item);
        }

        private void SelectTeaching() => TuningMode = (int)TuningType.Teaching;
        private void SelectOffset()   => TuningMode = (int)TuningType.Offset;

        protected override void OnConfirm()
        {
            if (SelectedEquipmentProduct == null) return;
            Result = new TuningResult
            {
                TuningType = (TuningType)TuningMode,
                EquipmentProductId = SelectedEquipmentProduct.EquipmentProductId
            };
            base.OnConfirm();
        }
    }
}
