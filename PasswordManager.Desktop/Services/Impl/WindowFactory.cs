using Microsoft.Extensions.DependencyInjection;
using PasswordManager.Desktop.Views;

namespace PasswordManager.Desktop.Services.Impl;

public sealed class WindowFactory : IWindowFactory
{
    private readonly IServiceProvider _serviceProvider;

    public WindowFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public LoginWindow CreateLoginWindow()
    {
        return _serviceProvider.GetRequiredService<LoginWindow>();
    }

    public MainWindow CreateMainWindow()
    {
        return _serviceProvider.GetRequiredService<MainWindow>();
    }
}