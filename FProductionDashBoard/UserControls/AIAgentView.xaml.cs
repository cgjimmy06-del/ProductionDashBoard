using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace FProductionDashBoard.UserControls
{
    public partial class AIAgentView : UserControl
    {
        private ChatSession? _subscribedSession;

        public AIAgentView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is AIAgentViewModel oldVm)
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;

            if (e.NewValue is AIAgentViewModel newVm)
            {
                newVm.PropertyChanged += OnViewModelPropertyChanged;
                SubscribeToCurrentSession(newVm.CurrentSession);
            }
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AIAgentViewModel.CurrentSession)
                && sender is AIAgentViewModel vm)
                SubscribeToCurrentSession(vm.CurrentSession);
        }

        private void SubscribeToCurrentSession(ChatSession? session)
        {
            if (_subscribedSession != null)
                _subscribedSession.Messages.CollectionChanged -= OnMessagesChanged;

            _subscribedSession = session;

            if (session != null)
            {
                session.Messages.CollectionChanged += OnMessagesChanged;
                Dispatcher.InvokeAsync(MessageScrollViewer.ScrollToBottom, DispatcherPriority.Background);
            }
        }

        private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // FlowDocument 排版延後執行，須以低優先權排入捲動，否則會捲不到底
            if (e.Action == NotifyCollectionChangedAction.Add)
                Dispatcher.InvokeAsync(MessageScrollViewer.ScrollToBottom, DispatcherPriority.Background);
        }

        // WPF 文字引擎對「按在既有選取範圍內」會進入拖放文字待命模式並吞掉這次拖曳，
        // 造成選取卡在舊範圍無法重選；按下左鍵時先收合既有選取，讓每次拖曳都重新開始
        private void MarkdownViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FlowDocumentScrollViewer viewer && viewer.Selection is { IsEmpty: false } selection)
                selection.Select(selection.Start, selection.Start);
        }

        // FlowDocumentScrollViewer 內部會吞掉 MouseWheel，一律轉發給外層訊息清單捲動
        private void MarkdownViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;
            e.Handled = true;
            MessageScrollViewer.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            });
        }
    }
}
