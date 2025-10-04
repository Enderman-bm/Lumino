using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Lumino.ViewModels;

namespace Lumino.Views.Converters
{
    /// <summary>
    /// 检查视图类型是否不是总轨视图的转换器
    /// </summary>
    public class IsNotTrackOverviewConverter : IValueConverter
    {
        public static IsNotTrackOverviewConverter Instance { get; } = new IsNotTrackOverviewConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ViewType viewType)
            {
                return viewType != ViewType.TrackOverview;
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}