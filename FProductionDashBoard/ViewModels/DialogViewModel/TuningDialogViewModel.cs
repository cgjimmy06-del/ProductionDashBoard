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
        public UserInfo Executor { get; set; } = null!;  // 執行人（下拉選定，OnConfirm 保證非 null）
        public bool IsForceArrange { get; set; }         // 強制安排
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

        // 強制安排：跨程式當前狀態挑選任一程式（僅 Setting 權限可用）
        [ObservableProperty] private bool isForceArrange;
        [ObservableProperty] private string keywordFilter = string.Empty;
        [ObservableProperty] private TuningType? statusFilter;
        public IReadOnlyList<TuningTypeFilterOption> TuningTypeFilterOptions { get; }

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

            TuningTypeFilterOptions = new TuningTypeFilterOption[] { new(null) }
                .Concat(Enum.GetValues<TuningType>().Select(t => new TuningTypeFilterOption(t)))
                .ToArray();

            TeachingCommand = new RelayCommand(SelectTeaching);
            OffsetCommand = new RelayCommand(SelectOffset);
            ConfirmCommand = new RelayCommand(() => OnConfirm(),
                () => SelectedEquipmentProduct != null && SelectedEmployee != null);

            RefilterProducts();
            RefilterEmployees();
            // 預設選當前人員；若當前人員不在清單（如 admin/visitor）則保留 RefilterEmployees 選定的第一項
            var current = _allEmployees.FirstOrDefault(e => e.Id == currentUserId);
            if (current != null)
                SelectedEmployee = current;
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

        partial void OnIsForceArrangeChanged(bool value) => RefilterProducts();

        partial void OnKeywordFilterChanged(string value)
        {
            if (IsForceArrange) RefilterProducts();
        }

        partial void OnStatusFilterChanged(TuningType? value)
        {
            if (IsForceArrange) RefilterProducts();
        }

        private void RefilterProducts()
        {
            var previous = SelectedEquipmentProduct;
            SelectedEquipmentProduct = null;
            FilteredEquipmentProducts.Clear();

            IEnumerable<EquipmentProductItem> source;
            if (IsForceArrange)
            {
                // 強制安排：列全部品項，套關鍵字（廠牌/件號/型號/工序）與狀態篩選；
                // 帶點/調品質按鈕此時只決定確認後的目標狀態，不影響清單
                source = _allItems;
                var kw = KeywordFilter?.Trim() ?? string.Empty;
                if (kw.Length > 0)
                    source = source.Where(i =>
                        i.Brand.Contains(kw, StringComparison.OrdinalIgnoreCase)
                        || i.PartNo.Contains(kw, StringComparison.OrdinalIgnoreCase)
                        || i.Model.Contains(kw, StringComparison.OrdinalIgnoreCase)
                        || i.Process.Contains(kw, StringComparison.OrdinalIgnoreCase));
                if (StatusFilter != null)
                    source = source.Where(i => i.ProductionStatus == StatusFilter);
            }
            else
            {
                var targetStatus = TuningMode == (int)TuningType.Teaching
                    ? TuningType.Teaching
                    : TuningType.Offset;
                source = _allItems.Where(i => i.ProductionStatus == targetStatus);
            }

            foreach (var item in source)
                FilteredEquipmentProducts.Add(item);

            // 原選取仍在結果內則保留（比照執行人員篩選行為）
            if (previous != null && FilteredEquipmentProducts.Contains(previous))
                SelectedEquipmentProduct = previous;
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

            // 篩選後預設選第一項，避免原選取被濾掉時變空白
            if (SelectedEmployee == null || !FilteredEmployees.Contains(SelectedEmployee))
                SelectedEmployee = FilteredEmployees.FirstOrDefault();
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
                Executor = SelectedEmployee,
                IsForceArrange = IsForceArrange
            };
            base.OnConfirm();
        }
    }
}
