using System;
using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;

namespace DominoNext.Views.Controls.Editing.Rendering
{
    /// <summary>
    /// 垂直网格线渲染器 - 负责绘制基于拍号的小节线和音符线
    /// 支持不同拍号（4/4拍、3/4拍、8/4拍等）的动态调整
    /// 优化策略：内部缓存计算结果，但总是执行绘制以确保稳定性
    /// </summary>
    public class VerticalGridRenderer
    {
        // 缓存上次渲染的参数，用于优化计算
        private double _lastHorizontalScrollOffset = double.NaN;
        private double _lastZoom = double.NaN;
        private double _lastViewportWidth = double.NaN;
        private double _lastPixelsPerTick = double.NaN;

        // 缓存计算结果
        private double _cachedVisibleStartTime;
        private double _cachedVisibleEndTime;
        private bool _cacheValid = false;

        /// <summary>
        /// 渲染垂直网格线（稳定版本 - 总是绘制，内部优化计算）
        /// </summary>
        public void RenderVerticalGrid(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, double scrollOffset)
        {
            var zoom = viewModel.Zoom;
            var pixelsPerTick = viewModel.PixelsPerTick;

            // 检查是否需要重新计算可见时间范围
            bool needsRecalculation = !_cacheValid ||
                !AreEqual(_lastHorizontalScrollOffset, scrollOffset) ||
                !AreEqual(_lastZoom, zoom) ||
                !AreEqual(_lastViewportWidth, bounds.Width) ||
                !AreEqual(_lastPixelsPerTick, pixelsPerTick);

            double visibleStartTime, visibleEndTime;

            if (needsRecalculation)
            {
                // 重新计算可见时间范围
                visibleStartTime = scrollOffset / (pixelsPerTick * zoom);
                visibleEndTime = (scrollOffset + bounds.Width) / (pixelsPerTick * zoom);

                // 更新缓存
                _cachedVisibleStartTime = visibleStartTime;
                _cachedVisibleEndTime = visibleEndTime;
                _lastHorizontalScrollOffset = scrollOffset;
                _lastZoom = zoom;
                _lastViewportWidth = bounds.Width;
                _lastPixelsPerTick = pixelsPerTick;
                _cacheValid = true;
            }
            else
            {
                // 使用缓存的值
                visibleStartTime = _cachedVisibleStartTime;
                visibleEndTime = _cachedVisibleEndTime;
            }

            var totalKeyHeight = 128 * viewModel.KeyHeight;
            var startY = 0;
            var endY = Math.Min(bounds.Height, totalKeyHeight);

            // 总是执行绘制，确保显示稳定
            // 按照从细到粗的顺序绘制网格线，确保粗线覆盖细线
            RenderSixteenthNoteLines(context, viewModel, bounds, scrollOffset, visibleStartTime, visibleEndTime, startY, endY);
            RenderEighthNoteLines(context, viewModel, bounds, scrollOffset, visibleStartTime, visibleEndTime, startY, endY);
            RenderBeatLines(context, viewModel, bounds, scrollOffset, visibleStartTime, visibleEndTime, startY, endY);
            RenderMeasureLines(context, viewModel, bounds, scrollOffset, visibleStartTime, visibleEndTime, startY, endY);
        }

        /// <summary>
        /// 比较两个double值是否相等（处理浮点精度问题）
        /// </summary>
        private static bool AreEqual(double a, double b, double tolerance = 1e-10)
        {
            if (double.IsNaN(a) && double.IsNaN(b)) return true;
            if (double.IsNaN(a) || double.IsNaN(b)) return false;
            return Math.Abs(a - b) < tolerance;
        }

        /// <summary>
        /// 渲染十六分音符网格线
        /// </summary>
        private void RenderSixteenthNoteLines(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, 
            double scrollOffset, double visibleStartTime, double visibleEndTime, double startY, double endY)
        {
            var sixteenthWidth = viewModel.SixteenthNoteWidth;
            if (sixteenthWidth <= 5) return; // 太密集时不绘制

            var sixteenthTicks = viewModel.TicksPerBeat / 4;
            var startSixteenth = (int)(visibleStartTime / sixteenthTicks);
            var endSixteenth = (int)(visibleEndTime / sixteenthTicks) + 1;

            var pen = GetSixteenthNotePen();

            for (int i = startSixteenth; i <= endSixteenth; i++)
            {
                if (i % 4 == 0) continue; // 跳过拍线位置

                var time = i * sixteenthTicks;
                var x = time * viewModel.PixelsPerTick * viewModel.Zoom - scrollOffset;
                
                if (x >= 0 && x <= bounds.Width)
                {
                    context.DrawLine(pen, new Point(x, startY), new Point(x, endY));
                }
            }
        }

