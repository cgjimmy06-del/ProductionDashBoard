using System.Collections.Generic;
using FProductionDashBoard.Properties;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class InspectionDialogViewModelTests
    {
        // 最後一項視為「其他」(Other)；TypeId 3 應被排除（僅保留 1/2）
        private static List<ErrorInfo> Errors() => new()
        {
            new() { ErrorCode = "E01",   Message = "Msg1", TypeId = 1 },
            new() { ErrorCode = "E02",   Message = "Msg2", TypeId = 2 },
            new() { ErrorCode = "OTHER", Message = "其他", TypeId = 2 },
        };

        private static InspectionDialogViewModel CreateVm(List<ErrorInfo> errs)
            => new("t", DeviceCardTestFactory.Create(), errs);

        [Fact]
        public void Ctor_FiltersErrorsByType_AndPreselectsFirst()
        {
            var errs = new List<ErrorInfo>
            {
                new() { ErrorCode = "E01", TypeId = 1 },
                new() { ErrorCode = "E02", TypeId = 2 },
                new() { ErrorCode = "E99", TypeId = 3 }, // 應被排除
            };
            var vm = CreateVm(errs);

            Assert.Equal(2, vm.ErrorCodes.Count);
            Assert.Equal("E01", vm.SelectionCode);
        }

        [Fact]
        public void IsOtherSelected_TrueOnlyForLastCode()
        {
            var vm = CreateVm(Errors());

            vm.SelectionCode = "OTHER";
            Assert.True(vm.IsOtherSelected);

            vm.SelectionCode = "E01";
            Assert.False(vm.IsOtherSelected);
        }

        [Fact]
        public void Confirm_Success_ResultIsNormal()
        {
            var vm = CreateVm(Errors()); // InspectionCheck 預設 Success
            vm.ConfirmCommand.Execute(null);

            Assert.True(vm.IsConfirmed);
            Assert.NotNull(vm.Result);
            Assert.True(vm.Result!.IsNormal);
        }

        [Fact]
        public void Confirm_FailureKnownCode_UsesCodeMessage()
        {
            var vm = CreateVm(Errors());
            vm.InspectionCheck = InspectionRadioCheck.Failure;
            vm.SelectionCode = "E01"; // 非 last → 已知碼，帶出該碼 Message

            vm.ConfirmCommand.Execute(null);

            Assert.False(vm.Result!.IsNormal);
            Assert.Equal("E01", vm.Result.ErrorCode);
            Assert.Equal("Msg1", vm.Result.Description);
        }

        [Fact]
        public void Confirm_FailureOther_UsesOtherDescription()
        {
            var vm = CreateVm(Errors());
            vm.InspectionCheck = InspectionRadioCheck.Failure;
            vm.SelectionCode = "OTHER"; // last → 使用自填描述
            vm.OtherDescription = "custom reason";

            vm.ConfirmCommand.Execute(null);

            Assert.False(vm.Result!.IsNormal);
            Assert.Equal("OTHER", vm.Result.ErrorCode);
            Assert.Equal("custom reason", vm.Result.Description);
        }
    }
}
