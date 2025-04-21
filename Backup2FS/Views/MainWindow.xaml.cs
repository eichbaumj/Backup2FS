using System;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using Backup2FS.ViewModels;
using System.Collections.Generic;
using System.Xml;
using System.IO;

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

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Window
                {
                    Title = "Options",
                    Width = 500,
                    Height = 350,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize,
                    Background = System.Windows.Media.Brushes.White
                };

                var mainGrid = new Grid();
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                dialog.Content = mainGrid;

                // Left sidebar
                var leftPanel = new Border
                {
                    Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#142A40"))
                };
                Grid.SetColumn(leftPanel, 0);
                mainGrid.Children.Add(leftPanel);

                var leftPanelStack = new StackPanel
                {
                    Margin = new Thickness(10, 20, 10, 10)
                };
                
                var sidebarHeader = new TextBlock
                {
                    Text = "Hash Algorithms",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.White,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                leftPanelStack.Children.Add(sidebarHeader);
                leftPanel.Child = leftPanelStack;

                // Right content area with grid layout
                var contentGrid = new Grid();
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(contentGrid, 1);
                mainGrid.Children.Add(contentGrid);

                // Content header
                var headerPanel = new StackPanel
                {
                    Margin = new Thickness(20, 20, 20, 10)
                };
                
                var headerText = new TextBlock
                {
                    Text = "Hash Algorithms",
                    FontSize = 18,
                    FontWeight = FontWeights.Regular,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                headerPanel.Children.Add(headerText);
                Grid.SetRow(headerPanel, 0);
                contentGrid.Children.Add(headerPanel);

                // Content panel with checkboxes
                var contentPanel = new StackPanel
                {
                    Margin = new Thickness(20, 0, 20, 0),
                    Background = System.Windows.Media.Brushes.WhiteSmoke
                };
                Grid.SetRow(contentPanel, 1);
                contentGrid.Children.Add(contentPanel);

                // Checkboxes for hash algorithms
                var md5CheckBox = new System.Windows.Controls.CheckBox
                {
                    Content = "MD5",
                    IsChecked = ViewModel.UsesMD5,
                    Margin = new Thickness(20, 15, 0, 10),
                    FontSize = 14,
                    BorderBrush = System.Windows.Media.Brushes.Black,
                    BorderThickness = new Thickness(2),
                    Background = System.Windows.Media.Brushes.White,
                    Foreground = System.Windows.Media.Brushes.Black
                };

                // Create a style for the checkboxes
                var checkBoxStyle = new System.Windows.Style(typeof(System.Windows.Controls.CheckBox));
                
                // Create a control template for checkbox
                var template = new System.Windows.Controls.ControlTemplate(typeof(System.Windows.Controls.CheckBox));
                
                // Create the template content
                var grid = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.Grid));
                
                var border = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.Border));
                border.SetValue(System.Windows.Controls.Border.WidthProperty, 20.0);
                border.SetValue(System.Windows.Controls.Border.HeightProperty, 20.0);
                border.SetValue(System.Windows.Controls.Border.BorderBrushProperty, System.Windows.Media.Brushes.Black);
                border.SetValue(System.Windows.Controls.Border.BorderThicknessProperty, new Thickness(2));
                border.SetValue(System.Windows.Controls.Border.BackgroundProperty, System.Windows.Media.Brushes.White);
                border.SetValue(System.Windows.Controls.Border.MarginProperty, new Thickness(0, 0, 10, 0));
                
                // Orange checkmark - using a path with the brand color
                var checkmark = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
                checkmark.SetValue(System.Windows.Shapes.Path.StrokeProperty, 
                                  new System.Windows.Media.SolidColorBrush(
                                      (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F37934")));
                checkmark.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty, 2.5);
                checkmark.SetValue(System.Windows.Shapes.Path.DataProperty, 
                                  System.Windows.Media.Geometry.Parse("M 3,10 L 8,15 L 17,4"));
                
                // Bind visibility to IsChecked state
                var binding = new System.Windows.Data.Binding("IsChecked");
                binding.RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent);
                binding.Converter = new System.Windows.Controls.BooleanToVisibilityConverter();
                checkmark.SetBinding(System.Windows.UIElement.VisibilityProperty, binding);
                
                border.AppendChild(checkmark);
                
                // Create the content presenter for label
                var contentPresenter = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
                contentPresenter.SetValue(System.Windows.Controls.ContentPresenter.VerticalAlignmentProperty, 
                                         System.Windows.VerticalAlignment.Center);
                
                // Layout horizontally
                var stackPanel = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.StackPanel));
                stackPanel.SetValue(System.Windows.Controls.StackPanel.OrientationProperty, 
                                   System.Windows.Controls.Orientation.Horizontal);
                stackPanel.AppendChild(border);
                stackPanel.AppendChild(contentPresenter);
                
                grid.AppendChild(stackPanel);
                template.VisualTree = grid;
                
                // Set template to style
                checkBoxStyle.Setters.Add(new System.Windows.Setter(
                    System.Windows.Controls.Control.TemplateProperty, template));
                
                // Apply the style to all checkboxes
                md5CheckBox.Style = checkBoxStyle;
                contentPanel.Children.Add(md5CheckBox);

                var sha1CheckBox = new System.Windows.Controls.CheckBox
                {
                    Content = "SHA-1",
                    IsChecked = ViewModel.UsesSHA1,
                    Margin = new Thickness(20, 0, 0, 10),
                    FontSize = 14,
                    Style = checkBoxStyle
                };
                contentPanel.Children.Add(sha1CheckBox);

                var sha256CheckBox = new System.Windows.Controls.CheckBox
                {
                    Content = "SHA-256",
                    IsChecked = ViewModel.UsesSHA256,
                    Margin = new Thickness(20, 0, 0, 20),
                    FontSize = 14,
                    Style = checkBoxStyle
                };
                contentPanel.Children.Add(sha256CheckBox);

                // Buttons panel
                var buttonsPanel = new Grid
                {
                    Margin = new Thickness(0, 10, 20, 20),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right
                };
                buttonsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                buttonsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetRow(buttonsPanel, 2);
                contentGrid.Children.Add(buttonsPanel);

                var okButton = new System.Windows.Controls.Button
                {
                    Content = "OK",
                    Width = 100,
                    Height = 35,
                    Margin = new Thickness(0, 0, 10, 0),
                    Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F37934")),
                    Foreground = System.Windows.Media.Brushes.White
                };
                Grid.SetColumn(okButton, 0);
                okButton.Click += (s, args) =>
                {
                    try
                    {
                        var selectedAlgorithms = new List<string>();
                        Console.WriteLine("==== OPTIONS DIALOG OK BUTTON CLICKED ====");
                        Console.WriteLine($"Checkbox states - MD5: {md5CheckBox.IsChecked}, SHA1: {sha1CheckBox.IsChecked}, SHA256: {sha256CheckBox.IsChecked}");
                        
                        // Build the algorithm string for display
                        if (md5CheckBox.IsChecked == true) selectedAlgorithms.Add("md5");
                        if (sha1CheckBox.IsChecked == true) selectedAlgorithms.Add("sha1");
                        if (sha256CheckBox.IsChecked == true) selectedAlgorithms.Add("sha256");
                        
                        // Create the algorithms string
                        string algorithmsString = string.Join(",", selectedAlgorithms);
                        Console.WriteLine($"Selected algorithms string: {algorithmsString}");
                        
                        // Call the SetHashAlgorithmsFromString method directly on the SettingsManager
                        // This bypasses the problematic property setters
                        Console.WriteLine("Directly updating settings with selected algorithms");
                        var settingsManager = new Backup2FS.Services.SettingsManager();
                        settingsManager.SetHashAlgorithmsFromString(algorithmsString);
                        
                        // Update the ViewModel's SelectedHashAlgorithm property
                        ViewModel.SelectedHashAlgorithm = algorithmsString;
                        
                        // Now reload the settings to refresh the ViewModel
                        Console.WriteLine("Refreshing ViewModel from saved settings");
                        ViewModel.SaveOptions();
                        
                        dialog.DialogResult = true;
                        dialog.Close();

                        System.Windows.MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERROR in okButton.Click: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                        System.Windows.MessageBox.Show($"Error saving options: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                buttonsPanel.Children.Add(okButton);

                var cancelButton = new System.Windows.Controls.Button
                {
                    Content = "Cancel",
                    Width = 100,
                    Height = 35,
                    Background = System.Windows.Media.Brushes.Gray,
                    Foreground = System.Windows.Media.Brushes.White
                };
                Grid.SetColumn(cancelButton, 1);
                cancelButton.Click += (s, args) =>
                {
                    dialog.DialogResult = false;
                    dialog.Close();
                };
                buttonsPanel.Children.Add(cancelButton);

                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening options dialog: {ex.Message}");
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