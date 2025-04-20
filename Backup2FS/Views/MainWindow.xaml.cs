using System;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using Backup2FS.ViewModels;

namespace Backup2FS.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel => (MainViewModel)DataContext;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                
                // Set DataContext to our ViewModel
                DataContext = new MainViewModel();
                
                // Subscribe to log property changes to auto-scroll
                if (DataContext is MainViewModel vm)
                {
                    vm.PropertyChanged += (sender, e) => {
                        if (e.PropertyName == nameof(MainViewModel.LogOutput))
                        {
                            ScrollLogToEnd();
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error initializing application: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        /// <summary>
        /// Scrolls the log TextBox to the end to show the latest entries
        /// </summary>
        private void ScrollLogToEnd()
        {
            if (LogTextBox != null)
            {
                LogTextBox.ScrollToEnd();
            }
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get the dialog template
                if (FindResource("OptionsDialogTemplate") is DataTemplate template)
                {
                    // Create a view model for the dialog
                    var dialogContext = new OptionsDialogViewModel
                    {
                        UsesMD5 = ViewModel.UsesMD5,
                        UsesSHA1 = ViewModel.UsesSHA1,
                        UsesSHA256 = ViewModel.UsesSHA256,
                        HashAppBinary = "true",
                        HashAppResources = "true"
                    };
                    
                    // Create content from template with the view model as context
                    ContentControl content = new ContentControl
                    {
                        ContentTemplate = template,
                        Content = dialogContext
                    };
                    
                    // Show dialog with properly templated content
                    var result = await DialogHost.Show(content, "RootDialog");
                    
                    // Process the result
                    if (result is bool dialogResult && dialogResult)
                    {
                        // Save settings to the view model
                        ViewModel.UsesMD5 = dialogContext.UsesMD5;
                        ViewModel.UsesSHA1 = dialogContext.UsesSHA1;
                        ViewModel.UsesSHA256 = dialogContext.UsesSHA256;
                        
                        // Ensure at least one hash algorithm is selected
                        if (!ViewModel.UsesMD5 && !ViewModel.UsesSHA1 && !ViewModel.UsesSHA256)
                        {
                            // Default to SHA-256 if nothing is selected
                            ViewModel.UsesSHA256 = true;
                            System.Windows.MessageBox.Show("At least one hash algorithm must be selected. SHA-256 has been enabled by default.", 
                                "Hash Algorithm Required", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Information);
                        }
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show("Could not find the options dialog template.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening options dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get the dialog template
                if (FindResource("AboutDialogTemplate") is DataTemplate template)
                {
                    // Create content from template
                    ContentControl content = new ContentControl
                    {
                        ContentTemplate = template,
                        Content = new object() // Simple content object since we don't need data binding
                    };
                    
                    // Show dialog with properly templated content
                    await DialogHost.Show(content, "RootDialog");
                }
                else
                {
                    System.Windows.MessageBox.Show("Could not find the about dialog template.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening about dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            // Exit application
            ViewModel.ExitCommand.Execute(null);
        }
    }
    
    /// <summary>
    /// View model for the options dialog
    /// </summary>
    public class OptionsDialogViewModel
    {
        public bool UsesMD5 { get; set; }
        public bool UsesSHA1 { get; set; }
        public bool UsesSHA256 { get; set; }
        public string HashAppBinary { get; set; }
        public string HashAppResources { get; set; }
    }

    // ViewModel classes for binding
    public class MainWindowViewModel
    {
        // Commands
        public RelayCommand OpenOptionsCommand { get; }
        public RelayCommand OpenAboutCommand { get; }
        public RelayCommand ExitCommand { get; }
        
        // Properties for device info
        public string DeviceName { get; set; } = "iPhone";
        public string DeviceModel { get; set; } = "iPhone 12";
        public string IosVersion { get; set; } = "15.0";
        public string BuildVersion { get; set; } = "19A346";
        public string SerialNumber { get; set; } = "C39JDQW3G5QQ";
        public string Udid { get; set; } = "00008020-000D5C100102002E";
        public string Imei { get; set; } = "353255090549947";
        public string Encryption { get; set; } = "No";
        
        // Property for log output
        public string LogOutput { get; set; } = "Application started...\n";
        
        // Collection of installed apps
        public System.Collections.ObjectModel.ObservableCollection<AppInfo> InstalledApps { get; }
        
        public MainWindowViewModel()
        {
            // Initialize commands
            OpenOptionsCommand = new RelayCommand(OpenOptions);
            OpenAboutCommand = new RelayCommand(OpenAbout);
            ExitCommand = new RelayCommand(Exit);
            
            // Sample apps data
            InstalledApps = new System.Collections.ObjectModel.ObservableCollection<AppInfo>
            {
                new AppInfo { Name = "Messages", BundleId = "com.apple.MobileSMS" },
                new AppInfo { Name = "Photos", BundleId = "com.apple.mobileslideshow" },
                new AppInfo { Name = "Camera", BundleId = "com.apple.camera" },
                new AppInfo { Name = "Maps", BundleId = "com.apple.Maps" },
                new AppInfo { Name = "Calendar", BundleId = "com.apple.mobilecal" }
            };
        }
        
        private async void OpenOptions(object parameter)
        {
            var viewModel = new OptionsViewModel
            {
                SelectedHashAlgorithm = "SHA256" // Default selected algorithm
            };

            // Open the options dialog
            var result = await DialogHost.Show(viewModel, "RootDialog");
            
            // Handle the dialog result
            if (result is bool dialogResult && dialogResult)
            {
                // Apply the options
                // Implementation would depend on your app's settings mechanism
                Console.WriteLine($"Selected hash algorithm: {viewModel.SelectedHashAlgorithm}");
            }
        }
        
        private void OpenAbout(object parameter)
        {
            // Show about information
            System.Windows.MessageBox.Show("Backup2FS - iOS Backup File Recovery\nVersion 1.0\n© 2023", "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void Exit(object parameter)
        {
            // Exit the application
            System.Windows.Application.Current.Shutdown();
        }
    }
    
    public class OptionsViewModel
    {
        public string SelectedHashAlgorithm { get; set; }
    }
    
    public class AppInfo
    {
        public string Name { get; set; }
        public string BundleId { get; set; }
    }
    
    // Simple implementation of RelayCommand pattern
    public class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;
        
        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        
        public event EventHandler CanExecuteChanged
        {
            add { System.Windows.Input.CommandManager.RequerySuggested += value; }
            remove { System.Windows.Input.CommandManager.RequerySuggested -= value; }
        }
        
        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }
        
        public void Execute(object parameter)
        {
            _execute(parameter);
        }
    }
} 