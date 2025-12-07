using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Lumino.Services.Interfaces;
using Lumino.Views.Rendering.Data;
using Lumino.Views.Rendering.Adapters;
using Lumino.Views.Rendering.Vulkan;
using EnderDebugger;

namespace Lumino.Views.Rendering.Notes
{
    /// <summary>
    /// 虚拟化音符渲染器 - 专门用于渲染大量音符（百万级别）
    /// 直接使用NoteData结构体，避免NoteViewModel的开销
    /// 使用批量渲染和GPU实例化技术
    /// </summary>
    public class VirtualizedNoteRenderer
    {
        private readonly EnderLogger _logger = EnderLogger.Instance;
        private readonly OptimizedViewportCuller _culler = new();

        // 颜色定义
        private readonly Color _noteColor = Color.Parse("#4CAF50");
        private readonly Color _noteBorderColor = Color.Parse("#2E7D32");
        private readonly Color _selectedNoteColor = Color.Parse("#FF9800");
        private readonly Color _selectedNoteBorderColor = Color.Parse("#F57C00");

        // 圆角半径
        private const double CornerRadius = 3.0;
        private const double MinNoteWidth = 4.0;

        // 画刷和笔缓存
        private SolidColorBrush? _normalBrush;
        private SolidColorBrush? _selectedBrush;
        private Pen? _normalPen;
        private Pen? _selectedPen;

        // 渲染批次
        private readonly NoteRenderBatch _normalBatch = new(8192);
        private readonly NoteRenderBatch _selectedBatch = new(1024);

        // 性能统计
        private int _lastRenderedCount;
        private double _lastRenderTimeMs;

        /// <summary>
        /// 上次渲染的音符数量
        /// </summary>
        public int LastRenderedCount => _lastRenderedCount;

        /// <summary>
        /// 上次渲染耗时（毫秒）
        /// </summary>
        public double LastRenderTimeMs => _lastRenderTimeMs;

        /// <summary>
        /// 渲染虚拟化音符数据
        /// </summary>
        /// <param name="context">绘制上下文</param>
        /// <param name="notes">音符数据列表</param>
        /// <param name="viewport">视口范围</param>
        /// <param name="baseQuarterNoteWidth">四分音符像素宽度</param>
        /// <param name="keyHeight">键高</param>
        /// <param name="scrollX">水平滚动偏移</param>
        /// <param name="scrollY">垂直滚动偏移</param>
        /// <param name="currentTrackIndex">当前轨道索引</param>
        public void RenderNotes(
            DrawingContext context,
            IReadOnlyList<NoteData> notes,
            Rect viewport,
            double baseQuarterNoteWidth,
            double keyHeight,
            double scrollX,
            double scrollY,
            int currentTrackIndex)
        {
            if (notes == null || notes.Count == 0)
                return;

            var startTime = DateTime.Now;

            try
            {
                // 确保画刷和笔已初始化
                EnsureBrushesInitialized();

                // 更新剔除器视口
                bool viewportChanged = _culler.UpdateViewport(viewport, baseQuarterNoteWidth, keyHeight, scrollX, scrollY, currentTrackIndex);

                // 执行剔除（使用缓存结果）
                var visibleNotes = _culler.CullNotes(notes);

                // 清空批次
                _normalBatch.Clear();

                // 将可见音符添加到批次
                for (int i = 0; i < visibleNotes.Count; i++)
                {
                    var (note, screenRect) = visibleNotes[i];
                    
                    var renderData = new NoteRenderData(note);
                    renderData.ScreenX = (float)screenRect.X;
                    renderData.ScreenY = (float)screenRect.Y;
                    renderData.ScreenWidth = (float)screenRect.Width;
                    renderData.ScreenHeight = (float)screenRect.Height;

                    _normalBatch.TryAdd(renderData);
                }

                // 批量绘制所有音符
                DrawBatch(context, _normalBatch, _normalBrush!, _normalPen!);

                _lastRenderedCount = visibleNotes.Count;
            }
            catch (Exception ex)
            {
                _logger.Error("VirtualizedNoteRenderer", $"渲染失败: {ex.Message}");
            }
            finally
            {
                _lastRenderTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
            }
        }

        /// <summary>
        /// 批量绘制音符
        /// </summary>
        private void DrawBatch(DrawingContext context, NoteRenderBatch batch, IBrush brush, IPen pen)
        {
            if (batch.Count == 0)
                return;

            // 使用单个笔刷批量绘制所有矩形
            for (int i = 0; i < batch.Count; i++)
            {
                ref var note = ref batch.Notes[i];
                var rect = new Rect(note.ScreenX, note.ScreenY, note.ScreenWidth, note.ScreenHeight);
                
                // 绘制圆角矩形
                context.DrawRectangle(brush, pen, new RoundedRect(rect, CornerRadius));
            }
        }

        /// <summary>
        /// 确保画刷已初始化
        /// </summary>
        private void EnsureBrushesInitialized()
        {
            _normalBrush ??= new SolidColorBrush(_noteColor);
            _selectedBrush ??= new SolidColorBrush(_selectedNoteColor);
            _normalPen ??= new Pen(new SolidColorBrush(_noteBorderColor), 1);
            _selectedPen ??= new Pen(new SolidColorBrush(_selectedNoteBorderColor), 1);
        }

        /// <summary>
        /// 清理缓存
        /// </summary>
        public void ClearCache()
        {
            _normalBatch.Clear();
            _selectedBatch.Clear();
            _culler.ClearCache();
        }

        /// <summary>
        /// 获取剔除统计
        /// </summary>
        public (int total, int visible, int culled, double efficiency) GetCullingStats()
        {
            return _culler.GetStats();
        }
    }
}