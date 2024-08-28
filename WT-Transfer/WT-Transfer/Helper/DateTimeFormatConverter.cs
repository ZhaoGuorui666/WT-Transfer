using Microsoft.UI.Xaml.Data;
using System;
using System.Globalization;

namespace WT_Transfer.Helper
{
    public class DateTimeFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string dateString && DateTime.TryParse(dateString, out DateTime dateTime))
            {
                return dateTime.ToString("MM/dd/yyyy hh:mm:ss tt", CultureInfo.InvariantCulture);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // 如果你不需要双向绑定，可以不实现这个方法
            throw new NotImplementedException();
        }
    }
}
