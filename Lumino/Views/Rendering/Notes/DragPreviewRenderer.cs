using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Utils;

namespace Lumino.Views.Rendering.Notes
{
    /// <summary>
    /// 拖拽预览渲染器 - 优化版本，支持缓存加速
    /// </summary>
    public class DragPreviewRenderer
    {
        // 圆角半径
        private const double CORNER_RADIUS = 3.0;

        // 缓存 - 用于拖拽画刷缓存
        private IBrush? _cachedDragBrush;
        private IPen? _cachedDragPen;

        /// <summary>
        /// 获取缓存的拖拽画刷
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
        /// 渲染拖拽预览效果 - 优化版本
        /// </summary>
        public void Render(DrawingContext context, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect)
        {
            if (viewModel.DragState.DraggingNotes == null || viewModel.DragState.DraggingNotes.Count == 0) return;

            var draggingNotes = viewModel.DragState.DraggingNotes;
            
            // 使用缓存的画刷
            var dragBrush = GetCachedDragBrush();
            var dragPen = GetCachedDragPen();
            
            // 直接渲染所有复制的分散处理
            foreach (var note in draggingNotes)
            {
                var noteRect = calculateNoteRect(note);
                if (noteRect.Width > 0 && noteRect.Height > 0)
                {
                    // 渲染圆角矩形
                    var roundedRect = new RoundedRect(noteRect, CORNER_RADIUS);
                    context.DrawRectangle(dragBrush, dragPen, roundedRect);
                    
                    // 只为足够大的音符显示文本
                    if (noteRect.Width > 25 && noteRect.Height > 8)
                    {
                        // 使用优化的文本渲染器
                        NoteTextRenderer.DrawNotePitchText(context, note.Pitch, noteRect);
                    }
                }
            }
        }

        /// <summary>
        /// 清除缓存画刷（当需要重新加载时使用）
        /// </summary>
        public void ClearCache()
        {
            _cachedDragBrush = null;
            _cachedDragPen = null;
        }
    }
}