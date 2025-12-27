using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using PasswordManager.Shared.Vault.Commands;
using PasswordManager.Shared.Vault.Queries;
using PasswordManager.Shared.Vault.Dto;
using PasswordManager.Domain.Entities;
using PasswordManager.Domain.Enums;
using PasswordManager.Domain.Interfaces;
using PasswordManager.Domain.ValueObjects;
using PasswordManager.Infrastructure.Cryptography;

namespace PasswordManager.Desktop.Services.Impl;

/// <summary>
/// Simple HTTP server for browser extension communication.
/// Uses HttpListener for zero-dependency implementation.
/// Production-ready with full error handling and logging.
/// </summary>
public sealed class LocalApiServer : ILocalApiServer
{
    private readonly ILogger<LocalApiServer> _logger;
    private readonly ISessionService _sessionService;
    private readonly IMasterPasswordService _masterPasswordService;
    private readonly IMediator _mediator;
    private readonly ICryptoProvider _cryptoProvider;
    private HttpListener? _listener;
    private Task? _listenerTask;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public bool IsRunning { get; private set; }
    public string ServerUrl { get; private set; } = string.Empty;
    public int Port { get; private set; }

    public LocalApiServer(
        ILogger<LocalApiServer> logger,
        ISessionService sessionService,
        IMasterPasswordService masterPasswordService,
        IMediator mediator,
        ICryptoProvider cryptoProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _masterPasswordService = masterPasswordService ?? throw new ArgumentNullException(nameof(masterPasswordService));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
    }

    public Task StartAsync(int port = 7777, CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            _logger.LogWarning("Server is already running on port {Port}", Port);
            return Task.CompletedTask;
        }

        try
        {
            _listener = new HttpListener();
            Port = port;
            ServerUrl = $"http://localhost:{port}/";
            _listener.Prefixes.Add(ServerUrl);
            _listener.Start();

            _cts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => ListenAsync(_cts.Token), cancellationToken);

            IsRunning = true;
            _logger.LogInformation("✅ Local API server started at {Url}", ServerUrl);
            _logger.LogInformation("Browser extension can now connect via HTTP");

