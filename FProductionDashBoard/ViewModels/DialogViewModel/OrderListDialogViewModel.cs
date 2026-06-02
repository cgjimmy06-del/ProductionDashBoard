using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using FProductionDashBoard.Properties;
using FProductionDashBoard.Services;
using FProductionDashBoard.Services.V1;
using FProductionDashBoard.UiModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FProductionDashBoard.ViewModels
{
    // ─── Enums / Result ───────────────────────────────────────────────────────

    public enum OrderListPage { OrderList, QuickOrder, SopChecklist, ProductionDetail }

    public enum OrderListAction { StartProduction, EndProduction, CancelProduction }

    public class OrderListResult
    {
        public OrderListAction Action { get; set; }
        public int OrderId { get; set; }
        public int SeqNo { get; set; }   // 開始生產時供卡片寫入 ABB 設備
        public string? Description { get; set; }
    }

    // ─── Inner UiModels ───────────────────────────────────────────────────────

    public class EquipmentProductItem
    {
        public int EquipmentProductId { get; set; }
        public int SopId { get; set; }
        public int SeqNo { get; set; }
        public string DisplayLabel { get; set; } = string.Empty;
        public TuningType ProductionStatus { get; set; }
    }

    public partial class SopChecklistDisplayItem : ObservableObject
    {
        public int ItemId { get; set; }
        public string DisplayText { get; set; } = string.Empty;
        [ObservableProperty] private bool isChecked = false;
    }

    // ─── ViewModel ────────────────────────────────────────────────────────────

    public partial class OrderListDialogViewModel : DialogBaseViewModel<OrderListResult>
    {
        private readonly DashboardCoreServices _core;
        private readonly int _equipmentId;
        private readonly UserInfo _currentUser;
        private int _pendingOrderId;
        private int _pendingSopId;
        private int _pendingSeqNo;

        // ─ Page 切換
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOnOrderList))]
        [NotifyPropertyChangedFor(nameof(IsOnQuickOrder))]
        [NotifyPropertyChangedFor(nameof(IsOnSopChecklist))]
        [NotifyPropertyChangedFor(nameof(IsOnProductionDetail))]
        private OrderListPage currentPage = OrderListPage.OrderList;

        public bool IsOnOrderList        => CurrentPage == OrderListPage.OrderList;
        public bool IsOnQuickOrder       => CurrentPage == OrderListPage.QuickOrder;
        public bool IsOnSopChecklist     => CurrentPage == OrderListPage.SopChecklist;
        public bool IsOnProductionDetail => CurrentPage == OrderListPage.ProductionDetail;

        // ─ OrderList Page
        public ObservableCollection<OrderProductionInfo> Orders { get; } = new();
        public bool HasSettingPermission => _core.Authorization.HasPermission(PermissionId.Setting);

        // ─ QuickOrder Page
        public List<EquipmentProductItem> EquipmentProducts { get; }
        public ObservableCollection<EquipmentProductItem> FilteredEquipmentProducts { get; } = new();
        [ObservableProperty] private string quickOrderFilterText = string.Empty;
        [ObservableProperty] private EquipmentProductItem? selectedEquipmentProduct;
        [ObservableProperty] private int? quantity;

        // ─ SopChecklist Page
        public ObservableCollection<SopChecklistDisplayItem> ChecklistItems { get; } = new();

        // ─ ProductionDetail Page
        [ObservableProperty] private OrderProductionInfo? activeOrder;
        [ObservableProperty] private bool isCancelInputVisible = false;
        [ObservableProperty] private string cancelReason = string.Empty;

        // ─ Commands
        public ICommand NavigateToQuickOrderCommand { get; }
        public ICommand NavigateToChecklistCommand { get; }
        public ICommand NavigateToDetailCommand { get; }
        public ICommand CancelPendingOrderCommand { get; }  // OrderList 直接取消 Pending 訂單
        public ICommand ShowCancelInputCommand { get; }
        public ICommand AbortCancelCommand { get; }
        public ICommand EndProductionCommand { get; }
        public ICommand ConfirmCancelCommand { get; }

        public OrderListDialogViewModel(
            DashboardCoreServices core,
            int equipmentId,
            List<EquipmentProduct> equipmentProducts,
            UserInfo currentUser,
            string dialogTitle) : base(dialogTitle)
        {
            _core = core;
            _equipmentId = equipmentId;
            _currentUser = currentUser;

            // 建立 QuickOrder 清單（此機台可生產品項，依 SeqNo 排序）
            EquipmentProducts = equipmentProducts
                .OrderBy(ep => ep.SeqNo)
                .Select(ep => new EquipmentProductItem
                {
                    EquipmentProductId = ep.EquipmentProductId,
                    SopId = ep.SopId,
                    SeqNo = ep.SeqNo,
                    DisplayLabel = BuildEquipmentProductLabel(ep),
                    ProductionStatus = ep.ProductionStatus
                })
                .ToList();
            RecomputeFilteredProducts();

            // 覆蓋 ConfirmCommand（async + CanExecute）
            ConfirmCommand = new AsyncRelayCommand(OnConfirmAsync, () => CanConfirm);
            CancelCommand  = new RelayCommand(OnCancelNav);

            NavigateToQuickOrderCommand  = new RelayCommand(NavigateToQuickOrder);
            NavigateToChecklistCommand   = new RelayCommand<OrderProductionInfo>(NavigateToChecklist);
            NavigateToDetailCommand      = new RelayCommand<OrderProductionInfo>(NavigateToDetail);
            CancelPendingOrderCommand    = new AsyncRelayCommand<OrderProductionInfo>(OnCancelPendingOrderAsync);
            ShowCancelInputCommand       = new RelayCommand(() => IsCancelInputVisible = true);
            AbortCancelCommand           = new RelayCommand(() => { IsCancelInputVisible = false; CancelReason = string.Empty; });
            EndProductionCommand         = new AsyncRelayCommand(OnEndProductionAsync);
            ConfirmCancelCommand         = new AsyncRelayCommand(OnConfirmCancelAsync);

            // IsConfirmVisible 初始為 false（OrderList Page）
            IsConfirmVisible = false;

            _ = LoadOrdersAsync();
        }

        // ─── Page 導航 ────────────────────────────────────────────────────────

        private void NavigateToQuickOrder()
        {
            SelectedEquipmentProduct = null;
            Quantity = null;
            QuickOrderFilterText = string.Empty;
            DialogErrorString = null;
            SetPage(OrderListPage.QuickOrder);
        }

        partial void OnQuickOrderFilterTextChanged(string value) => RecomputeFilteredProducts();

        private void RecomputeFilteredProducts()
        {
            FilteredEquipmentProducts.Clear();
            var filter = QuickOrderFilterText?.Trim();
            foreach (var item in EquipmentProducts)
            {
                if (string.IsNullOrEmpty(filter) ||
                    item.DisplayLabel.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    FilteredEquipmentProducts.Add(item);
            }
        }

        private void NavigateToChecklist(OrderProductionInfo? order)
        {
            if (order == null) return;

            // 同時一筆限制
            if (Orders.Any(o => o.IsInProduction))
            {
                DialogErrorString = Resources.OnlyOneProductionError;
                return;
            }

            DialogErrorString = null;
            _pendingOrderId = order.OrderId;
            _pendingSopId = order.SopId;
            _pendingSeqNo = order.SeqNo;
            _ = LoadChecklistAsync(_pendingSopId);
        }

        private void NavigateToDetail(OrderProductionInfo? order)
        {
            if (order == null) return;
            ActiveOrder = order;
            IsCancelInputVisible = false;
            CancelReason = string.Empty;
            DialogErrorString = null;
            SetPage(OrderListPage.ProductionDetail);
        }

        private void SetPage(OrderListPage page)
        {
            CurrentPage = page;
            IsConfirmVisible = page is OrderListPage.QuickOrder or OrderListPage.SopChecklist;
            NotifyCanExecuteChanged();
        }

        // ─── Confirm / Cancel 覆蓋 ────────────────────────────────────────────

        private bool CanConfirm => CurrentPage switch
        {
            OrderListPage.SopChecklist => ChecklistItems.Count > 0 && ChecklistItems.All(i => i.IsChecked),
            OrderListPage.QuickOrder   => SelectedEquipmentProduct != null,
            _                          => true
        };

        private async Task OnConfirmAsync()
        {
            switch (CurrentPage)
            {
                case OrderListPage.QuickOrder:
                    await AddOrderAsync();
                    break;
                case OrderListPage.SopChecklist:
                    Result = new OrderListResult
                    {
                        Action  = OrderListAction.StartProduction,
                        OrderId = _pendingOrderId,
                        SeqNo   = _pendingSeqNo
                    };
                    IsConfirmed = true;
                    OnRequestClose();
                    break;
            }
        }

        private void OnCancelNav()
        {
            if (CurrentPage == OrderListPage.OrderList)
            {
                IsConfirmed = false;
                OnRequestClose();
            }
            else
            {
                DialogErrorString = null;
                SetPage(OrderListPage.OrderList);
            }
        }

        // ─── QuickOrder：新增訂單 ─────────────────────────────────────────────

        private async Task AddOrderAsync()
        {
            if (SelectedEquipmentProduct == null) return;
            try
            {
                await _core.Data.AddOrderAsync(
                    _equipmentId,
                    SelectedEquipmentProduct.EquipmentProductId,
                    Quantity,
                    _core.Authorization.CurrentUser!.Id);

                _core.Log.AddLog($"訂單新增完成（SeqNo #{SelectedEquipmentProduct.SeqNo}）");
                await LoadOrdersAsync();
                SetPage(OrderListPage.OrderList);
            }
            catch (Exception ex)
            {
                DialogErrorString = ex.Message;
                _core.Log.AddErrorLog($"[AddOrderAsync] {ex.Message}");
            }
        }

        // ─── SopChecklist：載入點檢項目 ───────────────────────────────────────

        private async Task LoadChecklistAsync(int sopId)
        {
            try
            {
                var sop = await _core.Data.GetSopChecklistWithItemsAsync(sopId);
                if (sop?.Items == null) return;

                // 清除舊訂閱
                foreach (var old in ChecklistItems)
                    old.PropertyChanged -= OnChecklistItemChanged;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ChecklistItems.Clear();
                    foreach (var item in sop.Items.OrderBy(i => i.Seq))
                    {
                        var display = new SopChecklistDisplayItem
                        {
                            ItemId      = item.ItemId,
                            DisplayText = BuildChecklistDisplayText(item)
                        };
                        display.PropertyChanged += OnChecklistItemChanged;
                        ChecklistItems.Add(display);
                    }
                });

                SetPage(OrderListPage.SopChecklist);
            }
            catch (Exception ex)
            {
                DialogErrorString = ex.Message;
                _core.Log.AddErrorLog($"[LoadChecklistAsync] {ex.Message}");
            }
        }

        private void OnChecklistItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SopChecklistDisplayItem.IsChecked))
                NotifyCanExecuteChanged();
        }

        // ─── OrderList：直接取消 Pending 訂單 ────────────────────────────────────

        private async Task OnCancelPendingOrderAsync(OrderProductionInfo? order)
        {
            if (order == null || order.IsInProduction) return;

            var confirm = MessageBox.Show(
                $"#{order.SeqNo} {order.ProductName}\n{Resources.ConfirmCancelOrder}",
                Resources.ConfirmCancelOrder,
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var description = $"由{_currentUser.Name}取消訂單";
                await _core.Data.CancelOrderAsync(order.OrderId, description);
                _core.Log.AddLog($"訂單 #{order.OrderId} 取消完成");
                await LoadOrdersAsync();
            }
            catch (Exception ex)
            {
                DialogErrorString = ex.Message;
                _core.Log.AddErrorLog($"[CancelPendingOrderAsync] {ex.Message}");
            }
        }

        // ─── ProductionDetail：結束 / 取消 ────────────────────────────────────

        private async Task OnEndProductionAsync()
        {
            if (ActiveOrder == null) return;

            var confirm = MessageBox.Show(
                $"#{ActiveOrder.SeqNo} {ActiveOrder.ProductName}\n{Resources.ConfirmEndOrder}",
                Resources.ConfirmEndOrder,
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            Result = new OrderListResult
            {
                Action  = OrderListAction.EndProduction,
                OrderId = ActiveOrder.OrderId
            };
            IsConfirmed = true;
            OnRequestClose();
            await Task.CompletedTask;
        }

        private async Task OnConfirmCancelAsync()
        {
            if (ActiveOrder == null) return;

            var description = string.IsNullOrWhiteSpace(CancelReason)
                ? $"由{_currentUser.Name}取消訂單"
                : $"{_currentUser.Name}：{CancelReason}";

            Result = new OrderListResult
            {
                Action      = OrderListAction.CancelProduction,
                OrderId     = ActiveOrder.OrderId,
                Description = description
            };
            IsConfirmed = true;
            OnRequestClose();
            await Task.CompletedTask;
        }

        // ─── 訂單載入 ─────────────────────────────────────────────────────────

        private async Task LoadOrdersAsync()
        {
            try
            {
                var entities = await _core.Data.GetOrdersByEquipmentAsync(_equipmentId);
                var mapped   = entities.Select(OrderProductionInfo.FromEntity).ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Orders.Clear();
                    foreach (var o in mapped) Orders.Add(o);
                });
            }
            catch (Exception ex)
            {
                _core.Log.AddErrorLog($"[OrderListDialog.LoadOrdersAsync] {ex.Message}");
            }
        }

        // ─── 輔助方法 ─────────────────────────────────────────────────────────

        private void NotifyCanExecuteChanged()
        {
            if (ConfirmCommand is AsyncRelayCommand arc)
                arc.NotifyCanExecuteChanged();
        }

        partial void OnSelectedEquipmentProductChanged(EquipmentProductItem? value)
            => NotifyCanExecuteChanged();

        private static string BuildEquipmentProductLabel(EquipmentProduct ep)
        {
            var product = ep.Sop?.Product;
            var productName = product != null
                ? $"{product.Part?.PartNo}_{product.Model?.Name}"
                : string.Empty;
            var processName = ep.Sop?.Process?.Name ?? string.Empty;
            return $"#{ep.SeqNo}  {productName} · {processName}";
        }

        private static string BuildChecklistDisplayText(SopChecklistItem item) =>
            item.CheckType switch
            {
                CheckType.Station  => $"工位 #{item.WorkstationNo} - {item.Material?.Name ?? "(無)"}",
                CheckType.Fixture  => $"治夾具 - {item.Material?.Name ?? "(無)"}",
                CheckType.Quantity => $"數量 {item.Quantity} 件",
                _                  => item.Content ?? item.Remark ?? "(無說明)"
            };
    }
}