        /// <summary>
        /// 渲染八分音符网格线
        /// </summary>
        private void RenderEighthNoteLines(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, 
            double scrollOffset, double visibleStartTime, double visibleEndTime, double startY, double endY)
        {
            var eighthWidth = viewModel.EighthNoteWidth;
            if (eighthWidth <= 10) return; // 太密集时不绘制

            var eighthTicks = viewModel.TicksPerBeat / 2;
            var startEighth = (int)(visibleStartTime / eighthTicks);
            var endEighth = (int)(visibleEndTime / eighthTicks) + 1;

            var pen = GetEighthNotePen();

            for (int i = startEighth; i <= endEighth; i++)
            {
                if (i % 2 == 0) continue; // 跳过拍线位置

                var time = i * eighthTicks;
                var x = time * viewModel.PixelsPerTick * viewModel.Zoom - scrollOffset;
                
                if (x >= 0 && x <= bounds.Width)
                {
                    context.DrawLine(pen, new Point(x, startY), new Point(x, endY));
                }
            }
        }

        /// <summary>
        /// 渲染拍线 - 根据拍号动态调整
        /// </summary>
        private void RenderBeatLines(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, 
            double scrollOffset, double visibleStartTime, double visibleEndTime, double startY, double endY)
        {
            var beatTicks = viewModel.TicksPerBeat;
            var startBeat = (int)(visibleStartTime / beatTicks);
            var endBeat = (int)(visibleEndTime / beatTicks) + 1;

            var pen = GetBeatLinePen();

            for (int i = startBeat; i <= endBeat; i++)
            {
                if (i % viewModel.BeatsPerMeasure == 0) continue; // 跳过小节线位置

                var time = i * beatTicks;
                var x = time * viewModel.PixelsPerTick * viewModel.Zoom - scrollOffset;
                
                if (x >= 0 && x <= bounds.Width)
                {
                    context.DrawLine(pen, new Point(x, startY), new Point(x, endY));
                }
            }
        }

        /// <summary>
        /// 渲染小节线 - 根据拍号动态调整间距
        /// </summary>
        private void RenderMeasureLines(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, 
            double scrollOffset, double visibleStartTime, double visibleEndTime, double startY, double endY)
        {
            var measureTicks = viewModel.BeatsPerMeasure * viewModel.TicksPerBeat;
            var startMeasure = (int)(visibleStartTime / measureTicks);
            var endMeasure = (int)(visibleEndTime / measureTicks) + 1;

            var pen = GetMeasureLinePen();

            for (int i = startMeasure; i <= endMeasure; i++)
            {
                var time = i * measureTicks;
                var x = time * viewModel.PixelsPerTick * viewModel.Zoom - scrollOffset;
                
                if (x >= 0 && x <= bounds.Width)
                {
                    context.DrawLine(pen, new Point(x, startY), new Point(x, endY));
                }
            }
        }

        /// <summary>
        /// 获取十六分音符线画笔
        /// </summary>
        private IPen GetSixteenthNotePen()
        {
            var brush = GetResourceBrush("GridLineBrush", "#FFafafaf");
            return new Pen(brush, 0.5) { DashStyle = new DashStyle(new double[] { 1, 3 }, 0) };
        }

        /// <summary>
        /// 获取八分音符线画笔
        /// </summary>
        private IPen GetEighthNotePen()
        {
            var brush = GetResourceBrush("GridLineBrush", "#FFafafaf");
            return new Pen(brush, 0.7) { DashStyle = new DashStyle(new double[] { 2, 2 }, 0) };
        }

        /// <summary>
        /// 获取拍线画笔
        /// </summary>
        private IPen GetBeatLinePen()
        {
            var brush = GetResourceBrush("GridLineBrush", "#FFafafaf");
            return new Pen(brush, 0.8);
        }

        /// <summary>
        /// 获取小节线画笔
        /// </summary>
        private IPen GetMeasureLinePen()
        {
            var brush = GetResourceBrush("MeasureLineBrush", "#FF000080");
            return new Pen(brush, 1.2);
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