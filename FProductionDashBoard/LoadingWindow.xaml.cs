using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FProductionDashBoard.ViewModels;

namespace FProductionDashBoard
{
    public partial class LoadingWindow : Window
    {
        public LoadingWindow() : this(new LoadingViewModel()) { }

        public LoadingWindow(LoadingViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            Owner = Application.Current.MainWindow;
            Opacity = 0;
            vm.CloseRequested += (_, _) => Dispatcher.Invoke(Close);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoadingViewModel vm && vm.IsSpinning)
                StartSpinAnimation();

            var fadeIn = new DoubleAnimation
            {
                From = 0, To = 1, Duration = TimeSpan.FromSeconds(1),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            this.BeginAnimation(Window.OpacityProperty, fadeIn);
        }

        private void StartSpinAnimation()
        {
            var rotation = (RotateTransform)LoadingIcon.RenderTransform;
            var spin = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            rotation.BeginAnimation(RotateTransform.AngleProperty, spin);
        }

        private bool _isClosing = false;
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isClosing) return;

            e.Cancel = true;
            _isClosing = true;

            var fadeOut = new DoubleAnimation
            {
                From = this.Opacity,
                To = 0,
                Duration = TimeSpan.FromSeconds(1),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (s, _) =>
            {
                // _isClosing 保持 true，讓下一次 Window_Closing 直接 return 不攔截
                Dispatcher.BeginInvoke(new Action(() => base.Close()));
            };
            this.BeginAnimation(Window.OpacityProperty, fadeOut);
        }
    }
}
