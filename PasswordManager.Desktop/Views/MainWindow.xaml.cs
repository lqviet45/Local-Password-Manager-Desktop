using System.Windows;
using PasswordManager.Desktop.ViewModels;

namespace PasswordManager.Desktop.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Load vault items when window loads
        if (_viewModel.VaultViewModel != null)
        {
            await _viewModel.VaultViewModel.LoadItemsAsync();
        }
    }
}
