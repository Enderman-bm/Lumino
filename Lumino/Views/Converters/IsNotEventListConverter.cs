using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Lumino.ViewModels;

namespace Lumino.Views.Converters
{
    /// <summary>
    /// 检查视图类型是否不是事件列表的转换器
    /// 用于在非事件列表视图下显示音轨列表
    /// </summary>
    public class IsNotEventListConverter : IValueConverter
    {
        public static IsNotEventListConverter Instance { get; } = new IsNotEventListConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ViewType viewType)
            {
                return viewType != ViewType.EventList;
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
