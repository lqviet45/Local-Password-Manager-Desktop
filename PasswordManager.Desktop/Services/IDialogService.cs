namespace PasswordManager.Desktop.Services;

/// <summary>
/// Service for showing dialogs and messages to user.
/// </summary>
public interface IDialogService
{
    void ShowInfo(string message, string title = "Information");
    void ShowWarning(string message, string title = "Warning");
    void ShowError(string message, string title = "Error");
    bool ShowConfirmation(string message, string title = "Confirm");
    string? ShowInputDialog(string message, string title = "Input", string defaultValue = "");
}