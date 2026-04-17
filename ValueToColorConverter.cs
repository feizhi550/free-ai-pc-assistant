using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AIPCAssistant
{
    public class ValueToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is float floatValue)
            {
                if (floatValue > 90)
                {
                    return new SolidColorBrush(Color.FromRgb(255, 59, 48)); // 红色
                }
                else if (floatValue > 70)
                {
                    return new SolidColorBrush(Color.FromRgb(255, 149, 0)); // 橙色
                }
                else if (floatValue > 50)
                {
                    return new SolidColorBrush(Color.FromRgb(255, 204, 0)); // 黄色
                }
                else
                {
                    return new SolidColorBrush(Color.FromRgb(52, 199, 89)); // 绿色
                }
            }
            return new SolidColorBrush(Color.FromRgb(102, 102, 102)); // 默认灰色
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}