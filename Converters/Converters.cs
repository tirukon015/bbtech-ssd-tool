using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using BBTechSSDTool.Models;

namespace BBTechSSDTool.Converters
{
    public class HealthStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HealthStatus status)
            {
                return status switch
                {
                    HealthStatus.Good => new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76)),
                    HealthStatus.Warning => new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x00)),
                    HealthStatus.Critical => new SolidColorBrush(Color.FromRgb(0xFF, 0x3D, 0x00)),
                    HealthStatus.Failed => new SolidColorBrush(Color.FromRgb(0xD5, 0x00, 0x00)),
                    HealthStatus.Unavailable => new SolidColorBrush(Color.FromRgb(0x78, 0x90, 0x9C)),
                    _ => new SolidColorBrush(Color.FromRgb(0x60, 0x7D, 0x8B))
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class HealthStatusToBackgroundBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HealthStatus status)
            {
                return status switch
                {
                    HealthStatus.Good => new SolidColorBrush(Color.FromArgb(0x28, 0x00, 0xE6, 0x76)),
                    HealthStatus.Warning => new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xB3, 0x00)),
                    HealthStatus.Critical => new SolidColorBrush(Color.FromArgb(0x35, 0xFF, 0x3D, 0x00)),
                    HealthStatus.Failed => new SolidColorBrush(Color.FromArgb(0x35, 0xD5, 0x00, 0x00)),
                    HealthStatus.Unavailable => new SolidColorBrush(Color.FromArgb(0x20, 0x78, 0x90, 0x9C)),
                    _ => new SolidColorBrush(Color.FromArgb(0x20, 0x60, 0x7D, 0x8B))
                };
            }
            return new SolidColorBrush(Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool val = value is bool b && b;
            if (Invert) val = !val;
            return val ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : false;
        }
    }
}
