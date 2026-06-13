using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Services;
using FProductionDashBoard.Services.WebApi;
using FProductionDashBoard.UiModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;

namespace FProductionDashBoard.ViewModels
{
    public partial class AIAgentViewModel : ObservableObject
    {
        private readonly IAiChatService _aiChatService;
        private readonly DashboardCoreServices _core;

        #region -- 模型設定 --
        public string[] AvailableModels { get; } =
        [
            "gpt-4o",
            "gpt-4o-mini",
            "gpt-4-turbo"
        ];

        [ObservableProperty] private string selectedModel = "gpt-4o";
        #endregion

        #region -- 對話狀態 --
        [ObservableProperty] private ChatSession currentSession = null!;
        [ObservableProperty] private string inputText = "";
        [ObservableProperty] private bool isTyping = false;
        [ObservableProperty] private string? configWarning;
        #endregion

        #region -- 歷史與分類 --
        [ObservableProperty] private bool isHistoryVisible = false;
        [ObservableProperty] private string searchText = "";
        [ObservableProperty] private string selectedCategory = "全部";

        public ObservableCollection<ChatSession> AllSessions { get; } = new();
        public ObservableCollection<string> Categories { get; } = new() { "全部", "排單分析", "程式碼", "設計討論" };
        public ICollectionView FilteredSessions { get; }
        #endregion

        #region -- Commands --
        public IRelayCommand SendCommand          { get; }
        public IRelayCommand NewSessionCommand    { get; }
        public IRelayCommand ToggleHistoryCommand { get; }
        public IRelayCommand<ChatSession> LoadSessionCommand   { get; }
        public IRelayCommand<ChatSession> ToggleStarCommand    { get; }
        public IRelayCommand<string> SelectCategoryChipCommand { get; }
        #endregion

        public AIAgentViewModel(IAiChatService aiChatService, DashboardCoreServices core)
        {
            _aiChatService = aiChatService;
            _core = core;

            SendCommand          = new AsyncRelayCommand(SendAsync, () => !string.IsNullOrWhiteSpace(InputText) && !IsTyping);
            NewSessionCommand    = new RelayCommand(StartNewSession);
            ToggleHistoryCommand = new RelayCommand(() => IsHistoryVisible = !IsHistoryVisible);
            LoadSessionCommand   = new RelayCommand<ChatSession>(LoadSession);
            ToggleStarCommand    = new RelayCommand<ChatSession>(s => { if (s != null) s.IsStarred = !s.IsStarred; });
            SelectCategoryChipCommand = new RelayCommand<string>(cat => { if (cat != null) SelectedCategory = cat; });

            FilteredSessions = CollectionViewSource.GetDefaultView(AllSessions);
            FilteredSessions.Filter = FilterSession;

            StartNewSession();

            if (!_aiChatService.IsConfigured)
                ConfigWarning = "AI 功能尚未啟用";
        }

        partial void OnSearchTextChanged(string value)        => FilteredSessions.Refresh();
        partial void OnSelectedCategoryChanged(string value)  => FilteredSessions.Refresh();
        partial void OnInputTextChanged(string value)         => SendCommand.NotifyCanExecuteChanged();
        partial void OnIsTypingChanged(bool value)            => SendCommand.NotifyCanExecuteChanged();

        private bool FilterSession(object obj)
        {
            if (obj is not ChatSession s) return false;
            bool catMatch = SelectedCategory == "全部" || s.Category == SelectedCategory;
            bool searchMatch = string.IsNullOrWhiteSpace(SearchText)
                || s.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            return catMatch && searchMatch;
        }

        private void StartNewSession()
        {
            var session = new ChatSession
            {
                Title     = "新對話",
                ModelUsed = SelectedModel,
                LastTime  = DateTime.Now
            };
            session.Messages.Add(new ChatMessage
            {
                Sender  = ChatSender.Ai,
                Content = "您好！我是 AI 助理，可以協助分析程式碼、討論架構設計或回答開發問題。",
                Time    = DateTime.Now
            });
            CurrentSession = session;
            AllSessions.Insert(0, session);
            IsHistoryVisible = false;
        }

        private void LoadSession(ChatSession? session)
        {
            if (session == null) return;
            CurrentSession = session;
            IsHistoryVisible = false;
        }

        private async Task SendAsync()
        {
            var text = InputText.Trim();
            if (string.IsNullOrEmpty(text)) return;

            CurrentSession.Messages.Add(new ChatMessage
            {
                Sender  = ChatSender.User,
                Content = text,
                Time    = DateTime.Now
            });

            if (CurrentSession.Title == "新對話" && CurrentSession.Messages.Count == 2)
                CurrentSession.Title = text.Length > 20 ? text[..20] + "…" : text;

            CurrentSession.ModelUsed = SelectedModel;
            CurrentSession.LastTime  = DateTime.Now;
            InputText = "";
            IsTyping  = true;

            var typing = new ChatMessage { Sender = ChatSender.Ai, IsTyping = true, Time = DateTime.Now };
            CurrentSession.Messages.Add(typing);

            try
            {
                var reply = await _aiChatService.SendAsync(
                    SelectedModel,
                    CurrentSession.Messages.Where(m => !m.IsTyping).SkipLast(1),
                    text);

                CurrentSession.Messages.Remove(typing);
                CurrentSession.Messages.Add(new ChatMessage
                {
                    Sender  = ChatSender.Ai,
                    Content = reply,
                    Time    = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[AI 助理] 送出訊息失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[SendAsync] {ex.Message}");
                CurrentSession.Messages.Remove(typing);
                CurrentSession.Messages.Add(new ChatMessage
                {
                    Sender  = ChatSender.Ai,
                    Content = ex.Message.Contains("逾時") ? "請求逾時，請重試" : "訊息傳送失敗，請稍後再試",
                    Time    = DateTime.Now
                });
            }
            finally
            {
                IsTyping = false;
            }
        }
    }
}
