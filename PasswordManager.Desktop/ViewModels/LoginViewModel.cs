using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PasswordManager.Desktop.Services;
using PasswordManager.Domain.Entities;
using PasswordManager.Domain.Interfaces;
using PasswordManager.Infrastructure.Repositories;

namespace PasswordManager.Desktop.ViewModels;

/// <summary>
/// ViewModel for Login/Registration window.
/// Handles user authentication and master password setup.
/// </summary>
public partial class LoginViewModel : ViewModelBase
{
    private readonly VaultDbContext _dbContext;
    private readonly ICryptoProvider _cryptoProvider;
    private readonly IMasterPasswordService _masterPasswordService;
    private readonly ISessionService _sessionService;
    private readonly IPasswordStrengthService _passwordStrengthService;
    private readonly IWindowFactory _windowFactory;

    [ObservableProperty] private string _email = string.Empty;

    [ObservableProperty] private string _masterPassword = string.Empty;

    [ObservableProperty] private string _confirmPassword = string.Empty;

    [ObservableProperty] private bool _isRegisterMode;

    [ObservableProperty] private string _passwordStrengthText = string.Empty;

    [ObservableProperty] private string _passwordStrengthColor = "Gray";

    [ObservableProperty] private int _passwordStrengthScore;

    [ObservableProperty] private bool _showPassword;

    public LoginViewModel(
        VaultDbContext dbContext,
        ICryptoProvider cryptoProvider,
        IMasterPasswordService masterPasswordService,
        ISessionService sessionService,
        IPasswordStrengthService passwordStrengthService,
        IWindowFactory windowFactory,
        IDialogService dialogService,
        ILogger<LoginViewModel> logger)
        : base(dialogService, logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
        _masterPasswordService =
            masterPasswordService ?? throw new ArgumentNullException(nameof(masterPasswordService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _passwordStrengthService =
            passwordStrengthService ?? throw new ArgumentNullException(nameof(passwordStrengthService));
        _windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));

        Title = "Password Manager - Login";
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (!ValidateLoginInput())
            return;

        await ExecuteAsync(async () =>
        {
            Logger.LogInformation("=== LOGIN ATTEMPT START ===");
            Logger.LogInformation("Attempting login for user: {Email}", Email);

            // Get user from database
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email.ToLower() == Email.ToLower());

            if (user == null)
            {
                throw new InvalidOperationException("Invalid email or master password");
            }

            // ✅ DEBUG: Log salt information
            Logger.LogInformation("User found in database:");
            Logger.LogInformation("  - User ID: {UserId}", user.Id);
            Logger.LogInformation("  - Email: {Email}", user.Email);
            Logger.LogInformation("  - Salt length: {SaltLength} bytes", user.Salt?.Length ?? 0);
            Logger.LogInformation("  - Salt (first 8 bytes): {SaltPrefix}",
                user.Salt != null && user.Salt.Length >= 8
                    ? Convert.ToHexString(user.Salt[..8])
                    : "NULL or TOO SHORT");

            // Check if account is locked
            if (user.IsLocked)
            {
                throw new InvalidOperationException(
                    $"Account is locked due to too many failed attempts. Please try again later.");
            }

            // Verify master password
            Logger.LogInformation("Verifying master password...");
            var isValid = await _masterPasswordService.VerifyMasterPasswordAsync(
                MasterPassword,
                user.MasterPasswordHash);

            if (!isValid)
            {
                Logger.LogWarning("Password verification FAILED");
                await IncrementFailedLoginAttemptsAsync(user);
                throw new InvalidOperationException("Invalid email or master password");
            }

            Logger.LogInformation("✓ Password verification SUCCESS");

            // Reset failed attempts on successful login
            if (user.FailedLoginAttempts > 0)
            {
                var userToUpdate = await _dbContext.Users
                    .FindAsync(user.Id);
                
                if (userToUpdate == null)
                {
                    throw new InvalidOperationException("User not found during failed attempts reset");
                }
                
                userToUpdate.FailedLoginAttempts = 0;
                userToUpdate.LastFailedLoginUtc = null;
                userToUpdate.LastLoginUtc = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }

            await _masterPasswordService.InitializeAsync(MasterPassword, user.Salt);

            // Check state after initialization
            Logger.LogInformation("MasterPasswordService.IsInitialized AFTER: {IsInitialized}",
                _masterPasswordService.IsInitialized);

            // Start user session
            _sessionService.StartSession(user);

            Logger.LogInformation("Login successful for user: {Email}", Email);

            // Open main window
            OpenMainWindow();
        }, "Login failed. Please check your credentials.");
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (!ValidateRegisterInput())
            return;

