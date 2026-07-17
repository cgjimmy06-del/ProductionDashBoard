using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Services;
using FProductionDashBoard.Services.AiTools;
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
        private readonly AiAgentToolService _toolService;
        private CancellationTokenSource? _cts;

        #region -- 模型設定 --
        public string[] AvailableModels => _aiChatService.AvailableModels;

        [ObservableProperty] private string selectedModel = "";
        #endregion

        #region -- 對話狀態 --
        [ObservableProperty] private ChatSession currentSession = null!;
        [ObservableProperty] private string inputText = "";
        [ObservableProperty] private bool isTyping = false;
        [ObservableProperty] private string? configWarning;

        // 目前 session 累計 tokens 的顯示鏡像（ChatSession 為 POCO 無變更通知，故由 VM 同步）
        [ObservableProperty] private int sessionInputTokens;
        [ObservableProperty] private int sessionOutputTokens;

        partial void OnCurrentSessionChanged(ChatSession value)
        {
            SessionInputTokens  = value?.InputTokens  ?? 0;
            SessionOutputTokens = value?.OutputTokens ?? 0;
        }
        #endregion

        #region -- 歷史與分類 --
        [ObservableProperty] private bool isHistoryVisible = false;
        [ObservableProperty] private string searchText = "";
        [ObservableProperty] private string selectedMode = AiAgentMode.Chat;
        [ObservableProperty] private string selectedHistoryFilter = AiAgentMode.All;

        public ObservableCollection<ChatSession> AllSessions { get; } = new();
        public List<string> AvailableModes    { get; } = [AiAgentMode.Chat, AiAgentMode.Schedule];
        public List<string> HistoryFilterOptions { get; } = [AiAgentMode.All, AiAgentMode.Chat, AiAgentMode.Schedule];
        public ICollectionView FilteredSessions { get; }
        #endregion

        #region -- Commands --
        public IRelayCommand SendCommand          { get; }
        public IRelayCommand CancelCommand        { get; }
        public IRelayCommand NewSessionCommand    { get; }
        public IRelayCommand ToggleHistoryCommand { get; }
        public IRelayCommand<ChatSession> LoadSessionCommand       { get; }
        public IRelayCommand<ChatSession> ToggleStarCommand        { get; }
        public IRelayCommand<string> SelectModeCommand             { get; }
        public IRelayCommand<string> SelectHistoryFilterCommand    { get; }
        #endregion

        public AIAgentViewModel(IAiChatService aiChatService, DashboardCoreServices core, AiAgentToolService toolService)
        {
            _aiChatService = aiChatService;
            _core = core;
            _toolService = toolService;
            SelectedModel = _aiChatService.AvailableModels.FirstOrDefault() ?? "";

            SendCommand             = new AsyncRelayCommand(SendAsync, () => !string.IsNullOrWhiteSpace(InputText) && !IsTyping);
            CancelCommand           = new RelayCommand(() => _cts?.Cancel(), () => IsTyping);
            NewSessionCommand       = new RelayCommand(StartNewSession);
            ToggleHistoryCommand    = new RelayCommand(() => IsHistoryVisible = !IsHistoryVisible);
            LoadSessionCommand      = new RelayCommand<ChatSession>(LoadSession);
            ToggleStarCommand       = new RelayCommand<ChatSession>(s => { if (s != null) s.IsStarred = !s.IsStarred; });
            SelectModeCommand          = new RelayCommand<string>(mode   => { if (mode   != null) SelectedMode          = mode;   });
            SelectHistoryFilterCommand = new RelayCommand<string>(filter => { if (filter != null) SelectedHistoryFilter  = filter; });

            FilteredSessions = CollectionViewSource.GetDefaultView(AllSessions);
            FilteredSessions.Filter = FilterSession;

            StartNewSession();

            if (!_aiChatService.IsConfigured)
                ConfigWarning = Properties.Resources.AiNotConfigured;
        }

        partial void OnSearchTextChanged(string value)             => FilteredSessions.Refresh();
        partial void OnSelectedHistoryFilterChanged(string value)  => FilteredSessions.Refresh();
        partial void OnInputTextChanged(string value)              => SendCommand.NotifyCanExecuteChanged();
        partial void OnIsTypingChanged(bool value)
        {
            SendCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }

        private bool FilterSession(object obj)
        {
            if (obj is not ChatSession s) return false;
            bool catMatch = SelectedHistoryFilter == "All" || s.Category == SelectedHistoryFilter;
            bool searchMatch = string.IsNullOrWhiteSpace(SearchText)
                || s.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            return catMatch && searchMatch;
        }

        private void StartNewSession()
        {
            var session = new ChatSession
            {
                Title     = Properties.Resources.AiDefaultTitle,
                ModelUsed = SelectedModel,
                LastTime  = DateTime.Now,
                Category  = SelectedMode
            };
            session.Messages.Add(new ChatMessage
            {
                Sender   = ChatSender.Ai,
                Content  = Properties.Resources.AiWelcome,
                Time     = DateTime.Now,
                IsUiOnly = true
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

            if (_aiChatService.IsConfigured) ConfigWarning = null;

            CurrentSession.Messages.Add(new ChatMessage
            {
                Sender  = ChatSender.User,
                Content = text,
                Time    = DateTime.Now
            });

            if (CurrentSession.Title == Properties.Resources.AiDefaultTitle && CurrentSession.Messages.Count == 2)
                CurrentSession.Title = text.Length > 20 ? text[..20] + "…" : text;

            CurrentSession.ModelUsed = SelectedModel;
            CurrentSession.LastTime  = DateTime.Now;
            InputText = "";
            IsTyping  = true;

            var typing = new ChatMessage { Sender = ChatSender.Ai, IsTyping = true, Time = DateTime.Now };
            CurrentSession.Messages.Add(typing);

            _cts = new CancellationTokenSource();
            try
            {
                var systemPrompt = _toolService.GetSystemPromptForMode(SelectedMode);
                var modeTools    = _toolService.GetToolsForMode(SelectedMode);
                var result = await _aiChatService.SendAsync(
                    SelectedModel,
                    CurrentSession.Messages.Where(m => !m.IsTyping && !m.IsUiOnly).SkipLast(1),
                    text,
                    modeTools.Count > 0 ? modeTools : null,
                    systemPrompt,
                    _cts.Token);

                CurrentSession.Messages.Remove(typing);
                CurrentSession.Messages.Add(new ChatMessage
                {
                    Sender  = ChatSender.Ai,
                    Content = result.Reply,
                    Time    = DateTime.Now
                });
                CurrentSession.InputTokens  += result.InputTokens;
                CurrentSession.OutputTokens += result.OutputTokens;
                SessionInputTokens  = CurrentSession.InputTokens;
                SessionOutputTokens = CurrentSession.OutputTokens;
                ConfigWarning = null;
            }
            catch (OperationCanceledException)
            {
                CurrentSession.Messages.Remove(typing);
                CurrentSession.Messages.Add(new ChatMessage
                {
                    Sender  = ChatSender.Ai,
                    Content = Properties.Resources.AiCancelled,
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
                    Content = Properties.Resources.AiSendFailed,
                    Time    = DateTime.Now
                });
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                IsTyping = false;
            }
        }
    }
}
