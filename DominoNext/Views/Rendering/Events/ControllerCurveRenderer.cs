using Avalonia;
using Avalonia.Media;
using DominoNext.Views.Rendering.Tools;
using DominoNext.Views.Rendering.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DominoNext.Views.Rendering.Events
{
    /// <summary>
    /// 控制器曲线渲染器 - 展示基于MouseCurveRenderer
    /// 这是一个示例类，展示新的曲线渲染器通用模式
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

            // 创建音高编辑专用样式
            var pitchStyle = CreatePitchEditingStyle();
            
            // 使用通用的曲线渲染器
            _curveRenderer.DrawMouseTrail(context, pitchEditingPath, canvasBounds, scrollOffset, pitchStyle);
        }

        /// <summary>
        /// 绘制弯音控制曲线
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
            var brush = RenderingUtils.GetResourceBrush("PitchEditingBrush", "#FF9C27B0"); // 紫色
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
        /// 创建弯音控制样式
        /// </summary>
        private MouseCurveRenderer.CurveStyle CreatePitchBendStyle()
        {
            var brush = RenderingUtils.GetResourceBrush("PitchBendBrush", "#FF3F51B5"); // 蓝色
            return new MouseCurveRenderer.CurveStyle
            {
                Brush = brush,
                Pen = new Pen(brush, 3),
                ShowDots = false, // 弯音曲线不显示点
                UseSmoothCurve = true,
                BrushOpacity = 0.8
            };
        }
    }
}