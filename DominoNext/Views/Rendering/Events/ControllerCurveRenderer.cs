using Avalonia;
using Avalonia.Media;
using DominoNext.Views.Rendering.Tools;
using DominoNext.Views.Rendering.Utils;
using DominoNext.ViewModels.Editor.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DominoNext.Views.Rendering.Events
{
    /// <summary>
    /// 控制器曲线渲染器 - 展示基于MouseCurveRenderer
    /// 支持力度、弯音和CC控制器的曲线渲染
    /// </summary>
    public class ControllerCurveRenderer
    {
        private readonly MouseCurveRenderer _curveRenderer = new MouseCurveRenderer();

        /// <summary>
        /// 根据事件类型绘制相应的曲线
        /// </summary>
        /// <param name="context">绘制上下文</param>
        /// <param name="eventType">事件类型</param>
        /// <param name="curveData">曲线数据</param>
        /// <param name="canvasBounds">画布边界</param>
        /// <param name="scrollOffset">滚动偏移量</param>
        /// <param name="ccNumber">CC控制器号（仅在事件类型为ControlChange时使用）</param>
        public void DrawEventCurve(DrawingContext context, EventType eventType, 
            IEnumerable<Point> curveData, Rect canvasBounds, double scrollOffset = 0, int ccNumber = 1)
        {
            if (!curveData?.Any() == true) return;

            MouseCurveRenderer.CurveStyle style = eventType switch
            {
                EventType.Velocity => CreateVelocityStyle(),
                EventType.PitchBend => CreatePitchBendStyle(),
                EventType.ControlChange => CreateControlChangeStyle(ccNumber),
                _ => CreateDefaultStyle()
            };

            _curveRenderer.DrawCurve(context, curveData, canvasBounds, scrollOffset, style);
        }

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
        /// 绘制CC控制器曲线
        /// </summary>
        /// <param name="context">绘制上下文</param>
        /// <param name="ccCurve">CC控制器曲线</param>
        /// <param name="ccNumber">CC控制器号</param>
        /// <param name="canvasBounds">画布边界</param>
        /// <param name="scrollOffset">滚动偏移量</param>
        public void DrawControlChangeCurve(DrawingContext context, IEnumerable<Point> ccCurve, 
            int ccNumber, Rect canvasBounds, double scrollOffset = 0)
        {
            if (!ccCurve?.Any() == true) return;

            var ccStyle = CreateControlChangeStyle(ccNumber);
            
            _curveRenderer.DrawCurve(context, ccCurve, canvasBounds, scrollOffset, ccStyle);
        }

        /// <summary>
        /// 创建力度样式
        /// </summary>
        private MouseCurveRenderer.CurveStyle CreateVelocityStyle()
        {
            var brush = RenderingUtils.GetResourceBrush("VelocityBrush", "#FF4CAF50"); // 绿色
            return new MouseCurveRenderer.CurveStyle
            {
                Brush = brush,
                Pen = new Pen(brush, 2),
                ShowDots = true,
                DotSize = 3.0,
                MaxDotsToShow = 20,
                UseSmoothCurve = false, // 力度通常使用直线连接
                BrushOpacity = 0.8
            };
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

        /// <summary>
        /// 创建CC控制器样式
        /// </summary>
        /// <param name="ccNumber">CC控制器号</param>
        private MouseCurveRenderer.CurveStyle CreateControlChangeStyle(int ccNumber)
        {
            // 根据CC号生成不同的颜色
            var colorHue = (ccNumber * 360 / 128) % 360;
            var color = ColorFromHsv(colorHue, 0.7, 0.8);
            var brush = new SolidColorBrush(color);
            
            return new MouseCurveRenderer.CurveStyle
            {
                Brush = brush,
                Pen = new Pen(brush, 2),
                ShowDots = true,
                DotSize = 3.0,
                MaxDotsToShow = 25,
                UseSmoothCurve = true, // CC控制器使用平滑曲线
                BrushOpacity = 0.8
            };
        }

        /// <summary>
        /// 创建默认样式
        /// </summary>
        private MouseCurveRenderer.CurveStyle CreateDefaultStyle()
        {
            var brush = RenderingUtils.GetResourceBrush("DefaultEventBrush", "#FF757575"); // 灰色
            return new MouseCurveRenderer.CurveStyle
            {
                Brush = brush,
                Pen = new Pen(brush, 2),
                ShowDots = true,
                DotSize = 3.0,
                MaxDotsToShow = 20,
                UseSmoothCurve = true,
                BrushOpacity = 0.7
            };
        }

        /// <summary>
        /// 从HSV颜色空间创建Color
        /// </summary>
        /// <param name="h">色相 (0-360)</param>
        /// <param name="s">饱和度 (0-1)</param>
        /// <param name="v">明度 (0-1)</param>
        /// <returns>Color对象</returns>
        private static Color ColorFromHsv(double h, double s, double v)
        {
            h = h % 360;
            var c = v * s;
            var x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            var m = v - c;
            
            double r = 0, g = 0, b = 0;
            
            if (h < 60)
            {
                r = c; g = x; b = 0;
            }
            else if (h < 120)
            {
                r = x; g = c; b = 0;
            }
            else if (h < 180)
            {
                r = 0; g = c; b = x;
            }
            else if (h < 240)
            {
                r = 0; g = x; b = c;
            }
            else if (h < 300)
            {
                r = x; g = 0; b = c;
            }
            else
            {
                r = c; g = 0; b = x;
            }
            
            return Color.FromRgb(
                (byte)((r + m) * 255),
                (byte)((g + m) * 255),
                (byte)((b + m) * 255)
            );
        }
    }
}