using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace DominoNext.Views.Converters
{
    /// <summary>
    /// 双精度浮点数格式化转换器
    /// 将double值格式化为指定格式的字符串
    /// </summary>
    public class DoubleFormatConverter : IValueConverter
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        public static readonly DoubleFormatConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not double doubleValue)
            {
                return value?.ToString() ?? string.Empty;
            }

            var formatString = parameter as string ?? "F2";

            try
            {
                return doubleValue.ToString(formatString, culture);
            }
            catch
            {
                return doubleValue.ToString(CultureInfo.InvariantCulture);
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string stringValue)
            {
                return BindingOperations.DoNothing;
            }

            if (double.TryParse(stringValue, NumberStyles.Float, culture, out var result))
            {
                return result;
            }

            if (double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }

            return BindingOperations.DoNothing;
        }
    }
}