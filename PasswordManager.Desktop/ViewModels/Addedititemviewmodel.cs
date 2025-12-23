using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MediatR;
using PasswordManager.Desktop.Services;
using PasswordManager.Domain.Enums;
using PasswordManager.Domain.Interfaces;
using PasswordManager.Domain.ValueObjects;
using PasswordManager.Shared.Common.Result;
using PasswordManager.Shared.Vault.Commands;
using PasswordManager.Shared.Vault.Dto;

namespace PasswordManager.Desktop.ViewModels;

/// <summary>
/// ViewModel for adding or editing vault items.
/// Handles encryption, password generation, and strength checking.
/// </summary>
public partial class AddEditItemViewModel : ViewModelBase
{
    private readonly IMediator _mediator;
    private readonly ISessionService _sessionService;
    private readonly ICryptoProvider _cryptoProvider;
    private readonly IMasterPasswordService _masterPasswordService;
    private readonly IPasswordStrengthService _passwordStrengthService;
    private readonly IHibpService _hibpService;

    private VaultItemDto? _existingItem;
    private Action<VaultItemDto>? _onSaveCallback;

    [ObservableProperty]
    private VaultItemType _selectedType = VaultItemType.Login;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _tags = string.Empty;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _showPassword;

    [ObservableProperty]
    private string _passwordStrengthText = string.Empty;

    [ObservableProperty]
    private string _passwordStrengthColor = "Gray";

    [ObservableProperty]
    private int _passwordStrengthScore;

    [ObservableProperty]
    private bool _isPasswordBreached;

    [ObservableProperty]
    private int _breachCount;

    [ObservableProperty]
    private bool _isCheckingBreach;

    [ObservableProperty]
    private ObservableCollection<VaultItemType> _availableTypes = new();

    // Type-specific flags for UI
    public bool IsLoginType => SelectedType == VaultItemType.Login;
    public bool IsCreditCardType => SelectedType == VaultItemType.CreditCard;
    public bool IsSecureNoteType => SelectedType == VaultItemType.SecureNote;
    public bool IsIdentityType => SelectedType == VaultItemType.Identity;
    public bool IsBankAccountType => SelectedType == VaultItemType.BankAccount;

    // LOGIN-specific fields
    /// <summary>
    /// Website or URL for login items. Wraps the existing Url field.
    /// </summary>
    public string Website
    {
        get => Url;
        set => Url = value;
    }

    /// <summary>
    /// Optional email for login items. Currently not persisted separately from username.
    /// </summary>
    [ObservableProperty]
    private string _email = string.Empty;

    // CREDIT CARD fields
    [ObservableProperty]
    private string _cardholderName = string.Empty;

    [ObservableProperty]
    private string _cardNumber = string.Empty;

    [ObservableProperty]
    private string _expiryMonth = string.Empty;

    [ObservableProperty]
    private string _expiryYear = string.Empty;

    [ObservableProperty]
    private string _cVV = string.Empty;

    [ObservableProperty]
    private string _billingAddress = string.Empty;

    [ObservableProperty]
    private string _zipCode = string.Empty;

    // SECURE NOTE fields (wraps Notes)
    public string NoteContent
    {
        get => Notes;
        set => Notes = value;
    }

    // IDENTITY fields
    [ObservableProperty]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string _middleName = string.Empty;

    [ObservableProperty]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private string _dateOfBirth = string.Empty;

    [ObservableProperty]
    private string _identityEmail = string.Empty;

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _city = string.Empty;

    [ObservableProperty]
    private string _state = string.Empty;

    [ObservableProperty]
    private string _country = string.Empty;

    [ObservableProperty]
    private string _passportNumber = string.Empty;

    [ObservableProperty]
    private string _licenseNumber = string.Empty;

    // BANK ACCOUNT fields
    [ObservableProperty]
    private string _bankName = string.Empty;

    [ObservableProperty]
    private string _accountHolderName = string.Empty;

    [ObservableProperty]
    private string _accountNumber = string.Empty;

