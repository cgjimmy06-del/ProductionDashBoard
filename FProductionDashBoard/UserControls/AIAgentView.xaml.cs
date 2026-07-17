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
