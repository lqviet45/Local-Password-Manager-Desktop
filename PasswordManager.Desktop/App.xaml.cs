using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PasswordManager.Desktop.Services;
using PasswordManager.Desktop.Services.Impl;
using PasswordManager.Desktop.ViewModels;
using PasswordManager.Desktop.Views;
using PasswordManager.Infrastructure;
using Serilog;

namespace PasswordManager.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// Configures Dependency Injection and application lifetime.
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                ConfigureServices(context.Configuration, services);
            })
            .UseSerilog((context, loggerConfig) =>
            {
                loggerConfig
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .WriteTo.File(
                        path: "logs/app.log",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
            })
            .Build();
    }

    private void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        // Register Configuration
        services.AddSingleton(configuration);

        // Infrastructure Services (LOCAL-ONLY MODE)
        // SQLCipher password will be derived from user's master password
        // For now, we'll use a temporary placeholder that gets set after login
        services.AddInfrastructureForDesktop("temporary_password_will_be_replaced");

        // Application Services
        services.AddSingleton<IMasterPasswordService, MasterPasswordService>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddTransient<IDialogService, DialogService>();
        services.AddTransient<IClipboardService, ClipboardService>();

        // ViewModels (Transient for fresh instances)
        services.AddTransient<LoginViewModel>();
        // services.AddTransient<MainViewModel>();
        // services.AddTransient<VaultViewModel>();
        // services.AddTransient<AddEditItemViewModel>();
        // services.AddTransient<SettingsViewModel>();

        // Views (Transient)
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();

        // Register Window Factory
        services.AddSingleton<IWindowFactory, WindowFactory>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        await _host.StartAsync();

        // Initialize database
        var serviceProvider = _host.Services;
        await DependencyInjection.InitializeDatabaseAsync(serviceProvider);

        // Show login window
        var windowFactory = serviceProvider.GetRequiredService<IWindowFactory>();
        var loginWindow = windowFactory.CreateLoginWindow();
        loginWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        using (_host)
        {
            await _host.StopAsync();
        }

        base.OnExit(e);
    }

    public static IServiceProvider ServiceProvider => ((App)Current)._host.Services;
}
