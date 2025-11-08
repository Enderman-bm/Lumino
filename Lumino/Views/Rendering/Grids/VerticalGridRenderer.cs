using System;
using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Models.Music;
using Lumino.Views.Rendering.Utils;
using Lumino.Views.Rendering.Adapters;

namespace Lumino.Views.Rendering.Grids
{
    /// <summary>
    /// 垂直网格线渲染器 - 修复网格密度问题
    /// 支持不同拍号（4/4拍、3/4拍、8/4拍等）的动态调整
    /// 优化策略：内部缓存计算结果，但总是执行绘制以确保稳定性
    /// </summary>
    public class VerticalGridRenderer
    {
        // Measure positions cache computed on background thread
        private readonly object _measureLock = new object();
        private double _measureCacheStart = double.NaN;
        private double _measureCacheEnd = double.NaN;
        private double _measureCacheBaseQuarterNoteWidth = double.NaN;
        private int _measureCacheBeatsPerMeasure = -1;
        private double[]? _cachedMeasureXs;
        private volatile bool _measureCacheComputing = false;
        // 缓存上次渲染的参数，用于优化计算
        private double _lastHorizontalScrollOffset = double.NaN;
        private double _lastZoom = double.NaN;
        private double _lastViewportWidth = double.NaN;
        private double _lastTimeToPixelScale = double.NaN;

        // 缓存计算结果
        private double _cachedVisibleStartTime;
        private double _cachedVisibleEndTime;
        private bool _cacheValid = false;

        // 使用动态画笔获取，确保与主题状态同步
        private IPen SixteenthNotePen => new Pen(RenderingUtils.GetResourceBrush("GridLineBrush", "#FFafafaf"), 0.5) { DashStyle = new DashStyle(new double[] { 1, 3 }, 0) };
        private IPen EighthNotePen => new Pen(RenderingUtils.GetResourceBrush("GridLineBrush", "#FFafafaf"), 0.7) { DashStyle = new DashStyle(new double[] { 2, 2 }, 0) };
        private IPen BeatLinePen => new Pen(RenderingUtils.GetResourceBrush("GridLineBrush", "#FFafafaf"), 0.8);
        private IPen MeasureLinePen => new Pen(RenderingUtils.GetResourceBrush("MeasureLineBrush", "#FF000080"), 1.2);

        /// <summary>
        /// 渲染垂直网格线（修复网格密度问题）
        /// </summary>
        public void RenderVerticalGrid(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, double scrollOffset)
        {
            RenderVerticalGrid(context, null, viewModel, bounds, scrollOffset);
        }

