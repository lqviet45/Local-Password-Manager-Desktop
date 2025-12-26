using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;
using MediatR;
using Microsoft.Extensions.Logging;
using PasswordManager.Desktop.Services;
using PasswordManager.Domain.Enums;
using PasswordManager.Domain.Interfaces;
using PasswordManager.Domain.ValueObjects;
using PasswordManager.Shared.Vault.Commands;
using PasswordManager.Shared.Vault.Dto;

namespace PasswordManager.Desktop.Services.Impl;

/// <summary>
/// Production implementation of auto-save credentials service.
/// Monitors browser forms and captures new login credentials.
/// </summary>
public sealed class AutoSaveCredentialsService : IAutoSaveCredentialsService
{
    private readonly IMediator _mediator;
    private readonly ILogger<AutoSaveCredentialsService> _logger;
    private readonly IMasterPasswordService _masterPasswordService;
    private readonly IAutoFillService _autoFillService;
    private readonly ICryptoProvider _cryptoProvider;
    private readonly IDialogService _dialogService;
    private bool _isMonitoring;
    private bool _disposed;

    public bool IsMonitoring => _isMonitoring;

    public AutoSaveCredentialsService(
        IMediator mediator,
        ILogger<AutoSaveCredentialsService> logger,
        IMasterPasswordService masterPasswordService,
        IAutoFillService autoFillService,
        ICryptoProvider cryptoProvider,
        IDialogService dialogService)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _masterPasswordService = masterPasswordService ?? throw new ArgumentNullException(nameof(masterPasswordService));
        _autoFillService = autoFillService ?? throw new ArgumentNullException(nameof(autoFillService));
        _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_isMonitoring)
        {
            _logger.LogWarning("Auto-save monitoring is already active");
            return;
        }

        _isMonitoring = true;
        _logger.LogInformation("Started auto-save credentials monitoring");

        // TODO: Implement actual monitoring
        // This would typically:
        // 1. Hook into browser events via extension or Windows API
        // 2. Monitor form submissions
        // 3. Capture username/password fields
        // 4. Prompt user to save (or auto-save if configured)
    }

    public async Task StopMonitoringAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_isMonitoring)
        {
            return;
        }

        _isMonitoring = false;
        _logger.LogInformation("Stopped auto-save credentials monitoring");
    }

    public async Task<bool> CaptureCredentialsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            // Detect browser context
            var browserContext = await _autoFillService.DetectBrowserContextAsync(cancellationToken);
            if (browserContext == null)
            {
                _logger.LogWarning("Cannot capture credentials: No browser detected");
                return false;
            }

            _logger.LogInformation("Credential capture triggered for URL: {Url}", browserContext.Url);

            // Get foreground window
            var foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                _logger.LogWarning("No foreground window found");
                return false;
            }

            // Capture credentials from form fields using UI Automation
            var capturedCredentials = await CaptureFormFieldsAsync(foregroundWindow, cancellationToken);
            if (capturedCredentials == null)
            {
                _logger.LogWarning("Failed to capture credentials from form");
                return false;
            }

            // Prompt user to save
            var itemName = ExtractSiteName(browserContext.Url);
            var confirmMessage = $"Save credentials for {itemName}?\n\nUsername: {capturedCredentials.Username ?? "(empty)"}\nURL: {browserContext.Url}";
            
            if (!_dialogService.ShowConfirmation(confirmMessage, "Save Credentials?"))
            {
                _logger.LogInformation("User declined to save credentials");
                return false;
            }

            // Get item name from user if needed
            if (string.IsNullOrWhiteSpace(itemName))
            {
                itemName = _dialogService.ShowInputDialog("Enter a name for this login:", "Save Credentials", browserContext.Domain) ?? browserContext.Domain;
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    return false;
                }
            }

            // Create vault item
            var loginData = new LoginData
            {
                Username = capturedCredentials.Username,
                Password = capturedCredentials.Password,
                Website = browserContext.Url
            };

            // Create VaultItemRequest - the Password field contains the JSON payload
            // The handler will encrypt it
            var itemRequest = new VaultItemRequest
            {
                Type = VaultItemType.Login,
                Name = itemName,
                Username = capturedCredentials.Username,
                Password = loginData.ToJson(), // JSON payload to be encrypted by handler
                Url = browserContext.Url,
                Notes = null,
                Tags = null,
                IsFavorite = false
            };

            var encryptionKey = _masterPasswordService.GetPreferredKey();
            var command = new CreateVaultItemCommand(
                UserId: userId,
                Item: itemRequest,
                EncryptionKey: encryptionKey
            );

            var result = await _mediator.Send(command, cancellationToken);
            if (result.IsFailure)
            {
                _logger.LogError("Failed to create vault item: {Error}", result.Error);
                _dialogService.ShowError($"Failed to save credentials: {result.Error}", "Error");
                return false;
            }

            _logger.LogInformation("Successfully saved credentials for URL: {Url}", browserContext.Url);
            _dialogService.ShowInfo("Credentials saved successfully!", "Success");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing credentials");
            _dialogService.ShowError($"Error capturing credentials: {ex.Message}", "Error");
            return false;
        }
    }

    private async Task<CapturedCredentials?> CaptureFormFieldsAsync(IntPtr windowHandle, CancellationToken cancellationToken)
    {
        try
        {
            var rootElement = AutomationElement.FromHandle(windowHandle);
            if (rootElement == null)
            {
                _logger.LogWarning("Failed to get AutomationElement from window handle");
                return null;
            }

            // Find all input fields
            var inputFields = rootElement.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)
            );

            string? username = null;
            string? password = null;

            foreach (AutomationElement field in inputFields)
            {
                try
                {
                    var isPassword = field.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty) as bool? ?? false;
                    var value = GetFieldValue(field);

                    if (isPassword && string.IsNullOrEmpty(password))
                    {
                        password = value;
                        _logger.LogDebug("Captured password field");
                    }
                    else if (!isPassword && string.IsNullOrEmpty(username))
                    {
                        // Check if it looks like a username/email field
                        var name = field.GetCurrentPropertyValue(AutomationElement.NameProperty)?.ToString() ?? "";
                        var automationId = field.GetCurrentPropertyValue(AutomationElement.AutomationIdProperty)?.ToString() ?? "";
                        
                        if (name.Contains("user", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("email", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("login", StringComparison.OrdinalIgnoreCase) ||
                            automationId.Contains("user", StringComparison.OrdinalIgnoreCase) ||
                            automationId.Contains("email", StringComparison.OrdinalIgnoreCase) ||
                            automationId.Contains("login", StringComparison.OrdinalIgnoreCase))
                        {
                            username = value;
                            _logger.LogDebug("Captured username field");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error reading field value");
                }
            }

            // If we didn't find username by name, use the first non-password field
            if (string.IsNullOrEmpty(username))
            {
                foreach (AutomationElement field in inputFields)
                {
                    var isPassword = field.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty) as bool? ?? false;
                    if (!isPassword)
                    {
                        username = GetFieldValue(field);
                        if (!string.IsNullOrEmpty(username))
                        {
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
            {
                return null;
            }

            return new CapturedCredentials(username, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing form fields");
            return null;
        }
    }

    private string? GetFieldValue(AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var patternObj) && patternObj is ValuePattern valuePattern)
            {
                return valuePattern.Current.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting field value using ValuePattern");
        }

        return null;
    }

    private string ExtractSiteName(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return uri.Host.Replace("www.", "");
            }
        }
        catch
        {
            // Ignore
        }

        return url;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private sealed record CapturedCredentials(string? Username, string? Password);

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AutoSaveCredentialsService));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_isMonitoring)
        {
            StopMonitoringAsync().GetAwaiter().GetResult();
        }

        _disposed = true;
    }
}