        await ExecuteAsync(async () =>
        {
            Logger.LogInformation("Attempting registration for user: {Email}", Email);

            // Check if user already exists
            var existingUser = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == Email.ToLower());

            if (existingUser != null)
            {
                throw new InvalidOperationException("Email is already registered");
            }

            // Check password strength
            var strength = _passwordStrengthService.EvaluateStrength(MasterPassword);
            if (strength < Domain.Enums.StrengthLevel.Fair)
            {
                throw new InvalidOperationException(
                    "Master password is too weak. Please choose a stronger password.");
            }

            // Hash master password using Argon2id
            var passwordHash = await _cryptoProvider.HashPasswordAsync(MasterPassword);

            // Generate master encryption key (256-bit random key)
            var masterKey = _cryptoProvider.GenerateRandomKey(32);

            // Derive encryption key from master password
            var (encryptionKey, salt) = await _cryptoProvider.DeriveKeyAsync(MasterPassword);

            // Encrypt master key with derived key
            var encryptedMasterKey = await _cryptoProvider.EncryptAsync(
                Convert.ToBase64String(masterKey),
                encryptionKey);

            // Create user entity
            var user = new User
            {
                Email = Email,
                MasterPasswordHash = passwordHash,
                Salt = salt,
                EncryptedMasterKey = encryptedMasterKey.ToCombinedString(),
                IsPremium = false, // Free tier by default
                EmailVerified = false,
                TwoFactorEnabled = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            // Save to database
            await _dbContext.Users.AddAsync(user);
            await _dbContext.SaveChangesAsync();

            Logger.LogInformation("Registration successful for user: {Email}", Email);

            // Show success message
            ShowInfo(
                "Registration successful! You can now log in with your credentials.",
                "Success");

            // Switch to login mode
            IsRegisterMode = false;
            MasterPassword = string.Empty;
            ConfirmPassword = string.Empty;
        }, "Registration failed. Please try again.");
    }

    [RelayCommand]
    private void ToggleMode()
    {
        IsRegisterMode = !IsRegisterMode;
        Title = IsRegisterMode ? "Password Manager - Register" : "Password Manager - Login";
        ClearForm();
    }

    [RelayCommand]
    private void ToggleShowPassword()
    {
        ShowPassword = !ShowPassword;
    }

    partial void OnMasterPasswordChanged(string value)
    {
        if (IsRegisterMode && !string.IsNullOrEmpty(value))
        {
            UpdatePasswordStrength(value);
        }
        else
        {
            PasswordStrengthText = string.Empty;
            PasswordStrengthScore = 0;
        }
    }

    private void UpdatePasswordStrength(string password)
    {
        var analysis = _passwordStrengthService.AnalyzePassword(password);

        PasswordStrengthScore = analysis.Score;
        PasswordStrengthText = analysis.Level switch
        {
            Domain.Enums.StrengthLevel.VeryWeak => "Very Weak",
            Domain.Enums.StrengthLevel.Weak => "Weak",
            Domain.Enums.StrengthLevel.Fair => "Fair",
            Domain.Enums.StrengthLevel.Strong => "Strong",
            Domain.Enums.StrengthLevel.VeryStrong => "Very Strong",
            _ => "Unknown"
        };

        PasswordStrengthColor = analysis.Level switch
        {
            Domain.Enums.StrengthLevel.VeryWeak => "#D32F2F",
            Domain.Enums.StrengthLevel.Weak => "#F57C00",
            Domain.Enums.StrengthLevel.Fair => "#FBC02D",
            Domain.Enums.StrengthLevel.Strong => "#689F38",
            Domain.Enums.StrengthLevel.VeryStrong => "#388E3C",
            _ => "Gray"
        };
    }

    private bool ValidateLoginInput()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            ShowError("Please enter your email");
            return false;
        }

        if (string.IsNullOrWhiteSpace(MasterPassword))
        {
            ShowError("Please enter your master password");
            return false;
        }

        if (!IsValidEmail(Email))
        {
            ShowError("Please enter a valid email address");
            return false;
        }

        return true;
    }

    private bool ValidateRegisterInput()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            ShowError("Please enter your email");
            return false;
        }

        if (!IsValidEmail(Email))
        {
            ShowError("Please enter a valid email address");
            return false;
        }

        if (string.IsNullOrWhiteSpace(MasterPassword))
        {
            ShowError("Please enter a master password");
            return false;
        }

        if (MasterPassword.Length < 8)
        {
            ShowError("Master password must be at least 8 characters");
            return false;
        }

        if (string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            ShowError("Please confirm your master password");
            return false;
        }

        if (MasterPassword != ConfirmPassword)
        {
            ShowError("Passwords do not match");
            return false;
        }

        return true;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private void ClearForm()
    {
        Email = string.Empty;
        MasterPassword = string.Empty;
        ConfirmPassword = string.Empty;
        ErrorMessage = null;
        PasswordStrengthText = string.Empty;
        PasswordStrengthScore = 0;
    }

    private async Task IncrementFailedLoginAttemptsAsync(User user)
    {
        var failedAttempts = user.FailedLoginAttempts + 1;
        var isLocked = failedAttempts >= 5;

        user.FailedLoginAttempts = failedAttempts;
        user.LastFailedLoginUtc = DateTime.UtcNow;
        user.IsLocked = isLocked;

        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync();

        if (isLocked)
        {
            Logger.LogWarning("Account locked for user: {Email}", user.Email);
        }
    }

    private void OpenMainWindow()
    {
        try
        {
            Logger.LogInformation("=== Opening Main Window ===");

            var mainWindow = _windowFactory.CreateMainWindow();

            Logger.LogInformation("✓ MainWindow created successfully");
            Logger.LogInformation("  - DataContext Type: {Type}",
                mainWindow.DataContext?.GetType().Name ?? "NULL");

            if (mainWindow.DataContext is MainViewModel mainViewModel)
            {
                Logger.LogInformation("✓ DataContext is MainViewModel");
                Logger.LogInformation("  - CurrentViewModel: {Type}",
                    mainViewModel.CurrentViewModel?.GetType().Name ?? "NULL");
                Logger.LogInformation("  - UserEmail: {Email}", mainViewModel.UserEmail);
            }
            else
            {
                Logger.LogError("❌ MainWindow.DataContext is NOT MainViewModel!");
                ShowError("Failed to initialize main window. Please check logs.");
                return;
            }

            mainWindow.Show();
            Logger.LogInformation("✓ MainWindow displayed");

            // Close login window
            Application.Current.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w.DataContext == this)
                ?.Close();

            Logger.LogInformation("=== Login window closed ===");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to open main window");
            ShowError($"Failed to open application: {ex.Message}\n\nSee logs for details.");
        }
    }
}