        /// <summary>
        /// 渲染垂直网格线，支持Vulkan适配器
        /// </summary>
        public void RenderVerticalGrid(DrawingContext context, VulkanDrawingContextAdapter? vulkanAdapter, PianoRollViewModel viewModel, Rect bounds, double scrollOffset)
        {
            var timeToPixelScale = viewModel.TimeToPixelScale;
            
            // 检查是否需要重新计算可见范围（性能优化）
            bool needsRecalculation = !_cacheValid ||
                !AreEqual(_lastHorizontalScrollOffset, scrollOffset) ||
                !AreEqual(_lastZoom, viewModel.Zoom) ||
                !AreEqual(_lastViewportWidth, bounds.Width) ||
                !AreEqual(_lastTimeToPixelScale, timeToPixelScale);

            double visibleStartTime, visibleEndTime;

            if (needsRecalculation)
            {
                // 计算可见的时间范围（以四分音符为单位）
                visibleStartTime = scrollOffset / viewModel.BaseQuarterNoteWidth;
                visibleEndTime = (scrollOffset + bounds.Width) / viewModel.BaseQuarterNoteWidth;

                // 更新缓存
                _cachedVisibleStartTime = visibleStartTime;
                _cachedVisibleEndTime = visibleEndTime;
                _lastHorizontalScrollOffset = scrollOffset;
                _lastZoom = viewModel.Zoom;
                _lastViewportWidth = bounds.Width;
                _lastTimeToPixelScale = timeToPixelScale;
                _cacheValid = true;
            }
            else
            {
                // 使用缓存值
                visibleStartTime = _cachedVisibleStartTime;
                visibleEndTime = _cachedVisibleEndTime;
            }

            var totalKeyHeight = 128 * viewModel.KeyHeight;
            var startY = 0;
            var endY = Math.Min(bounds.Height, totalKeyHeight);

            // 总是执行绘制，确保显示稳定
            // 按照从细到粗的顺序绘制网格线，确保粗线覆盖细线
            RenderSixteenthNoteLines(context, vulkanAdapter, viewModel, bounds, scrollOffset, visibleStartTime, visibleEndTime, startY, endY);
            RenderEighthNoteLines(context, vulkanAdapter, viewModel, bounds, scrollOffset, visibleStartTime, visibleEndTime, startY, endY);
            RenderBeatLines(context, vulkanAdapter, viewModel, bounds, scrollOffset, visibleStartTime, visibleEndTime, startY, endY);
            // 渲染小节线（采用后台计算位置以减轻UI线程负担）
            RenderMeasureLines(context, vulkanAdapter, viewModel, bounds, scrollOffset, visibleStartTime, visibleEndTime, startY, endY);
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
        /// 渲染十六分音符网格线 - 修复间距
        /// </summary>
        private void RenderSixteenthNoteLines(DrawingContext context, VulkanDrawingContextAdapter? vulkanAdapter, PianoRollViewModel viewModel, Rect bounds, 
            double scrollOffset, double visibleStartTime, double visibleEndTime, double startY, double endY)
        {
            var sixteenthWidth = viewModel.SixteenthNoteWidth;
            if (sixteenthWidth <= 5) return; // 太密集时不绘制

            // 十六分音符间距：1/4四分音符 = 0.25
            var sixteenthInterval = 0.25;
            var startSixteenth = (int)(visibleStartTime / sixteenthInterval);
            var endSixteenth = (int)(visibleEndTime / sixteenthInterval) + 1;

            // 使用动态获取的画笔
            var pen = SixteenthNotePen;

            Lumino.Services.Implementation.PreparedRoundedRectBatch? batch = null;
            if (vulkanAdapter != null)
            {
                batch = new Lumino.Services.Implementation.PreparedRoundedRectBatch();
                var brushForColor = RenderingUtils.GetResourceBrush("GridLineBrush", "#FFafafaf");
                var color = brushForColor is Avalonia.Media.SolidColorBrush scb ? scb.Color : Avalonia.Media.Colors.Transparent;
                batch.A = color.A; batch.R = color.R; batch.G = color.G; batch.B = color.B;
            }

            for (int i = startSixteenth; i <= endSixteenth; i++)
            {
                // 跳过与拍线重合的位置（每4个十六分音符 = 1个四分音符）
                if (i % 4 == 0) continue;

                var timeValue = i * sixteenthInterval; // 以四分音符为单位
                var x = timeValue * viewModel.BaseQuarterNoteWidth - scrollOffset;
                
                if (x >= 0 && x <= bounds.Width)
                {
                    if (batch != null)
                    {
                        var rect = new Rect(x - 0.5, startY, 1.0, endY - startY);
                        batch.Add(rect.X, rect.Y, rect.Width, rect.Height, 0.0, 0.0);
                    }
                    else
                    {
                        context.DrawLine(pen, new Point(x, startY), new Point(x, endY));
                    }
                }
            }

            if (batch != null && batch.RoundedRects.Count > 0)
            {
                Lumino.Services.Implementation.VulkanRenderService.Instance.EnqueuePreparedRoundedRectBatch(batch);
            }
        }

        /// <summary>
        /// 渲染八分音符网格线 - 修复间距
        /// </summary>
        private void RenderEighthNoteLines(DrawingContext context, VulkanDrawingContextAdapter? vulkanAdapter, PianoRollViewModel viewModel, Rect bounds, 
            double scrollOffset, double visibleStartTime, double visibleEndTime, double startY, double endY)
        {
            var eighthWidth = viewModel.EighthNoteWidth;
            if (eighthWidth <= 10) return; // 太密集时不绘制

            // 八分音符间距：1/2四分音符 = 0.5
            var eighthInterval = 0.5;
            var startEighth = (int)(visibleStartTime / eighthInterval);
            var endEighth = (int)(visibleEndTime / eighthInterval) + 1;

            // 使用动态获取的画笔
            var pen = EighthNotePen;

            Lumino.Services.Implementation.PreparedRoundedRectBatch? batch = null;
            if (vulkanAdapter != null)
            {
                batch = new Lumino.Services.Implementation.PreparedRoundedRectBatch();
                var brushForColor = RenderingUtils.GetResourceBrush("GridLineBrush", "#FFafafaf");
                var color = brushForColor is Avalonia.Media.SolidColorBrush scb ? scb.Color : Avalonia.Media.Colors.Transparent;
                batch.A = color.A; batch.R = color.R; batch.G = color.G; batch.B = color.B;
            }

            for (int i = startEighth; i <= endEighth; i++)
            {
                // 跳过与拍线重合的位置（每2个八分音符 = 1个四分音符）
                if (i % 2 == 0) continue;

                var timeValue = i * eighthInterval; // 以四分音符为单位
                var x = timeValue * viewModel.BaseQuarterNoteWidth - scrollOffset;
                
                if (x >= 0 && x <= bounds.Width)
                {
                    if (batch != null)
                    {
                        var rect = new Rect(x - 0.5, startY, 1.0, endY - startY);
                        batch.Add(rect.X, rect.Y, rect.Width, rect.Height, 0.0, 0.0);
                    }
                    else
                    {
                        context.DrawLine(pen, new Point(x, startY), new Point(x, endY));
                    }
                }
            }

            if (batch != null && batch.RoundedRects.Count > 0)
            {
                Lumino.Services.Implementation.VulkanRenderService.Instance.EnqueuePreparedRoundedRectBatch(batch);
            }
        }

        /// <summary>
        /// 渲染拍线 - 修复间距
        /// </summary>
        private void RenderBeatLines(DrawingContext context, VulkanDrawingContextAdapter? vulkanAdapter, PianoRollViewModel viewModel, Rect bounds, 
            double scrollOffset, double visibleStartTime, double visibleEndTime, double startY, double endY)
        {
            // 拍线间距：1个四分音符 = 1.0
            var beatInterval = 1.0;
            var startBeat = (int)(visibleStartTime / beatInterval);
            var endBeat = (int)(visibleEndTime / beatInterval) + 1;

            // 使用动态获取的画笔
            var pen = BeatLinePen;

            Lumino.Services.Implementation.PreparedRoundedRectBatch? batch = null;
            if (vulkanAdapter != null)
            {
                batch = new Lumino.Services.Implementation.PreparedRoundedRectBatch();
                var brushForColor = RenderingUtils.GetResourceBrush("GridLineBrush", "#FFafafaf");
                var color = brushForColor is Avalonia.Media.SolidColorBrush scb ? scb.Color : Avalonia.Media.Colors.Transparent;
                batch.A = color.A; batch.R = color.R; batch.G = color.G; batch.B = color.B;
            }

            for (int i = startBeat; i <= endBeat; i++)
            {
                // 跳过与小节线重合的位置（每BeatsPerMeasure个拍 = 1个小节）
                if (i % viewModel.BeatsPerMeasure == 0) continue;

                var timeValue = i * beatInterval; // 以四分音符为单位
                var x = timeValue * viewModel.BaseQuarterNoteWidth - scrollOffset;
                
                if (x >= 0 && x <= bounds.Width)
                {
                    if (batch != null)
                    {
                        var rect = new Rect(x - 0.5, startY, 1.0, endY - startY);
                        batch.Add(rect.X, rect.Y, rect.Width, rect.Height, 0.0, 0.0);
                    }
                    else
                    {
                        context.DrawLine(pen, new Point(x, startY), new Point(x, endY));
                    }
                }
            }

            if (batch != null && batch.RoundedRects.Count > 0)
            {
                Lumino.Services.Implementation.VulkanRenderService.Instance.EnqueuePreparedRoundedRectBatch(batch);
            }
        }

        /// <summary>
        /// 渲染小节线 - 修复间距
        /// </summary>
        private void RenderMeasureLines(DrawingContext context, VulkanDrawingContextAdapter? vulkanAdapter, PianoRollViewModel viewModel, Rect bounds, 
            double scrollOffset, double visibleStartTime, double visibleEndTime, double startY, double endY)
        {
            // 小节线间距：BeatsPerMeasure个四分音符（4/4拍 = 4.0）
            var measureInterval = (double)viewModel.BeatsPerMeasure;
            var startMeasure = (int)(visibleStartTime / measureInterval);
            var endMeasure = (int)(visibleEndTime / measureInterval) + 1;

            // 使用动态获取的画笔
            var pen = MeasureLinePen;

            // 如果缓存可用且参数匹配，则直接使用缓存的 X 列表进行绘制（UI 线程开销小）
            bool useCache = false;
            double baseWidth = viewModel.BaseQuarterNoteWidth;
            double cacheStart, cacheEnd;
            double cacheBaseWidth;
            int cacheBeats;

            lock (_measureLock)
            {
                cacheStart = _measureCacheStart;
                cacheEnd = _measureCacheEnd;
                cacheBaseWidth = _measureCacheBaseQuarterNoteWidth;
                cacheBeats = _measureCacheBeatsPerMeasure;
                useCache = _cachedMeasureXs != null && cacheBeats == viewModel.BeatsPerMeasure && cacheBaseWidth == baseWidth &&
                           cacheStart <= visibleStartTime && cacheEnd >= visibleEndTime;
            }

                if (useCache)
                {
                    var xs = _cachedMeasureXs!;
                    if (vulkanAdapter != null)
                    {
                        var batch = new Lumino.Services.Implementation.PreparedRoundedRectBatch();
                        var brushForColor = RenderingUtils.GetResourceBrush("MeasureLineBrush", "#FF000080");
                        var color = brushForColor is Avalonia.Media.SolidColorBrush scb ? scb.Color : Avalonia.Media.Colors.Transparent;
                        batch.A = color.A; batch.R = color.R; batch.G = color.G; batch.B = color.B;
                        foreach (var x in xs)
                        {
                            if (x >= 0 && x <= bounds.Width)
                            {
                                var rect = new Rect(x - 0.5, startY, 1.0, endY - startY);
                                batch.Add(rect.X, rect.Y, rect.Width, rect.Height, 0.0, 0.0);
                            }
                        }
                        if (batch.RoundedRects.Count > 0)
                            Lumino.Services.Implementation.VulkanRenderService.Instance.EnqueuePreparedRoundedRectBatch(batch);
                    }
                    else
                    {
                        foreach (var x in xs)
                        {
                            if (x >= 0 && x <= bounds.Width)
                            {
                                context.DrawLine(pen, new Point(x, startY), new Point(x, endY));
                            }
                        }
                    }
                    return;
                }

            // 如果缓存不可用，则启动后台计算并回退到同步计算以保证立即绘制
            if (!_measureCacheComputing)
            {
                // capture parameters
                double s = visibleStartTime;
                double e = visibleEndTime;
                double bw = baseWidth;
                int beats = viewModel.BeatsPerMeasure;

                _measureCacheComputing = true;
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        var list = new System.Collections.Generic.List<double>();
                        for (int i = (int)(s / measureInterval); i <= (int)(e / measureInterval) + 4; i++)
                        {
                            var timeValue = i * measureInterval;
                            var x = timeValue * bw - scrollOffset;
                            // store absolute x relative to viewport left
                            list.Add(x);
                        }

                        lock (_measureLock)
                        {
                            _cachedMeasureXs = list.ToArray();
                            _measureCacheStart = s;
                            _measureCacheEnd = e + measureInterval * 4; // small lookahead
                            _measureCacheBaseQuarterNoteWidth = bw;
                            _measureCacheBeatsPerMeasure = beats;
                        }
                    }
                    catch { }
                    finally
                    {
                        _measureCacheComputing = false;
                    }
                });
            }

