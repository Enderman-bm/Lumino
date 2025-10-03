using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Lumino.Views.Converters
{
    /// <summary>
    /// 对象相等性比较转换器
    /// 比较绑定值与参数是否相等，返回布尔结果
    /// </summary>
    public class ObjectEqualsConverter : IValueConverter
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        public static readonly ObjectEqualsConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null && parameter == null)
            {
                return true;
            }

            if (value == null || parameter == null)
            {
                return false;
            }

            // 使用Equals方法进行比较
            return value.Equals(parameter);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // 如果值为true且有参数，返回参数值
            // 这在RadioButton等场景中很有用
            if (value is bool boolValue && boolValue && parameter != null)
            {
                return parameter;
            }
            
            // 其他情况返回DoNothing，避免异常
            return BindingOperations.DoNothing;
        }
    }
}