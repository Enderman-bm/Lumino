using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Controls;

namespace Lumino.Views.Converters
{
    public class SelectedItemToTagConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ComboBoxItem item && item.Tag is int tag)
            {
                return tag;
            }
            return 44100; // default
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int tagValue)
            {
                return new ComboBoxItem
                {
                    Content = tagValue.ToString(),
                    Tag = tagValue
                };
            }
            return null;
        }
    }
}