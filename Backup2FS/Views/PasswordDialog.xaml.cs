using System.Windows;

namespace Backup2FS.Views
{
    /// <summary>
    /// Modal dialog that collects the iTunes backup password for decrypting an encrypted backup.
    /// </summary>
    public partial class PasswordDialog : Window
    {
        /// <summary>The password the user entered (valid only when ShowDialog() returns true).</summary>
        public string EnteredPassword { get; private set; } = string.Empty;

        public PasswordDialog(string? errorMessage = null)
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(errorMessage))
            {
                ErrorText.Text = errorMessage;
                ErrorText.Visibility = Visibility.Visible;
            }
            DecryptButton.IsEnabled = false;
            Loaded += (_, _) => PasswordEntry.Focus();
        }

        private string CurrentPassword =>
            ShowPasswordCheck.IsChecked == true ? PlainEntry.Text : PasswordEntry.Password;

        private void UpdateDecryptEnabled() => DecryptButton.IsEnabled = CurrentPassword.Length > 0;

        private void PasswordEntry_PasswordChanged(object sender, RoutedEventArgs e) => UpdateDecryptEnabled();

        private void PlainEntry_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdateDecryptEnabled();

        private void ShowPasswordCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (ShowPasswordCheck.IsChecked == true)
            {
                PlainEntry.Text = PasswordEntry.Password;
                PlainEntry.Visibility = Visibility.Visible;
                PasswordEntry.Visibility = Visibility.Collapsed;
                PlainEntry.Focus();
                PlainEntry.CaretIndex = PlainEntry.Text.Length;
            }
            else
            {
                PasswordEntry.Password = PlainEntry.Text;
                PasswordEntry.Visibility = Visibility.Visible;
                PlainEntry.Visibility = Visibility.Collapsed;
                PasswordEntry.Focus();
            }
        }

        private void DecryptButton_Click(object sender, RoutedEventArgs e)
        {
            EnteredPassword = CurrentPassword;
            if (EnteredPassword.Length == 0) return;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
