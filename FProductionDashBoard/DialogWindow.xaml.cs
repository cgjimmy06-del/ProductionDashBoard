using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FProductionDashBoard
{
    /// <summary>
    /// DialogWindow.xaml 的互動邏輯
    /// </summary>
    public partial class DialogWindow : Window
    {
        public DialogWindow(object viewmodel, UserControl? content = null)
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;

            DialogContent.Content = content;

            DataContext = viewmodel;
            if (DataContext is ViewModels.ICloseable closeable) 
            { closeable.RequestClose += () => this.Close(); }
        }
    }
}
