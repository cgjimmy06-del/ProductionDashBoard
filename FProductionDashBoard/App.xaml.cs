using FProductionDashBoard.Services.WebApi;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
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

#if DEBUG
                // 取得登入資訊 ( Debug 模式 )
                var loginWindow = new LoginWindow();
                string selectedServer = loginWindow.SelectedServer;
                var user = loginWindow.User; user.Name = "Debug";
#else
                // 登入畫面 + 取得資訊
                var loginWindow = new LoginWindow();
                bool skipLogin = FProductionDashBoard.Properties.Settings.Default.SkipLoginScreen;
                if (!skipLogin && loginWindow.ShowDialog() != true) { Shutdown(); return; }

                // 取得登入資訊
                string selectedServer = skipLogin ? FProductionDashBoard.Properties.Settings.Default.Server 
                    : loginWindow.SelectedServer;
                var user = loginWindow.User;
#endif

                // 讀取設定檔 - 重用登入階段已讀取的 config（避免 ClickOnce 路徑不穩定造成二次讀取失敗）
                var config = Services.LoginDapper.GetCachedConfig();

                // 檢查目標資料夾
                Directory.CreateDirectory(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FProductionDashBoard"));

                var services = new ServiceCollection();

                // 註冊 ConfigurationBuilder 資訊
                services.AddSingleton(config);

                // 連線狀態訊號源（DbConnectionInterceptor 掛 MesDbContext）
                var connectionStatusService = new Services.ConnectionStatusService();
                services.AddSingleton(connectionStatusService);

                // 註冊 DbContext
                services.AddDbContextFactory<Repositories.MesDbContext>(options =>
                    options.UseSqlServer(config.GetConnectionString($"{selectedServer}_MESDashboard") ?? "")
                           .AddInterceptors(new Services.ConnectionStatusInterceptor(connectionStatusService)));
                services.AddDbContextFactory<Repositories.DataDbContext>(options =>
                    options.UseSqlServer(config.GetConnectionString($"{selectedServer}_MESData") ?? ""));
                services.AddDbContextFactory<Repositories.InfoDbContext>(options =>
                    options.UseSqlServer(config.GetConnectionString($"{selectedServer}_MESInfo") ?? ""));

                // 註冊 泛型 Repository / 專用 Repository
                services.AddScoped(typeof(Repositories.IRepository<,>), typeof(Repositories.Repository<,>));
                services.AddScoped<Repositories.IEquipmentRepository, Repositories.EquipmentRepository>();
                services.AddScoped<Repositories.IEmployeeRepository, Repositories.EmployeeRepository>();
                services.AddScoped<Repositories.IMaterialRepository, Repositories.MaterialRepository>();
                services.AddScoped<Repositories.IErrorListRepository, Repositories.ErrorListRepository>();
                services.AddScoped<Repositories.IMaterialReplacementRepository, Repositories.MaterialReplacementRepository>();
                services.AddScoped<Repositories.ITimeSlotLookupRepository, Repositories.TimeSlotLookupRepository>();
                services.AddScoped<Repositories.IInspectionRecordRepository, Repositories.InspectionRecordRepository>();
                services.AddScoped<Repositories.IRolePermissionRepository, Repositories.RolePermissionRepository>();
                services.AddScoped<Repositories.IProductPartRepository, Repositories.ProductPartRepository>();
                services.AddScoped<Repositories.IProductRepository, Repositories.ProductRepository>();
                services.AddScoped<Repositories.ISopChecklistRepository, Repositories.SopChecklistRepository>();
                services.AddScoped<Repositories.IEquipmentProductRepository, Repositories.EquipmentProductRepository>();
                services.AddScoped<Repositories.IOrderProductionRepository, Repositories.OrderProductionRepository>();
                services.AddScoped<Repositories.IProgramTuningRecordRepository, Repositories.ProgramTuningRecordRepository>();
                services.AddScoped<Repositories.IScheduleRepository, Repositories.ScheduleRepository>();
                services.AddScoped<Repositories.ExtraDb.IInfoDbRepository, Repositories.ExtraDb.InfoDbRepository>();
                services.AddScoped<Repositories.ExtraDb.IDataDbRepository, Repositories.ExtraDb.DataDbRepository>();

                // 註冊 Service
                services.AddScoped<Services.IDataService, Services.DataService>();
                services.AddScoped<Services.LogService>();
                services.AddSingleton<Services.AuthorizationService>();
                services.AddSingleton<Services.IDialogService, Services.DialogService>();
                services.AddSingleton<Services.MultiCardReaderService>();
                services.AddSingleton<Services.ICardReaderService>(sp =>
                    sp.GetRequiredService<Services.MultiCardReaderService>());

                // 設定持久化服務
                services.AddSingleton<Services.IConfigService<Dtos.SystemConfigDto>>(
                    _ => new Services.ConfigService<Dtos.SystemConfigDto>("system_config.json"));
                services.AddSingleton<Services.IConfigService<Dtos.HardwareConfigDto>>(
                    _ => new Services.ConfigService<Dtos.HardwareConfigDto>("hardware_config.json"));

                // 圖表定義儲存（本機 JSON，與上方設定檔同資料夾）
                services.AddSingleton<Services.IChartDefinitionStore, Services.JsonChartDefinitionStore>();

                // 授權服務（#if DEBUG 保留 Stub 供開發測試）
#if DEBUG
                services.AddSingleton<Services.ILicenseService, Services.DevelopmentLicenseService>();
#else
                services.AddSingleton<Services.ILicenseService, Services.FileLicenseService>();
#endif

                // 註冊 ABB 機器人用戶端工廠（每台設備卡片各自持有獨立實例，由 ViewModel 負責釋放）
                services.AddSingleton<Func<DeviceDrivers.Abb.IAbbRobotClient>>(
                    _ => () => new DeviceDrivers.Abb.AbbRobotClient());

                // 註冊 Modbus TCP 用戶端工廠（由 ModbusTcpSettingViewModel 負責釋放）
                services.AddSingleton<Func<string, int, byte, DeviceDrivers.Modbus.IModbusClient>>(
                    _ => (ip, port, unitId) => new DeviceDrivers.Modbus.ModbusTcpClient(ip, port, unitId));

                // 註冊 WebApi 服務
                var factoryArea = config[$"EriApi:{selectedServer}"] ?? selectedServer;
                services.Configure<ErpApiOptions>(opt => opt.FactoryArea = factoryArea);
                var erpBaseUrl = config["ErpApi:BaseUrl"] ?? "";
                services.AddHttpClient<Services.WebApi.IErpApiService, Services.WebApi.ErpApiService>(client =>
                {
                    if (Uri.TryCreate(erpBaseUrl, UriKind.Absolute, out var erpUri))
                        client.BaseAddress = erpUri;
                    client.Timeout = TimeSpan.FromSeconds(5);
                });

                // 註冊 上傳 Log WebApi 服務
                var logUploadBaseUrl = config["LogUploadApi:BaseUrl"] ?? "";
                services.AddHttpClient<Services.WebApi.ILogUploadService, Services.WebApi.LogUploadService>(client =>
                {
                    if (Uri.TryCreate(logUploadBaseUrl, UriKind.Absolute, out var baseUri))
                        client.BaseAddress = baseUri;
                    client.Timeout = TimeSpan.FromSeconds(5);
                });

                // 註冊 AI Chat 服務
                services.Configure<Services.WebApi.AiApiOptions>(config.GetSection("AiApi"));
                services.AddHttpClient<Services.WebApi.IAiChatService, Services.WebApi.OpenAiChatService>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                });
                services.AddScoped<Services.AiTools.AiAgentToolService>();

                // 註冊 Facade
                services.AddScoped<Services.DashboardCoreServices>();

                // 註冊共用清單狀態（單例，供各面板 ViewModel 直接注入）
                services.AddSingleton<UiModels.ListsFromSql>();

                // 註冊 ViewModel
                services.AddScoped<ViewModels.MainViewModel>();
                services.AddScoped<ViewModels.SettingViewModel>();
                services.AddScoped<ViewModels.HardwareViewModel>();
                services.AddScoped<ViewModels.LogPanelViewModel>();
                services.AddScoped<ViewModels.AIAgentViewModel>();
                services.AddScoped<ViewModels.SystemSettingsViewModel>();
                services.AddScoped<ViewModels.HomeViewModel>();
                services.AddScoped<ViewModels.ChartViewModel>();
                services.AddTransient<ViewModels.OperationViewModel>();
                services.AddTransient<ViewModels.ProgramLibraryViewModel>();
                services.AddTransient<ViewModels.ProductInOutViewModel>();
                services.AddTransient<ViewModels.ScheduleViewModel>();

                _serviceProvider = services.BuildServiceProvider();
                // 載入持久化設定
                _serviceProvider.GetRequiredService<Services.IConfigService<Dtos.SystemConfigDto>>().Load();
                _serviceProvider.GetRequiredService<Services.IConfigService<Dtos.HardwareConfigDto>>().Load();

                // 啟動登入權限
                var authService = _serviceProvider.GetRequiredService<Services.AuthorizationService>();
                try 
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dataService = scope.ServiceProvider.GetRequiredService<Services.IDataService>();
                    var roles = await dataService.GetAllRolesAsync();
                    authService.SetCachedRoles(roles);
                }
                catch { Debug.WriteLine("角色清單取得異常，進入離線模式..."); }
                await authService.InitializeAsync(user);

                // 預設啟動一台讀卡機（以硬體設定為準）
                var hwCfg = _serviceProvider.GetRequiredService<Services.IConfigService<Dtos.HardwareConfigDto>>().Current;
                _serviceProvider.GetRequiredService<Services.MultiCardReaderService>()
                    .AddReader(hwCfg.ReaderPort, hwCfg.ReaderBaud);

                // 取代在 App.xaml 中的 StartupUri
                ShutdownMode = ShutdownMode.OnMainWindowClose; //「被設定為 MainWindow 的介面關閉則結束」
                splash.Close(TimeSpan.FromSeconds(0.5)); // DI注入完成後關閉LOGO
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

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }

}
