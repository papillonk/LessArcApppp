using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace LessArcApppp.Converters
{
    public class DotColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int yuzde = (int)value;
            int siradakiNokta = int.Parse(parameter.ToString());

            if (yuzde >= (siradakiNokta + 1) * 20)
                return Color.FromArgb("#4CAF50"); // yeşilimsi
            else
                return Color.FromArgb("#E0E0E0"); // çok açık gri
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
