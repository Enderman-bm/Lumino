using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Lumino.Views.Converters
{
    /// <summary>
    /// ö��ֵת�ַ���ת����
    /// ��ö��ֵת��Ϊ���ַ�����ʾ��ʽ
    /// </summary>
    public class EnumToStringConverter : IValueConverter
    {
        /// <summary>
        /// ����ʵ��
        /// </summary>
        public static readonly EnumToStringConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return string.Empty;
            }

            // �����ö�����ͣ��������ַ�����ʾ
            if (value.GetType().IsEnum)
            {
                return value.ToString();
            }

            // �������ö�٣�ֱ��ת��Ϊ�ַ���
            return value.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string stringValue || !targetType.IsEnum)
            {
                return BindingOperations.DoNothing;
            }

            try
            {
                return Enum.Parse(targetType, stringValue, true);
            }
            catch
            {
                return BindingOperations.DoNothing;
            }
        }
    }
}