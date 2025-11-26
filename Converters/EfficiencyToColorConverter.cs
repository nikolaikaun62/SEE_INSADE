using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SEE_INSADE.Converters
{
    public class EfficiencyToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double efficiency)
            {
                return efficiency switch
                {
                    >= 90 => new SolidColorBrush(Color.FromRgb(76, 175, 80)),   // Green
                    >= 70 => new SolidColorBrush(Color.FromRgb(255, 193, 7)),   // Amber
                    _ => new SolidColorBrush(Color.FromRgb(244, 67, 54))        // Red
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}