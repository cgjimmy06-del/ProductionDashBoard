using FProductionDashBoard.Services.Offline;
using FProductionDashBoard.Services.Offline.Handlers;
using FProductionDashBoard.Services.WebApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace FProductionDashBoard
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
#if DEBUG
        public static Visibility DebugVisibility => Visibility.Visible;
#else
        public static Visibility DebugVisibility => Visibility.Collapsed;
#endif

        private ServiceProvider? _serviceProvider;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown; //「明確呼叫 Shutdown() 才結束」

                // 建立 SplashScreen，false 手動控制關閉
                SplashScreen splash = new SplashScreen((string)Application.Current.Resources["LoginLogoPath"]);
                splash.Show(false);
                
                // 登入畫面 + 取得資訊
                var loginWindow = new LoginWindow();
                splash.Close(TimeSpan.FromSeconds(0.5)); // login載入完成後關閉
                if (loginWindow.ShowDialog() != true) { Shutdown(); return; }

                // 後續加入語言
                string selectedServer = loginWindow.SelectedServer;
                var user = loginWindow.User;

                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Settings"));

                // 讀取設定檔 // 重用登入階段已讀取的 config（避免 ClickOnce 路徑不穩定造成二次讀取失敗）
                var services = new ServiceCollection();
                var config = Services.LoginDapper.GetCachedConfig();

                services.AddSingleton(config);

                // 註冊 DbContext
                services.AddDbContextFactory<Repositories.MesDbContext>(options =>
                    options.UseSqlServer(config.GetConnectionString($"{selectedServer}_MESDashboard") ?? ""));
                services.AddDbContextFactory<Repositories.DataDbContext>(options =>
                    options.UseSqlServer(config.GetConnectionString($"{selectedServer}_MESData") ?? ""));

                // 註冊 泛型 Repository / 專用 Repository
                services.AddScoped(typeof(Repositories.IRepository<,>), typeof(Repositories.Repository<,>));
                services.AddScoped<Repositories.IEquipmentRepository, Repositories.EquipmentRepository>();
                services.AddScoped<Repositories.IEmployeeRepository, Repositories.EmployeeRepository>();
                services.AddScoped<Repositories.IMaterialRepository, Repositories.MaterialRepository>();
                services.AddScoped<Repositories.IErrorListRepository, Repositories.ErrorListRepository>();
                services.AddScoped<Repositories.IMaterialReplacementRepository, Repositories.MaterialReplacementRepository>();
                services.AddScoped<Repositories.ITimeSlotLookupRepository, Repositories.TimeSlotLookupRepository>();
                services.AddScoped<Repositories.IInspectionRecordRepository, Repositories.InspectionRecordRepository>();
                services.AddScoped<Repositories.ITuningRecordRepository, Repositories.TuningRecordRepository>();
                services.AddScoped<Repositories.IRolePermissionRepository, Repositories.RolePermissionRepository>();

                // 離線暫存服務
                services.AddDbContextFactory<Repositories.LocalDbContext>(opt =>
                    opt.UseSqlite($"Data Source={Path.Combine(AppContext.BaseDirectory, "Settings", "local_cache.db")}"),
                    ServiceLifetime.Singleton);
                services.AddSingleton<IOfflineCacheService, OfflineCacheService>();
                services.AddSingleton<IOfflineSyncService, OfflineSyncService>();
                services.AddTransient<IPendingOperationHandler, ReplacementSyncHandler>();
                services.AddTransient<IPendingOperationHandler, FirstInspectionSyncHandler>();
                services.AddTransient<IPendingOperationHandler, RoutineInspectionSyncHandler>();
                services.AddTransient<IPendingOperationHandler, TuningSyncHandler>();

                // 註冊 Service
                services.AddScoped<Services.IDataService, Services.V1.DataService>();
                services.AddScoped<Services.LogService>();
                services.AddSingleton<Services.AuthorizationService>();
                services.AddSingleton<Services.IDialogService, Services.DialogService>();
                services.AddSingleton<Services.MultiCardReaderService>();
                services.AddSingleton<Services.ICardReaderService>(sp =>
                    sp.GetRequiredService<Services.MultiCardReaderService>());

                // 註冊 WebApi 服務
                var factoryArea = config[$"EriApi:{selectedServer}"] ?? selectedServer;
                services.Configure<ErpApiOptions>(opt => opt.FactoryArea = factoryArea);
                services.AddHttpClient<Services.WebApi.IErpApiService, Services.WebApi.ErpApiService>(client =>
                {
                    client.BaseAddress = new Uri("http://ssty-erpapp01.sporting.fusheng.com/Fusheng.WHD.ERP.Common/");
                    client.Timeout = TimeSpan.FromSeconds(5);
                });

                // 註冊 Facade
                services.AddScoped<Services.DashboardCoreServices>();

                // 註冊 ViewModel
                services.AddScoped<ViewModels.MainViewModel>();
                services.AddScoped<ViewModels.SettingViewModel>();
                services.AddScoped<ViewModels.HardwareViewModel>();

                _serviceProvider = services.BuildServiceProvider();
                var authService = _serviceProvider.GetRequiredService<Services.AuthorizationService>();
                await authService.InitializeAsync(user);

                // 預設啟動一台讀卡機 (以最後連線的設備為準)
                _serviceProvider.GetRequiredService<Services.MultiCardReaderService>()
                    .AddReader(FProductionDashBoard.Properties.Settings.Default.ReaderPort,
                               FProductionDashBoard.Properties.Settings.Default.ReaderBaud);

                // 取代在 App.xaml 中的 StartupUri
                ShutdownMode = ShutdownMode.OnMainWindowClose; //「被設定為 MainWindow 之介面關閉則結束」
                var mainWindow = new MainWindow
                { DataContext = _serviceProvider.GetRequiredService<ViewModels.MainViewModel>() };
                MainWindow = mainWindow; // 設定 Application 的 MainWindow
                mainWindow.Show();
            }
            catch(Exception ex)
            {
                MessageBox.Show($"啟動失敗: \n\n {ex.GetType().Name}\n{ex.Message}", 
                    "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }

}