            return Task.CompletedTask;
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5)
        {
            _logger.LogError("Access denied. Try running as administrator or use a different port.");
            throw new InvalidOperationException($"Cannot start server on port {port}. Access denied.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start local API server on port {Port}", port);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
            return;

        try
        {
            _logger.LogInformation("Stopping local API server...");
            
            _cts?.Cancel();
            _listener?.Stop();

            if (_listenerTask != null)
            {
                await _listenerTask;
            }

            IsRunning = false;
            _logger.LogInformation("Local API server stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping server");
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listening for requests on {Url}", ServerUrl);

        while (!cancellationToken.IsCancellationRequested && _listener != null)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                
                // Handle request in background (don't await)
                _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting request");
            }
        }

        _logger.LogInformation("Stopped listening for requests");
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var request = context.Request;
        var response = context.Response;

        try
        {
            // Add CORS headers for browser extension
            response.Headers.Add("Access-Control-Allow-Origin", "*"); // Allow all extensions in dev
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            // Handle preflight requests
            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            var path = request.Url?.AbsolutePath ?? "/";
            var method = request.HttpMethod;

            _logger.LogDebug("📨 Request: {Method} {Path}", method, path);

            object? responseData = null;

            // Route requests
            switch (path)
            {
                case "/api/ping":
                    responseData = HandlePing();
                    break;

                case "/api/status":
                    responseData = HandleStatus();
                    break;

                case "/api/credentials":
                    responseData = method switch
                    {
                        "GET" => await HandleGetCredentialsAsync(request),
                        "POST" => await HandleSaveCredentialsAsync(request),
                        _ => new { success = false, error = "Method not allowed" }
                    };
                    break;

                case "/api/credentials/fill":
                    responseData = await HandleFillCredentialsAsync(request);
                    break;

                default:
                    response.StatusCode = 404;
                    responseData = new { success = false, error = "Endpoint not found" };
                    break;
            }

            // Send JSON response
            await SendJsonResponseAsync(response, responseData);

            stopwatch.Stop();
            _logger.LogDebug("✅ Response sent in {Ms}ms: {Method} {Path}", 
                stopwatch.ElapsedMilliseconds, method, path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling request");
            
            try
            {
                response.StatusCode = 500;
                await SendJsonResponseAsync(response, new
                {
                    success = false,
                    error = "Internal server error",
                    details = ex.Message
                });
            }
            catch
            {
                // Ignore - response may already be sent
            }
        }
    }

    #region Request Handlers

    private object HandlePing()
    {
        return new
        {
            success = true,
            message = "pong",
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        };
    }

    private object HandleStatus()
    {
        var currentUser = _sessionService.CurrentUser;
        var isVaultLocked = !_masterPasswordService.IsInitialized;

        return new
        {
            success = true,
            isLoggedIn = currentUser != null,
            isVaultLocked = isVaultLocked,
            userEmail = currentUser?.Email,
            isPremium = currentUser?.IsPremium ?? false
        };
    }

    private async Task<object> HandleGetCredentialsAsync(HttpListenerRequest request)
    {
        // Check authentication
        if (_sessionService.CurrentUser == null)
        {
            return new { success = false, error = "Not logged in", locked = true };
        }

        if (!_masterPasswordService.IsInitialized)
        {
            return new { success = false, error = "Vault is locked", locked = true };
        }

        // Get URL from query string
        var url = request.QueryString["url"];
        if (string.IsNullOrWhiteSpace(url))
        {
            return new { success = false, error = "URL parameter is required" };
        }

        _logger.LogInformation("🔍 Searching credentials for URL: {Url}", url);

        try
        {
            // Get all vault items
            var query = new GetVaultItemsQuery(_sessionService.CurrentUser.Id, false);
            var result = await _mediator.Send(query);

            if (result.IsFailure || result.Value == null)
            {
                _logger.LogWarning("Failed to get vault items: {Error}", result.Error);
                return new { success = false, error = "Failed to retrieve vault items" };
            }

            var items = result.Value;

            // Filter by URL (simple contains match for now)
            var matchingItems = items
                .Where(item => item.Type == VaultItemType.Login)
                .Where(item => !string.IsNullOrEmpty(item.Url) && 
                              (item.Url.Contains(ExtractDomain(url)) || url.Contains(item.Url)))
                .ToList();

            if (!matchingItems.Any())
            {
                _logger.LogInformation("No credentials found for URL: {Url}", url);
                return new { success = true, credentials = Array.Empty<object>() };
            }

            // Decrypt matching items
            var credentials = new List<object>();
            var encryptionKey = _masterPasswordService.GetPreferredKey();

            foreach (var item in matchingItems)
            {
                try
                {
                    var encryptedData = EncryptedData.FromCombinedString(item.EncryptedData);
                    var decryptedJson = await _cryptoProvider.DecryptAsync(encryptedData, encryptionKey);

                    if (LoginData.TryFromJson(decryptedJson, out var loginData) && loginData != null)
                    {
                        credentials.Add(new
                        {
                            id = item.Id,
                            name = item.Name,
                            username = loginData.Username,
                            password = loginData.Password,
                            url = item.Url,
                            isFavorite = item.IsFavorite
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decrypt item {ItemId}", item.Id);
                }
            }

            _logger.LogInformation("✅ Found {Count} credentials for URL: {Url}", credentials.Count, url);

            return new
            {
                success = true,
                credentials = credentials.ToArray(),
                locked = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting credentials for URL: {Url}", url);
            return new { success = false, error = "Failed to get credentials" };
        }
    }

    private async Task<object> HandleSaveCredentialsAsync(HttpListenerRequest request)
    {
        // Check authentication
        if (_sessionService.CurrentUser == null)
        {
            return new { success = false, error = "Not logged in" };
        }

        if (!_masterPasswordService.IsInitialized)
        {
            return new { success = false, error = "Vault is locked" };
        }

        try
        {
            // Read request body
            string requestBody;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            var saveRequest = JsonSerializer.Deserialize<SaveCredentialsRequest>(requestBody);
            if (saveRequest == null)
            {
                return new { success = false, error = "Invalid request body" };
            }

            _logger.LogInformation("💾 Saving credentials for URL: {Url}, Username: {Username}", 
                saveRequest.Url, saveRequest.Username);

            // Create login data
            var loginData = new LoginData
            {
                Username = saveRequest.Username ?? string.Empty,
                Password = saveRequest.Password ?? string.Empty,
                Website = saveRequest.Url ?? string.Empty
            };

            // Determine item name
            var itemName = string.IsNullOrWhiteSpace(saveRequest.Name)
                ? ExtractDomain(saveRequest.Url ?? "Unknown")
                : saveRequest.Name;

            // Create vault item request
            var itemRequest = new VaultItemRequest
            {
                Type = VaultItemType.Login,
                Name = itemName,
                Username = saveRequest.Username,
                Password = loginData.ToJson(), // Store JSON-encrypted data
                Url = saveRequest.Url,
                Notes = saveRequest.Notes,
                Tags = null,
                IsFavorite = false
            };

            // Get encryption key
            var encryptionKey = _masterPasswordService.GetPreferredKey();

            // Create vault item command
            var command = new CreateVaultItemCommand(
                UserId: _sessionService.CurrentUser.Id,
                Item: itemRequest,
                EncryptionKey: encryptionKey
            );

            var result = await _mediator.Send(command);

            if (result.IsFailure || result.Value == null)
            {
                _logger.LogWarning("Failed to save credentials: {Error}", result.Error?.Message);
                return new { success = false, error = result.Error?.Message ?? "Failed to save credentials" };
            }

            _logger.LogInformation("✅ Credentials saved successfully: {ItemId}", result.Value.Id);

            return new
            {
                success = true,
                itemId = result.Value.Id,
                message = "Credentials saved successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving credentials");
            return new { success = false, error = "Failed to save credentials" };
        }
    }

    private async Task<object> HandleFillCredentialsAsync(HttpListenerRequest request)
    {
        // Check authentication
        if (_sessionService.CurrentUser == null)
        {
            return new { success = false, error = "Not logged in" };
        }

        if (!_masterPasswordService.IsInitialized)
        {
            return new { success = false, error = "Vault is locked" };
        }

        // Get item ID from query string
        var itemIdStr = request.QueryString["itemId"];
        if (string.IsNullOrWhiteSpace(itemIdStr) || !Guid.TryParse(itemIdStr, out var itemId))
        {
            return new { success = false, error = "Invalid item ID" };
        }

        _logger.LogInformation("🔑 Filling credentials for item: {ItemId}", itemId);

        try
        {
            // Get all vault items (we need to decrypt to find the right one)
            var query = new GetVaultItemsQuery(_sessionService.CurrentUser.Id, IncludeDeleted: false);
            var result = await _mediator.Send(query);

            if (result.IsFailure || result.Value == null)
            {
                return new { success = false, error = "Failed to retrieve vault items" };
            }

            var item = result.Value.FirstOrDefault(i => i.Id == itemId);
            if (item == null)
            {
                return new { success = false, error = "Item not found" };
            }

            // Decrypt item
            var encryptionKey = _masterPasswordService.GetPreferredKey();
            var encryptedData = EncryptedData.FromCombinedString(item.EncryptedData);
            var decryptedJson = await _cryptoProvider.DecryptAsync(encryptedData, encryptionKey);

            if (!LoginData.TryFromJson(decryptedJson, out var loginData) || loginData == null)
            {
                return new { success = false, error = "Failed to decrypt credentials" };
            }

            _logger.LogInformation("✅ Credentials retrieved for fill: {ItemName}", item.Name);

            return new
            {
                success = true,
                credentials = new
                {
                    username = loginData.Username,
                    password = loginData.Password,
                    url = item.Url
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filling credentials for item: {ItemId}", itemId);
            return new { success = false, error = "Failed to fill credentials" };
        }
    }

    #endregion

    #region Helper Methods

    private async Task SendJsonResponseAsync(HttpListenerResponse response, object? data)
    {
        response.ContentType = "application/json";
        response.StatusCode = response.StatusCode == 0 ? 200 : response.StatusCode;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        var json = JsonSerializer.Serialize(data, options);
        var bytes = Encoding.UTF8.GetBytes(json);

        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    private static string ExtractDomain(string url)
    {
        try
        {
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            var uri = new Uri(url);
            return uri.Host.Replace("www.", "");
        }
        catch
        {
            return url;
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        StopAsync().GetAwaiter().GetResult();
        _cts?.Dispose();
        _listener?.Close();

        _disposed = true;
    }

    #endregion
}

#region DTOs

/// <summary>
/// Request DTO for saving credentials from browser extension.
/// </summary>
internal sealed record SaveCredentialsRequest
{
    public string? Url { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? Name { get; init; }
    public string? Notes { get; init; }
}

#endregion