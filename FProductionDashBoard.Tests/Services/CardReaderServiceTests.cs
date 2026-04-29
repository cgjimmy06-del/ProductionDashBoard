using FProductionDashBoard.Services;
using Xunit;

namespace FProductionDashBoard.Tests.Services
{
    public class CardReaderServiceTests
    {
        [Fact]
        public void TryExtractCardId_CrTerminator_ReturnsCardId()
        {
            var buffer = "0012345678\r";
            var result = CardReaderService.TryExtractCardId(ref buffer);
            Assert.Equal("0012345678", result);
            Assert.Equal(string.Empty, buffer);
        }

        [Fact]
        public void TryExtractCardId_LfTerminator_ReturnsCardId()
        {
            var buffer = "ABCDEF\n";
            var result = CardReaderService.TryExtractCardId(ref buffer);
            Assert.Equal("ABCDEF", result);
            Assert.Equal(string.Empty, buffer);
        }

        [Fact]
        public void TryExtractCardId_CrLfTerminator_ReturnsCardId()
        {
            var buffer = "1234\r\n";
            var result = CardReaderService.TryExtractCardId(ref buffer);
            Assert.Equal("1234", result);
            Assert.Equal(string.Empty, buffer);
        }

        [Fact]
        public void TryExtractCardId_IncompleteData_ReturnsNullAndPreservesBuffer()
        {
            var buffer = "0012";
            var result = CardReaderService.TryExtractCardId(ref buffer);
            Assert.Null(result);
            Assert.Equal("0012", buffer);
        }

        [Fact]
        public void TryExtractCardId_EmptyLine_ReturnsNull()
        {
            var buffer = "\r\n";
            var result = CardReaderService.TryExtractCardId(ref buffer);
            Assert.Null(result);
        }

        [Fact]
        public void TryExtractCardId_TwoConsecutiveCards_ReturnsFirstAndLeavesSecond()
        {
            var buffer = "AAA\nBBB\n";
            var result = CardReaderService.TryExtractCardId(ref buffer);
            Assert.Equal("AAA", result);
            Assert.Equal("BBB\n", buffer);
        }
    }
}
