using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using DominoNext.Views.Rendering.Utils;

namespace DominoNext.Views.Rendering.Notes
{
    /// <summary>
    /// 音符渲染器 - 优化版本，支持画笔复用和性能优化
    /// </summary>
    public class NoteRenderer
    {
        private readonly Color _noteColor = Color.Parse("#4CAF50");
        private readonly IPen _noteBorderPen = new Pen(new SolidColorBrush(Color.Parse("#2E7D32")), 2);
        private readonly Color _selectedNoteColor = Color.Parse("#FF9800");
        private readonly IPen _selectedNoteBorderPen = new Pen(new SolidColorBrush(Color.Parse("#F57C00")), 2);
        private readonly Color _previewNoteColor = Color.Parse("#81C784");
        private readonly IPen _previewNoteBorderPen = new Pen(new SolidColorBrush(Color.Parse("#66BB6A")), 2);

        // 画笔缓存 - 按透明度级别缓存，避免重复创建
        private readonly Dictionary<double, IBrush> _normalBrushCache = new();
        private readonly Dictionary<double, IBrush> _selectedBrushCache = new();
        
        // 阴影画笔 - 复用单个实例
        private readonly IBrush _shadowBrush = new SolidColorBrush(Colors.Black, 0.2);
        
        // 性能优化配置
        private bool _enableShadowEffect = true; // 可配置是否启用阴影效果
        private int _shadowThreshold = 10000; // 当音符数量超过此阈值时禁用阴影

        /// <summary>
        /// 设置是否启用阴影效果（用于性能优化）
        /// </summary>
        public void SetShadowEnabled(bool enabled)
        {
            _enableShadowEffect = enabled;
        }

        /// <summary>
        /// 设置阴影效果的音符数量阈值
        /// </summary>
        public void SetShadowThreshold(int threshold)
        {
            _shadowThreshold = threshold;
        }

        /// <summary>
        /// 渲染所有音符
        /// </summary>
        public void RenderNotes(DrawingContext context, PianoRollViewModel viewModel, Dictionary<NoteViewModel, Rect> visibleNoteCache)
        {
            int drawnNotes = 0;
            int totalNotes = visibleNoteCache.Count;
            
            // 动态决定是否启用阴影效果
            bool shouldRenderShadow = _enableShadowEffect && totalNotes <= _shadowThreshold;
            
            foreach (var kvp in visibleNoteCache)
            {
                var note = kvp.Key;
                var rect = kvp.Value;

                if (rect.Width > 0 && rect.Height > 0)
                {
                    // 如果音符正在被拖拽或调整大小，使用较淡的颜色渲染原始位置
                    bool isBeingDragged = viewModel.DragState.IsDragging && viewModel.DragState.DraggingNotes.Contains(note);
                    bool isBeingResized = viewModel.ResizeState.IsResizing && viewModel.ResizeState.ResizingNotes.Contains(note);
                    bool isBeingManipulated = isBeingDragged || isBeingResized;
                    
                    DrawNote(context, note, rect, isBeingManipulated, shouldRenderShadow);
                    drawnNotes++;
                }
            }

            System.Diagnostics.Debug.WriteLine($"绘制了 {drawnNotes} 个可见音符，阴影效果: {(shouldRenderShadow ? "启用" : "禁用")}");
        }

        /// <summary>
        /// 渲染预览音符
        /// </summary>
        public void RenderPreviewNote(DrawingContext context, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect)
        {
            if (viewModel.PreviewNote == null) return;

            var previewRect = calculateNoteRect(viewModel.PreviewNote);
            if (previewRect.Width > 0 && previewRect.Height > 0)
            {
                var brush = new SolidColorBrush(_previewNoteColor, 0.6);
                context.DrawRectangle(brush, _previewNoteBorderPen, previewRect);

                var durationText = viewModel.PreviewNote.Duration.ToString();
                if (previewRect.Width > 30 && previewRect.Height > 10)
                {
                    NoteTextRenderer.DrawNoteText(context, durationText, previewRect, 9);
                }
            }
        }

        /// <summary>
        /// 绘制单个音符 - 优化版本，使用画笔缓存
        /// </summary>
        private void DrawNote(DrawingContext context, NoteViewModel note, Rect rect, bool isBeingManipulated = false, bool renderShadow = true)
        {
            var opacity = Math.Max(0.7, note.Velocity / 127.0);
            
            // 正在操作的音符使用更高的透明度，保持清晰可见
            if (isBeingManipulated)
            {
                opacity = Math.Min(1.0, opacity * 1.1); // 提高到接近不透明，保持视觉连续性
            }

            IBrush brush;
            IPen pen;

            if (note.IsSelected)
            {
                // 使用缓存的选中状态画笔
                brush = GetCachedSelectedBrush(opacity);
                pen = _selectedNoteBorderPen;
            }
            else
            {
                // 使用缓存的普通画笔
                brush = GetCachedNormalBrush(opacity);
                pen = _noteBorderPen;
            }

            // 为拖拽中的音符添加轻微的阴影效果（仅在性能允许时）
            if (isBeingManipulated && renderShadow)
            {
                var shadowOffset = new Vector(1, 1);
                var shadowRect = rect.Translate(shadowOffset);
                context.DrawRectangle(_shadowBrush, null, shadowRect);
            }

            context.DrawRectangle(brush, pen, rect);
        }

        /// <summary>
        /// 获取缓存的普通状态画笔
        /// </summary>
        private IBrush GetCachedNormalBrush(double opacity)
        {
            // 将透明度量化到最接近的0.1级别，减少缓存条目数量
            var quantizedOpacity = Math.Round(opacity, 1);
            
            if (!_normalBrushCache.TryGetValue(quantizedOpacity, out var brush))
            {
                brush = new SolidColorBrush(_noteColor, quantizedOpacity);
                _normalBrushCache[quantizedOpacity] = brush;
            }
            
            return brush;
        }

        /// <summary>
        /// 获取缓存的选中状态画笔
        /// </summary>
        private IBrush GetCachedSelectedBrush(double opacity)
        {
            // 将透明度量化到最接近的0.1级别，减少缓存条目数量
            var quantizedOpacity = Math.Round(opacity, 1);
            
            if (!_selectedBrushCache.TryGetValue(quantizedOpacity, out var brush))
            {
                brush = new SolidColorBrush(_selectedNoteColor, quantizedOpacity);
                _selectedBrushCache[quantizedOpacity] = brush;
            }
            
            return brush;
        }

        /// <summary>
        /// 清除画笔缓存（在主题变更时调用）
        /// </summary>
        public void ClearBrushCache()
        {
            _normalBrushCache.Clear();
            _selectedBrushCache.Clear();
        }
    }
}