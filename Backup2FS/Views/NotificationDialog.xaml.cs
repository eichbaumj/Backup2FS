using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Data;
using System.Globalization;
// This project enables WindowsForms, so disambiguate the WPF types we use here.
using Button = System.Windows.Controls.Button;
using Application = System.Windows.Application;

namespace Backup2FS.Views
{
    /// <summary>
    /// Custom notification dialog that replaces ugly MessageBox popups
    /// </summary>
    public partial class NotificationDialog : Window
    {
        private TaskCompletionSource<NotificationResult> _taskCompletionSource;
        private bool _isClosing = false;

        // The result the primary (default) button yields, so Enter can trigger it for
        // fast keyboard dismissal of a stack of popups. Defaults to OK.
        private NotificationResult _primaryResult = NotificationResult.OK;
        private bool _hasPrimary = false;
        private Button _primaryButton;

        public NotificationDialog()
        {
            InitializeComponent();
            Loaded += NotificationDialog_Loaded;
        }

        /// <summary>
        /// Shows a success notification
        /// </summary>
        public static Task<NotificationResult> ShowSuccessAsync(Window owner, string title, string message, List<string> details = null, NotificationButtons buttons = NotificationButtons.OK)
        {
            return ShowAsync(owner, title, message, NotificationType.Success, details, buttons);
        }

        /// <summary>
        /// Shows an info notification
        /// </summary>
        public static Task<NotificationResult> ShowInfoAsync(Window owner, string title, string message, List<string> details = null, NotificationButtons buttons = NotificationButtons.OK)
        {
            return ShowAsync(owner, title, message, NotificationType.Info, details, buttons);
        }

        /// <summary>
        /// Shows a warning notification
        /// </summary>
        public static Task<NotificationResult> ShowWarningAsync(Window owner, string title, string message, List<string> details = null, NotificationButtons buttons = NotificationButtons.OK)
        {
            return ShowAsync(owner, title, message, NotificationType.Warning, details, buttons);
        }

        /// <summary>
        /// Shows an error notification
        /// </summary>
        public static Task<NotificationResult> ShowErrorAsync(Window owner, string title, string message, List<string> details = null, NotificationButtons buttons = NotificationButtons.OK)
        {
            return ShowAsync(owner, title, message, NotificationType.Error, details, buttons);
        }

        /// <summary>
        /// Shows a forensic analysis notification (blue theme)
        /// </summary>
        public static Task<NotificationResult> ShowForensicAsync(Window owner, string title, string message, List<string> details = null, NotificationButtons buttons = NotificationButtons.OK)
        {
            return ShowAsync(owner, title, message, NotificationType.Forensic, details, buttons);
        }

        /// <summary>
        /// Shows a notification with custom parameters
        /// </summary>
        public static Task<NotificationResult> ShowAsync(Window owner, string title, string message, NotificationType type = NotificationType.Info, List<string> details = null, NotificationButtons buttons = NotificationButtons.OK)
        {
            var dialog = new NotificationDialog();
            dialog.Owner = owner;
            dialog.SetupNotification(title, message, type, details, buttons);
            
            return dialog.ShowDialogAsync();
        }

