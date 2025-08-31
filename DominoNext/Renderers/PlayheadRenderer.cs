using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;

namespace DominoNext.Renderers
{
    /// <summary>
    /// 播放指示线渲染器 - 负责绘制移动的播放指示线
    /// 分离出来便于单独更新和动画效果
    /// </summary>
    public class PlayheadRenderer
    {
        /// <summary>
        /// 渲染播放指示线
        /// </summary>
        /// <param name="context">绘制上下文</param>
        /// <param name="viewModel">钢琴卷帘ViewModel</param>
        /// <param name="bounds">绘制边界</param>
        public void Render(DrawingContext context, PianoRollViewModel viewModel, Rect bounds)
        {
            if (viewModel == null) return;

            // 绘制播放位置指示线
            if (viewModel.PlaybackPosition >= 0)
            {
                var playheadPen = GetResourcePen("PlayheadBrush", "#FFFF0000", 2);
                var playheadX = viewModel.PlaybackPosition * viewModel.PixelsPerTick;
                
                // 只在可视区域内绘制
                if (playheadX >= bounds.Left && playheadX <= bounds.Right)
                {
                    context.DrawLine(playheadPen, new Point(playheadX, bounds.Top), new Point(playheadX, bounds.Bottom));
                }
            }

            // 绘制时间线指示线（如果不同于播放位置）
            if (viewModel.TimelinePosition != viewModel.PlaybackPosition)
            {
                var timelinePen = GetResourcePen("PlaybackIndicatorBrush", "#FFFF0000", 1.5, new DashStyle(new double[] { 4, 2 }, 0));
                var timelineX = viewModel.TimelinePosition;
                
                // 只在可视区域内绘制
                if (timelineX >= bounds.Left && timelineX <= bounds.Right)
                {
                    context.DrawLine(timelinePen, new Point(timelineX, bounds.Top), new Point(timelineX, bounds.Bottom));
                }
            }
        }

        /// <summary>
        /// 渲染播放指示线的影子效果（可选）
        /// </summary>
        /// <param name="context">绘制上下文</param>
        /// <param name="viewModel">钢琴卷帘ViewModel</param>
        /// <param name="bounds">绘制边界</param>
        public void RenderWithShadow(DrawingContext context, PianoRollViewModel viewModel, Rect bounds)
        {
            if (viewModel == null || viewModel.PlaybackPosition < 0) return;

            var playheadX = viewModel.PlaybackPosition * viewModel.PixelsPerTick;
            
            // 只在可视区域内绘制
            if (playheadX >= bounds.Left && playheadX <= bounds.Right)
            {
                // 绘制阴影
                var shadowPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)), 3);
                context.DrawLine(shadowPen, new Point(playheadX + 1, bounds.Top), new Point(playheadX + 1, bounds.Bottom));
                
                // 绘制主线
                var playheadPen = GetResourcePen("PlayheadBrush", "#FFFF0000", 2);
                context.DrawLine(playheadPen, new Point(playheadX, bounds.Top), new Point(playheadX, bounds.Bottom));
            }
        }

        /// <summary>
        /// 获取播放指示线在当前视口中的区域（用于优化重绘）
        /// </summary>
        /// <param name="viewModel">钢琴卷帘ViewModel</param>
        /// <param name="bounds">绘制边界</param>
        /// <returns>播放指示线占用的矩形区域</returns>
        public Rect GetPlayheadRect(PianoRollViewModel viewModel, Rect bounds)
        {
            if (viewModel?.PlaybackPosition < 0) return new Rect();

            var playheadX = viewModel.PlaybackPosition * viewModel.PixelsPerTick;
            var lineWidth = 3; // 包含阴影的宽度
            
            return new Rect(playheadX - lineWidth / 2, bounds.Top, lineWidth, bounds.Height);
        }

        private IBrush GetResourceBrush(string key, string fallbackHex)
        {
            try
            {
                if (Avalonia.Application.Current?.Resources.TryGetResource(key, null, out var obj) == true && obj is IBrush brush)
                    return brush;
            }
            catch { }

            return new SolidColorBrush(Color.Parse(fallbackHex));
        }

        private IPen GetResourcePen(string brushKey, string fallbackHex, double thickness = 1, DashStyle? dashStyle = null)
        {
            var brush = GetResourceBrush(brushKey, fallbackHex);
            var pen = new Pen(brush, thickness);
            if (dashStyle != null)
                pen.DashStyle = dashStyle;
            return pen;
        }
    }
}