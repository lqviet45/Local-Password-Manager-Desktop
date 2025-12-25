using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.Logging;
using PasswordManager.Desktop.Services;
using WinApplication = System.Windows.Application;

namespace PasswordManager.Desktop.Services.Impl;

/// <summary>
/// Production implementation of system tray service using Hardcodet.NotifyIcon.Wpf.
/// Much better than Windows Forms NotifyIcon for WPF applications.
/// </summary>
public sealed class SystemTrayService : ISystemTrayService
{
    private readonly ILogger<SystemTrayService> _logger;
    private readonly TaskbarIcon _taskbarIcon;
    private bool _disposed;

    private const string AppName = "Password Manager";
    private const string DefaultTooltip = "Password Manager - Click to open";

    public SystemTrayService(ILogger<SystemTrayService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create TaskbarIcon
        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = DefaultTooltip,
            IconSource = LoadIconSource(),
            ContextMenu = CreateContextMenu(),
            Visibility = Visibility.Collapsed // Hidden by default
        };

        // Subscribe to events
        _taskbarIcon.TrayLeftMouseUp += OnTrayLeftMouseUp;
        _taskbarIcon.TrayMouseDoubleClick += OnTrayDoubleClick;

        _logger.LogInformation("System tray service initialized with Hardcodet.NotifyIcon.Wpf");
    }

    #region ISystemTrayService Implementation

    public bool IsVisible => _taskbarIcon.Visibility == Visibility.Visible;

    public void Show()
    {
        ThrowIfDisposed();
        _taskbarIcon.Visibility = Visibility.Visible;
        _logger.LogDebug("System tray icon shown");
    }

    public void Hide()
    {
        ThrowIfDisposed();
        _taskbarIcon.Visibility = Visibility.Collapsed;
        _logger.LogDebug("System tray icon hidden");
    }

    public void MinimizeToTray()
    {
        ThrowIfDisposed();
        
        var mainWindow = WinApplication.Current.MainWindow;
        if (mainWindow != null)
        {
            mainWindow.Hide();
            mainWindow.WindowState = WindowState.Minimized;
            Show();

            ShowNotification(
                "Running in Background",
                "Password Manager is still running. Double-click the tray icon to restore.",
                NotificationIcon.Info,
                3000
            );

            _logger.LogInformation("Application minimized to system tray");
        }
    }

    public void RestoreFromTray()
    {
        ThrowIfDisposed();
        
        var mainWindow = WinApplication.Current.MainWindow;
        if (mainWindow != null)
        {
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
            mainWindow.Focus();

            _logger.LogInformation("Application restored from system tray");
        }
    }

    public void ShowNotification(
        string title,
        string message,
        NotificationIcon icon = NotificationIcon.Info,
        int timeoutMs = 5000)
    {
        ThrowIfDisposed();

        var balloonIcon = ConvertNotificationIcon(icon);
        
        _taskbarIcon.ShowBalloonTip(title, message, balloonIcon);

        _logger.LogDebug("Notification shown: {Title} - {Message}", title, message);
    }

    public void UpdateTooltip(string text)
    {
        ThrowIfDisposed();
        
        // Hardcodet supports longer tooltips than Windows Forms
        _taskbarIcon.ToolTipText = text;
    }

    #endregion

    #region Event Handlers

    private void OnTrayLeftMouseUp(object sender, RoutedEventArgs e)
    {
        _logger.LogDebug("Tray icon left-clicked");
        // Single click - could show a popup menu or do nothing
    }

    private void OnTrayDoubleClick(object sender, RoutedEventArgs e)
    {
        _logger.LogDebug("Tray icon double-clicked");
        RestoreFromTray();
    }

    private void OnOpenClicked(object sender, RoutedEventArgs e)
    {
        _logger.LogDebug("Open menu item clicked");
        RestoreFromTray();
    }

    private void OnLockVaultClicked(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("Lock vault requested from tray menu");
        
        WinApplication.Current.Dispatcher.Invoke(() =>
        {
            // TODO: Integrate with your existing logout logic via MediatR
            // Send LogoutUserCommand
        });

        ShowNotification(
            "Vault Locked",
            "Your vault has been locked for security.",
            NotificationIcon.Info,
            3000
        );
    }

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("Exit requested from tray menu");

        var result = System.Windows.MessageBox.Show(
            "Are you sure you want to exit Password Manager?",
            "Confirm Exit",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question
        );

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            // Force exit - need to unsubscribe MainWindow.Closing event first
            WinApplication.Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = WinApplication.Current.MainWindow;
                if (mainWindow != null)
                {
                    // Try to call ForceExit method if it exists (cleaner approach)
                    var forceExitMethod = mainWindow.GetType().GetMethod("ForceExit");
                    if (forceExitMethod != null)
                    {
                        _logger.LogInformation("Calling MainWindow.ForceExit()");
                        forceExitMethod.Invoke(mainWindow, null);
                    }
                    else
                    {
                        // Fallback: Close the window first, then shutdown
                        _logger.LogInformation("ForceExit method not found, using shutdown");
                        mainWindow.Close();
                        WinApplication.Current.Shutdown();
                    }
                }
                else
                {
                    // No main window, just shutdown
                    WinApplication.Current.Shutdown();
                }
            });
        }
    }

    #endregion

    #region Private Helpers

    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();

        // Open
        var openItem = new MenuItem { Header = "Open Password Manager" };
        openItem.Click += OnOpenClicked;
        menu.Items.Add(openItem);

        menu.Items.Add(new Separator());

        // Lock Vault
        var lockItem = new MenuItem { Header = "Lock Vault" };
        lockItem.Click += OnLockVaultClicked;
        menu.Items.Add(lockItem);

        menu.Items.Add(new Separator());

        // Exit
        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += OnExitClicked;
        menu.Items.Add(exitItem);

        return menu;
    }

    private System.Windows.Media.ImageSource? LoadIconSource()
    {
        try
        {
            // Try to load from application resources
            var iconUri = new Uri("pack://application:,,,/Resources/app-icon.ico", UriKind.Absolute);
            var iconSource = new System.Windows.Media.Imaging.BitmapImage(iconUri);
            return iconSource;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load application icon, using default");
            return null; // TaskbarIcon will use default icon
        }
    }

    private static BalloonIcon ConvertNotificationIcon(NotificationIcon icon)
    {
        return icon switch
        {
            NotificationIcon.Info => BalloonIcon.Info,
            NotificationIcon.Warning => BalloonIcon.Warning,
            NotificationIcon.Error => BalloonIcon.Error,
            _ => BalloonIcon.None
        };
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SystemTrayService));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        _logger.LogInformation("Disposing system tray service");

        _taskbarIcon.Visibility = Visibility.Collapsed;
        _taskbarIcon.Dispose();

        _disposed = true;
    }

    #endregion
}