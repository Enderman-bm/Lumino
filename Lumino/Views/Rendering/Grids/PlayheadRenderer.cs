using System;
using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Utils;
using Lumino.Views.Rendering.Adapters;

namespace Lumino.Views.Rendering.Grids
{
    /// <summary>
    /// 播放头渲染器 - 性能优化版本，支持画笔复用
    /// </summary>
    public class PlayheadRenderer
    {
        public PlayheadRenderer()
        {
            RenderingUtils.BrushCacheCleared += OnGlobalBrushCacheCleared;
        }

        private void OnGlobalBrushCacheCleared()
        {
            try { ClearCache(); } catch { }
        }
    // 画笔缓存 - 复用播放头相关画笔
    private IBrush? _cachedPlayheadBrush;
    private IPen? _cachedPlayheadPen;
    // Cached color components for Vulkan batches (created on UI thread)
    private byte _playheadA, _playheadR, _playheadG, _playheadB;
    private volatile bool _initialized = false;

        /// <summary>
        /// 渲染播放头 - 统一入口方法
        /// </summary>
        public void RenderPlayhead(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, double scrollOffset)
        {
            RenderPlayhead(context, null, viewModel, bounds, scrollOffset);
        }

        /// <summary>
        /// 渲染播放头，支持Vulkan适配器
        /// </summary>
        public void RenderPlayhead(DrawingContext context, VulkanDrawingContextAdapter? vulkanAdapter, PianoRollViewModel viewModel, Rect bounds, double scrollOffset)
        {
            var timelinePosition = viewModel.TimelinePosition;
            var playheadX = timelinePosition * viewModel.BaseQuarterNoteWidth - scrollOffset;

            // 播放头在可见区域内时才渲染
            if (playheadX >= 0 && playheadX <= bounds.Width)
            {
                // 如果有 Vulkan 适配器，则将播放头线封装为 PreparedRoundedRectBatch 并提交，以便由 VulkanRenderService 合并提交
                if (vulkanAdapter != null)
                {
                    try
                    {
                        var batch = new Lumino.Services.Implementation.PreparedRoundedRectBatch();
                        if (_initialized)
                        {
                            batch.A = _playheadA; batch.R = _playheadR; batch.G = _playheadG; batch.B = _playheadB;
                        }
                        else
                        {
                            // best-effort fallback (may throw if called from background thread) - swallow below
                            var brush = GetCachedPlayheadBrush();
                            var color = brush is Avalonia.Media.SolidColorBrush scb ? scb.Color : Avalonia.Media.Colors.Transparent;
                            batch.A = color.A; batch.R = color.R; batch.G = color.G; batch.B = color.B;
                        }
                        var rect = new Rect(playheadX - 0.5, 0, 1.0, bounds.Height);
                        batch.Add(rect.X, rect.Y, rect.Width, rect.Height, 0.0, 0.0);
                        Lumino.Services.Implementation.VulkanRenderService.Instance.EnqueuePreparedRoundedRectBatch(batch);
                    }
                    catch { /* best-effort: fallback to immediate draw below */ }
                }
                else
                {
                    RenderPlayheadLine(context, playheadX, bounds.Height);
                }
                
                // 在顶部渲染播放头指示器（可选） - 仅在非 Vulkan 路径绘制
                if (vulkanAdapter == null && bounds.Height > 20)
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
                throw new InvalidOperationException("PlayheadRenderer not initialized. Call EnsureInitialized() on UI thread before using.");
            return _cachedPlayheadPen;
        }

        /// <summary>
        /// 获取缓存的播放头画刷
        /// </summary>
        private IBrush GetCachedPlayheadBrush()
        {
            if (_cachedPlayheadBrush == null)
                throw new InvalidOperationException("PlayheadRenderer not initialized. Call EnsureInitialized() on UI thread before using.");
            return _cachedPlayheadBrush;
        }

        /// <summary>
        /// Ensure brushes/pens and cached color bytes are created on UI thread.
        /// Call this from the UI thread before any background-thread rendering may occur.
        /// </summary>
        public void EnsureInitialized()
        {
            try
            {
                _cachedPlayheadBrush = RenderingUtils.GetResourceBrush("VelocityIndicatorBrush", "#FFFF0000");
                _cachedPlayheadPen = new Pen(_cachedPlayheadBrush, 2);

                if (_cachedPlayheadBrush is Avalonia.Media.SolidColorBrush scb)
                {
                    var c = scb.Color;
                    _playheadA = c.A; _playheadR = c.R; _playheadG = c.G; _playheadB = c.B;
                }
                else
                {
                    var c = Avalonia.Media.Colors.Transparent;
                    _playheadA = c.A; _playheadR = c.R; _playheadG = c.G; _playheadB = c.B;
                }
                _initialized = true;
            }
            catch { /* swallow to be best-effort */ }
        }

        /// <summary>
        /// 清除画笔缓存（主题变更时调用）
        /// </summary>
        public void ClearCache()
        {
            _cachedPlayheadBrush = null;
            _cachedPlayheadPen = null;
            _playheadA = _playheadR = _playheadG = _playheadB = 0;
            _initialized = false;
        }
    }
}