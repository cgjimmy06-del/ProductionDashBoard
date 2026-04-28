using FProductionDashBoard.Services;

namespace FProductionDashBoard.ViewModels
{
    public class HardwareViewModel
    {
        public CardReaderSettingViewModel CardReader { get; }

        public HardwareViewModel(MultiCardReaderService multi)
        {
            CardReader = new CardReaderSettingViewModel(multi);
        }
    }
}
