using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TabgInstaller.Gui.Converters
{
    public class ControlTypeVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string controlType && parameter is string expectedType)
            {
                return controlType == expectedType ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
