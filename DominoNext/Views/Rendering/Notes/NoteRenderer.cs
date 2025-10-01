using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using DominoNext.Views.Rendering.Utils;

namespace DominoNext.Views.Rendering.Notes
{
    /// <summary>
    /// 音符渲染器 - 优化版本，支持缓存优化和性能优化
    /// </summary>
    public class NoteRenderer
    {
        private readonly Color _noteColor = Color.Parse("#4CAF50");
        private readonly IPen _noteBorderPen = new Pen(new SolidColorBrush(Color.Parse("#2E7D32")), 2);
        private readonly Color _selectedNoteColor = Color.Parse("#FF9800");
        private readonly IPen _selectedNoteBorderPen = new Pen(new SolidColorBrush(Color.Parse("#F57C00")), 2);
        private readonly Color _previewNoteColor = Color.Parse("#81C784");
        private readonly IPen _previewNoteBorderPen = new Pen(new SolidColorBrush(Color.Parse("#66BB6A")), 2);

        // 圆角半径
        private const double CORNER_RADIUS = 3.0;

        // 缓存 - 用于透明度缓存，减少重复创建
        private readonly Dictionary<double, IBrush> _normalBrushCache = new();
        private readonly Dictionary<double, IBrush> _selectedBrushCache = new();
        
        // 阴影画笔 - 用于实现
        private readonly IBrush _shadowBrush = new SolidColorBrush(Colors.Black, 0.2);
        
        // 性能优化选项
        private bool _enableShadowEffect = true; // 控制是否启用阴影效果
        private int _shadowThreshold = 2000; // 当音符数量超过此值时禁用阴影（优化性能）

        /// <summary>
        /// 设置是否启用阴影效果，用于性能优化
        /// </summary>
        public void SetShadowEnabled(bool enabled)
        {
            _enableShadowEffect = enabled;
        }

        /// <summary>
        /// 设置阴影效果启用的阈值
        /// </summary>
        public void SetShadowThreshold(int threshold)
        {
            _shadowThreshold = threshold;
        }

        /// <summary>
        /// 渲染音符集合
        /// </summary>
        public void RenderNotes(DrawingContext context, PianoRollViewModel viewModel, Dictionary<NoteViewModel, Rect> visibleNoteCache)
        {
            int drawnNotes = 0;
            int totalNotes = visibleNoteCache.Count;
            
            // 动态判断是否启用阴影效果
            bool shouldRenderShadow = _enableShadowEffect && totalNotes <= _shadowThreshold;
            
            foreach (var kvp in visibleNoteCache)
            {
                var note = kvp.Key;
                var rect = kvp.Value;

                if (rect.Width > 0 && rect.Height > 0)
                {
                    // 检查音符是否正在被拖拽或调整大小，使用较高的透明度来确保可见
                    bool isBeingDragged = viewModel.DragState.IsDragging && viewModel.DragState.DraggingNotes.Contains(note);
                    bool isBeingResized = viewModel.ResizeState.IsResizing && viewModel.ResizeState.ResizingNotes.Contains(note);
                    bool isBeingManipulated = isBeingDragged || isBeingResized;
                    
                    DrawNote(context, note, rect, isBeingManipulated, shouldRenderShadow);
                    drawnNotes++;
                }
            }

            System.Diagnostics.Debug.WriteLine($"渲染了 {drawnNotes} 个可见音符，阴影效果: {(shouldRenderShadow ? "启用" : "禁用")}");
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
                var roundedRect = new RoundedRect(previewRect, CORNER_RADIUS);
                context.DrawRectangle(brush, _previewNoteBorderPen, roundedRect);

                var durationText = viewModel.PreviewNote.Duration.ToString();
                if (previewRect.Width > 30 && previewRect.Height > 10)
                {
                    NoteTextRenderer.DrawNoteText(context, durationText, previewRect, 9);
                }
            }
        }

        /// <summary>
        /// 绘制单个音符 - 优化版本，使用缓存
        /// </summary>
        private void DrawNote(DrawingContext context, NoteViewModel note, Rect rect, bool isBeingManipulated = false, bool renderShadow = true)
        {
            var opacity = Math.Max(0.7, note.Velocity / 127.0);
            
            // 当音符正在被操作时使用更高的透明度，使其更加可见
            if (isBeingManipulated)
            {
                opacity = Math.Min(1.0, opacity * 1.1); // 操作时增加透明度，增强视觉效果
            }

            IBrush brush;
            IPen pen;

            if (note.IsSelected)
            {
                // 使用缓存选中状态画刷
                brush = GetCachedSelectedBrush(opacity);
                pen = _selectedNoteBorderPen;
            }
            else
            {
                // 使用缓存普通画刷
                brush = GetCachedNormalBrush(opacity);
                pen = _noteBorderPen;
            }

            // 为拖拽中的音符添加微弱阴影效果，增强层次感
            if (isBeingManipulated && renderShadow)
            {
                var shadowOffset = new Vector(1, 1);
                var shadowRect = new RoundedRect(rect.Translate(shadowOffset), CORNER_RADIUS);
                context.DrawRectangle(_shadowBrush, null, shadowRect);
            }

            // 使用圆角矩形绘制音符
            var roundedRect = new RoundedRect(rect, CORNER_RADIUS);
            context.DrawRectangle(brush, pen, roundedRect);
        }

        /// <summary>
        /// 获取缓存的普通状态画刷
        /// </summary>
        private IBrush GetCachedNormalBrush(double opacity)
        {
            // 将透明度量化到0.1精度，减少缓存对象数量
            var quantizedOpacity = Math.Round(opacity, 1);
            
            if (!_normalBrushCache.TryGetValue(quantizedOpacity, out var brush))
            {
                brush = new SolidColorBrush(_noteColor, quantizedOpacity);
                _normalBrushCache[quantizedOpacity] = brush;
            }
            
            return brush;
        }

        /// <summary>
        /// 获取缓存的选中状态画刷
        /// </summary>
        private IBrush GetCachedSelectedBrush(double opacity)
        {
            // 将透明度量化到0.1精度，减少缓存对象数量
            var quantizedOpacity = Math.Round(opacity, 1);
            
            if (!_selectedBrushCache.TryGetValue(quantizedOpacity, out var brush))
            {
                brush = new SolidColorBrush(_selectedNoteColor, quantizedOpacity);
                _selectedBrushCache[quantizedOpacity] = brush;
            }
            
            return brush;
        }

        /// <summary>
        /// 清除缓存画刷（当需要重新加载时使用）
        /// </summary>
        public void ClearBrushCache()
        {
            _normalBrushCache.Clear();
            _selectedBrushCache.Clear();
        }
    }
}