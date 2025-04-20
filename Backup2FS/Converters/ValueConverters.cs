using System;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;
using System.Diagnostics;

namespace Backup2FS.Converters
{
    /// <summary>
    /// Converts a boolean value to its inverse
    /// </summary>
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return value;
        }
    }

    /// <summary>
    /// Converts a boolean value to a play/pause icon
    /// </summary>
    public class BooleanToPlayPauseIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPaused && isPaused)
                return PackIconKind.Play;
            return PackIconKind.Pause;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a boolean value to "Pause" or "Resume" text
    /// </summary>
    public class BooleanToPauseResumeTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try 
            {
                // Ensure we're dealing with a boolean
                if (value is bool isPaused)
                {
                    return isPaused ? "Resume" : "Pause";
                }
                else if (value != null)
                {
                    // For debugging - this shouldn't happen, but if it does,
                    // we want to know what type we're getting
                    Debug.WriteLine($"BooleanToPauseResumeTextConverter received non-boolean: {value.GetType()}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in BooleanToPauseResumeTextConverter: {ex.Message}");
            }
            
            // Default to Pause if we can't determine the state
            return "Pause";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 