using System.Windows;
using Microsoft.Extensions.Logging;
using PasswordManager.Desktop.ViewModels;

namespace PasswordManager.Desktop.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ILogger<MainWindow> _logger;

    public MainWindow(MainViewModel viewModel, ILogger<MainWindow> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _logger.LogInformation("=== MainWindow Constructor ===");
        _logger.LogInformation("Received MainViewModel: {Type}", viewModel?.GetType().Name ?? "NULL");
        
        if (viewModel == null)
        {
            _logger.LogError("MainViewModel is NULL in constructor!");
            throw new ArgumentNullException(nameof(viewModel));
        }
        
        // CRITICAL: Set DataContext BEFORE InitializeComponent
        DataContext = viewModel;
        _logger.LogInformation("DataContext set to MainViewModel");
        
        InitializeComponent();
        
        _logger.LogInformation("MainWindow initialized");
        _logger.LogInformation("  - CurrentViewModel: {Type}", 
            viewModel.CurrentViewModel?.GetType().Name ?? "NULL");
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("=== MainWindow Loaded Event ===");
        
        if (DataContext is not MainViewModel vm)
        {
            _logger.LogError("DataContext is not MainViewModel!");
            return;
        }
        
        _logger.LogInformation("DataContext confirmed as MainViewModel");
        _logger.LogInformation("  - CurrentViewModel: {Type}", 
            vm.CurrentViewModel?.GetType().Name ?? "NULL");
        
        // Ensure CurrentViewModel is set
        if (vm.CurrentViewModel == null)
        {
            _logger.LogWarning("CurrentViewModel is NULL! Setting to VaultViewModel");
            vm.CurrentViewModel = vm.VaultViewModel;
        }
        
        // Load vault items
        if (vm.VaultViewModel != null)
        {
            _logger.LogInformation("Loading VaultViewModel items...");
            try
            {
                await vm.VaultViewModel.LoadItemsAsync();
                _logger.LogInformation("✓ VaultViewModel loaded. Items: {Count}", 
                    vm.VaultViewModel.TotalItemsCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load vault items");
                System.Windows.MessageBox.Show($"Failed to load vault items: {ex.Message}", 
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        else
        {
            _logger.LogError("VaultViewModel is NULL!");
        }
    }
}