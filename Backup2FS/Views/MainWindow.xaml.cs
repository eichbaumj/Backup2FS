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
                var options = new OptionsWindow { Owner = this, DataContext = ViewModel };
                options.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening options dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var about = new AboutWindow { Owner = this };
                about.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening about dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/eichbaumj/Backup2FS",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening help: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===== Custom window chrome (the brand ribbon is the caption bar) =====

        private void TitleBarMinimize_Click(object sender, RoutedEventArgs e)
            => SystemCommands.MinimizeWindow(this);

        private void TitleBarMaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                SystemCommands.RestoreWindow(this);
            else
                SystemCommands.MaximizeWindow(this);
        }

        private void TitleBarClose_Click(object sender, RoutedEventArgs e)
            => SystemCommands.CloseWindow(this);

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (MaximizeIcon != null)
            {
                MaximizeIcon.Kind = WindowState == WindowState.Maximized
                    ? PackIconKind.WindowRestore
                    : PackIconKind.WindowMaximize;
            }
        }
    }
}
