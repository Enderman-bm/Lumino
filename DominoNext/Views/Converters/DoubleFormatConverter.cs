using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Lumino.Views.Converters
{
    /// <summary>
    /// ˫���ȸ�������ʽ��ת����
    /// ��doubleֵ��ʽ��Ϊָ����ʽ���ַ���
    /// </summary>
    public class DoubleFormatConverter : IValueConverter
    {
        /// <summary>
        /// ����ʵ��
        /// </summary>
        public static readonly DoubleFormatConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not double doubleValue)
            {
                return value?.ToString() ?? string.Empty;
            }

            var formatString = parameter as string ?? "F2";

            try
            {
                return doubleValue.ToString(formatString, culture);
            }
            catch
            {
                return doubleValue.ToString(CultureInfo.InvariantCulture);
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string stringValue)
            {
                return BindingOperations.DoNothing;
            }

            if (double.TryParse(stringValue, NumberStyles.Float, culture, out var result))
            {
                return result;
            }

            if (double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }

            return BindingOperations.DoNothing;
        }
    }
}