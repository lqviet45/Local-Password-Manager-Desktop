using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using PasswordManager.Desktop.Services;
using PasswordManager.Domain.Enums;
using PasswordManager.Domain.Interfaces;
using PasswordManager.Domain.ValueObjects;
using PasswordManager.Shared.Vault.Commands;
using PasswordManager.Shared.Vault.Dto;
using PasswordManager.Shared.Vault.Queries;

namespace PasswordManager.Desktop.Services.Impl;

/// <summary>
/// Handles messages from browser extensions.
/// Processes GetCredentials, SaveCredentials, FillCredentials, and other extension requests.
/// </summary>
public sealed class BrowserExtensionMessageHandler : IDisposable
{
    private readonly IMediator _mediator;
    private readonly ILogger<BrowserExtensionMessageHandler> _logger;
    private readonly IMasterPasswordService _masterPasswordService;
    private readonly ICryptoProvider _cryptoProvider;
    private readonly IBrowserExtensionCommunicator _communicator;
    private readonly IAutoFillService _autoFillService;
    private readonly IAutoSaveCredentialsService _autoSaveService;
    private readonly ISessionService _sessionService;
    private bool _disposed;

    public BrowserExtensionMessageHandler(
        IMediator mediator,
        ILogger<BrowserExtensionMessageHandler> logger,
        IMasterPasswordService masterPasswordService,
        ICryptoProvider cryptoProvider,
        IBrowserExtensionCommunicator communicator,
        IAutoFillService autoFillService,
        IAutoSaveCredentialsService autoSaveService,
        ISessionService sessionService)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _masterPasswordService = masterPasswordService ?? throw new ArgumentNullException(nameof(masterPasswordService));
        _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
        _communicator = communicator ?? throw new ArgumentNullException(nameof(communicator));
        _autoFillService = autoFillService ?? throw new ArgumentNullException(nameof(autoFillService));
        _autoSaveService = autoSaveService ?? throw new ArgumentNullException(nameof(autoSaveService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));

