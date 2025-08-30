using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace DominoNext.Converters
{
    /// <summary>
    /// 项目用途：
    /// 本文件包含 MVVM 架构下用于数据绑定的 ValueConverter。
    /// 这些转换器在 View 层的 XAML 绑定中被引用，实现 ViewModel 与 View 间的数据格式转换，
    /// 以适配界面显示或交互需求，提升数据与界面的解耦性。
    /// </summary>
    
    /// <summary>
    /// 对象相等性转换器。
    /// 用于判断绑定值与参数是否相等，常用于控件选中状态等场景。
    /// 在 MVVM 中，帮助 View 根据 ViewModel 的数据状态进行显示。
    /// </summary>
    public class ObjectEqualsConverter : IValueConverter
    {
        /// <summary>
        /// 判断 value 与 parameter 是否相等。
        /// 用于将 ViewModel 的数据与控件参数进行比较，返回布尔值。
        /// </summary>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Equals(value, parameter);
        }

        /// <summary>
        /// 如果 value 为 true，则返回 parameter，否则返回 DoNothing。
        /// 用于将控件状态回传到 ViewModel。
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                return parameter;
            }
            return BindingOperations.DoNothing;
        }
    }

    /// <summary>
    /// 枚举到字符串转换器。
    /// 用于将枚举值转换为字符串，或将字符串转换为枚举值。
    /// 在 MVVM 中，便于枚举类型在界面上显示和编辑。
    /// </summary>
    public class EnumToStringConverter : IValueConverter
    {
        /// <summary>
        /// 将枚举值转换为字符串。
        /// 用于将 ViewModel 的枚举属性显示为字符串。
        /// </summary>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.ToString();
        }

        /// <summary>
        /// 将字符串转换为枚举值。
        /// 用于将界面输入的字符串回传为枚举类型到 ViewModel。
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue && targetType.IsEnum)
            {
                try
                {
                    return Enum.Parse(targetType, stringValue);
                }
                catch
                {
                    return BindingOperations.DoNothing;
                }
            }
            return BindingOperations.DoNothing;
        }
    }

    /// <summary>
    /// 双精度浮点数格式化转换器。
    /// 用于将 double 类型格式化为字符串，或将字符串解析为 double。
    /// 在 MVVM 中，便于数值在界面上以指定格式显示和编辑。
    /// </summary>
    public class DoubleFormatConverter : IValueConverter
    {
        /// <summary>
        /// 将 double 按指定格式转换为字符串。
        /// 用于将 ViewModel 的 double 属性格式化显示在界面。
        /// </summary>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double doubleValue && parameter is string format)
            {
                return doubleValue.ToString(format, culture);
            }
            return value?.ToString();
        }

        /// <summary>
        /// 将字符串解析为 double。
        /// 用于将界面输入的字符串回传为 double 类型到 ViewModel。
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue && double.TryParse(stringValue, NumberStyles.Float, culture, out double result))
            {
                return result;
            }
            return BindingOperations.DoNothing;
        }
    }
}