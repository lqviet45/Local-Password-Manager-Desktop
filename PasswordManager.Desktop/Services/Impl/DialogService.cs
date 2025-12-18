using System.Windows;

namespace PasswordManager.Desktop.Services.Impl;

public sealed class DialogService : IDialogService
{
    public void ShowInfo(string message, string title = "Information")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowWarning(string message, string title = "Warning")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void ShowError(string message, string title = "Error")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public bool ShowConfirmation(string message, string title = "Confirm")
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    public string? ShowInputDialog(string message, string title = "Input", string defaultValue = "")
    {
        // For now, use a simple InputBox (you can create a custom window later)
        var dialog = new InputDialog(message, title, defaultValue);
        var result = dialog.ShowDialog();
        return result == true ? dialog.ResponseText : null;
    }
}
