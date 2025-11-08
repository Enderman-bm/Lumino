using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace Lumino.Views.Rendering.Utils
{
    /// <summary>
    /// 渲染工具类 - 提供通用的资源获取和刷新方法
    /// 遵循MVVM规范，不直接访问ViewModel
    /// </summary>
    public static class RenderingUtils
    {
        // Event raised when brush cache is cleared so renderers can invalidate their own caches.
        public static event Action? BrushCacheCleared;

        // 画刷缓存 - 提高性能，避免重复创建相同画刷
        private static readonly Dictionary<string, IBrush> _brushCache = new Dictionary<string, IBrush>();
        
        /// <summary>
        /// 获取资源画刷，如果资源不存在则使用后备十六进制颜色值
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="fallbackHex">后备十六进制颜色值</param>
        /// <returns>画刷对象</returns>
        public static IBrush GetResourceBrush(string key, string fallbackHex)
        {
            try
            {
                // 首先尝试从缓存获取
                if (_brushCache.TryGetValue(key, out IBrush? cachedBrush) && cachedBrush != null)
                {
                    return cachedBrush;
                }
                
                if (Application.Current?.Resources.TryGetResource(key, null, out var obj) == true && obj is IBrush brush)
                {
                    // 缓存找到的资源画刷
                    _brushCache[key] = brush;
                    return brush;
                }
            }
            catch { }

            try
            {
                var brush = new SolidColorBrush(Color.Parse(fallbackHex));
                // 缓存后备画刷
                _brushCache[key] = brush;
                return brush;
            }
            catch
            {
                return Brushes.Transparent;
            }
        }

        /// <summary>
        /// 获取资源画笔，如果资源不存在则使用后备颜色
        /// </summary>
        /// <param name="brushKey">画刷资源键</param>
        /// <param name="fallbackHex">后备十六进制颜色值</param>
        /// <param name="thickness">画笔粗细，默认为1</param>
        /// <returns>画笔对象</returns>
        public static IPen GetResourcePen(string brushKey, string fallbackHex, double thickness = 1)
        {
            var cacheKey = $"{brushKey}_{fallbackHex}_{thickness}";
            
            // 尝试从缓存获取
            if (_brushCache.TryGetValue(cacheKey, out IBrush? cachedBrush) && cachedBrush is ISolidColorBrush solidColorBrush)
            {
                return new Pen(solidColorBrush, thickness);
            }
            
            var brush = GetResourceBrush(brushKey, fallbackHex);
            var pen = new Pen(brush, thickness);
            
            // 缓存画刷以便后续使用
            _brushCache[cacheKey] = brush;
            return pen;
        }

        /// <summary>
        /// 创建指定透明度的画刷
        /// </summary>
        /// <param name="originalBrush">原始画刷</param>
        /// <param name="opacity">透明度，0.0-1.0之间</param>
        /// <returns>新的画刷对象</returns>
        public static IBrush CreateBrushWithOpacity(IBrush originalBrush, double opacity)
        {
            if (originalBrush is SolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                var cacheKey = $"color_{color}_{opacity}";
                
                // 尝试从缓存获取
                if (_brushCache.TryGetValue(cacheKey, out IBrush? cachedBrush) && cachedBrush != null)
                {
                    return cachedBrush;
                }
                
                var newBrush = new SolidColorBrush(color, opacity);
                // 缓存新创建的画刷
                _brushCache[cacheKey] = newBrush;
                return newBrush;
            }
            return originalBrush;
        }

        /// <summary>
        /// 创建指定透明度的颜色画刷
        /// </summary>
        /// <param name="colorHex">十六进制颜色值</param>
        /// <param name="opacity">透明度，0.0-1.0之间</param>
        /// <returns>新的画刷对象</returns>
        public static IBrush CreateBrushWithOpacity(string colorHex, double opacity)
        {
            try
            {
                var cacheKey = $"hex_{colorHex}_{opacity}";
                
                // 尝试从缓存获取
                if (_brushCache.TryGetValue(cacheKey, out IBrush? cachedBrush) && cachedBrush != null)
                {
                    return cachedBrush;
                }
                
                var color = Color.Parse(colorHex);
                var brush = new SolidColorBrush(color, opacity);
                // 缓存新创建的画刷
                _brushCache[cacheKey] = brush;
                return brush;
            }
            catch
            {
                return Brushes.Transparent;
            }
        }
        
        /// <summary>
        /// 清除画刷缓存
        /// </summary>
        public static void ClearBrushCache()
        {
            _brushCache.Clear();
            try { BrushCacheCleared?.Invoke(); } catch { }
        }
    }
}