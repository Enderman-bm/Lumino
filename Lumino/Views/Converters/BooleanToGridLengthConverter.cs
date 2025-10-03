using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Lumino.Views.Converters
{
    /// <summary>
    /// 将布尔值转换为GridLength的转换器
    /// 用于根据布尔状态动态设置行或列的高度/宽度
    /// </summary>
    public class BooleanToGridLengthConverter : IValueConverter
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        public static readonly BooleanToGridLengthConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not bool boolValue || parameter is not string parameterString)
            {
                return new GridLength(0);
            }

            // 参数格式: "TrueValue,FalseValue"
            // 例如: "Auto,0" 或 "1*,0" 或 "100,0"
            var parts = parameterString.Split(',');
            if (parts.Length != 2)
            {
                return new GridLength(0);
            }

            var targetValue = boolValue ? parts[0].Trim() : parts[1].Trim();

            // 解析GridLength
            return ParseGridLength(targetValue);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // BooleanToGridLengthConverter通常不需要反向转换
            // 返回DoNothing避免异常
            return BindingOperations.DoNothing;
        }

        private static GridLength ParseGridLength(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new GridLength(0);
            }

            // 处理 Auto
            if (value.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                return GridLength.Auto;
            }

            // 处理星号（*）单位
            if (value.EndsWith("*"))
            {
                var coefficient = value.TrimEnd('*');
                if (string.IsNullOrEmpty(coefficient))
                {
                    return new GridLength(1, GridUnitType.Star);
                }

                if (double.TryParse(coefficient, NumberStyles.Float, CultureInfo.InvariantCulture, out var starValue))
                {
                    return new GridLength(starValue, GridUnitType.Star);
                }
            }

            // 处理像素值
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixelValue))
            {
                return new GridLength(pixelValue, GridUnitType.Pixel);
            }

            // 默认返回0
            return new GridLength(0);
        }
    }
}