using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Lumino.Views.Converters
{
    /// <summary>
    /// 将布尔值转换为字符串的转换器
    /// </summary>
    public class BooleanToStringConverter : IValueConverter
    {
        public static readonly BooleanToStringConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                if (parameter is string paramString && !string.IsNullOrEmpty(paramString))
                {
                    var parts = paramString.Split('|');
                    if (parts.Length >= 2)
                    {
                        return boolValue ? parts[0] : parts[1];
                    }
                }
                return boolValue ? "True" : "False";
            }
            return "False";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                if (parameter is string paramString && !string.IsNullOrEmpty(paramString))
                {
                    var parts = paramString.Split('|');
                    if (parts.Length >= 2)
                    {
                        return stringValue == parts[0];
                    }
                }
                return bool.TryParse(stringValue, out var result) ? result : false;
            }
            return false;
        }
    }
}