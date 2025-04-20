using System;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace TestPauseButton.Converters
{
    /// <summary>
    /// Converts a boolean value to "Pause" or "Resume" text
    /// </summary>
    public class BooleanToPauseResumeTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPaused)
            {
                return isPaused ? "Resume" : "Pause";
            }
            return "Pause";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 