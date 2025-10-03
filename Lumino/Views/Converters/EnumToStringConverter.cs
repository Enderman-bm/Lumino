using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Lumino.Views.Converters
{
    /// <summary>
    /// 枚举值转字符串转换器
    /// 将枚举值转换为其字符串表示形式
    /// </summary>
    public class EnumToStringConverter : IValueConverter
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        public static readonly EnumToStringConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return string.Empty;
            }

            // 如果是枚举类型，返回其字符串表示
            if (value.GetType().IsEnum)
            {
                return value.ToString();
            }

            // 如果不是枚举，直接转换为字符串
            return value.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string stringValue || !targetType.IsEnum)
            {
                return BindingOperations.DoNothing;
            }

            try
            {
                return Enum.Parse(targetType, stringValue, true);
            }
            catch
            {
                return BindingOperations.DoNothing;
            }
        }
    }
}