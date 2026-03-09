using FProductionDashBoard.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;

namespace FProductionDashBoard
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider _serviceProvider;

        public App()
        {
            var services = new ServiceCollection();

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // 註冊 DbContext
            services.AddDbContext<Repositories.AppDbContext > (options =>
                options.UseSqlServer(config.GetConnectionString("FS_MESInfo") ?? "")); //, ServiceLifetime.Scoped

            // 註冊 Repository
            //services.AddScoped<IRepository<DeviceInfo>, Repository<DeviceInfo>>();
            services.AddScoped<Repositories.IDeviceRepository, Repositories.DeviceRepository>();
            //services.AddScoped<Repositories.IWorkerRepository, Repositories.WorkerRepository>();

            // 註冊 ViewModel
            services.AddScoped<MainViewModel>();

            // 註冊 MainWindow (MainWindow 的 InitializeComponent() 可能沒有正確執行，導致 XAML 裡的 UI 元件沒有完整載入) error1
            // services.AddScoped<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            
            base.OnStartup(e);

            // 從 DI 容器取得 MainWindow（會自動注入 MainViewModel）同error1
            //var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

            // 取代在 App.xaml 中的 StartupUri = "MainWindow.xaml
            var mainWindow = new MainWindow
            { DataContext = _serviceProvider.GetRequiredService<MainViewModel>() };

            mainWindow.Show();

        }
    }

}
