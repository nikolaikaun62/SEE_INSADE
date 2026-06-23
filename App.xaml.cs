using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SEE_INSADE.Core.Localization;
using SEE_INSADE.Core.Security;
using SEE_INSADE.Data;
using SEE_INSADE.Services;
using SEE_INSADE.UI.Dialogs;
using SEE_INSADE.UI.MainWindows;
using SEE_INSADE.ViewModels;
using System.Windows;

namespace SEE_INSADE
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Setup DI container
            var services = new ServiceCollection();

            // Database
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite("Data Source=see_insade.db"));

            // Services
            services.AddSingleton<IDetectorService, DetectorService>();
            services.AddSingleton<IScanEmulationService, ScanEmulationService>();
            services.AddSingleton<IDetectorDiagnosticService, DetectorDiagnosticService>();

            // ViewModels
            services.AddTransient<MainViewModel>();

            var serviceProvider = services.BuildServiceProvider();

            // Initialize database
            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                context.Database.EnsureCreated();
                SeedData.Initialize(context);
            }

            LocalizationManager.Instance.LoadLanguages();
            UserAccessService.Instance.Load();

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var loginWindow = new LoginWindow();
            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            // Create and show main window
            var mainWindow = new MainWindow();
            var viewModel = serviceProvider.GetRequiredService<MainViewModel>();
            mainWindow.DataContext = viewModel;
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }
    }
}
