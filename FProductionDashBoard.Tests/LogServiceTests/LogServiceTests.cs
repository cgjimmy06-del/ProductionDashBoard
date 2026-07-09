using FProductionDashBoard.Services;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Xunit;

namespace FProductionDashBoard.Tests.LogServiceTests
{
    public class LogServiceTests
    {
        private static readonly WpfDispatcherHost Wpf = new();

        private sealed class WpfDispatcherHost
        {
            private readonly ManualResetEventSlim _ready = new();
            private Dispatcher? _dispatcher;

            public WpfDispatcherHost()
            {
                var thread = new Thread(() =>
                {
                    _ = new Application();
                    _dispatcher = Dispatcher.CurrentDispatcher;
                    _ready.Set();
                    Dispatcher.Run();
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
            }

            public void Invoke(Action action)
            {
                _ready.Wait();
                Exception? exception = null;
                _dispatcher!.Invoke(() =>
                {
                    try
                    {
                        action();
                        DrainDispatcher();
                    }
                    catch (Exception ex)
                    {
                        exception = ex;
                    }
                });

                if (exception != null)
                    ExceptionDispatchInfo.Capture(exception).Throw();
            }

            public static void DrainDispatcher()
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }
        }

        // ─── SaveAllLogsToFileAsync ───────────────────────────────────────────────

        [Fact]
        public async Task SaveAllLogsToFileAsync_CreatesLogAndErrorLogFiles()
        {
            var svc = new LogService();
            await svc.ExportInMemoryLogsAsync();

            var files = Directory.GetFiles(svc.LogDirectory);
            Assert.Contains(files, f => Path.GetFileName(f).StartsWith("log_") && f.EndsWith("_t.txt"));
            Assert.Contains(files, f => Path.GetFileName(f).StartsWith("elog_") && f.EndsWith("_t.txt"));
        }

        // ─── RefreshAvailableLogFiles ─────────────────────────────────────────────

        [Fact]
        public void RefreshAvailableLogFiles_PopulatesListWithMatchingFiles()
        {
            Wpf.Invoke(() =>
            {
                var svc = new LogService();
                var suffix = Guid.NewGuid().ToString("N")[..6];
                var fileName = $"logs_{DateTime.Now:yyyy-MM-dd}_{suffix}.txt";
                var filePath = Path.Combine(svc.LogDirectory, fileName);
                try
                {
                    File.WriteAllText(filePath, "test");
                    svc.RefreshAvailableLogFiles();
                    WpfDispatcherHost.DrainDispatcher();
                    Assert.Contains(fileName, svc.AvailableLogFiles);
                }
                finally
                {
                    if (File.Exists(filePath)) File.Delete(filePath);
                }
            });
        }

        [Fact]
        public void RefreshAvailableLogFiles_DoesNotIncludeNonMatchingFiles()
        {
            Wpf.Invoke(() =>
            {
                var svc = new LogService();
                var fileName = $"app_report_{Guid.NewGuid():N}.txt";
                var filePath = Path.Combine(svc.LogDirectory, fileName);
                try
                {
                    File.WriteAllText(filePath, "test");
                    svc.RefreshAvailableLogFiles();
                    WpfDispatcherHost.DrainDispatcher();
                    Assert.DoesNotContain(fileName, svc.AvailableLogFiles);
                }
                finally
                {
                    if (File.Exists(filePath)) File.Delete(filePath);
                }
            });
        }
    }
}
