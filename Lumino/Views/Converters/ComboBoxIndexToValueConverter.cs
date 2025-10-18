using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Lumino.Views.Converters
{
    /// <summary>
    /// ComboBox 索引到值转换器
    /// 将 ComboBox 的索引转换为对应的值
    /// </summary>
    public class ComboBoxIndexToValueConverter : IValueConverter
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        public static readonly ComboBoxIndexToValueConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                // 将值转换为索引
                if (intValue == 44100) return 0;
                if (intValue == 22050) return 1;
                if (intValue == 11025) return 2;
                // 默认返回第一个选项的索引
                return 0;
            }

            return 0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                // 将索引转换回值
                if (index == 0) return 44100;
                if (index == 1) return 22050;
                if (index == 2) return 11025;
                // 默认返回第一个选项的值
                return 44100;
            }

            return 44100;
        }
    }
}