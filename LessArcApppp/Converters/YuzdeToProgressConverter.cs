using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace LessArcApppp
{
    public class YuzdeToProgressConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int yuzde)
                return yuzde / 100.0;

            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
