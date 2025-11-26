using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SEE_INSADE.Data;
using SEE_INSADE.Services;
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

            // Create and show main window
            var mainWindow = new MainWindow();
            var viewModel = serviceProvider.GetService<MainViewModel>();
            mainWindow.DataContext = viewModel;
            mainWindow.Show();
        }
    }
}