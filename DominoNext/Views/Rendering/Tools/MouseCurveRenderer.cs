using Avalonia;
using Avalonia.Media;
using Lumino.Views.Rendering.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lumino.Views.Rendering.Tools
{
    /// <summary>
    /// ���������Ⱦ�� - ͨ�õ����켣�������
    /// ���������ȱ༭�����߱༭����Ҫ�����ƹ��ܵĳ���
    /// </summary>
    public class MouseCurveRenderer
    {
        /// <summary>
        /// ������ʽ����
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

        /// <summary>
        /// �������켣����
        /// </summary>
        /// <param name="context">����������</param>
        /// <param name="worldPoints">��������ϵ�еĵ㼯��</param>
        /// <param name="canvasBounds">�����߽�</param>
        /// <param name="scrollOffset">����ƫ����</param>
        /// <param name="style">������ʽ</param>
        public void DrawCurve(DrawingContext context, IEnumerable<Point> worldPoints, 
            Rect canvasBounds, double scrollOffset, CurveStyle style)
        {
            if (!worldPoints?.Any() == true) return;

            // ����������ת��Ϊ��Ļ����
            var screenPoints = ConvertToScreenPoints(worldPoints, scrollOffset);
            
            // ������Ļ��ĵ�
            var visiblePoints = FilterVisiblePoints(screenPoints, canvasBounds).ToArray();
            
            if (visiblePoints.Length <= 1) return;

            // ������ʽ���Ļ���
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
        /// ���������ϵĹؼ���
        /// </summary>
        /// <param name="context">����������</param>
        /// <param name="worldPoints">��������ϵ�еĵ㼯��</param>
        /// <param name="canvasBounds">�����߽�</param>
        /// <param name="scrollOffset">����ƫ����</param>
        /// <param name="style">��ʽ����</param>
        public void DrawDots(DrawingContext context, IEnumerable<Point> worldPoints, 
            Rect canvasBounds, double scrollOffset, CurveStyle style)
        {
            if (!worldPoints?.Any() == true || !style.ShowDots) return;

            var screenPoints = ConvertToScreenPoints(worldPoints, scrollOffset);
            var visiblePoints = FilterVisiblePoints(screenPoints, canvasBounds).ToArray();
            
            if (visiblePoints.Length == 0) return;

            var dotBrush = RenderingUtils.CreateBrushWithOpacity(style.Brush, style.BrushOpacity);
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
        /// �������������켣������ + �ؼ��㣩
        /// </summary>
        /// <param name="context">����������</param>
        /// <param name="worldPoints">��������ϵ�еĵ㼯��</param>
        /// <param name="canvasBounds">�����߽�</param>
        /// <param name="scrollOffset">����ƫ����</param>
        /// <param name="style">��ʽ����</param>
        public void DrawMouseTrail(DrawingContext context, IEnumerable<Point> worldPoints, 
            Rect canvasBounds, double scrollOffset, CurveStyle style)
        {
            DrawCurve(context, worldPoints, canvasBounds, scrollOffset, style);
            DrawDots(context, worldPoints, canvasBounds, scrollOffset, style);
        }

        /// <summary>
        /// ����Ĭ�ϵı༭Ԥ����ʽ
        /// </summary>
        public CurveStyle CreateEditingPreviewStyle()
        {
            var brush = RenderingUtils.GetResourceBrush("VelocityEditingPreviewBrush", "#80FF5722");
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
            const double margin = 50; // ��΢��չ�߽���ȷ�����ߵ�������
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
                
                // ʹ�ñ��������ߴ���ƽ��Ч��
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