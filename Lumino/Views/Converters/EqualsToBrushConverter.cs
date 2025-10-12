using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Lumino.Views.Converters
{
    /// <summary>
    /// 对象相等性转换为画刷转换器
    /// 当值与参数相等时返回高亮色，否则返回透明色
    /// </summary>
    public class EqualsToBrushConverter : IValueConverter
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        public static readonly EqualsToBrushConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null && parameter == null)
            {
                return new SolidColorBrush(Color.Parse("#007ACC"));
            }

            if (value == null || parameter == null)
            {
                return Brushes.Transparent;
            }

            // 使用Equals方法进行比较
            return value.Equals(parameter) 
                ? new SolidColorBrush(Color.Parse("#007ACC"))
                : Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