            // 回退：同步绘制当前可见范围（简单快速计算）
            Lumino.Services.Implementation.PreparedRoundedRectBatch? fallbackBatch = null;
            if (vulkanAdapter != null)
            {
                fallbackBatch = new Lumino.Services.Implementation.PreparedRoundedRectBatch();
                var brushForColor = RenderingUtils.GetResourceBrush("MeasureLineBrush", "#FF000080");
                var color = brushForColor is Avalonia.Media.SolidColorBrush scb ? scb.Color : Avalonia.Media.Colors.Transparent;
                fallbackBatch.A = color.A; fallbackBatch.R = color.R; fallbackBatch.G = color.G; fallbackBatch.B = color.B;
            }

            for (int i = startMeasure; i <= endMeasure; i++)
            {
                var timeValue = i * measureInterval; // 以四分音符为单位
                var x = timeValue * baseWidth - scrollOffset;

                if (x >= 0 && x <= bounds.Width)
                {
                    if (fallbackBatch != null)
                    {
                        var rect = new Rect(x - 0.5, startY, 1.0, endY - startY);
                        fallbackBatch.Add(rect.X, rect.Y, rect.Width, rect.Height, 0.0, 0.0);
                    }
                    else
                    {
                        context.DrawLine(pen, new Point(x, startY), new Point(x, endY));
                    }
                }
            }

            if (fallbackBatch != null && fallbackBatch.RoundedRects.Count > 0)
            {
                Lumino.Services.Implementation.VulkanRenderService.Instance.EnqueuePreparedRoundedRectBatch(fallbackBatch);
            }
        }
    }
}