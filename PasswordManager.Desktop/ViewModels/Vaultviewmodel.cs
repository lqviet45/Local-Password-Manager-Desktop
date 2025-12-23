using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MediatR;
using PasswordManager.Desktop.Services;
using PasswordManager.Desktop.Views;
using PasswordManager.Domain.Enums;
using PasswordManager.Domain.Interfaces;
using PasswordManager.Domain.ValueObjects;
using PasswordManager.Shared.Vault.Commands;
using PasswordManager.Shared.Vault.Dto;
using PasswordManager.Shared.Vault.Queries;

namespace PasswordManager.Desktop.ViewModels;

/// <summary>
/// ViewModel for displaying and managing vault items (passwords, notes, etc.)
/// </summary>
public partial class VaultViewModel : ViewModelBase
{
    private readonly IMediator _mediator;
    private readonly ISessionService _sessionService;
    private readonly IClipboardService _clipboardService;
    private readonly IMasterPasswordService _masterPasswordService;
    private readonly ICryptoProvider _cryptoProvider;

    [ObservableProperty] private ObservableCollection<VaultItemViewModel> _vaultItems = new();

    [ObservableProperty] private ObservableCollection<VaultItemViewModel> _filteredItems = new();

    [ObservableProperty] private VaultItemViewModel? _selectedItem;

    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] private VaultItemType? _filterType;

    [ObservableProperty] private bool _showFavoritesOnly;

    [ObservableProperty] private int _totalItemsCount;

    [ObservableProperty] private int _loginItemsCount;

    [ObservableProperty] private int _noteItemsCount;

    public VaultViewModel(
        IMediator mediator,
        ISessionService sessionService,
        IClipboardService clipboardService,
        IMasterPasswordService masterPasswordService,
        ICryptoProvider cryptoProvider,
        IDialogService dialogService,
        ILogger<VaultViewModel> logger)
        : base(dialogService, logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _masterPasswordService =
            masterPasswordService ?? throw new ArgumentNullException(nameof(masterPasswordService));
        _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));

        Title = "My Vault";
    }

    [RelayCommand]
    public async Task LoadItemsAsync()
    {
        await ExecuteAsync(async () =>
        {
            if (_sessionService.CurrentUser == null)
            {
                throw new InvalidOperationException("No user session");
            }

            Logger.LogInformation("Loading vault items for user: {UserId}", _sessionService.CurrentUser.Id);

            var result = await _mediator.Send(new GetVaultItemsQuery(_sessionService.CurrentUser.Id, false));

            if (result.IsFailure || result.Value == null)
            {
                throw new InvalidOperationException(result.Error?.Message ?? "Failed to load vault items");
            }

            VaultItems.Clear();

            foreach (var item in result.Value)
            {
                var viewModel = new VaultItemViewModel(item, this);
                VaultItems.Add(viewModel);
            }

            UpdateCounts();
            ApplyFilters();

            Logger.LogInformation("Loaded {Count} vault items", VaultItems.Count);
        });
    }

    [RelayCommand]
    private async Task AddNewItemAsync()
    {
        try
        {
            Logger.LogInformation("Opening Add Item dialog...");

            // Create AddEditItemViewModel
            var addEditViewModel = App.ServiceProvider.GetRequiredService<AddEditItemViewModel>();

            // Initialize with callback that refreshes vault
            addEditViewModel.InitializeForCreate(async (savedItem) =>
            {
                Logger.LogInformation("Item created: {ItemId}, triggering vault refresh", savedItem.Id);
                // Note: This callback might not be needed anymore since we refresh in ShowDialog check
            });

            // Find the actual MainWindow
            var mainWindow = System.Windows.Application.Current.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.DataContext is MainViewModel);

            // Create and show window
            var window = new PasswordManager.Desktop.Views.AddEditItemWindow(addEditViewModel);

            if (mainWindow != null)
            {
                window.Owner = mainWindow;
                window.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            }
            else
            {
                window.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            }

            Logger.LogInformation("Showing AddEditItemWindow...");
            var result = window.ShowDialog();

            // ALWAYS refresh after dialog closes (whether success or cancel)
            // This ensures the vault is in sync with the database
            Logger.LogInformation("Dialog closed with result: {Result}, refreshing vault", result);
            await LoadItemsAsync();

            if (result == true)
            {
                ShowInfo("Item added successfully!");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to add new item");
            ShowError($"Failed to add item: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task EditItemAsync(VaultItemViewModel? item)
    {
        if (item == null && SelectedItem == null)
        {
            ShowWarning("Please select an item to edit");
            return;
        }

        var itemToEdit = item ?? SelectedItem!;

        try
        {
            Logger.LogInformation("Opening Edit Item dialog for: {ItemId}", itemToEdit.Id);

            // Create AddEditItemViewModel
            var addEditViewModel = App.ServiceProvider.GetRequiredService<AddEditItemViewModel>();

            // Initialize for editing with callback
            await addEditViewModel.InitializeForEditAsync(itemToEdit.VaultItem,
                (updatedItem) =>
                {
                    Logger.LogInformation("Item updated: {ItemId}, triggering vault refresh", updatedItem.Id);
                });

            // Find the actual MainWindow
            var mainWindow = System.Windows.Application.Current.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.DataContext is MainViewModel);

            // Create and show window
            var window = new PasswordManager.Desktop.Views.AddEditItemWindow(addEditViewModel);

            if (mainWindow != null)
            {
                window.Owner = mainWindow;
                window.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            }
            else
            {
                window.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            }

            Logger.LogInformation("Showing AddEditItemWindow for edit...");
            var result = window.ShowDialog();

            // ALWAYS refresh after dialog closes
            Logger.LogInformation("Edit dialog closed with result: {Result}, refreshing vault", result);
            await LoadItemsAsync();

            if (result == true)
            {
                ShowInfo("Item updated successfully!");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to edit item");
            ShowError($"Failed to edit item: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteItemAsync(VaultItemViewModel? item)
    {
        if (item == null && SelectedItem == null)
        {
            ShowWarning("Please select an item to delete");
            return;
        }

        var itemToDelete = item ?? SelectedItem!;

        if (!Confirm($"Are you sure you want to delete '{itemToDelete.Name}'?", "Delete Item"))
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            var result = await _mediator.Send(new DeleteVaultItemCommand(itemToDelete.Id));
            if (result.IsFailure)
            {
                throw new InvalidOperationException(result.Error?.Message ?? "Failed to delete item");
            }

            // Refresh from database instead of just removing from collection
            // This ensures we have the latest state
            await LoadItemsAsync();

            ShowInfo("Item deleted successfully");
            Logger.LogInformation("Deleted vault item: {ItemId}", itemToDelete.Id);
        });
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(VaultItemViewModel? item)
    {
        if (item == null) return;

        await ExecuteAsync(async () =>
        {
            var result = await _mediator.Send(new ToggleFavoriteCommand(item.Id));
            if (result.IsFailure || result.Value == null)
            {
                throw new InvalidOperationException(result.Error?.Message ?? "Failed to toggle favorite");
            }

            item.UpdateFromVaultItem(result.Value);
            ApplyFilters();

            Logger.LogInformation("Toggled favorite for item: {ItemId}", item.Id);
        });
    }

    [RelayCommand]
    private async Task CopyPasswordAsync(VaultItemViewModel? item)
    {
        if (item == null) return;

        await ExecuteAsync(async () =>
        {
            if (string.IsNullOrEmpty(item.DecryptedPassword))
            {
                // Decrypt password
                var encryptionKey = _masterPasswordService.GetEncryptionKey();
                var encryptedData = Domain.ValueObjects.EncryptedData.FromCombinedString(item.VaultItem.EncryptedData);
                var decryptedPassword = await _cryptoProvider.DecryptAsync(encryptedData, encryptionKey);

                item.DecryptedPassword = decryptedPassword;
            }

            _clipboardService.CopyToClipboard(item.DecryptedPassword, TimeSpan.FromSeconds(30));
            ShowInfo("Password copied to clipboard (will clear in 30 seconds)");

            Logger.LogInformation("Copied password for item: {ItemId}", item.Id);
        });
    }

    [RelayCommand]
    private async Task CopyUsernameAsync(VaultItemViewModel? item)
    {
        if (item == null || string.IsNullOrEmpty(item.Username)) return;

        await Task.Run(() =>
        {
            _clipboardService.CopyToClipboard(item.Username, TimeSpan.FromSeconds(30));
            ShowInfo("Username copied to clipboard");
        });
    }

    [RelayCommand]
    private void FilterByType(VaultItemType? type)
    {
        FilterType = type;
        ApplyFilters();
    }

    [RelayCommand]
    private void ToggleFavoritesFilter()
    {
        ShowFavoritesOnly = !ShowFavoritesOnly;
        ApplyFilters();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        FilterType = null;
        ShowFavoritesOnly = false;
        ApplyFilters();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = VaultItems.AsEnumerable();

        // Search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            filtered = filtered.Where(i =>
                i.Name.ToLowerInvariant().Contains(searchLower) ||
                (i.Username?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                (i.Url?.ToLowerInvariant().Contains(searchLower) ?? false));
        }

        // Type filter
        if (FilterType.HasValue)
        {
            filtered = filtered.Where(i => i.Type == FilterType.Value);
        }

        // Favorites filter
        if (ShowFavoritesOnly)
        {
            filtered = filtered.Where(i => i.IsFavorite);
        }

        FilteredItems = new ObservableCollection<VaultItemViewModel>(
            filtered.OrderByDescending(i => i.IsFavorite)
                .ThenByDescending(i => i.LastModifiedUtc));
    }

    private void UpdateCounts()
    {
        TotalItemsCount = VaultItems.Count;
        LoginItemsCount = VaultItems.Count(i => i.Type == VaultItemType.Login);
        NoteItemsCount = VaultItems.Count(i => i.Type == VaultItemType.SecureNote);
    }

    internal byte[] GetDecryptionKey() => _masterPasswordService.GetEncryptionKey();

    internal ICryptoProvider CryptoProvider => _cryptoProvider;

    public async Task RefreshAsync()
    {
        await LoadItemsAsync();
    }
}

/// <summary>
/// ViewModel wrapper for VaultItem entity.
/// Provides UI-friendly properties and decryption on-demand.
/// </summary>
public partial class VaultItemViewModel : ObservableObject
{
    private readonly VaultViewModel _parentViewModel;

    public VaultItemDto VaultItem { get; private set; }

    public Guid Id => VaultItem.Id;
    public VaultItemType Type => VaultItem.Type;
    public string Name => VaultItem.Name;
    public string? Username => VaultItem.Username;
    public string? Url => VaultItem.Url;
    public string? Notes => VaultItem.Notes;
    public bool IsFavorite => VaultItem.IsFavorite;
    public DateTime LastModifiedUtc => VaultItem.LastModifiedUtc;

    [ObservableProperty] private string? _decryptedPassword;

    [ObservableProperty] private bool _isPasswordVisible;

    public string TypeIcon => Type switch
    {
        VaultItemType.Login => "🔑",
        VaultItemType.SecureNote => "📝",
        VaultItemType.CreditCard => "💳",
        VaultItemType.Identity => "👤",
        VaultItemType.BankAccount => "🏦",
        _ => "📄"
    };

    public string TypeName => Type switch
    {
        VaultItemType.Login => "Login",
        VaultItemType.SecureNote => "Secure Note",
        VaultItemType.CreditCard => "Credit Card",
        VaultItemType.Identity => "Identity",
        VaultItemType.BankAccount => "Bank Account",
        _ => "Unknown"
    };

    /// <summary>
    /// Primary display text rendered for this vault item, varying by item type.
    /// </summary>
    public string PrimaryDisplay => Type switch
    {
        VaultItemType.Login => ExtractDomain(Url) ?? Name,
        VaultItemType.CreditCard => MaskNumberWithLast4(ParsedSensitiveData.CardNumber) ?? Name,
        VaultItemType.SecureNote => Name,
        VaultItemType.Identity => ParsedSensitiveData.FullName ?? Name,
        VaultItemType.BankAccount => ParsedSensitiveData.BankName ?? Name,
        _ => Name
    };

    /// <summary>
    /// Secondary display text rendered for this vault item, varying by item type.
    /// </summary>
    public string SecondaryDisplay => Type switch
    {
        VaultItemType.Login => Username ?? string.Empty,
        VaultItemType.CreditCard => ParsedSensitiveData.CardholderName ?? Username ?? string.Empty,
        VaultItemType.SecureNote => BuildNotePreview(ParsedSensitiveData.NoteContent ?? Notes) ?? string.Empty,
        VaultItemType.Identity => ParsedSensitiveData.Email ?? Username ?? string.Empty,
        VaultItemType.BankAccount => MaskNumberWithLast4(ParsedSensitiveData.BankAccountNumber) ?? Username ?? string.Empty,
        _ => Username ?? string.Empty
    };

    /// <summary>
    /// Tertiary display text rendered for this vault item, used for optional extra context.
    /// </summary>
    public string TertiaryDisplay => Type switch
    {
        VaultItemType.Login => Name ?? string.Empty,
        VaultItemType.CreditCard => ParsedSensitiveData.ExpiryDisplay ?? ParsedSensitiveData.CardholderName ?? string.Empty,
        VaultItemType.SecureNote => Notes ?? string.Empty,
        VaultItemType.Identity => ParsedSensitiveData.Phone ?? string.Empty,
        VaultItemType.BankAccount => ParsedSensitiveData.BankRoutingNumber ?? string.Empty,
        _ => Url ?? string.Empty
    };

    private VaultItemSensitiveData _sensitiveData = VaultItemSensitiveData.Empty;
    private bool _hasParsedSensitiveData;
    private bool _hasAttemptedDecryption;

    public VaultItemViewModel(VaultItemDto vaultItem, VaultViewModel parentViewModel)
    {
        VaultItem = vaultItem ?? throw new ArgumentNullException(nameof(vaultItem));
        _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));
    }

    public void UpdateFromVaultItem(VaultItemDto updatedItem)
    {
        VaultItem = updatedItem;
        _hasParsedSensitiveData = false;
        _hasAttemptedDecryption = false;
        _sensitiveData = VaultItemSensitiveData.Empty;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(Url));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(LastModifiedUtc));
        OnPropertyChanged(nameof(PrimaryDisplay));
        OnPropertyChanged(nameof(SecondaryDisplay));
        OnPropertyChanged(nameof(TertiaryDisplay));
    }

    [RelayCommand]
    private async Task CopyPasswordAsync()
    {
        await _parentViewModel.CopyPasswordCommand.ExecuteAsync(this);
    }

    [RelayCommand]
    private async Task CopyUsernameAsync()
    {
        await _parentViewModel.CopyUsernameCommand.ExecuteAsync(this);
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        await _parentViewModel.EditItemCommand.ExecuteAsync(this);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        await _parentViewModel.DeleteItemCommand.ExecuteAsync(this);
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        await _parentViewModel.ToggleFavoriteCommand.ExecuteAsync(this);
    }

    private VaultItemSensitiveData ParsedSensitiveData
    {
        get
        {
            if (_hasParsedSensitiveData)
            {
                return _sensitiveData;
            }

            _sensitiveData = ParseSensitiveData();
            _hasParsedSensitiveData = true;
            return _sensitiveData;
        }
    }

    private VaultItemSensitiveData ParseSensitiveData()
    {
        var decrypted = EnsureDecryptedData();
        if (string.IsNullOrWhiteSpace(decrypted))
        {
            return VaultItemSensitiveData.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(decrypted);
            var root = document.RootElement;

            var firstName = TryGetPropertyValue(root, "firstName");
            var lastName = TryGetPropertyValue(root, "lastName");

            return new VaultItemSensitiveData
            {
                Password = TryGetPropertyValue(root, "password") ?? decrypted,
                CardNumber = TryGetPropertyValue(root, "cardNumber"),
                CardholderName = TryGetPropertyValue(root, "cardholderName"),
                NoteContent = TryGetPropertyValue(root, "content") ?? TryGetPropertyValue(root, "note"),
                FullName = TryGetPropertyValue(root, "fullName") ?? BuildFullName(firstName, lastName),
                Email = TryGetPropertyValue(root, "email"),
                Phone = TryGetPropertyValue(root, "phone"),
                BankName = TryGetPropertyValue(root, "bankName"),
                BankAccountNumber = TryGetPropertyValue(root, "accountNumber"),
                BankRoutingNumber = TryGetPropertyValue(root, "routingNumber"),
                ExpiryMonth = TryGetPropertyValue(root, "expiryMonth"),
                ExpiryYear = TryGetPropertyValue(root, "expiryYear")
            };
        }
        catch (JsonException)
        {
            return new VaultItemSensitiveData
            {
                Password = decrypted,
                NoteContent = decrypted
            };
        }
    }

    private string? EnsureDecryptedData()
    {
        if (!string.IsNullOrWhiteSpace(DecryptedPassword))
        {
            return DecryptedPassword;
        }

        if (_hasAttemptedDecryption)
        {
            return DecryptedPassword;
        }

        _hasAttemptedDecryption = true;

        try
        {
            var encryptedData = EncryptedData.FromCombinedString(VaultItem.EncryptedData);
            var decrypted = _parentViewModel.CryptoProvider.DecryptAsync(encryptedData, _parentViewModel.GetDecryptionKey())
                .GetAwaiter()
                .GetResult();
            DecryptedPassword = decrypted;
        }
        catch
        {
            // Best-effort; display falls back to non-sensitive fields when decryption fails
        }

        return DecryptedPassword;
    }

    private static string? TryGetPropertyValue(JsonElement root, string propertyName)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }

                return property.Value.ToString();
            }
        }

        return null;
    }

    private static string? MaskNumberWithLast4(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return null;
        }

        var digits = new string(number.Where(char.IsDigit).ToArray());
        if (digits.Length < 4)
        {
            return null;
        }

        var last4 = digits[^4..];
        return $"**** {last4}";
    }

    private static string? BuildNotePreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        const int maxLength = 60;
        return content.Length <= maxLength
            ? content
            : $"{content[..maxLength]}…";
    }

    private static string? ExtractDomain(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            // Try adding scheme if missing
            if (!Uri.TryCreate($"https://{url}", UriKind.Absolute, out uri))
            {
                return url;
            }
        }

        var host = uri.Host;
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? host[4..]
            : host;
    }

    private static string? BuildFullName(string? firstName, string? lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(firstName))
        {
            return lastName;
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            return firstName;
        }

        return $"{firstName} {lastName}";
    }

    private sealed record VaultItemSensitiveData
    {
        public static VaultItemSensitiveData Empty { get; } = new();

        public string? Password { get; init; }
        public string? CardNumber { get; init; }
        public string? CardholderName { get; init; }
        public string? NoteContent { get; init; }
        public string? FullName { get; init; }
        public string? Email { get; init; }
        public string? Phone { get; init; }
        public string? BankName { get; init; }
        public string? BankAccountNumber { get; init; }
        public string? BankRoutingNumber { get; init; }
        public string? ExpiryMonth { get; init; }
        public string? ExpiryYear { get; init; }

        public string? ExpiryDisplay =>
            string.IsNullOrWhiteSpace(ExpiryMonth) || string.IsNullOrWhiteSpace(ExpiryYear)
                ? null
                : $"{ExpiryMonth}/{ExpiryYear}";
    }
}