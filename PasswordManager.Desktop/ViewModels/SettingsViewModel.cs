using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PasswordManager.Desktop.Services;

namespace PasswordManager.Desktop.ViewModels;

/// <summary>
/// ViewModel for application settings.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISessionService _sessionService;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private bool _isPremium;

    [ObservableProperty]
    private DateTime _accountCreatedDate;

    [ObservableProperty]
    private int _autoLockMinutes = 15;

    [ObservableProperty]
    private int _clipboardClearSeconds = 30;

    [ObservableProperty]
    private bool _enableAutoLock = true;

    [ObservableProperty]
    private bool _enableClipboardClear = true;

    [ObservableProperty]
    private string _databasePath = "vault.db";

    [ObservableProperty]
    private long _databaseSize;

    public SettingsViewModel(
        ISessionService sessionService,
        IDialogService dialogService,
        ILogger<SettingsViewModel> logger)
        : base(dialogService, logger)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));

        Title = "Settings";

        LoadUserInfo();
    }

    private void LoadUserInfo()
    {
        if (_sessionService.CurrentUser != null)
        {
            UserEmail = _sessionService.CurrentUser.Email;
            IsPremium = _sessionService.CurrentUser.IsPremium;
            AccountCreatedDate = _sessionService.CurrentUser.CreatedAtUtc.ToLocalTime();
        }

        // Load database info
        if (File.Exists(DatabasePath))
        {
            var fileInfo = new FileInfo(DatabasePath);
            DatabaseSize = fileInfo.Length;
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        await ExecuteAsync(async () =>
        {
            // TODO: Save settings to file or database
            await Task.Delay(500); // Simulate save

            ShowInfo("Settings saved successfully");
            Logger.LogInformation("Settings saved");
        });
    }

    [RelayCommand]
    private async Task ChangeMasterPasswordAsync()
    {
        ShowInfo("Change master password - Coming soon!");
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ExportVaultAsync()
    {
        if (!Confirm("Export vault to unencrypted file? This is not recommended!", "Export Vault"))
        {
            return;
        }

        ShowInfo("Export vault - Coming soon!");
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ImportVaultAsync()
    {
        ShowInfo("Import vault - Coming soon!");
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task BackupDatabaseAsync()
    {
        await ExecuteAsync(async () =>
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = $"vault_backup_{timestamp}.db";

            if (File.Exists(DatabasePath))
            {
                await Task.Run(() => File.Copy(DatabasePath, backupPath));
                ShowInfo($"Database backed up to: {backupPath}");
                Logger.LogInformation("Database backed up to: {Path}", backupPath);
            }
            else
            {
                ShowWarning("Database file not found");
            }
        });
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        var logsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        
        if (Directory.Exists(logsPath))
        {
            System.Diagnostics.Process.Start("explorer.exe", logsPath);
        }
        else
        {
            ShowWarning("Logs folder not found");
        }
    }

    [RelayCommand]
    private void UpgradeToPremium()
    {
        ShowInfo("Premium subscription - Coming soon!\n\nPremium features:\n- Cloud sync across devices\n- 2FA support\n- Priority support");
    }

    public string DatabaseSizeFormatted => FormatBytes(DatabaseSize);

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}