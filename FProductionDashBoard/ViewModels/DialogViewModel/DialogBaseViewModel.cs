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

namespace FProductionDashBoard.ViewModels
{
    public interface ICloseable
    {
        event Action RequestClose;
    }
    public partial class DialogBaseViewModel<TResult> : ObservableObject, ICloseable
    {
        [ObservableProperty]
        public string? dialogInfoString;
        [ObservableProperty]
        public string? dialogErrorString;
        [ObservableProperty]
        protected bool isConfirmVisible = true;

        public bool IsConfirmed { get; protected set; }

        public ICommand ConfirmCommand { get; protected set; }
        public ICommand CancelCommand { get; protected set; }

        public event Action? RequestClose;
        protected void OnRequestClose()
        { RequestClose?.Invoke(); }
        
        public TResult? Result { get; protected set; }

        public DialogBaseViewModel(string dialogstring)
        {
            DialogInfoString = dialogstring;

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
