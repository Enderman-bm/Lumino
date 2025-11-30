using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Models.Music;
using Lumino.Views.Rendering.Utils;
using Lumino.Views.Rendering.Adapters;
using EnderDebugger;

namespace Lumino.Views.Rendering.Grids
{
    /// <summary>
    /// 垂直网格线渲染器 - 修复网格密度问题
    /// 支持不同拍号（4/4拍、3/4拍、8/4拍等）的动态调整
    /// 优化策略：内部缓存计算结果，但总是执行绘制以确保稳定性
    /// </summary>
    public class VerticalGridRenderer
    {
        public VerticalGridRenderer()
        {
            // Subscribe to global brush cache clear so we can invalidate our cached pens/colors.
            Rendering.Utils.RenderingUtils.BrushCacheCleared += OnGlobalBrushCacheCleared;
        }

        private void OnGlobalBrushCacheCleared()
        {
            try { ClearCache(); } catch { }
        }
        // UI-thread cached pens (must be created on UI thread)
        private IPen? _sixteenthNotePenCached;
        private IPen? _eighthNotePenCached;
        private IPen? _beatLinePenCached;
        private IPen? _measureLinePenCached;
    // Cached color components for Vulkan batches (created on UI thread)
    private byte _gridLineA, _gridLineR, _gridLineG, _gridLineB;
    private byte _measureLineA, _measureLineR, _measureLineG, _measureLineB;
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
        // 这些画笔需要在 UI 线程上创建。调用方应先调用 EnsurePensInitialized() 在 UI 线程上创建好画笔，
        // 然后才能在后台线程安全地使用渲染逻辑（实际绘制调用仍应序列化到 UI 线程）。
        private IPen SixteenthNotePen
        {
            get
            {
                if (_sixteenthNotePenCached == null)
                    throw new InvalidOperationException("SixteenthNotePen not initialized. Call EnsurePensInitialized() from UI thread before rendering.");
                return _sixteenthNotePenCached;
            }
        }

        private IPen EighthNotePen
        {
            get
            {
                if (_eighthNotePenCached == null)
                    throw new InvalidOperationException("EighthNotePen not initialized. Call EnsurePensInitialized() from UI thread before rendering.");
                return _eighthNotePenCached;
            }
        }

        private IPen BeatLinePen
        {
            get
            {
                if (_beatLinePenCached == null)
                    throw new InvalidOperationException("BeatLinePen not initialized. Call EnsurePensInitialized() from UI thread before rendering.");
                return _beatLinePenCached;
            }
        }

        private IPen MeasureLinePen
        {
            get
            {
                if (_measureLinePenCached == null)
                    throw new InvalidOperationException("MeasureLinePen not initialized. Call EnsurePensInitialized() from UI thread before rendering.");
                return _measureLinePenCached;
            }
        }

        /// <summary>
        /// 在 UI 线程上创建并缓存需要的画笔。必须在 Render 开始时由 UI 线程调用一次。
        /// </summary>
        public void EnsurePensInitialized()
        {
                // Temporary debug hook: force measure lines to be clearly visible
                // Set to true only while debugging visibility issues; keep false for normal operation.
                bool DebugForceVisibleMeasureLines = false;

            // Create pens using RenderingUtils which may touch Avalonia resources
            // Use a more visible gray as default fallback
            var defaultGridColor = "#FF808080";
            _sixteenthNotePenCached = new Pen(RenderingUtils.GetResourceBrush("GridLineBrush", defaultGridColor), 0.5) { DashStyle = new DashStyle(new double[] { 1, 3 }, 0) };
            _eighthNotePenCached = new Pen(RenderingUtils.GetResourceBrush("GridLineBrush", defaultGridColor), 0.7) { DashStyle = new DashStyle(new double[] { 2, 2 }, 0) };
            _beatLinePenCached = new Pen(RenderingUtils.GetResourceBrush("GridLineBrush", defaultGridColor), 0.8);
                // Use the same grid brush as other grid lines for measure lines (no special deep-blue)
                _measureLinePenCached = new Pen(RenderingUtils.GetResourceBrush("GridLineBrush", defaultGridColor), 1.2);

                // If debugging visibility, override the measure pen to a very visible solid red
                if (DebugForceVisibleMeasureLines)
                {
                        try
                        {
                        // Use an explicit solid red brush on UI thread so DrawLine path is clearly visible
                        _measureLinePenCached = new Pen(Brushes.Red, 2.0);
                        EnderLogger.Instance.Debug("VerticalGridRenderer", "[VGR] DebugForceVisibleMeasureLines: forcing measure pen to red, thickness=2.0");
                    }
                    catch (Exception ex)
                    {
                        EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] DebugForceVisibleMeasureLines failed to set pen: {ex.Message}");
                    }
                }

