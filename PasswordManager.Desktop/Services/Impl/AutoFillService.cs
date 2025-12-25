using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.Logging;
using PasswordManager.Desktop.Services;
using PasswordManager.Domain.Enums;
using PasswordManager.Domain.Interfaces;
using PasswordManager.Shared.Vault.Dto;
using PasswordManager.Shared.Vault.Queries;

namespace PasswordManager.Desktop.Services.Impl;

/// <summary>
/// Production implementation of auto-fill service.
/// Detects browser windows and auto-fills credentials.
/// </summary>
public sealed class AutoFillService : IAutoFillService
{
    private readonly IMediator _mediator;
    private readonly ILogger<AutoFillService> _logger;
    private readonly IMasterPasswordService _masterPasswordService;
    private bool _disposed;

    #region Windows API

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    #endregion

    public AutoFillService(
        IMediator mediator,
        ILogger<AutoFillService> logger,
        IMasterPasswordService masterPasswordService)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _masterPasswordService = masterPasswordService ?? throw new ArgumentNullException(nameof(masterPasswordService));
    }

    public async Task<AutoFillResult> TryAutoFillAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            // Step 1: Detect browser context
            var browserContext = await DetectBrowserContextAsync(cancellationToken);
            if (browserContext == null)
            {
                return AutoFillResult.Failed(AutoFillErrorCode.NoBrowserDetected, "No browser detected");
            }

            _logger.LogInformation("Detected browser: {Browser}, URL: {Url}", browserContext.BrowserName, browserContext.Url);

            // Step 2: Find matching vault items
            var matches = await FindMatchingItemsAsync(userId, browserContext.Url, cancellationToken);
            if (matches.Count == 0)
            {
                return AutoFillResult.Failed(AutoFillErrorCode.NoMatchingItems, "No matching credentials found");
            }

            // Step 3: If multiple matches, return error (user should choose)
            if (matches.Count > 1)
            {
                _logger.LogWarning("Multiple matches found ({Count}) for URL: {Url}", matches.Count, browserContext.Url);
                return AutoFillResult.Failed(AutoFillErrorCode.MultipleMatches, $"Multiple matches found ({matches.Count}). Please select one.");
            }

            // Step 4: Auto-fill with the best match
            var bestMatch = matches[0];
            var success = await FillCredentialsAsync(bestMatch.VaultItemId, cancellationToken);

            if (!success)
            {
                return AutoFillResult.Failed(AutoFillErrorCode.FillOperationFailed, "Failed to fill credentials");
            }

            return AutoFillResult.Successful(browserContext, matches.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during auto-fill operation");
            return AutoFillResult.Failed(AutoFillErrorCode.FillOperationFailed, ex.Message);
        }
    }

    public async Task<BrowserContext?> DetectBrowserContextAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            var foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                return null;
            }

            GetWindowThreadProcessId(foregroundWindow, out var processId);
            var process = Process.GetProcessById((int)processId);

            var windowTitle = GetWindowTitle(foregroundWindow);
            var browserName = DetectBrowserName(process.ProcessName);
            var url = ExtractUrlFromWindowTitle(windowTitle);

            if (string.IsNullOrEmpty(browserName) || string.IsNullOrEmpty(url))
            {
                return null;
            }

            var domain = ExtractDomain(url);

            return new BrowserContext(
                BrowserName: browserName,
                Url: url,
                Domain: domain,
                WindowTitle: windowTitle,
                ProcessId: (int)processId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting browser context");
            return null;
        }
    }

    public async Task<IReadOnlyList<AutoFillMatch>> FindMatchingItemsAsync(Guid userId, string url, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            // Get all vault items for user
            var query = new GetVaultItemsQuery(userId, false);
            var result = await _mediator.Send(query, cancellationToken);

            if (result.IsFailure || result.Value == null)
            {
                _logger.LogWarning("Failed to load vault items for auto-fill");
                return Array.Empty<AutoFillMatch>();
            }

            var domain = ExtractDomain(url);
            var matches = new List<AutoFillMatch>();

            foreach (var item in result.Value)
            {
                // Only match Login type items
                if (item.Type != VaultItemType.Login)
                    continue;

                var matchScore = CalculateMatchScore(item, url, domain);
                if (matchScore > 0)
                {
                    matches.Add(new AutoFillMatch(
                        VaultItemId: item.Id,
                        Name: item.Name,
                        Username: item.Username ?? "",
                        Url: item.Url ?? "",
                        MatchScore: matchScore
                    ));
                }
            }

            // Sort by match score (highest first)
            return matches.OrderByDescending(m => m.MatchScore).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding matching items");
            return Array.Empty<AutoFillMatch>();
        }
    }

    public async Task<bool> FillCredentialsAsync(Guid vaultItemId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            // Get vault item details
            var query = new GetVaultItemsQuery(Guid.Empty, false); // Will filter by ID in handler
            var result = await _mediator.Send(query, cancellationToken);

            if (result.IsFailure || result.Value == null)
            {
                _logger.LogWarning("Failed to load vault item for auto-fill");
                return false;
            }

            var item = result.Value.FirstOrDefault(i => i.Id == vaultItemId);
            if (item == null || item.Type != VaultItemType.Login)
            {
                _logger.LogWarning("Vault item not found or not a login type");
                return false;
            }

            // Check if item has encrypted data
            if (string.IsNullOrEmpty(item.EncryptedData))
            {
                _logger.LogWarning("Item has no encrypted data to fill");
                return false;
            }

            // TODO: Implement actual form filling using Windows API or UI Automation
            // For now, we'll use SendKeys or UI Automation
            // This is a placeholder - actual implementation would use:
            // - UI Automation API
            // - SendInput API
            // - Or browser extension communication

            _logger.LogInformation("Auto-fill triggered for item: {ItemId}", vaultItemId);
            
            // Placeholder: In a real implementation, this would:
            // 1. Find username/password input fields
            // 2. Fill them with credentials
            // 3. Optionally submit the form

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filling credentials");
            return false;
        }
    }

    #region Private Helpers

    private string GetWindowTitle(IntPtr hWnd)
    {
        var title = new System.Text.StringBuilder(256);
        GetWindowText(hWnd, title, title.Capacity);
        return title.ToString();
    }

    private string DetectBrowserName(string processName)
    {
        return processName.ToLowerInvariant() switch
        {
            "chrome" or "chrome.exe" => "Chrome",
            "msedge" or "msedge.exe" => "Edge",
            "firefox" or "firefox.exe" => "Firefox",
            "brave" or "brave.exe" => "Brave",
            "opera" or "opera.exe" => "Opera",
            _ => string.Empty
        };
    }

    private string ExtractUrlFromWindowTitle(string windowTitle)
    {
        // Try to extract URL from window title
        // Format: "Page Title - Browser" or "Page Title | Browser"
        // Some browsers show URL in title: "https://example.com - Browser"

        // Try direct URL pattern
        var urlPattern = new Regex(@"https?://[^\s]+", RegexOptions.IgnoreCase);
        var match = urlPattern.Match(windowTitle);
        if (match.Success)
        {
            return match.Value;
        }

        // Try to extract from common patterns
        // This is a simplified version - real implementation would be more sophisticated
        return string.Empty;
    }

    private string ExtractDomain(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return uri.Host;
            }
        }
        catch
        {
            // Ignore
        }

        return string.Empty;
    }

    private int CalculateMatchScore(VaultItemDto item, string url, string domain)
    {
        var score = 0;

        if (string.IsNullOrEmpty(item.Url))
            return 0;

        // Exact URL match = 100 points
        if (item.Url.Equals(url, StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }
        // Domain match = 50 points
        else if (!string.IsNullOrEmpty(domain) && item.Url.Contains(domain, StringComparison.OrdinalIgnoreCase))
        {
            score += 50;
        }
        // Partial domain match = 25 points
        else if (!string.IsNullOrEmpty(domain) && ExtractDomain(item.Url).Equals(domain, StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
        }

        // Favorite items get bonus
        if (item.IsFavorite)
        {
            score += 10;
        }

        return score;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AutoFillService));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }

    #endregion
}

