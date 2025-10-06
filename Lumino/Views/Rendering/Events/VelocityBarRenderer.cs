using Avalonia;
using Avalonia.Media;
using EnderDebugger;
using Lumino.ViewModels.Editor;
using Lumino.ViewModels.Editor.Modules;
using Lumino.Views.Rendering.Tools;
using Lumino.Views.Rendering.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lumino.Views.Rendering.Events
{
    /// <summary>
    /// 力度条渲染器 - 性能优化版本，支持画笔复用和后台预计算
    /// </summary>
    public class VelocityBarRenderer
    {
        private static readonly EnderLogger _logger = EnderLogger.Instance;
        private const double BAR_MARGIN = 1.0;
        private const double MIN_BAR_WIDTH = 2.0;

        // 鼠标曲线渲染器实例
        private readonly MouseCurveRenderer _curveRenderer = new MouseCurveRenderer();

        #region 画笔缓存系统

        // 按渲染类型和透明度级别缓存画笔
        private readonly Dictionary<(VelocityRenderType, double), (IBrush brush, IPen pen)> _styleCache = new();

        // 文本渲染缓存
        private readonly Dictionary<(int velocity, bool isPreview), FormattedText> _textCache = new();
        private readonly Dictionary<string, Typeface> _typefaceCache = new();

        // 预览画笔缓存
        private readonly Dictionary<double, (IBrush brush, IPen pen)> _previewStyleCache = new();

        // 性能优化配置
        private bool _enableBackgroundPrecomputation = true;
        private int _precomputationThreshold = 1000; // 超过此数量的音符时启用后台预计算

        #endregion

        #region 后台预计算系统

        // 预计算结果缓存
        private readonly ConcurrentDictionary<string, VelocityBarData> _precomputedBars = new();
        private volatile bool _precomputationInProgress = false;

        /// <summary>
        /// 力度条预计算数据
        /// </summary>
        private class VelocityBarData
        {
            public Rect BarRect { get; set; }
            public double Opacity { get; set; }
            public bool IsVisible { get; set; }
            public string CacheKey { get; set; } = string.Empty;
        }

        #endregion

        /// <summary>
        /// 设置后台预计算功能
        /// </summary>
        public void SetBackgroundPrecomputationEnabled(bool enabled)
        {
            _enableBackgroundPrecomputation = enabled;
        }

        /// <summary>
        /// 设置后台预计算的音符数量阈值
        /// </summary>
        public void SetPrecomputationThreshold(int threshold)
        {
            _precomputationThreshold = threshold;
        }

        /// <summary>
        /// 主要的力度条绘制方法 - 性能优化版本
        /// </summary>
        public void DrawVelocityBar(DrawingContext context, NoteViewModel note, Rect canvasBounds,
            double timeToPixelScale, VelocityRenderType renderType = VelocityRenderType.Normal,
            double scrollOffset = 0)
        {
            // 生成缓存键
            var cacheKey = GenerateCacheKey(note, canvasBounds, timeToPixelScale, scrollOffset);

            // 尝试从预计算缓存获取数据
            if (_precomputedBars.TryGetValue(cacheKey, out var precomputedData))
            {
                if (!precomputedData.IsVisible) return;

                // 使用预计算的数据快速渲染
                DrawVelocityBarFast(context, precomputedData, note, renderType);
                return;
            }

            // 回退到常规计算方式
            DrawVelocityBarRegular(context, note, canvasBounds, timeToPixelScale, renderType, scrollOffset);
        }

        /// <summary>
        /// 快速渲染（使用预计算数据）
        /// </summary>
        private void DrawVelocityBarFast(DrawingContext context, VelocityBarData data, NoteViewModel note, VelocityRenderType renderType)
        {
            var (brush, pen) = GetCachedStyle(renderType, data.Opacity);
            context.DrawRectangle(brush, pen, data.BarRect);

            // 如果条形足够大，绘制力度值
            if (data.BarRect.Width > 30 && renderType == VelocityRenderType.Selected)
            {
                DrawVelocityValueCached(context, data.BarRect, note.Velocity);
            }
        }

        /// <summary>
        /// 常规渲染方式（兼容性保证）
        /// </summary>
        private void DrawVelocityBarRegular(DrawingContext context, NoteViewModel note, Rect canvasBounds,
            double timeToPixelScale, VelocityRenderType renderType, double scrollOffset)
        {
            // 计算音符在时间轴上的位置和宽度（世界坐标）
            var absoluteNoteX = note.GetX(timeToPixelScale);
            var noteWidth = note.GetWidth(timeToPixelScale);

            // 应用滚动偏移得到画面坐标
            var noteX = absoluteNoteX - scrollOffset;

            // 确保音符在画布范围内
            if (noteX + noteWidth < 0 || noteX > canvasBounds.Width) return;

            // 计算力度条的尺寸
            var barWidth = Math.Max(MIN_BAR_WIDTH, noteWidth - BAR_MARGIN * 2);
            var barHeight = CalculateBarHeight(note.Velocity, canvasBounds.Height);

            var barX = noteX + BAR_MARGIN;
            var barY = canvasBounds.Height - barHeight;

            var barRect = new Rect(barX, barY, barWidth, barHeight);
            var opacity = CalculateOpacity(note.Velocity);

            // 使用缓存的样式
            var (brush, pen) = GetCachedStyle(renderType, opacity);

            // 绘制力度条
            context.DrawRectangle(brush, pen, barRect);

            // 如果条形足够大，绘制力度值
            if (barWidth > 30 && renderType == VelocityRenderType.Selected)
            {
                DrawVelocityValueCached(context, barRect, note.Velocity);
            }
        }

        /// <summary>
        /// 批量预计算力度条数据（后台线程）
        /// </summary>
        public async Task PrecomputeVelocityBarsAsync(IEnumerable<NoteViewModel> notes, Rect canvasBounds,
            double timeToPixelScale, double scrollOffset)
        {
            if (!_enableBackgroundPrecomputation || _precomputationInProgress) return;

            var noteList = notes.ToList();
            if (noteList.Count < _precomputationThreshold) return;

            _precomputationInProgress = true;

            try
            {
                await Task.Run(() =>
                {
                    var newCache = new Dictionary<string, VelocityBarData>();

                    foreach (var note in noteList)
                    {
                        var cacheKey = GenerateCacheKey(note, canvasBounds, timeToPixelScale, scrollOffset);

                        // 计算力度条数据
                        var absoluteNoteX = note.GetX(timeToPixelScale);
                        var noteWidth = note.GetWidth(timeToPixelScale);
                        var noteX = absoluteNoteX - scrollOffset;

                        var isVisible = !(noteX + noteWidth < 0 || noteX > canvasBounds.Width);

                        if (isVisible)
                        {
                            var barWidth = Math.Max(MIN_BAR_WIDTH, noteWidth - BAR_MARGIN * 2);
                            var barHeight = CalculateBarHeight(note.Velocity, canvasBounds.Height);
                            var barX = noteX + BAR_MARGIN;
                            var barY = canvasBounds.Height - barHeight;

                            newCache[cacheKey] = new VelocityBarData
                            {
                                BarRect = new Rect(barX, barY, barWidth, barHeight),
                                Opacity = CalculateOpacity(note.Velocity),
                                IsVisible = true,
                                CacheKey = cacheKey
                            };
                        }
                        else
                        {
                            newCache[cacheKey] = new VelocityBarData
                            {
                                IsVisible = false,
                                CacheKey = cacheKey
                            };
                        }
                    }

                    // 原子性更新缓存
                    foreach (var kvp in newCache)
                    {
                        _precomputedBars.AddOrUpdate(kvp.Key, kvp.Value, (key, oldValue) => kvp.Value);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error("PrecomputeVelocityBars", $"预计算力度条时出错: {ex.Message}");
            }
            finally
            {
                _precomputationInProgress = false;
            }
        }

        /// <summary>
        /// 绘制编辑预览 - 优化版本
        /// </summary>
        public void DrawEditingPreview(DrawingContext context, Rect canvasBounds,
            VelocityEditingModule editingModule, double timeToPixelScale, double scrollOffset = 0)
        {
            if (editingModule.EditingPath?.Any() != true) return;

            // 使用鼠标曲线渲染器绘制编辑轨迹
            var curveStyle = _curveRenderer.CreateEditingPreviewStyle();
            _curveRenderer.DrawMouseTrail(context, editingModule.EditingPath, canvasBounds, scrollOffset, curveStyle);

            // 绘制当前编辑位置的力度条预览
            if (editingModule.CurrentEditPosition.HasValue)
            {
                DrawCurrentEditPositionPreviewCached(context, editingModule.CurrentEditPosition.Value,
                    canvasBounds, scrollOffset, curveStyle.Brush);
            }
        }

        #region 缓存优化方法

        /// <summary>
        /// 获取缓存的样式
        /// </summary>
        private (IBrush brush, IPen pen) GetCachedStyle(VelocityRenderType renderType, double opacity)
        {
            // 量化透明度到0.1级别
            var quantizedOpacity = Math.Round(opacity, 1);
            var cacheKey = (renderType, quantizedOpacity);

            if (!_styleCache.TryGetValue(cacheKey, out var style))
            {
                style = CreateStyle(renderType, quantizedOpacity);
                _styleCache[cacheKey] = style;
            }

            return style;
        }

        /// <summary>
        /// 创建样式
        /// </summary>
        private (IBrush brush, IPen pen) CreateStyle(VelocityRenderType renderType, double opacity)
        {
            return renderType switch
            {
                VelocityRenderType.Selected => (
                    RenderingUtils.CreateBrushWithOpacity(
                        RenderingUtils.GetResourceBrush("VelocitySelectedBrush", "#FFFF9800"), opacity),
                    RenderingUtils.GetResourcePen("VelocitySelectedPenBrush", "#FFF57C00", 1)
                ),
                VelocityRenderType.Editing => (
                    RenderingUtils.CreateBrushWithOpacity(
                        RenderingUtils.GetResourceBrush("VelocityEditingBrush", "#FFFF5722"), opacity),
                    RenderingUtils.GetResourcePen("VelocityEditingPenBrush", "#FFD84315", 2)
                ),
                VelocityRenderType.Dragging => (
                    RenderingUtils.CreateBrushWithOpacity(
                        RenderingUtils.GetResourceBrush("VelocityDraggingBrush", "#FF2196F3"), opacity),
                    RenderingUtils.GetResourcePen("VelocityDraggingPenBrush", "#FF1976D2", 1)
                ),
                _ => ( // Normal
                    RenderingUtils.CreateBrushWithOpacity(
                        RenderingUtils.GetResourceBrush("VelocityBrush", "#FF4CAF50"), opacity),
                    RenderingUtils.GetResourcePen("VelocityPenBrush", "#FF2E7D32", 1)
                )
            };
        }

        /// <summary>
        /// 获取缓存的字体
        /// </summary>
        private Typeface GetCachedTypeface(string fontFamily = "Segoe UI")
        {
            if (!_typefaceCache.TryGetValue(fontFamily, out var typeface))
            {
                typeface = new Typeface(fontFamily, FontStyle.Normal, FontWeight.Normal);
                _typefaceCache[fontFamily] = typeface;
            }
            return typeface;
        }

        /// <summary>
        /// 绘制力度值 - 缓存优化版本
        /// </summary>
        private void DrawVelocityValueCached(DrawingContext context, Rect barRect, int velocity, bool isPreview = false)
        {
            var cacheKey = (velocity, isPreview);

            if (!_textCache.TryGetValue(cacheKey, out var formattedText))
            {
                var textBrush = RenderingUtils.GetResourceBrush("VelocityTextBrush", "#FFFFFFFF");
                var typeface = GetCachedTypeface();

                formattedText = new FormattedText(
                    velocity.ToString(),
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    isPreview ? 12 : 10,
                    textBrush);

                _textCache[cacheKey] = formattedText;
            }

            var textX = barRect.X + (barRect.Width - formattedText.Width) / 2;
            var textY = isPreview ? barRect.Y - 15 : barRect.Y + 2;

            context.DrawText(formattedText, new Point(textX, textY));
        }

        /// <summary>
        /// 绘制当前编辑位置预览 - 缓存优化版本
        /// </summary>
        private void DrawCurrentEditPositionPreviewCached(DrawingContext context, Point worldPosition,
            Rect canvasBounds, double scrollOffset, IBrush previewBrush)
        {
            var screenPos = new Point(worldPosition.X - scrollOffset, worldPosition.Y);

            // 只在屏幕范围内绘制预览
            if (screenPos.X < -20 || screenPos.X > canvasBounds.Width + 20) return;

            var velocity = CalculateVelocityFromY(screenPos.Y, canvasBounds.Height);
            var previewHeight = CalculateBarHeight(velocity, canvasBounds.Height);
            var previewRect = new Rect(screenPos.X - 8, canvasBounds.Height - previewHeight, 16, previewHeight);

            // 使用缓存的预览样式
            var opacity = 0.7;
            var quantizedOpacity = Math.Round(opacity, 1);

            if (!_previewStyleCache.TryGetValue(quantizedOpacity, out var previewStyle))
            {
                var previewBarBrush = RenderingUtils.CreateBrushWithOpacity(previewBrush, opacity);
                var previewPen = new Pen(previewBrush, 2, new DashStyle(new double[] { 3, 3 }, 0));
                previewStyle = (previewBarBrush, previewPen);
                _previewStyleCache[quantizedOpacity] = previewStyle;
            }

            context.DrawRectangle(previewStyle.brush, previewStyle.pen, previewRect);

            // 显示当前力度值
            DrawVelocityValueCached(context, previewRect, velocity, true);
        }

        /// <summary>
        /// 生成缓存键
        /// </summary>
        private string GenerateCacheKey(NoteViewModel note, Rect canvasBounds, double timeToPixelScale, double scrollOffset)
        {
            return $"{note.Id}_{canvasBounds.Width:F0}_{canvasBounds.Height:F0}_{timeToPixelScale:F2}_{scrollOffset:F0}_{note.Velocity}";
        }

        #endregion

        #region 计算方法

        private double CalculateOpacity(int velocity)
        {
            // 根据力度值计算透明度，确保可见性
            return Math.Max(0.4, velocity / 127.0);
        }

        private double CalculateBarHeight(int velocity, double maxHeight)
        {
            // 将MIDI力度值(0-127)映射到条形高度
            var normalizedVelocity = Math.Max(0, Math.Min(127, velocity)) / 127.0;
            return normalizedVelocity * maxHeight;
        }

        public static int CalculateVelocityFromY(double y, double maxHeight)
        {
            // 从Y坐标反算力度值
            var normalizedY = Math.Max(0, Math.Min(1, (maxHeight - y) / maxHeight));
            var velocity = Math.Max(1, Math.Min(127, (int)Math.Round(normalizedY * 127)));

            return velocity;
        }

        #endregion

        #region 缓存管理

        /// <summary>
        /// 清除所有缓存（主题变更或内存压力时调用）
        /// </summary>
        public void ClearAllCaches()
        {
            _styleCache.Clear();
            _textCache.Clear();
            _typefaceCache.Clear();
            _previewStyleCache.Clear();
            _precomputedBars.Clear();
        }

        /// <summary>
        /// 清除预计算缓存（滚动或缩放时调用）
        /// </summary>
        public void ClearPrecomputedCache()
        {
            _precomputedBars.Clear();
        }

        /// <summary>
        /// 获取缓存统计信息（调试用）
        /// </summary>
        public string GetCacheStatistics()
        {
            return $"样式缓存: {_styleCache.Count}, 文本缓存: {_textCache.Count}, " +
                   $"预计算缓存: {_precomputedBars.Count}, 预览样式缓存: {_previewStyleCache.Count}";
        }

        #endregion
    }

    /// <summary>
    /// 力度条渲染类型
    /// </summary>
    public enum VelocityRenderType
    {
        Normal,
        Selected,
        Editing,
        Dragging
    }
}