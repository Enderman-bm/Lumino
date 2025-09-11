using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DominoNext.ViewModels.Base
{
    /// <summary>
    /// 通用缓存管理器基类 - 提供高性能的缓存管理功能
    /// 解决NoteViewModel等类中重复的缓存管理代码
    /// </summary>
    /// <typeparam name="TKey">缓存键类型</typeparam>
    /// <typeparam name="TValue">缓存值类型</typeparam>
    public abstract class CacheManagerBase<TKey, TValue> where TKey : notnull
    {
        #region 常量定义
        protected const double ToleranceValue = 1e-10; // 浮点数比较容差
        protected static readonly TValue InvalidValue = GetInvalidValue();
        #endregion

        #region 私有字段
        private readonly Dictionary<TKey, TValue> _cache = new();
        private readonly Dictionary<TKey, object[]> _cacheParameters = new();
        #endregion

        #region 抽象方法
        /// <summary>
        /// 获取无效值标记
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
        /// 检查值是否为无效值
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
        /// 比较两个参数数组是否相等
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
        /// 比较两个参数值是否相等（支持浮点数容差比较）
        /// </summary>
        protected virtual bool AreParameterValuesEqual(object value1, object value2)
        {
            if (value1 == null && value2 == null) return true;
            if (value1 == null || value2 == null) return false;

            // 浮点数容差比较
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

        #region 公共方法
        /// <summary>
        /// 获取缓存值，如果缓存无效则计算新值
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="calculator">值计算函数</param>
        /// <param name="parameters">计算参数</param>
        /// <returns>缓存或计算的值</returns>
        public TValue GetOrCalculate(TKey key, Func<object[], TValue> calculator, params object[] parameters)
        {
            // 检查缓存是否有效
            if (_cache.TryGetValue(key, out var cachedValue) && 
                !IsInvalidValue(cachedValue) &&
                _cacheParameters.TryGetValue(key, out var cachedParams) &&
                AreParametersEqual(cachedParams, parameters))
            {
                return cachedValue;
            }

            // 计算新值
            var newValue = calculator(parameters);
            
            // 更新缓存
            _cache[key] = newValue;
            _cacheParameters[key] = (object[])parameters.Clone();
            
            return newValue;
        }

        /// <summary>
        /// 设置缓存值
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="value">值</param>
        /// <param name="parameters">参数</param>
        public void SetCache(TKey key, TValue value, params object[] parameters)
        {
            _cache[key] = value;
            _cacheParameters[key] = (object[])parameters.Clone();
        }

        /// <summary>
        /// 检查缓存是否有效
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="parameters">参数</param>
        /// <returns>缓存是否有效</returns>
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
        /// 失效指定键的缓存
        /// </summary>
        /// <param name="key">缓存键</param>
        public void InvalidateCache(TKey key)
        {
            _cache[key] = InvalidValue;
            _cacheParameters.Remove(key);
        }

        /// <summary>
        /// 失效所有缓存
        /// </summary>
        public void InvalidateAllCache()
        {
            _cache.Clear();
            _cacheParameters.Clear();
        }

        /// <summary>
        /// 获取缓存项数量
        /// </summary>
        public int CacheCount => _cache.Count;
        #endregion
    }

    /// <summary>
    /// 专门用于UI计算的缓存管理器
    /// 针对常见的UI计算场景（如位置、尺寸等）进行优化
    /// </summary>
    public class UiCalculationCacheManager : CacheManagerBase<string, double>
    {
        #region 常用缓存键常量
        public const string X_POSITION = "X";
        public const string Y_POSITION = "Y";
        public const string WIDTH = "Width";
        public const string HEIGHT = "Height";
        public const string SCREEN_X = "ScreenX";
        public const string SCREEN_Y = "ScreenY";
        public const string SCREEN_WIDTH = "ScreenWidth";
        public const string SCREEN_HEIGHT = "ScreenHeight";
        #endregion

        #region 便捷方法
        /// <summary>
        /// 获取或计算X坐标
        /// </summary>
        public double GetOrCalculateX(Func<double[], double> calculator, params double[] parameters)
        {
            return GetOrCalculate(X_POSITION, args => calculator(ConvertToDoubleArray(args)), ConvertToObjectArray(parameters));
        }

        /// <summary>
        /// 获取或计算Y坐标
        /// </summary>
        public double GetOrCalculateY(Func<double[], double> calculator, params double[] parameters)
        {
            return GetOrCalculate(Y_POSITION, args => calculator(ConvertToDoubleArray(args)), ConvertToObjectArray(parameters));
        }

        /// <summary>
        /// 获取或计算宽度
        /// </summary>
        public double GetOrCalculateWidth(Func<double[], double> calculator, params double[] parameters)
        {
            return GetOrCalculate(WIDTH, args => calculator(ConvertToDoubleArray(args)), ConvertToObjectArray(parameters));
        }

        /// <summary>
        /// 获取或计算高度
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