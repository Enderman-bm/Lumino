using System;
using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Utils;

namespace Lumino.Views.Rendering.Grids
{
    /// <summary>
    /// 播放头渲染器 - 性能优化版本，支持画笔复用
    /// </summary>
    public class PlayheadRenderer
    {
        // 画笔缓存 - 复用播放头相关画笔
        private IBrush? _cachedPlayheadBrush;
        private IPen? _cachedPlayheadPen;

        /// <summary>
        /// 渲染播放头 - 统一入口方法
        /// </summary>
        public void RenderPlayhead(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, double scrollOffset)
        {
            var timelinePosition = viewModel.TimelinePosition;
            var playheadX = timelinePosition * viewModel.BaseQuarterNoteWidth - scrollOffset;

            // 播放头在可见区域内时才渲染
            if (playheadX >= 0 && playheadX <= bounds.Width)
            {
                RenderPlayheadLine(context, playheadX, bounds.Height);
                
                // 在顶部渲染播放头指示器（可选）
                if (bounds.Height > 20)
                {
                    RenderPlayheadIndicator(context, playheadX);
                }
            }
        }

        /// <summary>
        /// 渲染播放头线条
        /// </summary>
        private void RenderPlayheadLine(DrawingContext context, double x, double canvasHeight)
        {
            var pen = GetCachedPlayheadPen();
            context.DrawLine(pen, new Point(x, 0), new Point(x, canvasHeight));
        }

        /// <summary>
        /// 渲染播放头顶部指示器（三角形）
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
        /// 获取缓存的播放头画笔
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
        /// 获取缓存的播放头画刷
        /// </summary>
        private IBrush GetCachedPlayheadBrush()
        {
            return _cachedPlayheadBrush ??= RenderingUtils.GetResourceBrush("VelocityIndicatorBrush", "#FFFF0000");
        }

        /// <summary>
        /// 清除画笔缓存（主题变更时调用）
        /// </summary>
        public void ClearCache()
        {
            _cachedPlayheadBrush = null;
            _cachedPlayheadPen = null;
        }
    }
}