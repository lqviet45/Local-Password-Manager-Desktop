using PasswordManager.Desktop.Views;

namespace PasswordManager.Desktop.Services;

/// <summary>
/// Factory for creating windows with dependency injection.
/// </summary>
public interface IWindowFactory
{
    LoginWindow CreateLoginWindow();
    MainWindow CreateMainWindow();
}