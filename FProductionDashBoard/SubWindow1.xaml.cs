using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace FProductionDashBoard
{
    /// <summary>
    /// Window1.xaml 的互動邏輯
    /// </summary>
    public partial class SubWindow1 : Window
    {
        public SubWindow1()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
            Opacity = 0; // 初始透明度設為 0
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        { 
            // 建立淡入動畫
            var fadeIn = new DoubleAnimation 
            { 
                From = 0, To = 1, Duration = TimeSpan.FromSeconds(1), 
                // 1 秒淡入
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } 
            }; 
            // 套用到視窗的 Opacity 屬性
                this.BeginAnimation(Window.OpacityProperty, fadeIn);
        }

        private bool _isClosing = false;
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 如果已經在淡出，就不要再阻止
            if (_isClosing) return;

            // 阻止立即關閉，先播放淡出動畫
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
                // 動畫完成後再真正關閉視窗
                // 使用 Dispatcher.BeginInvoke 來避免再次觸發 Closing
                Dispatcher.BeginInvoke(new Action(() => {
                    _isClosing = false; 
                    base.Close(); // 這次會真正關閉
                }));
                }; 

            this.BeginAnimation(Window.OpacityProperty, fadeOut); 
        }
    }

}
