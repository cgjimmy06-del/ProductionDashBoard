using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using FProductionDashBoard.UiModels;
using System;
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
        public int StartedBy { get; set; }        // 執行人（下拉選定）
        public bool IsForceArrange { get; set; }  // 強制安排（Commit 3 toggle 才會設 true）
    }

    public partial class TuningDialogViewModel : DialogBaseViewModel<TuningResult>
    {
        private const string LastTeachingPlaceholder = "—";

        public string CurrentDevice { get; }
        public string CurrentUser { get; }
        public bool CanForceArrange { get; }

        private readonly IList<EquipmentProductItem> _allItems;
        private readonly IList<UserInfo> _allEmployees;
        private readonly IReadOnlyDictionary<int, string> _lastTeachingMap;

        public ObservableCollection<EquipmentProductItem> FilteredEquipmentProducts { get; } = new();
        public ObservableCollection<UserInfo> FilteredEmployees { get; } = new();

        [ObservableProperty] private EquipmentProductItem? selectedEquipmentProduct;
        [ObservableProperty] private int tuningMode = (int)TuningType.Teaching;
        [ObservableProperty] private UserInfo? selectedEmployee;
        [ObservableProperty] private string employeeFilterText = string.Empty;
        [ObservableProperty] private string teachedUserName = LastTeachingPlaceholder;

        public ICommand TeachingCommand { get; }
        public ICommand OffsetCommand { get; }

        public TuningDialogViewModel(
            string device,
            string user,
            IList<EquipmentProductItem> items,
            IList<UserInfo> employees,
            int currentUserId,
            IReadOnlyDictionary<int, string> lastTeachingMap,
            bool canForceArrange)
            : base(Properties.Resources.TuningDialogTitle)
        {
            CurrentDevice = device;
            CurrentUser = user;
            _allItems = items;
            _allEmployees = employees;
            _lastTeachingMap = lastTeachingMap;
            CanForceArrange = canForceArrange;

            TeachingCommand = new RelayCommand(SelectTeaching);
            OffsetCommand = new RelayCommand(SelectOffset);
            ConfirmCommand = new RelayCommand(() => OnConfirm(),
                () => SelectedEquipmentProduct != null && SelectedEmployee != null);

            RefilterProducts();
            RefilterEmployees();
            SelectedEmployee = _allEmployees.FirstOrDefault(e => e.Id == currentUserId);
        }

        partial void OnSelectedEquipmentProductChanged(EquipmentProductItem? value)
        {
            ((RelayCommand)ConfirmCommand).NotifyCanExecuteChanged();
            TeachedUserName = value != null
                && _lastTeachingMap.TryGetValue(value.EquipmentProductId, out var name)
                && !string.IsNullOrEmpty(name)
                    ? name
                    : LastTeachingPlaceholder;
        }

        partial void OnSelectedEmployeeChanged(UserInfo? value)
            => ((RelayCommand)ConfirmCommand).NotifyCanExecuteChanged();

        partial void OnTuningModeChanged(int value) => RefilterProducts();

        partial void OnEmployeeFilterTextChanged(string value) => RefilterEmployees();

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

        private void RefilterEmployees()
        {
            FilteredEmployees.Clear();
            var kw = EmployeeFilterText?.Trim() ?? string.Empty;
            IEnumerable<UserInfo> source = _allEmployees;
            if (kw.Length > 0)
                source = source.Where(e =>
                    (e.Name?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (e.UserId?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false));
            foreach (var e in source)
                FilteredEmployees.Add(e);
        }

        private void SelectTeaching() => TuningMode = (int)TuningType.Teaching;
        private void SelectOffset()   => TuningMode = (int)TuningType.Offset;

        protected override void OnConfirm()
        {
            if (SelectedEquipmentProduct == null || SelectedEmployee == null) return;
            Result = new TuningResult
            {
                TuningType = (TuningType)TuningMode,
                EquipmentProductId = SelectedEquipmentProduct.EquipmentProductId,
                StartedBy = SelectedEmployee.Id,
                IsForceArrange = false
            };
            base.OnConfirm();
        }
    }
}
