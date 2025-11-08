using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Lumino.Views.Converters
{
    /// <summary>
    /// 将十六进制颜色字符串（例如 #RRGGBB 或 #AARRGGBB）转换为 Avalonia 的 Brush
    /// </summary>
    public class HexToBrushConverter : IValueConverter
    {
        public static readonly HexToBrushConverter Instance = new HexToBrushConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string s)
            {
                try
                {
                    var color = Color.Parse(s);
                    return new SolidColorBrush(color);
                }
                catch
                {
                    // 解析失败时返回透明画刷以避免崩溃
                    return Brushes.Transparent;
                }
            }

            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
