using FProductionDashBoard.Services;
using System.IO;
using Xunit;

namespace FProductionDashBoard.Tests.LogServiceTests
{
    public class LogServiceTests
    {
        // ─── SaveAllLogsToFileAsync ───────────────────────────────────────────────

        [Fact]
        public async Task SaveAllLogsToFileAsync_CreatesLogAndErrorLogFiles()
        {
            var svc = new LogService();
            await svc.ExportInMemoryLogsAsync();

            var files = Directory.GetFiles("Logs");
            Assert.Contains(files, f => Path.GetFileName(f).StartsWith("log_") && f.EndsWith("_t.txt"));
            Assert.Contains(files, f => Path.GetFileName(f).StartsWith("elog_") && f.EndsWith("_t.txt"));
        }

        // ─── RefreshAvailableLogFiles ─────────────────────────────────────────────

        [Fact]
        public void RefreshAvailableLogFiles_PopulatesListWithMatchingFiles()
        {
            var svc = new LogService();
            var suffix = Guid.NewGuid().ToString("N")[..6];
            var fileName = $"logs_{DateTime.Now:yyyy-MM-dd}_{suffix}.txt";
            var filePath = Path.Combine("Logs", fileName);
            try
            {
                File.WriteAllText(filePath, "test");
                svc.RefreshAvailableLogFiles();
                Assert.Contains(fileName, svc.AvailableLogFiles);
            }
            finally
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }
        }

        [Fact]
        public void RefreshAvailableLogFiles_DoesNotIncludeNonMatchingFiles()
        {
            var svc = new LogService();
            var fileName = $"app_report_{Guid.NewGuid():N}.txt";
            var filePath = Path.Combine("Logs", fileName);
            try
            {
                File.WriteAllText(filePath, "test");
                svc.RefreshAvailableLogFiles();
                Assert.DoesNotContain(fileName, svc.AvailableLogFiles);
            }
            finally
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }
        }
    }
}
