using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Lumino.Views.Converters
{
    /// <summary>
    /// 多值对象相等性比较转换器
    /// 比较两个绑定值是否相等，返回布尔结果
    /// 用于 RadioButton 的 IsChecked 属性与动态 ConverterParameter 场景
    /// </summary>
    public class MultiObjectEqualsConverter : IMultiValueConverter
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        public static readonly MultiObjectEqualsConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 2)
            {
                return false;
            }

            var value1 = values[0];  // 例如: SelectedLanguageCode
            var value2 = values[1];  // 例如: item.Code

            if (value1 == null && value2 == null)
            {
                return true;
            }

            if (value1 == null || value2 == null)
            {
                return false;
            }

            // 使用Equals方法进行比较
            return value1.Equals(value2);
        }
    }
}
