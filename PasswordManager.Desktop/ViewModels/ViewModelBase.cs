using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PasswordManager.Desktop.Services;

namespace PasswordManager.Desktop.ViewModels;

/// <summary>
/// Base class for all ViewModels.
/// Provides common functionality like error handling, busy state, and services.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    protected readonly IDialogService DialogService;
    protected readonly ILogger Logger;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _title = string.Empty;

    protected ViewModelBase(IDialogService dialogService, ILogger logger)
    {
        DialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes an async action with error handling and busy state management.
    /// </summary>
    protected async Task ExecuteAsync(Func<Task> action, string? errorMessage = null)
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing action");
            ErrorMessage = errorMessage ?? ex.Message;
            DialogService.ShowError(ErrorMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Executes an async action with result, error handling and busy state.
    /// </summary>
    protected async Task<T?> ExecuteAsync<T>(Func<Task<T>> action, string? errorMessage = null)
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing action");
            ErrorMessage = errorMessage ?? ex.Message;
            DialogService.ShowError(ErrorMessage);
            return default;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Shows a confirmation dialog.
    /// </summary>
    protected bool Confirm(string message, string title = "Confirm")
    {
        return DialogService.ShowConfirmation(message, title);
    }

    /// <summary>
    /// Shows an info message.
    /// </summary>
    protected void ShowInfo(string message, string title = "Information")
    {
        DialogService.ShowInfo(message, title);
    }

    /// <summary>
    /// Shows an error message.
    /// </summary>
    protected void ShowError(string message, string title = "Error")
    {
        DialogService.ShowError(message, title);
    }
}