            // Cache the solid color components for use by background threads when building Vulkan batches.
            try
            {
                var gridBrush = RenderingUtils.GetResourceBrush("GridLineBrush", "#FFafafaf");
                    if (gridBrush is Avalonia.Media.SolidColorBrush gscb)
                {
                    var c = gscb.Color;
                    _gridLineA = c.A; _gridLineR = c.R; _gridLineG = c.G; _gridLineB = c.B;
                    EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] EnsurePens: GridLine ARGB=({c.A},{c.R},{c.G},{c.B})");
                }
                else
                {
                    // Fallback to opaque gray if not a solid color brush
                    _gridLineA = 255; _gridLineR = 175; _gridLineG = 175; _gridLineB = 175;
                    EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] EnsurePens: GridLine using fallback ARGB=(255,175,175,175)");
                }

                // Measure lines use the same ARGB as grid lines (no special deep-blue)
                _measureLineA = _gridLineA; _measureLineR = _gridLineR; _measureLineG = _gridLineG; _measureLineB = _gridLineB;
                // If debugging, force measure batch to fully opaque red so Vulkan batch path is visually obvious.
                if (DebugForceVisibleMeasureLines)
                {
                    _measureLineA = 255; _measureLineR = 255; _measureLineG = 0; _measureLineB = 0;
                    EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] DebugForceVisibleMeasureLines: forcing measure ARGB=({_measureLineA},{_measureLineR},{_measureLineG},{_measureLineB})");
                }
                EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] EnsurePens: MeasureLine ARGB same as GridLine=({_measureLineA},{_measureLineR},{_measureLineG},{_measureLineB})");
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "VerticalGridRenderer", "EnsurePens exception");
                // Ultimate fallback to visible colors
                _gridLineA = 255; _gridLineR = 175; _gridLineG = 175; _gridLineB = 175;
                _measureLineA = 255; _measureLineR = 0; _measureLineG = 0; _measureLineB = 128;
            }
        }

        /// <summary>
        /// 清除内部缓存（在主题切换或资源重置时由调用者在 UI 线程触发）
        /// </summary>
        public void ClearCache()
        {
            _sixteenthNotePenCached = null;
            _eighthNotePenCached = null;
            _beatLinePenCached = null;
            _measureLinePenCached = null;

            _gridLineA = _gridLineR = _gridLineG = _gridLineB = 0;
            _measureLineA = _measureLineR = _measureLineG = _measureLineB = 0;

            lock (_measureLock)
            {
                _cachedMeasureXs = null;
                _measureCacheStart = double.NaN;
                _measureCacheEnd = double.NaN;
                _measureCacheBaseQuarterNoteWidth = double.NaN;
                _measureCacheBeatsPerMeasure = -1;
                _cacheValid = false;
            }
        }

        /// <summary>
        /// 在后台线程安全地计算垂直网格线批次（Compute 阶段）。
        /// 此方法不访问任何 Avalonia UI 对象，仅使用已缓存的 ARGB bytes 生成 PreparedRoundedRectBatch。
        /// 调用前必须确保 EnsurePensInitialized 已在 UI 线程执行。
        /// </summary>
        /// <param name="stripe">要计算的视口分片区域</param>
        /// <param name="viewModel">视图模型（仅读取纯数据属性）</param>
        /// <param name="scrollOffset">水平滚动偏移</param>
        /// <returns>包含所有网格线的批次列表，或 null（如果没有生成任何批次）</returns>
        public List<Services.Implementation.PreparedRoundedRectBatch>? ComputeGridBatches(
            Rect stripe, 
            PianoRollViewModel viewModel, 
            double scrollOffset)
        {
            var batches = new List<Services.Implementation.PreparedRoundedRectBatch>();
            var totalKeyHeight = 128 * viewModel.KeyHeight;
            var startY = Math.Max(stripe.Top, 0);
            var endY = Math.Min(stripe.Bottom, totalKeyHeight);

            // 计算可见时间范围
            var visibleStartTime = scrollOffset / viewModel.BaseQuarterNoteWidth;
            var visibleEndTime = (scrollOffset + stripe.Width) / viewModel.BaseQuarterNoteWidth;

            // 生成小节线批次
            var measureInterval = (double)viewModel.BeatsPerMeasure;
            var startMeasure = (int)(visibleStartTime / measureInterval);
            var endMeasure = (int)(visibleEndTime / measureInterval) + 1;

            var measureBatch = new Services.Implementation.PreparedRoundedRectBatch();
            measureBatch.A = _measureLineA; measureBatch.R = _measureLineR; 
            measureBatch.G = _measureLineG; measureBatch.B = _measureLineB;

            for (int i = startMeasure; i <= endMeasure; i++)
            {
                var timeValue = i * measureInterval;
                var x = timeValue * viewModel.BaseQuarterNoteWidth - scrollOffset;
                if (x >= stripe.Left && x <= stripe.Right)
                {
                    measureBatch.Add(x - 0.5, startY, 1.0, endY - startY, 0.0, 0.0);
                }
            }

            if (measureBatch.RoundedRects.Count > 0)
                batches.Add(measureBatch);

            // 生成拍线批次（跳过与小节线重合的）
            var beatInterval = 1.0;
            var startBeat = (int)(visibleStartTime / beatInterval);
            var endBeat = (int)(visibleEndTime / beatInterval) + 1;

            var beatBatch = new Services.Implementation.PreparedRoundedRectBatch();
            beatBatch.A = _gridLineA; beatBatch.R = _gridLineR; 
            beatBatch.G = _gridLineG; beatBatch.B = _gridLineB;

            for (int i = startBeat; i <= endBeat; i++)
            {
                if (i % viewModel.BeatsPerMeasure == 0) continue; // Skip measure lines
                var timeValue = i * beatInterval;
                var x = timeValue * viewModel.BaseQuarterNoteWidth - scrollOffset;
                if (x >= stripe.Left && x <= stripe.Right)
                {
                    beatBatch.Add(x - 0.5, startY, 1.0, endY - startY, 0.0, 0.0);
                }
            }

            if (beatBatch.RoundedRects.Count > 0)
                batches.Add(beatBatch);

            // 可选：八分音符和十六分音符线（根据缩放级别）
            var eighthWidth = viewModel.EighthNoteWidth;
            if (eighthWidth > 10)
            {
                var eighthInterval = 0.5;
                var startEighth = (int)(visibleStartTime / eighthInterval);
                var endEighth = (int)(visibleEndTime / eighthInterval) + 1;

                var eighthBatch = new Services.Implementation.PreparedRoundedRectBatch();
                eighthBatch.A = _gridLineA; eighthBatch.R = _gridLineR; 
                eighthBatch.G = _gridLineG; eighthBatch.B = _gridLineB;

                for (int i = startEighth; i <= endEighth; i++)
                {
                    if (i % 2 == 0) continue; // Skip beat lines
                    var timeValue = i * eighthInterval;
                    var x = timeValue * viewModel.BaseQuarterNoteWidth - scrollOffset;
                    if (x >= stripe.Left && x <= stripe.Right)
                    {
                        eighthBatch.Add(x - 0.5, startY, 1.0, endY - startY, 0.0, 0.0);
                    }
                }

                if (eighthBatch.RoundedRects.Count > 0)
                    batches.Add(eighthBatch);
            }

            var sixteenthWidth = viewModel.SixteenthNoteWidth;
            if (sixteenthWidth > 5)
            {
                var sixteenthInterval = 0.25;
                var startSixteenth = (int)(visibleStartTime / sixteenthInterval);
                var endSixteenth = (int)(visibleEndTime / sixteenthInterval) + 1;

                var sixteenthBatch = new Services.Implementation.PreparedRoundedRectBatch();
                sixteenthBatch.A = _gridLineA; sixteenthBatch.R = _gridLineR; 
                sixteenthBatch.G = _gridLineG; sixteenthBatch.B = _gridLineB;

                for (int i = startSixteenth; i <= endSixteenth; i++)
                {
                    if (i % 4 == 0) continue; // Skip beat lines
                    var timeValue = i * sixteenthInterval;
                    var x = timeValue * viewModel.BaseQuarterNoteWidth - scrollOffset;
                    if (x >= stripe.Left && x <= stripe.Right)
                    {
                        sixteenthBatch.Add(x - 0.5, startY, 1.0, endY - startY, 0.0, 0.0);
                    }
                }

                if (sixteenthBatch.RoundedRects.Count > 0)
                    batches.Add(sixteenthBatch);
            }

            return batches.Count > 0 ? batches : null;
        }

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

            // Only prepare Vulkan batches when the Vulkan render service is initialized and usable.
            // Otherwise fallback to immediate UI-thread drawing to guarantee visibility.
            Lumino.Services.Implementation.PreparedRoundedRectBatch? batch = null;
            var vulkanReady = vulkanAdapter != null && Lumino.Services.Implementation.VulkanRenderService.Instance.IsInitialized;
            if (vulkanReady)
            {
                batch = new Lumino.Services.Implementation.PreparedRoundedRectBatch();
                batch.A = _gridLineA; batch.R = _gridLineR; batch.G = _gridLineG; batch.B = _gridLineB;
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
            var vulkanReady = vulkanAdapter != null && Lumino.Services.Implementation.VulkanRenderService.Instance.IsInitialized;
            if (vulkanReady)
            {
                batch = new Lumino.Services.Implementation.PreparedRoundedRectBatch();
                batch.A = _gridLineA; batch.R = _gridLineR; batch.G = _gridLineG; batch.B = _gridLineB;
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
            var vulkanReady = vulkanAdapter != null && Lumino.Services.Implementation.VulkanRenderService.Instance.IsInitialized;
            if (vulkanReady)
            {
                batch = new Lumino.Services.Implementation.PreparedRoundedRectBatch();
                batch.A = _gridLineA; batch.R = _gridLineR; batch.G = _gridLineG; batch.B = _gridLineB;
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
            EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] RenderMeasureLines called. vulkanAdapterNull={(vulkanAdapter==null)} bounds.Left={bounds.Left:F2} bounds.Width={bounds.Width:F2} scrollOffset={scrollOffset:F2}");

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
                    EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] useCache=true; cachedCount={xs.Length}; cacheStart={cacheStart:F2}, cacheEnd={cacheEnd:F2}, cacheBaseWidth={cacheBaseWidth:F2}, cacheBeats={cacheBeats}");
                    if (vulkanAdapter != null)
                    {
                        var batch = new Lumino.Services.Implementation.PreparedRoundedRectBatch();
                        batch.A = _measureLineA; batch.R = _measureLineR; batch.G = _measureLineG; batch.B = _measureLineB;
                        batch.Source = "VerticalGridRenderer";
                        EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] Using cached measure Xs count={xs.Length}, visibleRange=[{visibleStartTime:F2},{visibleEndTime:F2}] baseWidth={baseWidth:F2} ARGB=({_measureLineA},{_measureLineR},{_measureLineG},{_measureLineB})");
                        foreach (var x in xs)
                        {
                            EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] cached x={x:F2}");
                            if (x >= 0 && x <= bounds.Width)
                            {
                                var rect = new Rect(x - 0.5, startY, 1.0, endY - startY);
                                batch.Add(rect.X, rect.Y, rect.Width, rect.Height, 0.0, 0.0);
                                EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] cached -> added rect at x={rect.X:F2}");
                            }
                        }
                        if (batch.RoundedRects.Count > 0)
                        {
                            EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] EnqueuePreparedRoundedRectBatch (cached) count={batch.RoundedRects.Count} sampleX={(batch.RoundedRects.Count>0?batch.RoundedRects[0].X:double.NaN)}");
                            Lumino.Services.Implementation.VulkanRenderService.Instance.EnqueuePreparedRoundedRectBatch(batch);
                        }
                        else
                        {
                            EnderLogger.Instance.Debug("VerticalGridRenderer", "[VGR] cached batch had 0 rects (maybe coords out of stripe bounds)");
                        }
                    }
                    else
                    {
                        foreach (var x in xs)
                        {
                            EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] drawing cached x={x:F2} within boundsWidth={bounds.Width:F2}");
                            if (x >= 0 && x <= bounds.Width)
                            {
                                EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] cached -> context.DrawLine at x={x:F2}");
                                context.DrawLine(pen, new Point(x, startY), new Point(x, endY));
                            }
                            else
                            {
                                EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] cached x={x:F2} skipped (out of bounds)");
                            }
                        }
                    }
                    return;
                }

            // 如果缓存不可用，则启动后台计算并回退到同步计算以保证立即绘制
            if (!_measureCacheComputing)
            {
                // capture parameters (including scrollOffset to avoid closure capturing changing value)
                double s = visibleStartTime;
                double e = visibleEndTime;
                double bw = baseWidth;
                int beats = viewModel.BeatsPerMeasure;
                double capturedScrollOffset = scrollOffset;

                _measureCacheComputing = true;
                EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] Start background compute measure cache start={s:F2} end={e:F2} baseWidth={bw:F2} beats={beats} scrollOffset={capturedScrollOffset:F2}");
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        var list = new System.Collections.Generic.List<double>();
                        for (int i = (int)(s / measureInterval); i <= (int)(e / measureInterval) + 4; i++)
                        {
                            var timeValue = i * measureInterval;
                            var x = timeValue * bw - capturedScrollOffset;
                            // store absolute x relative to viewport left at capture time
                            list.Add(x);
                        }

                        lock (_measureLock)
                        {
                            _cachedMeasureXs = list.ToArray();
                            _measureCacheStart = s;
                            _measureCacheEnd = e + measureInterval * 4; // small lookahead
                            _measureCacheBaseQuarterNoteWidth = bw;
                            _measureCacheBeatsPerMeasure = beats;
                            EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] Background computed measureXs count={_cachedMeasureXs.Length}");
                        }
                    }
                    catch (Exception ex)
                    {
                        EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] Background measure cache error: {ex.Message}");
                    }
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
                    fallbackBatch.A = _measureLineA; fallbackBatch.R = _measureLineR; fallbackBatch.G = _measureLineG; fallbackBatch.B = _measureLineB;
                    fallbackBatch.Source = "VerticalGridRenderer-fallback";
                EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] MeasureLine fallback batch ARGB=({_measureLineA},{_measureLineR},{_measureLineG},{_measureLineB})");
            }

            EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] Fallback sync render measures: startMeasure={startMeasure} endMeasure={endMeasure} measureInterval={measureInterval:F2} baseWidth={baseWidth:F2} scrollOffset={scrollOffset:F2}");

            for (int i = startMeasure; i <= endMeasure; i++)
            {
                var timeValue = i * measureInterval; // 以四分音符为单位
                var x = timeValue * baseWidth - scrollOffset;

                EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] fallback compute i={i} timeValue={timeValue:F2} x={x:F2} (withinBounds={x>=0 && x<=bounds.Width})");

                if (x >= 0 && x <= bounds.Width)
                {
                    if (fallbackBatch != null)
                    {
                        var rect = new Rect(x - 0.5, startY, 1.0, endY - startY);
                        fallbackBatch.Add(rect.X, rect.Y, rect.Width, rect.Height, 0.0, 0.0);
                    }
                    else
                    {
                        EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] Drawing measure line at x={x:F2} using pen width={(pen?.Thickness ?? double.NaN)}");
                        context.DrawLine(pen!, new Point(x, startY), new Point(x, endY));
                    }
                }
            }

            if (fallbackBatch != null && fallbackBatch.RoundedRects.Count > 0)
            {
                EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] EnqueuePreparedRoundedRectBatch (fallback sync) count={fallbackBatch.RoundedRects.Count} sampleX={(fallbackBatch.RoundedRects.Count>0?fallbackBatch.RoundedRects[0].X:double.NaN)}");
                Lumino.Services.Implementation.VulkanRenderService.Instance.EnqueuePreparedRoundedRectBatch(fallbackBatch);
            }
            else if (fallbackBatch != null)
            {
                EnderLogger.Instance.Debug("VerticalGridRenderer", $"[VGR] Fallback batch empty - no measure lines in visible range or coords out of bounds");
            }
        }
    }
}