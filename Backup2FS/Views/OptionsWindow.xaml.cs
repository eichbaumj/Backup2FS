using System.Windows;
using System.Windows.Controls;
using Backup2FS.ViewModels;

namespace Backup2FS.Views
{
    /// <summary>
    /// Interaction logic for OptionsWindow.xaml
    /// </summary>
    public partial class OptionsWindow : Window
    {
        // Snapshot of the hash-algorithm selection on open, so Cancel can revert.
        private bool _origMD5;
        private bool _origSHA1;
        private bool _origSHA256;
        private bool _snapshotTaken;

        private MainViewModel? ViewModel => DataContext as MainViewModel;

        public OptionsWindow()
        {
            InitializeComponent();
            Loaded += OptionsWindow_Loaded;
        }

        private void OptionsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel is { } vm && !_snapshotTaken)
            {
                _origMD5 = vm.UsesMD5;
                _origSHA1 = vm.UsesSHA1;
                _origSHA256 = vm.UsesSHA256;
                _snapshotTaken = true;
            }
        }

        private void SectionButton_Click(object sender, RoutedEventArgs e)
        {
            // Single section today; selection styling is handled in XAML. Hook kept so
            // additional sections can be added without restructuring.
            if (sender is System.Windows.Controls.Button button && button.Tag is string section)
            {
                GeneralContent.Visibility = section == "General" ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.SaveOptions();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel is { } vm && _snapshotTaken)
            {
                vm.UsesMD5 = _origMD5;
                vm.UsesSHA1 = _origSHA1;
                vm.UsesSHA256 = _origSHA256;
            }
            DialogResult = false;
            Close();
        }
    }
}
