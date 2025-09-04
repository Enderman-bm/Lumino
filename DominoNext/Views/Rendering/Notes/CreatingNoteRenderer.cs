using System;
using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using DominoNext.Views.Rendering.Utils;

namespace DominoNext.Views.Rendering.Notes
{
    /// <summary>
    /// 创建音符渲染器
    /// </summary>
    public class CreatingNoteRenderer
    {
        /// <summary>
        /// 渲染正在创建的音符
        /// </summary>
        public void Render(DrawingContext context, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect)
        {
            if (viewModel.CreatingNote == null || !viewModel.CreationModule.IsCreatingNote) return;

            var creatingRect = calculateNoteRect(viewModel.CreatingNote);
            if (creatingRect.Width > 0 && creatingRect.Height > 0)
            {
                // 使用预览音符颜色，但透明度稍微高一些以区分正在创建的状态
                var brush = RenderingUtils.CreateBrushWithOpacity(
                    RenderingUtils.GetResourceBrush("NotePreviewBrush", "#804CAF50"), 0.85);
                var pen = RenderingUtils.GetResourcePen("NotePreviewPenBrush", "#FF689F38", 2);
                
                context.DrawRectangle(brush, pen, creatingRect);

                // 显示当前音符信息
                if (creatingRect.Width > 30 && creatingRect.Height > 10)
                {
                    var durationText = viewModel.CreatingNote.Duration.ToString();
                    NoteTextRenderer.DrawNoteText(context, durationText, creatingRect, 11);
                }
            }
        }
    }
}