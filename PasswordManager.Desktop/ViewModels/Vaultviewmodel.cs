using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PasswordManager.Desktop.Services;
using PasswordManager.Domain.Entities;
using PasswordManager.Domain.Enums;
using PasswordManager.Domain.Interfaces;

namespace PasswordManager.Desktop.ViewModels;

/// <summary>
/// ViewModel for displaying and managing vault items (passwords, notes, etc.)
/// </summary>
public partial class VaultViewModel : ViewModelBase
{
    private readonly IVaultRepository _vaultRepository;
    private readonly ISessionService _sessionService;
    private readonly IClipboardService _clipboardService;
    private readonly IMasterPasswordService _masterPasswordService;
    private readonly ICryptoProvider _cryptoProvider;

    [ObservableProperty]
    private ObservableCollection<VaultItemViewModel> _vaultItems = new();

    [ObservableProperty]
    private ObservableCollection<VaultItemViewModel> _filteredItems = new();

    [ObservableProperty]
    private VaultItemViewModel? _selectedItem;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private VaultItemType? _filterType;

    [ObservableProperty]
    private bool _showFavoritesOnly;

    [ObservableProperty]
    private int _totalItemsCount;

    [ObservableProperty]
    private int _loginItemsCount;

    [ObservableProperty]
    private int _noteItemsCount;

    public VaultViewModel(
        IVaultRepository vaultRepository,
        ISessionService sessionService,
        IClipboardService clipboardService,
        IMasterPasswordService masterPasswordService,
        ICryptoProvider cryptoProvider,
        IDialogService dialogService,
        ILogger<VaultViewModel> logger)
        : base(dialogService, logger)
    {
        _vaultRepository = vaultRepository ?? throw new ArgumentNullException(nameof(vaultRepository));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _masterPasswordService = masterPasswordService ?? throw new ArgumentNullException(nameof(masterPasswordService));
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

            var items = await _vaultRepository.GetAllAsync(
                _sessionService.CurrentUser.Id, 
                includeDeleted: false);

            VaultItems.Clear();

            foreach (var item in items)
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
        // TODO: Open AddEditItemViewModel in dialog or navigate
        ShowInfo("Add new item feature - Coming soon!");
        
        // Placeholder for now
        await Task.CompletedTask;
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
        
        // TODO: Open AddEditItemViewModel with item
        ShowInfo($"Edit item: {itemToEdit.Name} - Coming soon!");
        
        await Task.CompletedTask;
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
            await _vaultRepository.DeleteAsync(itemToDelete.Id);
            VaultItems.Remove(itemToDelete);
            
            UpdateCounts();
            ApplyFilters();
            
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
            var updatedItem = item.VaultItem with { IsFavorite = !item.IsFavorite };
            await _vaultRepository.UpdateAsync(updatedItem);
            
            item.UpdateFromVaultItem(updatedItem);
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

    public VaultItem VaultItem { get; private set; }

    public Guid Id => VaultItem.Id;
    public VaultItemType Type => VaultItem.Type;
    public string Name => VaultItem.Name;
    public string? Username => VaultItem.Username;
    public string? Url => VaultItem.Url;
    public bool IsFavorite => VaultItem.IsFavorite;
    public DateTime LastModifiedUtc => VaultItem.LastModifiedUtc;

    [ObservableProperty]
    private string? _decryptedPassword;

    [ObservableProperty]
    private bool _isPasswordVisible;

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

    public VaultItemViewModel(VaultItem vaultItem, VaultViewModel parentViewModel)
    {
        VaultItem = vaultItem ?? throw new ArgumentNullException(nameof(vaultItem));
        _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));
    }

    public void UpdateFromVaultItem(VaultItem updatedItem)
    {
        VaultItem = updatedItem;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(Url));
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(LastModifiedUtc));
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
}