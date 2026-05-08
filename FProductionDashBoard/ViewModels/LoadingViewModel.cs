using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FProductionDashBoard.ViewModels
{
    public enum LoadingMode { Processing, LogoutCountdown, CardReader }

    public partial class LoadingViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSpinning))]
        [NotifyPropertyChangedFor(nameof(CountdownVisibility))]
        [NotifyPropertyChangedFor(nameof(ProgressBarVisibility))]
        private LoadingMode mode = LoadingMode.Processing;

        [ObservableProperty] private string message = "";
        [ObservableProperty] private bool canCancel = true;
        [ObservableProperty] private bool isIndeterminate = true;
        [ObservableProperty] private int progressValue = 0;
        [ObservableProperty] private int countdownSeconds = 30;

        public bool IsSpinning => Mode == LoadingMode.Processing;
        public Visibility CountdownVisibility => Mode == LoadingMode.LogoutCountdown ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ProgressBarVisibility => Mode == LoadingMode.Processing ? Visibility.Visible : Visibility.Collapsed;

        private readonly CancellationTokenSource _cts = new();
        public CancellationToken Token => _cts.Token;

        private DispatcherTimer? _countdownTimer;

        public event EventHandler? SessionExtended;
        public event EventHandler? CloseRequested;

        public void RequestClose() => CloseRequested?.Invoke(this, EventArgs.Empty);

        public void StartCountdown()
        {
            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _countdownTimer.Tick += (_, _) =>
            {
                CountdownSeconds--;
                if (CountdownSeconds <= 0)
                {
                    _countdownTimer.Stop();
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                }
            };
            _countdownTimer.Start();
        }

        public void Dispose()
        {
            _countdownTimer?.Stop();
            _cts.Cancel();
            _cts.Dispose();
        }

        [RelayCommand]
        private void Cancel()
        {
            if (Mode == LoadingMode.LogoutCountdown)
            {
                _countdownTimer?.Stop();
                SessionExtended?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _cts.Cancel();
            }
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
