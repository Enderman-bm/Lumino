using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using DominoNext.Views.Rendering.Utils;

namespace DominoNext.Views.Rendering.Notes
{
    /// <summary>
    /// 拖拽预览渲染器 - 性能优化版本，支持画笔复用
    /// </summary>
    public class DragPreviewRenderer
    {
        // 画笔缓存 - 复用拖拽相关的画笔
        private IBrush? _cachedDragBrush;
        private IPen? _cachedDragPen;

        /// <summary>
        /// 获取缓存的拖拽画笔
        /// </summary>
        private IBrush GetCachedDragBrush()
        {
            return _cachedDragBrush ??= RenderingUtils.CreateBrushWithOpacity(
                RenderingUtils.GetResourceBrush("NoteDraggingBrush", "#FF2196F3"), 0.9);
        }

        /// <summary>
        /// 获取缓存的拖拽边框画笔
        /// </summary>
        private IPen GetCachedDragPen()
        {
            return _cachedDragPen ??= RenderingUtils.GetResourcePen("NoteDraggingPenBrush", "#FF1976D2", 2);
        }

        /// <summary>
        /// 渲染拖拽预览效果 - 性能优化版本
        /// </summary>
        public void Render(DrawingContext context, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect)
        {
            if (viewModel.DragState.DraggingNotes == null || viewModel.DragState.DraggingNotes.Count == 0) return;

            var draggingNotes = viewModel.DragState.DraggingNotes;
            
            // 使用缓存的画笔
            var dragBrush = GetCachedDragBrush();
            var dragPen = GetCachedDragPen();
            
            // 直接渲染，避免复杂的分层处理
            foreach (var note in draggingNotes)
            {
                var noteRect = calculateNoteRect(note);
                if (noteRect.Width > 0 && noteRect.Height > 0)
                {
                    // 渲染音符
                    context.DrawRectangle(dragBrush, dragPen, noteRect);
                    
                    // 只为足够大的音符显示文本
                    if (noteRect.Width > 25 && noteRect.Height > 8)
                    {
                        // 使用优化的音符文本渲染
                        NoteTextRenderer.DrawNotePitchText(context, note.Pitch, noteRect);
                    }
                }
            }
        }

        /// <summary>
        /// 清除画笔缓存（主题变更时调用）
        /// </summary>
        public void ClearCache()
        {
            _cachedDragBrush = null;
            _cachedDragPen = null;
        }
    }
}