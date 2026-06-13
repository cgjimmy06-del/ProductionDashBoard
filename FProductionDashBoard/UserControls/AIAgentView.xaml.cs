using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

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
                session.Messages.CollectionChanged += OnMessagesChanged;
        }

        private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
                MessageScrollViewer.ScrollToBottom();
        }
    }
}
