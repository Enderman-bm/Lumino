using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace DominoNext.Converters
{
    /// <summary>
    /// ��Ŀ��;��
    /// ���ļ����� MVVM �ܹ����������ݰ󶨵� ValueConverter��
    /// ��Щת������ View ��� XAML ���б����ã�ʵ�� ViewModel �� View ������ݸ�ʽת����
    /// �����������ʾ�򽻻������������������Ľ����ԡ�
    /// </summary>
    
    /// <summary>
    /// ���������ת������
    /// �����жϰ�ֵ������Ƿ���ȣ������ڿؼ�ѡ��״̬�ȳ�����
    /// �� MVVM �У����� View ���� ViewModel ������״̬������ʾ��
    /// </summary>
    public class ObjectEqualsConverter : IValueConverter
    {
        /// <summary>
        /// �ж� value �� parameter �Ƿ���ȡ�
        /// ���ڽ� ViewModel ��������ؼ��������бȽϣ����ز���ֵ��
        /// </summary>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Equals(value, parameter);
        }

        /// <summary>
        /// ��� value Ϊ true���򷵻� parameter�����򷵻� DoNothing��
        /// ���ڽ��ؼ�״̬�ش��� ViewModel��
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                return parameter;
            }
            return BindingOperations.DoNothing;
        }
    }

    /// <summary>
    /// ö�ٵ��ַ���ת������
    /// ���ڽ�ö��ֵת��Ϊ�ַ��������ַ���ת��Ϊö��ֵ��
    /// �� MVVM �У�����ö�������ڽ�������ʾ�ͱ༭��
    /// </summary>
    public class EnumToStringConverter : IValueConverter
    {
        /// <summary>
        /// ��ö��ֵת��Ϊ�ַ�����
        /// ���ڽ� ViewModel ��ö��������ʾΪ�ַ�����
        /// </summary>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.ToString();
        }

        /// <summary>
        /// ���ַ���ת��Ϊö��ֵ��
        /// ���ڽ�����������ַ����ش�Ϊö�����͵� ViewModel��
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue && targetType.IsEnum)
            {
                try
                {
                    return Enum.Parse(targetType, stringValue);
                }
                catch
                {
                    return BindingOperations.DoNothing;
                }
            }
            return BindingOperations.DoNothing;
        }
    }

    /// <summary>
    /// ˫���ȸ�������ʽ��ת������
    /// ���ڽ� double ���͸�ʽ��Ϊ�ַ��������ַ�������Ϊ double��
    /// �� MVVM �У�������ֵ�ڽ�������ָ����ʽ��ʾ�ͱ༭��
    /// </summary>
    public class DoubleFormatConverter : IValueConverter
    {
        /// <summary>
        /// �� double ��ָ����ʽת��Ϊ�ַ�����
        /// ���ڽ� ViewModel �� double ���Ը�ʽ����ʾ�ڽ��档
        /// </summary>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double doubleValue && parameter is string format)
            {
                return doubleValue.ToString(format, culture);
            }
            return value?.ToString();
        }

        /// <summary>
        /// ���ַ�������Ϊ double��
        /// ���ڽ�����������ַ����ش�Ϊ double ���͵� ViewModel��
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue && double.TryParse(stringValue, NumberStyles.Float, culture, out double result))
            {
                return result;
            }
            return BindingOperations.DoNothing;
        }
    }

    /// <summary>
    /// 整数到索引的转换器
    /// 用于将整数转换为ComboBox的SelectedIndex，支持反向转换
    /// 适用于SubdivisionLevel等整数绑定
    /// </summary>
    public class IntToIndexConverter : IValueConverter
    {
        /// <summary>
        /// 将整数值转换为索引
        /// 4 -> 0, 8 -> 1, 16 -> 2
        /// </summary>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue switch
                {
                    4 => 0,
                    8 => 1,
                    16 => 2,
                    _ => 0
                };
            }
            return 0;
        }

        /// <summary>
        /// 将索引转换回整数值
        /// 0 -> 4, 1 -> 8, 2 -> 16
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return index switch
                {
                    0 => 4,
                    1 => 8,
                    2 => 16,
                    _ => 4
                };
            }
            return 4;
        }
    }
}