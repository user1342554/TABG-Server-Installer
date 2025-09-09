using System;
using System.Globalization;
using System.Windows.Data;

namespace TabgInstaller.Gui.Converters
{
    public class FloatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue && float.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            {
                return (double)result;
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                return ((float)doubleValue).ToString(CultureInfo.InvariantCulture);
            }
            return "0";
        }
    }
}
