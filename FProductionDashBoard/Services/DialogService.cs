using FProductionDashBoard.UserControls;
using FProductionDashBoard.UserControls.DialogView;
using FProductionDashBoard.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace FProductionDashBoard.Services
{
    public class DialogService : IDialogService
    {
        private static readonly Dictionary<Type, Func<object, UserControl>> _factories = new()
        {
            [typeof(MaterialDialogViewModel)]   = vm => new MaterialsDialog { DataContext = vm },
            [typeof(InspectionDialogViewModel)] = vm => new InspectionDialog { DataContext = vm },
            [typeof(TuningDialogViewModel)]     = vm => new TuningDialog { DataContext = vm },
            [typeof(AddDeviceDialogViewModel)]  = vm => new AddDeviceDialog { DataContext = vm },
            [typeof(OrderListDialogViewModel)]  = vm => new OrderListDialog { DataContext = vm },
            [typeof(ProgramStatusDialogViewModel)]          = vm => new ProgramStatusDialog { DataContext = vm },
            [typeof(ScheduleOperationDialogViewModel)]      = vm => new ScheduleOperationDialog { DataContext = vm },
            [typeof(OrderAssignmentDialogViewModel)]        = vm => new OrderAssignmentDialog { DataContext = vm },
        };

        public bool ShowConfirm(string message)
        {
            var vm = new DialogBaseViewModel<bool>(message);
            new DialogWindow(vm).ShowDialog();
            return vm.IsConfirmed;
        }

        public TResult? ShowDialog<TResult>(DialogBaseViewModel<TResult> vm)
        {
            if (_factories.TryGetValue(vm.GetType(), out var factory))
                new DialogWindow(vm, factory(vm)).ShowDialog();
            else
                new DialogWindow(vm).ShowDialog();
            return vm.Result;
        }
    }
}
