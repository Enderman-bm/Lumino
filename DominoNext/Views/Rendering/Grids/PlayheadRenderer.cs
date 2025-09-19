using System;
using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Utils;

namespace Lumino.Views.Rendering.Grids
{
    /// <summary>
    /// ����ͷ��Ⱦ�� - �����Ż��汾��֧�ֻ��ʸ���
    /// </summary>
    public class PlayheadRenderer
    {
        // ���ʻ��� - ���ò���ͷ��ػ���
        private IBrush? _cachedPlayheadBrush;
        private IPen? _cachedPlayheadPen;

        /// <summary>
        /// ��Ⱦ����ͷ - ͳһ��ڷ���
        /// </summary>
        public void RenderPlayhead(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, double scrollOffset)
        {
            var timelinePosition = viewModel.TimelinePosition;
            var playheadX = timelinePosition * viewModel.BaseQuarterNoteWidth - scrollOffset;

            // ����ͷ�ڿɼ�������ʱ����Ⱦ
            if (playheadX >= 0 && playheadX <= bounds.Width)
            {
                RenderPlayheadLine(context, playheadX, bounds.Height);
                
                // �ڶ�����Ⱦ����ͷָʾ������ѡ��
                if (bounds.Height > 20)
                {
                    RenderPlayheadIndicator(context, playheadX);
                }
            }
        }

        /// <summary>
        /// ��Ⱦ����ͷ����
        /// </summary>
        private void RenderPlayheadLine(DrawingContext context, double x, double canvasHeight)
        {
            var pen = GetCachedPlayheadPen();
            context.DrawLine(pen, new Point(x, 0), new Point(x, canvasHeight));
        }

        /// <summary>
        /// ��Ⱦ����ͷ����ָʾ���������Σ�
        /// </summary>
        private void RenderPlayheadIndicator(DrawingContext context, double x)
        {
            var indicatorHeight = 8;
            var indicatorWidth = 6;
            
            var triangle = new PathGeometry();
            var figure = new PathFigure
            {
                StartPoint = new Point(x, 0),
                IsClosed = true
            };
            
            figure.Segments!.Add(new LineSegment { Point = new Point(x - indicatorWidth / 2, indicatorHeight) });
            figure.Segments!.Add(new LineSegment { Point = new Point(x + indicatorWidth / 2, indicatorHeight) });
            
            triangle.Figures!.Add(figure);
            
            var brush = GetCachedPlayheadBrush();
            context.DrawGeometry(brush, null, triangle);
        }

        /// <summary>
        /// ��ȡ����Ĳ���ͷ����
        /// </summary>
        private IPen GetCachedPlayheadPen()
        {
            if (_cachedPlayheadPen == null)
            {
                var brush = GetCachedPlayheadBrush();
                _cachedPlayheadPen = new Pen(brush, 2);
            }
            return _cachedPlayheadPen;
        }

        /// <summary>
        /// ��ȡ����Ĳ���ͷ��ˢ
        /// </summary>
        private IBrush GetCachedPlayheadBrush()
        {
            return _cachedPlayheadBrush ??= RenderingUtils.GetResourceBrush("VelocityIndicatorBrush", "#FFFF0000");
        }

        /// <summary>
        /// ������ʻ��棨������ʱ���ã�
        /// </summary>
        public void ClearCache()
        {
            _cachedPlayheadBrush = null;
            _cachedPlayheadPen = null;
        }
    }
}