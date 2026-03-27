using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FProductionDashBoard
{
    public interface ICloseable
    {
        event Action RequestClose;
    }
    public class DialogBaseViewModel<TResult> : ObservableObject, ICloseable
    {
        public bool IsConfirmed { get; protected set; }

        public ICommand ConfirmCommand { get; protected set; }
        public ICommand CancelCommand { get; protected set; }

        public event Action? RequestClose;
        protected void OnRequestClose()
        { RequestClose?.Invoke(); }
        
        public TResult? Result { get; protected set; }

        public DialogBaseViewModel()
        {
            ConfirmCommand = new RelayCommand(() => OnConfirm());
            CancelCommand = new RelayCommand(() => OnCancel());
        }

        protected virtual void OnConfirm()
        {
            IsConfirmed = true;
            OnRequestClose();
        }
        protected virtual void OnCancel()
        {
            IsConfirmed = false;
            OnRequestClose();
        }
    }
}
