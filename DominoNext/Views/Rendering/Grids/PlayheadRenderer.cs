using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using System;

namespace DominoNext.Views.Rendering.Grids
{
    /// <summary>
    /// 播放头渲染器 - 负责绘制可移动的演奏指示线
    /// 独立于网格系统，便于播放状态的管理和渲染
    /// 优化策略：总是执行绘制，确保播放头始终可见
    /// </summary>
    public class PlayheadRenderer
    {
        /// <summary>
        /// 渲染播放头/时间轴（稳定版本 - 总是绘制）
        /// </summary>
        public void RenderPlayhead(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, double scrollOffset)
        {
            var timeToPixelScale = viewModel.TimeToPixelScale;
            var timelinePosition = viewModel.TimelinePosition;
            var zoom = viewModel.Zoom;

            var playheadX = viewModel.TimelinePosition * timeToPixelScale - scrollOffset;

            // 总是尝试绘制播放头，只有在完全不可见时才跳过
            if (playheadX >= -10 && playheadX <= bounds.Width + 10) // 增加一些容差范围
            {
                var pen = GetPlayheadPen();
                var startPoint = new Point(playheadX, 0);
                var endPoint = new Point(playheadX, bounds.Height);
                
                context.DrawLine(pen, startPoint, endPoint);
                
                // 可选：绘制播放头顶部的三角形指示器
                if (playheadX >= 0 && playheadX <= bounds.Width)
                {
                    RenderPlayheadIndicator(context, playheadX);
                }
            }
        }

        /// <summary>
        /// 渲染播放头顶部指示器（可选的三角形）
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
            
            var brush = GetPlayheadBrush();
            context.DrawGeometry(brush, null, triangle);
        }

        /// <summary>
        /// 获取播放头画笔
        /// </summary>
        private IPen GetPlayheadPen()
        {
            var brush = GetPlayheadBrush();
            return new Pen(brush, 2);
        }

        /// <summary>
        /// 获取播放头画刷
        /// </summary>
        private IBrush GetPlayheadBrush()
        {
            return GetResourceBrush("VelocityIndicatorBrush", "#FFFF0000");
        }

        /// <summary>
        /// 资源画刷获取助手方法
        /// </summary>
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