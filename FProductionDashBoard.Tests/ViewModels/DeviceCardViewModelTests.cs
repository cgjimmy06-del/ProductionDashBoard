using FProductionDashBoard.ViewModels;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class DeviceCardViewModelTests
    {
        [Theory]
        [InlineData(true, 1, null, TuningRefreshAction.Reset)]
        [InlineData(false, null, null, TuningRefreshAction.Skip)]
        [InlineData(true, 1, 1, TuningRefreshAction.Skip)]
        [InlineData(true, 1, 2, TuningRefreshAction.Activate)]
        [InlineData(false, null, 1, TuningRefreshAction.Activate)]
        public void DecideTuningRefreshAction_ReturnsExpectedAction(
            bool isTuningLocally, int? localProgramTuningId, int? remoteProgramTuningId, TuningRefreshAction expected)
        {
            var action = DeviceCardViewModel.DecideTuningRefreshAction(
                isTuningLocally, localProgramTuningId, remoteProgramTuningId);

            Assert.Equal(expected, action);
        }
    }
}
