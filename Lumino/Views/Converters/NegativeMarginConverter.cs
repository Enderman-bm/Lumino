using Avalonia.Data.Converters;
using System;

namespace Lumino.Views.Converters
{
    /// <summary>
    /// 将滚动位置值转换为负值的Margin，用于实现元素跟随滚动的效果
    /// </summary>
    public class NegativeMarginConverter : IValueConverter
    {
        public static NegativeMarginConverter Instance { get; } = new NegativeMarginConverter();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double scrollPosition)
            {
                // 创建一个左Margin为-scrollPosition的Thickness对象
                return new Avalonia.Thickness(-scrollPosition, 0, 0, 0);
            }
            return Avalonia.Thickness.Parse("0");
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}