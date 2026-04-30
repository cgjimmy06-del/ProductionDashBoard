using FProductionDashBoard.Services;
using System.IO;
using Xunit;

namespace FProductionDashBoard.Tests.LogServiceTests
{
    public class LogServiceTests
    {
        // ─── LoadLogFile ──────────────────────────────────────────────────────────

        [Fact]
        public void LoadLogFile_NonExistentFile_ReturnsErrorMessage()
        {
            var svc = new LogService();
            var result = svc.LoadLogFile("nonexistent_xyz_9999.txt");
            Assert.Equal("檔案不存在或已被壓縮備份。", result);
        }

        [Fact]
        public void LoadLogFile_ExistingFile_ReturnsFileContent()
        {
            var svc = new LogService();
            var fileName = $"testload_{Guid.NewGuid():N}.txt";
            var filePath = Path.Combine("Logs", fileName);
            try
            {
                File.WriteAllText(filePath, "hello world");
                Assert.Equal("hello world", svc.LoadLogFile(fileName));
            }
            finally
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }
        }

        // ─── SaveAllLogsToFileAsync ───────────────────────────────────────────────

        [Fact]
        public async Task SaveAllLogsToFileAsync_CreatesLogAndErrorLogFiles()
        {
            var svc = new LogService();
            await svc.SaveAllLogsToFileAsync();

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
