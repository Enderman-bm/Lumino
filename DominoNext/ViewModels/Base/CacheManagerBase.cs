using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Lumino.ViewModels.Base
{
    /// <summary>
    /// ͨ�û������������ - �ṩ�����ܵĻ����������
    /// ���NoteViewModel�������ظ��Ļ����������
    /// </summary>
    /// <typeparam name="TKey">���������</typeparam>
    /// <typeparam name="TValue">����ֵ����</typeparam>
    public abstract class CacheManagerBase<TKey, TValue> where TKey : notnull
    {
        #region ��������
        protected const double ToleranceValue = 1e-10; // �������Ƚ��ݲ�
        protected static readonly TValue InvalidValue = GetInvalidValue();
        #endregion

        #region ˽���ֶ�
        private readonly Dictionary<TKey, TValue> _cache = new();
        private readonly Dictionary<TKey, object[]> _cacheParameters = new();
        #endregion

        #region ���󷽷�
        /// <summary>
        /// ��ȡ��Чֵ���
        /// </summary>
        protected static TValue GetInvalidValue()
        {
            if (typeof(TValue) == typeof(double))
            {
                return (TValue)(object)double.NaN;
            }
            if (typeof(TValue) == typeof(float))
            {
                return (TValue)(object)float.NaN;
            }
            return default(TValue)!;
        }

        /// <summary>
        /// ���ֵ�Ƿ�Ϊ��Чֵ
        /// </summary>
        protected virtual bool IsInvalidValue(TValue value)
        {
            if (value == null) return true;
            
            if (typeof(TValue) == typeof(double))
            {
                return double.IsNaN((double)(object)value);
            }
            if (typeof(TValue) == typeof(float))
            {
                return float.IsNaN((float)(object)value);
            }
            
            return EqualityComparer<TValue>.Default.Equals(value, InvalidValue);
        }

        /// <summary>
        /// �Ƚ��������������Ƿ����
        /// </summary>
        protected virtual bool AreParametersEqual(object[] params1, object[] params2)
        {
            if (params1.Length != params2.Length) return false;
            
            for (int i = 0; i < params1.Length; i++)
            {
                if (!AreParameterValuesEqual(params1[i], params2[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// �Ƚ���������ֵ�Ƿ���ȣ�֧�ָ������ݲ�Ƚϣ�
        /// </summary>
        protected virtual bool AreParameterValuesEqual(object value1, object value2)
        {
            if (value1 == null && value2 == null) return true;
            if (value1 == null || value2 == null) return false;

            // �������ݲ�Ƚ�
            if (value1 is double d1 && value2 is double d2)
            {
                return Math.Abs(d1 - d2) < ToleranceValue;
            }
            if (value1 is float f1 && value2 is float f2)
            {
                return Math.Abs(f1 - f2) < ToleranceValue;
            }

            return value1.Equals(value2);
        }
        #endregion

        #region ��������
        /// <summary>
        /// ��ȡ����ֵ�����������Ч�������ֵ
        /// </summary>
        /// <param name="key">�����</param>
        /// <param name="calculator">ֵ���㺯��</param>
        /// <param name="parameters">�������</param>
        /// <returns>���������ֵ</returns>
        public TValue GetOrCalculate(TKey key, Func<object[], TValue> calculator, params object[] parameters)
        {
            // ��黺���Ƿ���Ч
            if (_cache.TryGetValue(key, out var cachedValue) && 
                !IsInvalidValue(cachedValue) &&
                _cacheParameters.TryGetValue(key, out var cachedParams) &&
                AreParametersEqual(cachedParams, parameters))
            {
                return cachedValue;
            }

            // ������ֵ
            var newValue = calculator(parameters);
            
            // ���»���
            _cache[key] = newValue;
            _cacheParameters[key] = (object[])parameters.Clone();
            
            return newValue;
        }

        /// <summary>
        /// ���û���ֵ
        /// </summary>
        /// <param name="key">�����</param>
        /// <param name="value">ֵ</param>
        /// <param name="parameters">����</param>
        public void SetCache(TKey key, TValue value, params object[] parameters)
        {
            _cache[key] = value;
            _cacheParameters[key] = (object[])parameters.Clone();
        }

        /// <summary>
        /// ��黺���Ƿ���Ч
        /// </summary>
        /// <param name="key">�����</param>
        /// <param name="parameters">����</param>
        /// <returns>�����Ƿ���Ч</returns>
        public bool IsCacheValid(TKey key, params object[] parameters)
        {
            if (!_cache.TryGetValue(key, out var cachedValue) || IsInvalidValue(cachedValue))
            {
                return false;
            }

            if (!_cacheParameters.TryGetValue(key, out var cachedParams))
            {
                return false;
            }

            return AreParametersEqual(cachedParams, parameters);
        }

        /// <summary>
        /// ʧЧָ�����Ļ���
        /// </summary>
        /// <param name="key">�����</param>
        public void InvalidateCache(TKey key)
        {
            _cache[key] = InvalidValue;
            _cacheParameters.Remove(key);
        }

        /// <summary>
        /// ʧЧ���л���
        /// </summary>
        public void InvalidateAllCache()
        {
            _cache.Clear();
            _cacheParameters.Clear();
        }

        /// <summary>
        /// ��ȡ����������
        /// </summary>
        public int CacheCount => _cache.Count;
        #endregion
    }

    /// <summary>
    /// ר������UI����Ļ��������
    /// ��Գ�����UI���㳡������λ�á��ߴ�ȣ������Ż�
    /// </summary>
    public class UiCalculationCacheManager : CacheManagerBase<string, double>
    {
        #region ���û��������
        public const string X_POSITION = "X";
        public const string Y_POSITION = "Y";
        public const string WIDTH = "Width";
        public const string HEIGHT = "Height";
        public const string SCREEN_X = "ScreenX";
        public const string SCREEN_Y = "ScreenY";
        public const string SCREEN_WIDTH = "ScreenWidth";
        public const string SCREEN_HEIGHT = "ScreenHeight";
        #endregion

        #region ��ݷ���
        /// <summary>
        /// ��ȡ�����X����
        /// </summary>
        public double GetOrCalculateX(Func<double[], double> calculator, params double[] parameters)
        {
            return GetOrCalculate(X_POSITION, args => calculator(ConvertToDoubleArray(args)), ConvertToObjectArray(parameters));
        }

        /// <summary>
        /// ��ȡ�����Y����
        /// </summary>
        public double GetOrCalculateY(Func<double[], double> calculator, params double[] parameters)
        {
            return GetOrCalculate(Y_POSITION, args => calculator(ConvertToDoubleArray(args)), ConvertToObjectArray(parameters));
        }

        /// <summary>
        /// ��ȡ��������
        /// </summary>
        public double GetOrCalculateWidth(Func<double[], double> calculator, params double[] parameters)
        {
            return GetOrCalculate(WIDTH, args => calculator(ConvertToDoubleArray(args)), ConvertToObjectArray(parameters));
        }

        /// <summary>
        /// ��ȡ�����߶�
        /// </summary>
        public double GetOrCalculateHeight(Func<double[], double> calculator, params double[] parameters)
        {
            return GetOrCalculate(HEIGHT, args => calculator(ConvertToDoubleArray(args)), ConvertToObjectArray(parameters));
        }

        private static double[] ConvertToDoubleArray(object[] args)
        {
            var result = new double[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                result[i] = Convert.ToDouble(args[i]);
            }
            return result;
        }

        private static object[] ConvertToObjectArray(double[] args)
        {
            var result = new object[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                result[i] = args[i];
            }
            return result;
        }
        #endregion
    }
}