        // Subscribe to message events
        _communicator.MessageReceived += OnMessageReceived;
        _communicator.BrowserConnected += OnBrowserConnected;
    }

    private async void OnMessageReceived(object? sender, ExtensionMessageReceivedEventArgs e)
    {
        try
        {
            _logger.LogInformation("Received message from browser extension: {Type}, MessageId: {MessageId}", 
                e.Message.Type, e.Message.MessageId);

            ExtensionMessage? response = null;

            switch (e.Message.Type)
            {
                case ExtensionMessageType.Ping:
                    response = await HandlePingAsync(e.Message);
                    break;

                case ExtensionMessageType.GetCredentials:
                    response = await HandleGetCredentialsAsync(e.Message);
                    break;

                case ExtensionMessageType.SaveCredentials:
                    response = await HandleSaveCredentialsAsync(e.Message);
                    break;

                case ExtensionMessageType.FillCredentials:
                    response = await HandleFillCredentialsAsync(e.Message);
                    break;

                case ExtensionMessageType.CheckVaultLocked:
                    response = await HandleCheckVaultLockedAsync(e.Message);
                    break;

                default:
                    _logger.LogWarning("Unknown message type: {Type}", e.Message.Type);
                    response = CreateErrorResponse(e.Message, "Unknown message type");
                    break;
            }

            if (response != null)
            {
                await _communicator.SendMessageAsync(response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling extension message: {Type}", e.Message.Type);
            try
            {
                var errorResponse = CreateErrorResponse(e.Message, ex.Message);
                await _communicator.SendMessageAsync(errorResponse);
            }
            catch
            {
                // Ignore if we can't send error response
            }
        }
    }

    private async Task<ExtensionMessage> HandlePingAsync(ExtensionMessage message)
    {
        _logger.LogDebug("Handling Ping message");
        return new ExtensionMessage
        {
            Type = ExtensionMessageType.Pong,
            MessageId = message.MessageId,
            Data = new Dictionary<string, object> { { "status", "ok" } },
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task<ExtensionMessage> HandleGetCredentialsAsync(ExtensionMessage message)
    {
        _logger.LogInformation("Handling GetCredentials request");

        if (_sessionService.CurrentUser == null)
        {
            return CreateErrorResponse(message, "User not logged in");
        }

        if (!_masterPasswordService.IsInitialized)
        {
            return CreateErrorResponse(message, "Vault is locked");
        }

        try
        {
            // Extract URL from message data
            var url = message.Data?.GetValueOrDefault("url")?.ToString();
            if (string.IsNullOrEmpty(url))
            {
                return CreateErrorResponse(message, "URL is required");
            }

            // Find matching vault items
            var matches = await _autoFillService.FindMatchingItemsAsync(_sessionService.CurrentUser.Id, url);
            
            if (matches.Count == 0)
            {
                return new ExtensionMessage
                {
                    Type = ExtensionMessageType.GetCredentials,
                    MessageId = message.MessageId,
                    Data = new Dictionary<string, object>
                    {
                        { "success", false },
                        { "message", "No matching credentials found" },
                        { "credentials", new List<object>() }
                    },
                    Timestamp = DateTime.UtcNow
                };
            }

            // Get credentials for all matches
            var credentialsList = new List<Dictionary<string, object>>();
            foreach (var match in matches)
            {
                var item = await GetVaultItemAsync(match.VaultItemId);
                if (item != null)
                {
                    var decrypted = await DecryptVaultItemAsync(item);
                    if (decrypted != null)
                    {
                        credentialsList.Add(new Dictionary<string, object>
                        {
                            { "id", match.VaultItemId.ToString() },
                            { "name", match.Name },
                            { "username", decrypted.Username ?? "" },
                            { "password", decrypted.Password ?? "" },
                            { "url", match.Url },
                            { "matchScore", match.MatchScore }
                        });
                    }
                }
            }

            return new ExtensionMessage
            {
                Type = ExtensionMessageType.GetCredentials,
                MessageId = message.MessageId,
                Data = new Dictionary<string, object>
                {
                    { "success", true },
                    { "credentials", credentialsList },
                    { "count", credentialsList.Count }
                },
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting credentials");
            return CreateErrorResponse(message, $"Error getting credentials: {ex.Message}");
        }
    }

    private async Task<ExtensionMessage> HandleSaveCredentialsAsync(ExtensionMessage message)
    {
        _logger.LogInformation("Handling SaveCredentials request");

        if (_sessionService.CurrentUser == null)
        {
            return CreateErrorResponse(message, "User not logged in");
        }

        if (!_masterPasswordService.IsInitialized)
        {
            return CreateErrorResponse(message, "Vault is locked");
        }

        try
        {
            // Extract data from message
            var url = message.Data?.GetValueOrDefault("url")?.ToString();
            var username = message.Data?.GetValueOrDefault("username")?.ToString();
            var password = message.Data?.GetValueOrDefault("password")?.ToString();
            var name = message.Data?.GetValueOrDefault("name")?.ToString();

            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(password))
            {
                return CreateErrorResponse(message, "URL and password are required");
            }

            // Extract site name from URL
            var itemName = name;
            if (string.IsNullOrEmpty(itemName))
            {
                try
                {
                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        itemName = uri.Host.Replace("www.", "");
                    }
                    else
                    {
                        itemName = url;
                    }
                }
                catch
                {
                    itemName = url;
                }
            }

            // Create login data
            var loginData = new LoginData
            {
                Username = username,
                Password = password,
                Website = url
            };

            // Create vault item request
            var itemRequest = new VaultItemRequest
            {
                Type = VaultItemType.Login,
                Name = itemName,
                Username = username,
                Password = loginData.ToJson(),
                Url = url,
                Notes = null,
                Tags = null,
                IsFavorite = false
            };

            var encryptionKey = _masterPasswordService.GetPreferredKey();
            var command = new CreateVaultItemCommand(
                UserId: _sessionService.CurrentUser.Id,
                Item: itemRequest,
                EncryptionKey: encryptionKey
            );

            var result = await _mediator.Send(command);
            if (result.IsFailure)
            {
                return CreateErrorResponse(message, $"Failed to save credentials: {result.Error?.Message}");
            }

            _logger.LogInformation("Successfully saved credentials for URL: {Url}", url);

            return new ExtensionMessage
            {
                Type = ExtensionMessageType.SaveCredentials,
                MessageId = message.MessageId,
                Data = new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", "Credentials saved successfully" },
                    { "itemId", result.Value?.Id.ToString() ?? "" }
                },
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving credentials");
            return CreateErrorResponse(message, $"Error saving credentials: {ex.Message}");
        }
    }

    private async Task<ExtensionMessage> HandleFillCredentialsAsync(ExtensionMessage message)
    {
        _logger.LogInformation("Handling FillCredentials request");

        if (_sessionService.CurrentUser == null)
        {
            return CreateErrorResponse(message, "User not logged in");
        }

        if (!_masterPasswordService.IsInitialized)
        {
            return CreateErrorResponse(message, "Vault is locked");
        }

        try
        {
            // Extract vault item ID from message
            var itemIdStr = message.Data?.GetValueOrDefault("vaultItemId")?.ToString();
            if (string.IsNullOrEmpty(itemIdStr) || !Guid.TryParse(itemIdStr, out var itemId))
            {
                return CreateErrorResponse(message, "Invalid vault item ID");
            }

            // Fill credentials using AutoFillService
            var success = await _autoFillService.FillCredentialsAsync(itemId);

            return new ExtensionMessage
            {
                Type = ExtensionMessageType.FillCredentials,
                MessageId = message.MessageId,
                Data = new Dictionary<string, object>
                {
                    { "success", success },
                    { "message", success ? "Credentials filled successfully" : "Failed to fill credentials" }
                },
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filling credentials");
            return CreateErrorResponse(message, $"Error filling credentials: {ex.Message}");
        }
    }

    private async Task<ExtensionMessage> HandleCheckVaultLockedAsync(ExtensionMessage message)
    {
        var isLocked = !_masterPasswordService.IsInitialized || _sessionService.CurrentUser == null;

        return new ExtensionMessage
        {
            Type = ExtensionMessageType.CheckVaultLocked,
            MessageId = message.MessageId,
            Data = new Dictionary<string, object>
            {
                { "locked", isLocked },
                { "message", isLocked ? "Vault is locked" : "Vault is unlocked" }
            },
            Timestamp = DateTime.UtcNow
        };
    }

    private void OnBrowserConnected(object? sender, BrowserConnectedEventArgs e)
    {
        _logger.LogInformation("Browser extension connected: {Browser}, ExtensionId: {ExtensionId}", 
            e.Browser, e.ExtensionId);

        // Auto-start monitoring when extension connects
        _ = Task.Run(async () =>
        {
            try
            {
                await _autoSaveService.StartMonitoringAsync();
                _logger.LogInformation("Auto-save monitoring started after browser connection");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start auto-save monitoring");
            }
        });
    }

    private ExtensionMessage CreateErrorResponse(ExtensionMessage originalMessage, string errorMessage)
    {
        return new ExtensionMessage
        {
            Type = originalMessage.Type,
            MessageId = originalMessage.MessageId,
            Data = new Dictionary<string, object>
            {
                { "success", false },
                { "error", errorMessage }
            },
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task<VaultItemDto?> GetVaultItemAsync(Guid itemId)
    {
        if (_sessionService.CurrentUser == null)
            return null;

        try
        {
            var query = new GetVaultItemsQuery(_sessionService.CurrentUser.Id, false);
            var result = await _mediator.Send(query);
            if (result.IsFailure || result.Value == null)
                return null;

            return result.Value.FirstOrDefault(i => i.Id == itemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vault item: {ItemId}", itemId);
            return null;
        }
    }

    private async Task<LoginData?> DecryptVaultItemAsync(VaultItemDto item)
    {
        try
        {
            var encryptedData = EncryptedData.FromCombinedString(item.EncryptedData);
            var encryptionKey = _masterPasswordService.GetPreferredKey();
            var decryptedJson = await _cryptoProvider.DecryptAsync(encryptedData, encryptionKey);

            if (LoginData.TryFromJson(decryptedJson, out var loginData))
            {
                return loginData;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrypting vault item: {ItemId}", item.Id);
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _communicator.MessageReceived -= OnMessageReceived;
        _communicator.BrowserConnected -= OnBrowserConnected;

        _disposed = true;
    }
}