        private void SetupNotification(string title, string message, NotificationType type, List<string> details, NotificationButtons buttons)
        {
            // Set title and message
            TitleText.Text = title;
            MessageText.Text = message;

            // Set icon based on type
            switch (type)
            {
                case NotificationType.Success:
                    IconText.Text = "\u2611"; // ☑️ - Square with check mark (professional)
                    IconText.SetResourceReference(TextBlock.ForegroundProperty, "SuccessDialogAccentBrush");
                    IconText.FontWeight = FontWeights.Bold;
                    IconText.FontSize = 28;
                    // Also set border and title colors - use SetResourceReference for theme-aware binding
                    MainBorder.SetResourceReference(Border.BorderBrushProperty, "SuccessDialogAccentBrush");
                    TitleText.SetResourceReference(TextBlock.ForegroundProperty, "SuccessDialogAccentBrush");
                    break;
                case NotificationType.Info:
                    IconText.Text = "ℹ️";
                    IconText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 121, 52)); // Orange theme color
                    IconText.FontWeight = FontWeights.Bold;
                    IconText.FontSize = 28;
                    break;
                case NotificationType.Warning:
                    IconText.Text = "⚠️";
                    IconText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 121, 52)); // Orange theme color
                    IconText.FontWeight = FontWeights.Bold;
                    IconText.FontSize = 28;
                    break;
                case NotificationType.Error:
                    IconText.Text = "❌";
                    IconText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 61, 83)); // Brand red #EE3D53
                    IconText.FontWeight = FontWeights.Bold;
                    IconText.FontSize = 28;
                    break;
                case NotificationType.Forensic:
                    IconText.Text = "ℹ️";
                    IconText.SetResourceReference(TextBlock.ForegroundProperty, "SuccessDialogAccentBrush");
                    IconText.FontWeight = FontWeights.Bold;
                    IconText.FontSize = 28;
                    // Also set border and title colors - use SetResourceReference for theme-aware binding
                    MainBorder.SetResourceReference(Border.BorderBrushProperty, "SuccessDialogAccentBrush");
                    TitleText.SetResourceReference(TextBlock.ForegroundProperty, "SuccessDialogAccentBrush");
                    break;
            }

            // Setup details if provided
            if (details != null && details.Count > 0)
            {
                DetailsPanel.Visibility = Visibility.Visible;
                DetailsList.ItemsSource = details;
            }

            // Setup buttons
            SetupButtons(buttons);
        }

        private void SetupButtons(NotificationButtons buttons)
        {
            ButtonsPanel.Children.Clear();

            switch (buttons)
            {
                case NotificationButtons.OK:
                    AddButton("OK", NotificationResult.OK, true);
                    break;
                case NotificationButtons.YesNo:
                    AddButton("No", NotificationResult.No, false);
                    AddButton("Yes", NotificationResult.Yes, true);
                    break;
                case NotificationButtons.OKCancel:
                    AddButton("Cancel", NotificationResult.Cancel, false);
                    AddButton("OK", NotificationResult.OK, true);
                    break;
                case NotificationButtons.Custom:
                    // For custom buttons like "Open Table", "OK"
                    AddButton("OK", NotificationResult.OK, false);
                    AddButton("Open Table", NotificationResult.Custom, true);
                    break;
            }
        }

        private void AddButton(string text, NotificationResult result, bool isPrimary)
        {
            var button = new Button
            {
                Content = text,
                Style = isPrimary ?
                    (Style)FindResource("NotificationButtonStyle") :
                    (Style)FindResource("SecondaryButtonStyle"),
                // IsDefault so Enter activates the primary button; IsCancel so Escape
                // activates the dismiss/No/Cancel button — quick keyboard navigation.
                IsDefault = isPrimary,
                IsCancel = !isPrimary && (result == NotificationResult.Cancel || result == NotificationResult.No)
            };

            button.Click += (s, e) => CloseWithResult(result);
            ButtonsPanel.Children.Add(button);

            if (isPrimary)
            {
                _primaryResult = result;
                _hasPrimary = true;
                _primaryButton = button;
            }
        }

        private Task<NotificationResult> ShowDialogAsync()
        {
            _taskCompletionSource = new TaskCompletionSource<NotificationResult>();
            
            // Show the dialog
            Show();
            
            return _taskCompletionSource.Task;
        }

        private void CloseWithResult(NotificationResult result)
        {
            if (_isClosing) return;
            _isClosing = true;

            // Animate out
            var fadeOut = (Storyboard)FindResource("FadeOutAnimation");
            fadeOut.Completed += (s, e) =>
            {
                _taskCompletionSource?.TrySetResult(result);
                Close();

                // The dialog is shown non-modally (Show + awaited task), and closing a
                // non-modal owned window does NOT re-activate whatever is underneath —
                // Windows hands focus to the next z-order window (e.g. a console). If another
                // notification is stacked behind this one, focus IT so Enter keeps working
                // without a click; otherwise pull focus back to the owner window.
                ActivateNextDialogOrOwner();
            };
            fadeOut.Begin(this);
        }

        private void NotificationDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // Animate in
            var fadeIn = (Storyboard)FindResource("FadeInAnimation");
            fadeIn.Begin(this);

            // Pull keyboard focus onto the primary button so Enter dismisses immediately —
            // lets the examiner click through a stack of popups from the keyboard.
            try
            {
                Activate();
                _primaryButton?.Focus();
            }
            catch { }
        }

        // Enter activates the primary action, Escape dismisses. Handled here too (not just via
        // IsDefault/IsCancel) so it works reliably for this non-modal, animated dialog.
        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Return)
            {
                e.Handled = true;
                CloseWithResult(_hasPrimary ? _primaryResult : NotificationResult.OK);
                return;
            }
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                e.Handled = true;
                CloseWithResult(NotificationResult.Cancel);
                return;
            }
            base.OnPreviewKeyDown(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (!_isClosing)
            {
                _taskCompletionSource?.TrySetResult(NotificationResult.Cancel);
            }
            base.OnClosed(e);

            // Safety net: the dialog is non-modal, and closing a non-modal owned window
            // does NOT re-activate its owner — keep focus inside the app (and hand it to a
            // stacked notification underneath if one is still open).
            ActivateNextDialogOrOwner();
        }

        /// <summary>
        /// Give keyboard focus to the next still-open notification stacked behind this one (so Enter
        /// dismisses it without a click), or fall back to the owner window when this is the last one.
        /// </summary>
        private void ActivateNextDialogOrOwner()
        {
            try
            {
                NotificationDialog next = null;
                if (Application.Current != null)
                {
                    foreach (Window w in Application.Current.Windows)
                    {
                        if (w is NotificationDialog nd && !ReferenceEquals(nd, this) && !nd._isClosing && nd.IsVisible)
                            next = nd; // last visible one in the collection = most recently shown (topmost)
                    }
                }

                if (next != null)
                {
                    next.Activate();
                    if (next._primaryButton != null)
                        next._primaryButton.Focus();
                }
                else
                {
                    Owner?.Activate();
                }
            }
            catch { }
        }

        // Allow clicking outside to close
        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.OriginalSource == this)
            {
                CloseWithResult(NotificationResult.Cancel);
            }
            base.OnMouseLeftButtonDown(e);
        }
    }

    /// <summary>
    /// Types of notifications
    /// </summary>
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error,
        Forensic
    }

    /// <summary>
    /// Button configurations for notifications
    /// </summary>
    public enum NotificationButtons
    {
        OK,
        YesNo,
        OKCancel,
        Custom
    }

    /// <summary>
    /// Results from notification dialog
    /// </summary>
    public enum NotificationResult
    {
        OK,
        Cancel,
        Yes,
        No,
        Custom
    }

    /// <summary>
    /// Converter to check if a string starts with a specific value
    /// </summary>
    public class StartsWithConverter : IValueConverter
    {
        public static StartsWithConverter Instance { get; } = new StartsWithConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text && parameter is string prefix)
            {
                return text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 