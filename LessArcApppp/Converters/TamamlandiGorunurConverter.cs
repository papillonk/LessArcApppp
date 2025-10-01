using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace LessArcApppp.Converters
{
    public class TamamlandiGorunurConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string durum)
            {
                return durum.Trim().ToLower() == "tamamlandı";
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
