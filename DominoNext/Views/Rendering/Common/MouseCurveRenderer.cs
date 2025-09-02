using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DominoNext.Views.Rendering.Common
{
    /// <summary>
    /// 鼠标曲线渲染器 - 通用的鼠标轨迹绘制组件
    /// 可用于力度编辑、音高编辑等需要鼠标绘制功能的场景
    /// </summary>
    public class MouseCurveRenderer
    {
        /// <summary>
        /// 曲线样式配置
        /// </summary>
        public class CurveStyle
        {
            public IBrush Brush { get; set; } = Brushes.Gray;
            public IPen Pen { get; set; } = new Pen(Brushes.Gray, 2);
            public bool ShowDots { get; set; } = true;
            public double DotSize { get; set; } = 3.0;
            public int MaxDotsToShow { get; set; } = 20;
            public bool UseSmoothCurve { get; set; } = true;
            public double[] DashPattern { get; set; } = new double[] { 3, 3 };
            public double BrushOpacity { get; set; } = 0.8;
        }

        // 资源画刷获取助手方法
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

        private IBrush CreateBrushWithOpacity(IBrush originalBrush, double opacity)
        {
            if (originalBrush is SolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                return new SolidColorBrush(color, opacity);
            }
            return originalBrush;
        }

        /// <summary>
        /// 绘制鼠标轨迹曲线
        /// </summary>
        /// <param name="context">绘制上下文</param>
        /// <param name="worldPoints">世界坐标系中的点集合</param>
        /// <param name="canvasBounds">画布边界</param>
        /// <param name="scrollOffset">滚动偏移量</param>
        /// <param name="style">曲线样式</param>
        public void DrawCurve(DrawingContext context, IEnumerable<Point> worldPoints, 
            Rect canvasBounds, double scrollOffset, CurveStyle style)
        {
            if (!worldPoints?.Any() == true) return;

            // 将世界坐标转换为屏幕坐标
            var screenPoints = ConvertToScreenPoints(worldPoints, scrollOffset);
            
            // 过滤屏幕外的点
            var visiblePoints = FilterVisiblePoints(screenPoints, canvasBounds).ToArray();
            
            if (visiblePoints.Length <= 1) return;

            // 创建样式化的画笔
            var pen = CreateStyledPen(style);

            if (style.UseSmoothCurve && visiblePoints.Length > 3)
            {
                DrawSmoothCurve(context, visiblePoints, pen);
            }
            else
            {
                DrawLinearCurve(context, visiblePoints, pen);
            }
        }

        /// <summary>
        /// 绘制曲线上的关键点
        /// </summary>
        /// <param name="context">绘制上下文</param>
        /// <param name="worldPoints">世界坐标系中的点集合</param>
        /// <param name="canvasBounds">画布边界</param>
        /// <param name="scrollOffset">滚动偏移量</param>
        /// <param name="style">样式配置</param>
        public void DrawDots(DrawingContext context, IEnumerable<Point> worldPoints, 
            Rect canvasBounds, double scrollOffset, CurveStyle style)
        {
            if (!worldPoints?.Any() == true || !style.ShowDots) return;

            var screenPoints = ConvertToScreenPoints(worldPoints, scrollOffset);
            var visiblePoints = FilterVisiblePoints(screenPoints, canvasBounds).ToArray();
            
            if (visiblePoints.Length == 0) return;

            var dotBrush = CreateBrushWithOpacity(style.Brush, style.BrushOpacity);
            var step = Math.Max(1, visiblePoints.Length / style.MaxDotsToShow);
            
            for (int i = 0; i < visiblePoints.Length; i += step)
            {
                var point = visiblePoints[i];
                var dotRect = new Rect(
                    point.X - style.DotSize / 2, 
                    point.Y - style.DotSize / 2, 
                    style.DotSize, 
                    style.DotSize);
                    
                context.DrawEllipse(dotBrush, null, dotRect);
            }
        }

        /// <summary>
        /// 绘制完整的鼠标轨迹（曲线 + 关键点）
        /// </summary>
        /// <param name="context">绘制上下文</param>
        /// <param name="worldPoints">世界坐标系中的点集合</param>
        /// <param name="canvasBounds">画布边界</param>
        /// <param name="scrollOffset">滚动偏移量</param>
        /// <param name="style">样式配置</param>
        public void DrawMouseTrail(DrawingContext context, IEnumerable<Point> worldPoints, 
            Rect canvasBounds, double scrollOffset, CurveStyle style)
        {
            DrawCurve(context, worldPoints, canvasBounds, scrollOffset, style);
            DrawDots(context, worldPoints, canvasBounds, scrollOffset, style);
        }

        /// <summary>
        /// 创建默认的编辑预览样式
        /// </summary>
        public CurveStyle CreateEditingPreviewStyle()
        {
            var brush = GetResourceBrush("VelocityEditingPreviewBrush", "#80FF5722");
            return new CurveStyle
            {
                Brush = brush,
                Pen = new Pen(brush, 2, new DashStyle(new double[] { 3, 3 }, 0)),
                ShowDots = true,
                DotSize = 3.0,
                MaxDotsToShow = 20,
                UseSmoothCurve = true,
                BrushOpacity = 0.8
            };
        }
        
        private IEnumerable<Point> ConvertToScreenPoints(IEnumerable<Point> worldPoints, double scrollOffset)
        {
            return worldPoints.Select(p => new Point(p.X - scrollOffset, p.Y));
        }

        private IEnumerable<Point> FilterVisiblePoints(IEnumerable<Point> screenPoints, Rect canvasBounds)
        {
            const double margin = 50; // 稍微扩展边界以确保连线的连续性
            return screenPoints.Where(p => p.X >= -margin && p.X <= canvasBounds.Width + margin);
        }

        private IPen CreateStyledPen(CurveStyle style)
        {
            if (style.DashPattern?.Length > 0)
            {
                return new Pen(style.Brush, style.Pen.Thickness, new DashStyle(style.DashPattern, 0));
            }
            return style.Pen;
        }

        private void DrawSmoothCurve(DrawingContext context, Point[] points, IPen pen)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(points[0], false);
                
                // 使用贝塞尔曲线创建平滑效果
                for (int i = 1; i < points.Length - 2; i += 2)
                {
                    var controlPoint1 = points[i];
                    var controlPoint2 = i + 1 < points.Length ? points[i + 1] : points[i];
                    var endPoint = i + 2 < points.Length ? points[i + 2] : points[^1];
                    
                    ctx.CubicBezierTo(controlPoint1, controlPoint2, endPoint);
                }
            }
            
            context.DrawGeometry(null, pen, geometry);
        }

        private void DrawLinearCurve(DrawingContext context, Point[] points, IPen pen)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(points[0], false);
                for (int i = 1; i < points.Length; i++)
                {
                    ctx.LineTo(points[i]);
                }
            }
            
            context.DrawGeometry(null, pen, geometry);
        }
    }
}