    [ObservableProperty]
    private string _routingNumber = string.Empty;

    [ObservableProperty]
    private string _iBAN = string.Empty;

    // Property to signal successful save
    [ObservableProperty]
    private bool _shouldCloseWindow;

    public bool IsEditMode => _existingItem != null;
    public string WindowTitle => IsEditMode ? $"Edit {_existingItem!.Name}" : "Add New Item";

    public AddEditItemViewModel(
        IMediator mediator,
        ISessionService sessionService,
        ICryptoProvider cryptoProvider,
        IMasterPasswordService masterPasswordService,
        IPasswordStrengthService passwordStrengthService,
        IHibpService hibpService,
        IDialogService dialogService,
        ILogger<AddEditItemViewModel> logger)
        : base(dialogService, logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
        _masterPasswordService = masterPasswordService ?? throw new ArgumentNullException(nameof(masterPasswordService));
        _passwordStrengthService = passwordStrengthService ?? throw new ArgumentNullException(nameof(passwordStrengthService));
        _hibpService = hibpService ?? throw new ArgumentNullException(nameof(hibpService));

        Title = "Add Item";

        // Load available types
        AvailableTypes = new ObservableCollection<VaultItemType>(
            Enum.GetValues<VaultItemType>());
    }

    partial void OnSelectedTypeChanged(VaultItemType value)
    {
        OnPropertyChanged(nameof(IsLoginType));
        OnPropertyChanged(nameof(IsCreditCardType));
        OnPropertyChanged(nameof(IsSecureNoteType));
        OnPropertyChanged(nameof(IsIdentityType));
        OnPropertyChanged(nameof(IsBankAccountType));
    }

    /// <summary>
    /// Initialize for creating new item
    /// </summary>
    public void InitializeForCreate(Action<VaultItemDto> onSave)
    {
        _onSaveCallback = onSave;
        _existingItem = null;
        Title = "Add New Item";
    }

