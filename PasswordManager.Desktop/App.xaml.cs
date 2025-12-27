using System;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PasswordManager.Desktop.Extensions;
using PasswordManager.Desktop.Services;
using PasswordManager.Desktop.Services.Impl;
using InfrastructureDI = PasswordManager.Infrastructure.DependencyInjection;
using ApplicationDI = PasswordManager.Application.DependencyInjection;

namespace PasswordManager.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// Configures Dependency Injection and application lifetime.
/// Uses HTTP Server for browser extension communication (simpler than Native Messaging).
/// </summary>
public partial class App : System.Windows.Application
{
    private readonly IHost _host;
    
    // Desktop-specific services
    private ISystemTrayService? _systemTrayService;
    private IGlobalHotKeyService? _hotKeyService;
    
    // ✅ CHANGED: Local HTTP Server (replaces Native Messaging)
    private ILocalApiServer? _localApiServer;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureDesktopHost()
            .ConfigureServices((context, services) =>
            {
                ConfigureServices(context.Configuration, services);
            })
            .Build();
    }

    private void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        // Register Configuration
        services.AddSingleton(configuration);

        // Application + Infrastructure
        ApplicationDI.AddApplication(services);
        InfrastructureDI.AddInfrastructureForDesktop(services, "temporary_password_will_be_replaced");

        // Application Services
        services.AddApplicationServices();
        
        // Desktop-specific services (System Tray, Hotkeys, Clipboard, HTTP Server)
        services.AddDesktopServices();

        // ViewModels
        services.AddViewModels();

        // Views
        services.AddViews();
        
        // Add Logging
        services.AddLogging();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        await _host.StartAsync();

        // Get logger
        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("=== APPLICATION STARTUP ===");

        // Log command line arguments for debugging
        var args = Environment.GetCommandLineArgs();
        logger.LogInformation("Command line arguments: {Args}", string.Join(" ", args));

        // Initialize database
        var serviceProvider = _host.Services;
        try
        {
            await InfrastructureDI.InitializeDatabaseAsync(serviceProvider);
            logger.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize database");
            System.Windows.MessageBox.Show($"Failed to initialize database: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // Initialize desktop-specific services
        try
        {
            _systemTrayService = serviceProvider.GetRequiredService<ISystemTrayService>();
            _hotKeyService = serviceProvider.GetRequiredService<IGlobalHotKeyService>();
            logger.LogInformation("Desktop services initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to initialize desktop services (non-critical)");
            // Continue without desktop features
        }

        // ✅ NEW: Start Local HTTP Server for browser extension
        try
        {
            _localApiServer = serviceProvider.GetRequiredService<ILocalApiServer>();
            await _localApiServer.StartAsync(port: 7777);
            
            logger.LogInformation("═══════════════════════════════════════════════════════");
            logger.LogInformation("🌐 Local API Server started for browser extension");
            logger.LogInformation("📍 Server URL: {Url}", _localApiServer.ServerUrl);
            logger.LogInformation("🔌 Extension can now connect via HTTP");
            logger.LogInformation("═══════════════════════════════════════════════════════");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to start local API server (non-critical)");
            logger.LogWarning("⚠️  Browser extension will NOT work without the API server");
        }

        // Show login window
        try
        {
            var windowFactory = serviceProvider.GetRequiredService<IWindowFactory>();
            var loginWindow = windowFactory.CreateLoginWindow();
            loginWindow.Show();
            logger.LogInformation("Login window displayed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to show login window");
            System.Windows.MessageBox.Show($"Failed to show login window: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("=== APPLICATION SHUTDOWN ===");

        // ✅ CHANGED: Stop HTTP Server
        try
        {
            if (_localApiServer != null)
            {
                await _localApiServer.StopAsync();
                _localApiServer.Dispose();
                logger.LogInformation("Local API server stopped and disposed");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error stopping local API server");
        }

        // Dispose desktop services
        try
        {
            _systemTrayService?.Dispose();
            _hotKeyService?.Dispose();
            logger.LogInformation("Desktop services disposed successfully");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error disposing desktop services");
        }

        using (_host)
        {
            await _host.StopAsync();
        }

        base.OnExit(e);
    }

    /// <summary>
    /// Initializes system tray and hotkeys for the main window.
    /// Call this from MainWindow.OnLoaded event.
    /// </summary>
    public void InitializeDesktopFeatures(Window mainWindow)
    {
        var logger = _host.Services.GetRequiredService<ILogger<App>>();

        if (_systemTrayService == null || _hotKeyService == null)
        {
            logger.LogWarning("Cannot initialize desktop features: Services not available");
            return;
        }

        try
        {
            // Initialize hotkey service with main window handle
            var helper = new System.Windows.Interop.WindowInteropHelper(mainWindow);
            _hotKeyService.Initialize(helper.Handle);

            // Register default hotkeys
            RegisterDefaultHotKeys(mainWindow, logger);

            logger.LogInformation("Desktop features (System Tray + Hotkeys) initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize desktop features");
        }
    }

    /// <summary>
    /// Registers default global hotkeys for the application.
    /// </summary>
    private void RegisterDefaultHotKeys(Window mainWindow, ILogger logger)
    {
        if (_hotKeyService == null || _systemTrayService == null)
            return;

        // Ctrl+Shift+L - Show/Hide Password Manager
        _hotKeyService.RegisterHotKey(
            "toggle-window",
            HotKeyModifiers.Control | HotKeyModifiers.Shift,
            0x4C, // VK_L
            () =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (mainWindow.IsVisible && mainWindow.WindowState != WindowState.Minimized)
                    {
                        _systemTrayService.MinimizeToTray();
                    }
                    else
                    {
                        _systemTrayService.RestoreFromTray();
                    }
                });
            },
            "Toggle Password Manager Window (Ctrl+Shift+L)"
        );

        // Ctrl+Shift+C - Copy password (will be implemented in VaultViewModel)
        _hotKeyService.RegisterHotKey(
            "copy-password",
            HotKeyModifiers.Control | HotKeyModifiers.Shift,
            0x43, // VK_C
            () =>
            {
                logger.LogDebug("Copy password hotkey triggered");
                // TODO: Implement copy selected password from vault
            },
            "Copy Selected Password (Ctrl+Shift+C)"
        );
    }

    public static IServiceProvider ServiceProvider => ((App)Current)._host.Services;

    /// <summary>
    /// Gets the system tray service instance.
    /// </summary>
    public ISystemTrayService? SystemTray => _systemTrayService;

    /// <summary>
    /// Gets the hotkey service instance.
    /// </summary>
    public IGlobalHotKeyService? HotKeys => _hotKeyService;
    
    /// <summary>
    /// Gets the local API server instance.
    /// </summary>
    public ILocalApiServer? LocalApiServer => _localApiServer;
}