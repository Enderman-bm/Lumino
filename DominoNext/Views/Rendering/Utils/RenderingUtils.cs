using System;
using Avalonia;
using Avalonia.Media;

namespace DominoNext.Views.Rendering.Utils
{
    /// <summary>
    /// 渲染工具类 - 提供通用的资源获取和画刷操作
    /// 符合MVVM规范，纯静态工具方法，不依赖具体的ViewModel
    /// </summary>
    public static class RenderingUtils
    {
        /// <summary>
        /// 获取资源画刷，如果资源不存在则使用回退颜色
        /// </summary>
        /// <param name="key">资源键名</param>
        /// <param name="fallbackHex">回退的十六进制颜色值</param>
        /// <returns>画刷对象</returns>
        public static IBrush GetResourceBrush(string key, string fallbackHex)
        {
            try
            {
                if (Application.Current?.Resources.TryGetResource(key, null, out var obj) == true && obj is IBrush brush)
                    return brush;
            }
            catch { }

            try
            {
                return new SolidColorBrush(Color.Parse(fallbackHex));
            }
            catch
            {
                return Brushes.Transparent;
            }
        }

        /// <summary>
        /// 获取资源画笔，如果资源不存在则使用回退颜色
        /// </summary>
        /// <param name="brushKey">画刷资源键名</param>
        /// <param name="fallbackHex">回退的十六进制颜色值</param>
        /// <param name="thickness">画笔粗细，默认为1</param>
        /// <returns>画笔对象</returns>
        public static IPen GetResourcePen(string brushKey, string fallbackHex, double thickness = 1)
        {
            var brush = GetResourceBrush(brushKey, fallbackHex);
            return new Pen(brush, thickness);
        }

        /// <summary>
        /// 创建具有指定透明度的画刷
        /// </summary>
        /// <param name="originalBrush">原始画刷</param>
        /// <param name="opacity">透明度（0.0-1.0）</param>
        /// <returns>新的画刷对象</returns>
        public static IBrush CreateBrushWithOpacity(IBrush originalBrush, double opacity)
        {
            if (originalBrush is SolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                return new SolidColorBrush(color, opacity);
            }
            return originalBrush;
        }

        /// <summary>
        /// 创建具有指定透明度的纯色画刷
        /// </summary>
        /// <param name="colorHex">十六进制颜色值</param>
        /// <param name="opacity">透明度（0.0-1.0）</param>
        /// <returns>新的画刷对象</returns>
        public static IBrush CreateBrushWithOpacity(string colorHex, double opacity)
        {
            try
            {
                var color = Color.Parse(colorHex);
                return new SolidColorBrush(color, opacity);
            }
            catch
            {
                return Brushes.Transparent;
            }
        }
    }
}