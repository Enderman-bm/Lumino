using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Controls;

namespace DominoNext.Converters
{
    /// <summary>
    /// 对象相等转换器
    /// </summary>
    public class ObjectEqualsConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Equals(value, parameter);
        }

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
    /// 枚举到字符串转换器
    /// </summary>
    public class EnumToStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.ToString();
        }

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
    /// 双精度浮点数格式化转换器
    /// </summary>
    public class DoubleFormatConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double doubleValue && parameter is string format)
            {
                return doubleValue.ToString(format, culture);
            }
            return value?.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue && double.TryParse(stringValue, NumberStyles.Float, culture, out double result))
            {
                return result;
            }
            return BindingOperations.DoNothing;
        }
    }

    /// <summary>
    /// 布尔值到GridLength转换器
    /// 用于根据布尔值动态控制Grid行或列的高度/宽度
    /// </summary>
    public class BooleanToGridLengthConverter : IValueConverter
    {
        public static readonly BooleanToGridLengthConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isVisible && parameter is string parameterString)
            {
                // 参数格式: "visibleValue,hiddenValue"
                // 例如: "1*,0" 表示可见时为1*，隐藏时为0
                var parts = parameterString.Split(',');
                if (parts.Length == 2)
                {
                    var visibleValue = parts[0].Trim();
                    var hiddenValue = parts[1].Trim();
                    
                    var targetValue = isVisible ? visibleValue : hiddenValue;
                    
                    // 解析GridLength
                    if (targetValue == "0")
                    {
                        return new GridLength(0);
                    }
                    else if (targetValue == "Auto")
                    {
                        return GridLength.Auto;
                    }
                    else if (targetValue.EndsWith("*"))
                    {
                        var starValue = targetValue.TrimEnd('*');
                        if (string.IsNullOrEmpty(starValue) || starValue == "1")
                        {
                            return new GridLength(1, GridUnitType.Star);
                        }
                        else if (double.TryParse(starValue, out double starMultiplier))
                        {
                            return new GridLength(starMultiplier, GridUnitType.Star);
                        }
                    }
                    else if (double.TryParse(targetValue, out double absoluteValue))
                    {
                        return new GridLength(absoluteValue);
                    }
                }
            }
            
            // 默认返回Auto
            return GridLength.Auto;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return BindingOperations.DoNothing;
        }
    }
}