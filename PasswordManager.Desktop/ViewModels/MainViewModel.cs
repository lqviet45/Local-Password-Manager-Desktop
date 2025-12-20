using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PasswordManager.Desktop.Services;

namespace PasswordManager.Desktop.ViewModels;

/// <summary>
/// Main window ViewModel.
/// Orchestrates navigation between different views (Vault, Settings, etc.)
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly ISessionService _sessionService;
    private readonly IMasterPasswordService _masterPasswordService;
    private readonly IWindowFactory _windowFactory;

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private bool _isPremium;

    [ObservableProperty]
    private string _selectedNavItem = "Vault";

    public VaultViewModel VaultViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }

    public MainViewModel(
        VaultViewModel vaultViewModel,
        SettingsViewModel settingsViewModel,
        ISessionService sessionService,
        IMasterPasswordService masterPasswordService,
        IWindowFactory windowFactory,
        IDialogService dialogService,
        ILogger<MainViewModel> logger)
        : base(dialogService, logger)
    {
        VaultViewModel = vaultViewModel ?? throw new ArgumentNullException(nameof(vaultViewModel));
        SettingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _masterPasswordService = masterPasswordService ?? throw new ArgumentNullException(nameof(masterPasswordService));
        _windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));

        Title = "Password Manager";

        // Initialize user info
        if (_sessionService.CurrentUser != null)
        {
            UserEmail = _sessionService.CurrentUser.Email;
            IsPremium = _sessionService.CurrentUser.IsPremium;
        }

        // Set default view
        CurrentViewModel = VaultViewModel;

        // Subscribe to session events
        _sessionService.SessionEnding += OnSessionEnding;
    }

    [RelayCommand]
    private void NavigateToVault()
    {
        CurrentViewModel = VaultViewModel;
        SelectedNavItem = "Vault";
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        CurrentViewModel = SettingsViewModel;
        SelectedNavItem = "Settings";
    }

    [RelayCommand]
    private async Task LockAsync()
    {
        if (Confirm("Are you sure you want to lock the vault?", "Lock Vault"))
        {
            await LogoutAsync();
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        Logger.LogInformation("User logging out: {Email}", UserEmail);

        // Clear sensitive data
        _masterPasswordService.ClearSensitiveData();

        // End session
        _sessionService.EndSession();

        // Show login window
        var loginWindow = _windowFactory.CreateLoginWindow();
        loginWindow.Show();

        // Close main window
        Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.DataContext == this)
            ?.Close();
    }

    private void OnSessionEnding(object? sender, EventArgs e)
    {
        Logger.LogInformation("Session ending for user: {Email}", UserEmail);
    }
}