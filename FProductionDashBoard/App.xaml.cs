using FProductionDashBoard.Services.Offline;
using FProductionDashBoard.Services.Offline.Handlers;
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
        private ServiceProvider? _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
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
            Debug.WriteLine($"Login User: {user.Name}, RoleId: {user.RoleId}");

            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).Build();

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

            // 註冊 資訊
            services.AddSingleton(user);

            // 離線暫存服務
            services.AddDbContext<Repositories.LocalDbContext>(opt =>
                opt.UseSqlite("Data Source=Settings/local_cache.db"), ServiceLifetime.Singleton);
            services.AddSingleton<IOfflineCacheService, OfflineCacheService>();
            services.AddSingleton<IOfflineSyncService, OfflineSyncService>();
            services.AddTransient<IPendingOperationHandler, ReplacementSyncHandler>();
            services.AddTransient<IPendingOperationHandler, FirstInspectionSyncHandler>();
            services.AddTransient<IPendingOperationHandler, RoutineInspectionSyncHandler>();

            // 註冊 Service
            services.AddScoped<Services.IDataService, Services.V1.DataService>();
            services.AddScoped<Services.LogService>();
            services.AddScoped<Services.AuthorizationService>();

            // 註冊 ViewModel
            services.AddScoped<ViewModels.MainViewModel>();

            // 註冊 MainWindow 的 InitializeComponent()可能沒有正確執行，致 XAML 裡的 UI 元件沒有完整載入
            // services.AddScoped<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();

            // 取代在 App.xaml 中的 StartupUri
            ShutdownMode = ShutdownMode.OnMainWindowClose; //「被設定為 MainWindow 之介面關閉則結束」
            var mainWindow = new MainWindow
            { DataContext = _serviceProvider.GetRequiredService<ViewModels.MainViewModel>() };
            MainWindow = mainWindow; // 設定 Application 的 MainWindow
            mainWindow.Show();
        }
    }

}
