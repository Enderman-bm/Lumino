using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Lumino.Views.Converters
{
    /// <summary>
    /// ��������ԱȽ�ת����
    /// �Ƚϰ�ֵ������Ƿ���ȣ����ز������
    /// </summary>
    public class ObjectEqualsConverter : IValueConverter
    {
        /// <summary>
        /// ����ʵ��
        /// </summary>
        public static readonly ObjectEqualsConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null && parameter == null)
            {
                return true;
            }

            if (value == null || parameter == null)
            {
                return false;
            }

            // ʹ��Equals�������бȽ�
            return value.Equals(parameter);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // ���ֵΪtrue���в��������ز���ֵ
            // ����RadioButton�ȳ����к�����
            if (value is bool boolValue && boolValue && parameter != null)
            {
                return parameter;
            }
            
            // �����������DoNothing�������쳣
            return BindingOperations.DoNothing;
        }
    }
}