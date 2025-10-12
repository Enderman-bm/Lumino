using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Lumino.Views.Converters
{
    /// <summary>
    /// 对象相等性转换为前景色转换器
    /// 当值与参数相等时返回白色，否则返回默认色
    /// </summary>
    public class EqualsToForegroundConverter : IValueConverter
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        public static readonly EqualsToForegroundConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null && parameter == null)
            {
                return Brushes.White;
            }

            if (value == null || parameter == null)
            {
                return Brushes.Black; // 返回黑色作为默认值
            }

            // 使用Equals方法进行比较
            return value.Equals(parameter) 
                ? Brushes.White
                : Brushes.Black; // 返回黑色作为默认值
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
