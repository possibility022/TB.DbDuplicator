using System;
using System.Globalization;
using System.Windows.Data;

namespace DatabaseCopier.Converters
{
    public class SecoundsToTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var secounds = (int)value;
            int min = 0;
            int hours = 0;

            while(secounds >= 60)
            {
                min += 1;
                secounds -= 60;
            }

            while(min >= 60)
            {
                hours += 1;
                min -= 60;
            }

            return $"{hours}:{min}:{secounds}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
