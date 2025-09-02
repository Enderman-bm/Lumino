using Avalonia;
using Avalonia.Media;
using DominoNext.Views.Rendering.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DominoNext.Views.Rendering.Events
{
    /// <summary>
    /// 音高曲线渲染器示例 - 展示如何复用MouseCurveRenderer
    /// 这是一个示例类，展示新的曲线渲染器的通用性
    /// </summary>
    public class ControllerCurveRenderer
    {
        private readonly MouseCurveRenderer _curveRenderer = new MouseCurveRenderer();

        /// <summary>
        /// 绘制音高编辑预览
        /// </summary>
        /// <param name="context">绘制上下文</param>
        /// <param name="pitchEditingPath">音高编辑路径</param>
        /// <param name="canvasBounds">画布边界</param>
        /// <param name="scrollOffset">滚动偏移量</param>
        public void DrawPitchEditingPreview(DrawingContext context, IEnumerable<Point> pitchEditingPath, 
            Rect canvasBounds, double scrollOffset = 0)
        {
            if (!pitchEditingPath?.Any() == true) return;

            // 创建音高编辑专用的样式
            var pitchStyle = CreatePitchEditingStyle();
            
            // 使用通用的曲线渲染器
            _curveRenderer.DrawMouseTrail(context, pitchEditingPath, canvasBounds, scrollOffset, pitchStyle);
        }

        /// <summary>
        /// 绘制音高弯曲曲线
        /// </summary>
        /// <param name="context">绘制上下文</param>
        /// <param name="bendCurve">弯音曲线</param>
        /// <param name="canvasBounds">画布边界</param>
        /// <param name="scrollOffset">滚动偏移量</param>
        public void DrawPitchBendCurve(DrawingContext context, IEnumerable<Point> bendCurve, 
            Rect canvasBounds, double scrollOffset = 0)
        {
            if (!bendCurve?.Any() == true) return;

            var bendStyle = CreatePitchBendStyle();
            
            // 弯音曲线通常不需要显示关键点
            _curveRenderer.DrawCurve(context, bendCurve, canvasBounds, scrollOffset, bendStyle);
        }

        /// <summary>
        /// 创建音高编辑样式
        /// </summary>
        private MouseCurveRenderer.CurveStyle CreatePitchEditingStyle()
        {
            var brush = GetResourceBrush("PitchEditingBrush", "#FF9C27B0"); // 紫色
            return new MouseCurveRenderer.CurveStyle
            {
                Brush = brush,
                Pen = new Pen(brush, 2, new DashStyle(new double[] { 5, 3 }, 0)),
                ShowDots = true,
                DotSize = 4.0,
                MaxDotsToShow = 15,
                UseSmoothCurve = true,
                BrushOpacity = 0.9
            };
        }

        /// <summary>
        /// 创建音高弯曲样式
        /// </summary>
        private MouseCurveRenderer.CurveStyle CreatePitchBendStyle()
        {
            var brush = GetResourceBrush("PitchBendBrush", "#FF3F51B5"); // 蓝色
            return new MouseCurveRenderer.CurveStyle
            {
                Brush = brush,
                Pen = new Pen(brush, 3),
                ShowDots = false, // 弯音曲线不显示点
                UseSmoothCurve = true,
                BrushOpacity = 0.8
            };
        }

        // 资源画刷获取助手方法 - 复用自VelocityBarRenderer的模式
        private IBrush GetResourceBrush(string key, string fallbackHex)
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
    }
}