using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Lumino.Views.Converters
{
    /// <summary>
    /// ������ֵת��ΪGridLength��ת����
    /// ���ڸ��ݲ���״̬��̬�����л��еĸ߶�/����
    /// </summary>
    public class BooleanToGridLengthConverter : IValueConverter
    {
        /// <summary>
        /// ����ʵ��
        /// </summary>
        public static readonly BooleanToGridLengthConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not bool boolValue || parameter is not string parameterString)
            {
                return new GridLength(0);
            }

            // ������ʽ: "TrueValue,FalseValue"
            // ����: "Auto,0" �� "1*,0" �� "100,0"
            var parts = parameterString.Split(',');
            if (parts.Length != 2)
            {
                return new GridLength(0);
            }

            var targetValue = boolValue ? parts[0].Trim() : parts[1].Trim();

            // ����GridLength
            return ParseGridLength(targetValue);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // BooleanToGridLengthConverterͨ������Ҫ����ת��
            // ����DoNothing�����쳣
            return BindingOperations.DoNothing;
        }

        private static GridLength ParseGridLength(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new GridLength(0);
            }

            // ���� Auto
            if (value.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                return GridLength.Auto;
            }

            // �����Ǻţ�*����λ
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

            // ��������ֵ
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixelValue))
            {
                return new GridLength(pixelValue, GridUnitType.Pixel);
            }

            // Ĭ�Ϸ���0
            return new GridLength(0);
        }
    }
}