    /// <summary>
    /// Initialize for editing existing item.
    /// Decrypts stored data and hydrates type-specific fields when JSON is detected.
    /// Falls back to legacy single-password behavior if JSON deserialization fails.
    /// </summary>
    public async Task InitializeForEditAsync(VaultItemDto item, Action<VaultItemDto> onSave)
    {
        _existingItem = item ?? throw new ArgumentNullException(nameof(item));
        _onSaveCallback = onSave;
        Title = $"Edit {item.Name}";

        await ExecuteAsync(async () =>
        {
            // Decrypt existing data (try preferred key, fall back to derived key)
            var encryptedData = EncryptedData.FromCombinedString(item.EncryptedData);
            string decrypted;
            try
            {
                var key = _masterPasswordService.GetPreferredKey();
                decrypted = await _cryptoProvider.DecryptAsync(encryptedData, key);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Preferred key failed to decrypt item, trying derived key");
                var fallbackKey = _masterPasswordService.GetEncryptionKey();
                decrypted = await _cryptoProvider.DecryptAsync(encryptedData, fallbackKey);
            }

            // Load common metadata (works for both new JSON format and legacy format)
            SelectedType = item.Type;
            Name = item.Name;
            Url = item.Url ?? string.Empty;
            Notes = item.Notes ?? string.Empty;
            Tags = item.Tags ?? string.Empty;
            IsFavorite = item.IsFavorite;

            // Hydrate type-specific fields from decrypted JSON when possible.
            try
            {
                switch (item.Type)
                {
                    case VaultItemType.Login:
                        if (LoginData.TryFromJson(decrypted, out var loginData))
                        {
                            Username = loginData!.Username ?? item.Username ?? string.Empty;
                            Password = loginData.Password ?? string.Empty;
                            Website = loginData.Website ?? item.Url ?? string.Empty;
                            Email = loginData.Email ?? string.Empty;
                        }
                        else
                        {
                            Logger.LogWarning("Item {ItemId} uses legacy login format, treating decrypted value as plain password", item.Id);
                            // Legacy format: decrypted value is the password string
                            Username = item.Username ?? string.Empty;
                            Password = decrypted;
                            Website = item.Url ?? string.Empty;
                        }
                        break;

                    case VaultItemType.CreditCard:
                        if (CreditCardData.TryFromJson(decrypted, out var cardData))
                        {
                            CardholderName = cardData!.CardholderName ?? string.Empty;
                            CardNumber = cardData.CardNumber ?? string.Empty;
                            ExpiryMonth = cardData.ExpiryMonth ?? string.Empty;
                            ExpiryYear = cardData.ExpiryYear ?? string.Empty;
                            CVV = cardData.CVV ?? string.Empty;
                            BillingAddress = cardData.BillingAddress ?? string.Empty;
                            ZipCode = cardData.ZipCode ?? string.Empty;
                        }
                        else
                        {
                            Logger.LogWarning("Item {ItemId} uses legacy credit card format, storing decrypted payload in Notes", item.Id);
                            // Legacy fallback: store entire payload in notes
                            Notes = decrypted;
                        }
                        break;

                    case VaultItemType.SecureNote:
                        if (SecureNoteData.TryFromJson(decrypted, out var noteData))
                        {
                            NoteContent = noteData!.Content ?? item.Notes ?? string.Empty;
                        }
                        else
                        {
                            Logger.LogWarning("Item {ItemId} uses legacy secure note format, treating decrypted value as content", item.Id);
                            // Legacy behavior: treat decrypted as raw note content
                            NoteContent = decrypted;
                        }
                        break;

                    case VaultItemType.Identity:
                        if (IdentityData.TryFromJson(decrypted, out var identity))
                        {
                            FirstName = identity!.FirstName ?? string.Empty;
                            MiddleName = identity.MiddleName ?? string.Empty;
                            LastName = identity.LastName ?? string.Empty;
                            DateOfBirth = identity.DateOfBirth ?? string.Empty;
                            IdentityEmail = identity.Email ?? string.Empty;
                            Phone = identity.Phone ?? string.Empty;
                            Address = identity.Address ?? string.Empty;
                            City = identity.City ?? string.Empty;
                            State = identity.State ?? string.Empty;
                            Country = identity.Country ?? string.Empty;
                            PassportNumber = identity.PassportNumber ?? string.Empty;
                            LicenseNumber = identity.LicenseNumber ?? string.Empty;
                        }
                        else
                        {
                            Logger.LogWarning("Item {ItemId} uses legacy identity format, storing decrypted payload in Notes", item.Id);
                            Notes = decrypted;
                        }
                        break;

                    case VaultItemType.BankAccount:
                        if (BankAccountData.TryFromJson(decrypted, out var bank))
                        {
                            BankName = bank!.BankName ?? string.Empty;
                            AccountHolderName = bank.AccountHolderName ?? string.Empty;
                            AccountNumber = bank.AccountNumber ?? string.Empty;
                            RoutingNumber = bank.RoutingNumber ?? string.Empty;
                            IBAN = bank.IBAN ?? string.Empty;
                        }
                        else
                        {
                            Logger.LogWarning("Item {ItemId} uses legacy bank account format, storing decrypted payload in Notes", item.Id);
                            Notes = decrypted;
                        }
                        break;

                    default:
                        // Unknown type: preserve legacy behavior
                        Username = item.Username ?? string.Empty;
                        Password = decrypted;
                        Website = item.Url ?? string.Empty;
                        Notes = item.Notes ?? string.Empty;
                        break;
                }
            }
            catch (Exception ex)
            {
                // Robustness: if anything goes wrong, fall back to legacy behavior
                Logger.LogError(ex, "Failed to parse vault item data for editing. Falling back to legacy fields.");
                Username = item.Username ?? string.Empty;
                Password = decrypted;
                Url = item.Url ?? string.Empty;
                Notes = item.Notes ?? string.Empty;
            }

            Logger.LogInformation("Loaded item for editing: {ItemId}, Type: {Type}", item.Id, item.Type);
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!ValidateInput())
            return;

        await ExecuteAsync(async () =>
        {
            Logger.LogInformation("Saving vault item: {Name}, Type: {Type}", Name, SelectedType);

            // Build type-specific payload (JSON) to be encrypted by the application layer.
            string dataToEncrypt;
            try
            {
                dataToEncrypt = SelectedType switch
                {
                    VaultItemType.Login => new LoginData
                    {
                        Username = string.IsNullOrWhiteSpace(Username) ? null : Username.Trim(),
                        Password = Password,
                        Website = string.IsNullOrWhiteSpace(Website) ? null : Website.Trim(),
                        Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim()
                    }.ToJson(),

                    VaultItemType.CreditCard => new CreditCardData
                    {
                        CardholderName = string.IsNullOrWhiteSpace(CardholderName) ? null : CardholderName.Trim(),
                        CardNumber = string.IsNullOrWhiteSpace(CardNumber) ? null : CardNumber.Trim(),
                        ExpiryMonth = string.IsNullOrWhiteSpace(ExpiryMonth) ? null : ExpiryMonth.Trim(),
                        ExpiryYear = string.IsNullOrWhiteSpace(ExpiryYear) ? null : ExpiryYear.Trim(),
                        CVV = string.IsNullOrWhiteSpace(CVV) ? null : CVV.Trim(),
                        BillingAddress = string.IsNullOrWhiteSpace(BillingAddress) ? null : BillingAddress.Trim(),
                        ZipCode = string.IsNullOrWhiteSpace(ZipCode) ? null : ZipCode.Trim()
                    }.ToJson(),

                    VaultItemType.SecureNote => new SecureNoteData
                    {
                        Content = string.IsNullOrWhiteSpace(NoteContent) ? null : NoteContent
                    }.ToJson(),

                    VaultItemType.Identity => new IdentityData
                    {
                        FirstName = string.IsNullOrWhiteSpace(FirstName) ? null : FirstName.Trim(),
                        MiddleName = string.IsNullOrWhiteSpace(MiddleName) ? null : MiddleName.Trim(),
                        LastName = string.IsNullOrWhiteSpace(LastName) ? null : LastName.Trim(),
                        DateOfBirth = string.IsNullOrWhiteSpace(DateOfBirth) ? null : DateOfBirth.Trim(),
                        Email = string.IsNullOrWhiteSpace(IdentityEmail) ? null : IdentityEmail.Trim(),
                        Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
                        Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
                        City = string.IsNullOrWhiteSpace(City) ? null : City.Trim(),
                        State = string.IsNullOrWhiteSpace(State) ? null : State.Trim(),
                        Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim(),
                        PassportNumber = string.IsNullOrWhiteSpace(PassportNumber) ? null : PassportNumber.Trim(),
                        LicenseNumber = string.IsNullOrWhiteSpace(LicenseNumber) ? null : LicenseNumber.Trim()
                    }.ToJson(),

                    VaultItemType.BankAccount => new BankAccountData
                    {
                        BankName = string.IsNullOrWhiteSpace(BankName) ? null : BankName.Trim(),
                        AccountHolderName = string.IsNullOrWhiteSpace(AccountHolderName) ? null : AccountHolderName.Trim(),
                        AccountNumber = string.IsNullOrWhiteSpace(AccountNumber) ? null : AccountNumber.Trim(),
                        RoutingNumber = string.IsNullOrWhiteSpace(RoutingNumber) ? null : RoutingNumber.Trim(),
                        IBAN = string.IsNullOrWhiteSpace(IBAN) ? null : IBAN.Trim()
                    }.ToJson(),

                    _ => throw new InvalidOperationException($"Unknown vault item type: {SelectedType}")
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to serialize vault item data to JSON");
                throw;
            }

            // Encryption key is still required by the application layer to encrypt the JSON payload.
            var encryptionKey = _masterPasswordService.GetPreferredKey();

            var request = new VaultItemRequest
            {
                Type = SelectedType,
                Name = Name.Trim(),
                Username = SelectedType == VaultItemType.Login && !string.IsNullOrWhiteSpace(Username)
                    ? Username.Trim()
                    : null,
                // Pass JSON payload to application layer, which performs encryption.
                Password = dataToEncrypt,
                Url = string.IsNullOrWhiteSpace(Url) ? null : Url.Trim(),
                Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                Tags = string.IsNullOrWhiteSpace(Tags) ? null : Tags.Trim(),
                IsFavorite = IsFavorite
            };

            Result<VaultItemDto> result;
            if (_existingItem == null)
            {
                result = await _mediator.Send(new CreateVaultItemCommand(_sessionService.CurrentUser!.Id, request, encryptionKey));
                Logger.LogInformation("Created new vault item");
            }
            else
            {
                result = await _mediator.Send(new UpdateVaultItemCommand(_sessionService.CurrentUser!.Id, _existingItem.Id, request, encryptionKey));
                Logger.LogInformation("Updated vault item: {ItemId}", _existingItem.Id);
            }

            if (result.IsFailure || result.Value == null)
            {
                throw new InvalidOperationException(result.Error?.Message ?? "Failed to save item");
            }

            // Callback to parent ViewModel
            _onSaveCallback?.Invoke(result.Value);

            Logger.LogInformation("Save successful, signaling window to close");
            
            // Set flag to trigger window close
            ShouldCloseWindow = true;

        }, "Failed to save item");
    }

    [RelayCommand]
    private async Task GeneratePasswordAsync()
    {
        await ExecuteAsync(async () =>
        {
            // Generate strong random password
            var length = 20; // Default length
            var includeSymbols = true;
            var includeNumbers = true;
            var includeUppercase = true;
            var includeLowercase = true;

            var password = GenerateSecurePassword(
                length, 
                includeLowercase, 
                includeUppercase, 
                includeNumbers, 
                includeSymbols);

            Password = password;

            Logger.LogInformation("Generated password with length {Length}", length);
            ShowInfo("Strong password generated!");

            await Task.CompletedTask;
        });
    }

    [RelayCommand]
    private async Task CheckPasswordBreachAsync()
    {
        if (string.IsNullOrWhiteSpace(Password))
        {
            ShowWarning("Please enter a password to check");
            return;
        }

        IsCheckingBreach = true;
        
        await ExecuteAsync(async () =>
        {
            Logger.LogInformation("Checking password breach status...");

            var result = await _hibpService.CheckPasswordAsync(Password);

            IsPasswordBreached = result.IsBreached;
            BreachCount = result.BreachCount;

            if (result.IsBreached)
            {
                ShowWarning(
                    $"⚠️ This password has been found in {result.BreachCount:N0} data breaches!\n\n" +
                    "It is strongly recommended to use a different password.",
                    "Security Warning");
            }
            else
            {
                ShowInfo("✓ This password has not been found in known data breaches.", "Good News");
            }

            Logger.LogInformation("Breach check complete. Breached: {IsBreached}, Count: {Count}", 
                result.IsBreached, result.BreachCount);

        }, "Failed to check password breach status");

        IsCheckingBreach = false;
    }

    [RelayCommand]
    private void ToggleShowPassword()
    {
        ShowPassword = !ShowPassword;
    }

    partial void OnPasswordChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            UpdatePasswordStrength(value);
        }
        else
        {
            PasswordStrengthText = string.Empty;
            PasswordStrengthScore = 0;
            IsPasswordBreached = false;
            BreachCount = 0;
        }
    }

