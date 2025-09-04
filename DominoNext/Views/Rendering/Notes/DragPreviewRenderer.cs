using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using DominoNext.Views.Rendering.Utils;

namespace DominoNext.Views.Rendering.Notes
{
    /// <summary>
    /// 拖拽预览渲染器 - 重构简化版本
    /// </summary>
    public class DragPreviewRenderer
    {
        /// <summary>
        /// 渲染拖拽预览效果 - 重构简化版本
        /// </summary>
        public void Render(DrawingContext context, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect)
        {
            if (viewModel.DragState.DraggingNotes == null || viewModel.DragState.DraggingNotes.Count == 0) return;

            var draggingNotes = viewModel.DragState.DraggingNotes;
            
            // 获取拖拽颜色资源
            var dragBrush = RenderingUtils.CreateBrushWithOpacity(
                RenderingUtils.GetResourceBrush("NoteDraggingBrush", "#FF2196F3"), 0.9);
            var dragPen = RenderingUtils.GetResourcePen("NoteDraggingPenBrush", "#FF1976D2", 2);
            
            // 直接渲染，避免复杂的分层处理
            foreach (var note in draggingNotes)
            {
                var noteRect = calculateNoteRect(note);
                if (noteRect.Width > 0 && noteRect.Height > 0)
                {
                    // 渲染音符体
                    context.DrawRectangle(dragBrush, dragPen, noteRect);
                    
                    // 只为足够大的音符显示文本
                    if (noteRect.Width > 25 && noteRect.Height > 8)
                    {
                        // 使用优化的音高文本绘制
                        NoteTextRenderer.DrawNotePitchText(context, note.Pitch, noteRect);
                    }
                }
            }
        }
    }
}