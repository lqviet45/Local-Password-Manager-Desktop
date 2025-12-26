using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PasswordManager.Desktop.Services;
using PasswordManager.Desktop.Services.Impl;
using PasswordManager.Desktop.ViewModels;
using PasswordManager.Desktop.Views;
using Serilog;

namespace PasswordManager.Desktop.Extensions;

/// <summary>
/// Extension methods for registering desktop-specific services.
/// Follows Dependency Injection and Interface Segregation principles.
/// </summary>
public static class DesktopServiceExtensions
{
    /// <summary>
    /// Registers all desktop UI services following SOLID principles.
    /// All services are registered as Singleton for performance and state management.
    /// </summary>
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        // System Tray Service (D: Dependency Inversion - depends on interface)
        services.AddSingleton<ISystemTrayService, SystemTrayService>();

        // Global Hotkey Service (D: Dependency Inversion)
        services.AddSingleton<IGlobalHotKeyService, GlobalHotKeyService>();

        // Secure Clipboard Service (D: Dependency Inversion)
        // BACKWARD COMPATIBLE with existing IClipboardService
        services.AddSingleton<IClipboardService, ClipboardService>();

        // Auto-Fill Service (D: Dependency Inversion)
        services.AddSingleton<IAutoFillService, AutoFillService>();

        // Auto-Save Credentials Service (D: Dependency Inversion)
        services.AddSingleton<IAutoSaveCredentialsService, AutoSaveCredentialsService>();

        // Browser Extension Communicator (D: Dependency Inversion)
        services.AddSingleton<IBrowserExtensionCommunicator, BrowserExtensionCommunicator>();

        // Browser Extension Message Handler (D: Dependency Inversion)
        services.AddSingleton<BrowserExtensionMessageHandler>();

        return services;
    }

    /// <summary>
    /// Registers all application-specific services (Master Password, Session, Dialog).
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IMasterPasswordService, MasterPasswordService>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IWindowFactory, WindowFactory>();

        return services;
    }

    /// <summary>
    /// Registers all ViewModels with appropriate lifetimes.
    /// </summary>
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        // Transient ViewModels - new instance each time
        services.AddTransient<LoginViewModel>();
        services.AddTransient<AddEditItemViewModel>();

        // Singleton ViewModels - shared instance
        services.AddSingleton<VaultViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();

        return services;
    }

    /// <summary>
    /// Registers all Views with appropriate lifetimes.
    /// </summary>
    public static IServiceCollection AddViews(this IServiceCollection services)
    {
        services.AddTransient<LoginWindow>();
        // Other views are created through WindowFactory

        return services;
    }

    /// <summary>
    /// Configures the host builder with desktop-specific settings.
    /// </summary>
    public static IHostBuilder ConfigureDesktopHost(this IHostBuilder hostBuilder)
    {
        return hostBuilder
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .UseSerilog((context, loggerConfig) =>
            {
                loggerConfig
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .WriteTo.File(
                        path: "logs/app.log",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
            });
    }
}