    private void UpdatePasswordStrength(string password)
    {
        var analysis = _passwordStrengthService.AnalyzePassword(password);
        
        PasswordStrengthScore = analysis.Score;
        PasswordStrengthText = analysis.Level switch
        {
            StrengthLevel.VeryWeak => "Very Weak",
            StrengthLevel.Weak => "Weak",
            StrengthLevel.Fair => "Fair",
            StrengthLevel.Strong => "Strong",
            StrengthLevel.VeryStrong => "Very Strong",
            _ => "Unknown"
        };

        PasswordStrengthColor = analysis.Level switch
        {
            StrengthLevel.VeryWeak => "#D32F2F",
            StrengthLevel.Weak => "#F57C00",
            StrengthLevel.Fair => "#FBC02D",
            StrengthLevel.Strong => "#689F38",
            StrengthLevel.VeryStrong => "#388E3C",
            _ => "Gray"
        };
    }

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ShowError("Please enter a name for this item");
            return false;
        }

        switch (SelectedType)
        {
            case VaultItemType.Login:
                if (string.IsNullOrWhiteSpace(Password))
                {
                    ShowError("Please enter a password");
                    return false;
                }

                if (_passwordStrengthService.EvaluateStrength(Password) < StrengthLevel.Fair)
                {
                    if (!Confirm(
                            "This password is weak. Are you sure you want to save it?",
                            "Weak Password"))
                    {
                        return false;
                    }
                }
                break;

            case VaultItemType.CreditCard:
                if (string.IsNullOrWhiteSpace(CardholderName) ||
                    string.IsNullOrWhiteSpace(CardNumber))
                {
                    ShowError("Please enter cardholder name and card number");
                    return false;
                }
                break;

            case VaultItemType.SecureNote:
                if (string.IsNullOrWhiteSpace(NoteContent))
                {
                    ShowError("Please enter note content");
                    return false;
                }
                break;

            case VaultItemType.Identity:
                if (string.IsNullOrWhiteSpace(FirstName) &&
                    string.IsNullOrWhiteSpace(LastName) &&
                    string.IsNullOrWhiteSpace(IdentityEmail))
                {
                    ShowError("Please enter at least a name or email for the identity");
                    return false;
                }
                break;

            case VaultItemType.BankAccount:
                if (string.IsNullOrWhiteSpace(BankName) ||
                    string.IsNullOrWhiteSpace(AccountNumber))
                {
                    ShowError("Please enter bank name and account number");
                    return false;
                }
                break;
        }

        return true;
    }

    /// <summary>
    /// Generates a cryptographically secure random password.
    /// </summary>
    private string GenerateSecurePassword(
        int length,
        bool includeLowercase,
        bool includeUppercase,
        bool includeNumbers,
        bool includeSymbols)
    {
        const string lowercase = "abcdefghijklmnopqrstuvwxyz";
        const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string numbers = "0123456789";
        const string symbols = "!@#$%^&*()_+-=[]{}|;:,.<>?";

        var characterSet = string.Empty;
        if (includeLowercase) characterSet += lowercase;
        if (includeUppercase) characterSet += uppercase;
        if (includeNumbers) characterSet += numbers;
        if (includeSymbols) characterSet += symbols;

        if (string.IsNullOrEmpty(characterSet))
        {
            characterSet = lowercase + uppercase + numbers + symbols;
        }

        var password = new char[length];
        var randomBytes = _cryptoProvider.GenerateRandomKey(length);

        for (int i = 0; i < length; i++)
        {
            password[i] = characterSet[randomBytes[i] % characterSet.Length];
        }

        // Ensure at least one character from each selected category
        var random = new Random();
        if (includeLowercase && !password.Any(c => lowercase.Contains(c)))
        {
            password[random.Next(length)] = lowercase[randomBytes[0] % lowercase.Length];
        }
        if (includeUppercase && !password.Any(c => uppercase.Contains(c)))
        {
            password[random.Next(length)] = uppercase[randomBytes[1] % uppercase.Length];
        }
        if (includeNumbers && !password.Any(c => numbers.Contains(c)))
        {
            password[random.Next(length)] = numbers[randomBytes[2] % numbers.Length];
        }
        if (includeSymbols && !password.Any(c => symbols.Contains(c)))
        {
            password[random.Next(length)] = symbols[randomBytes[3] % symbols.Length];
        }

        return new string(password);
    }
}