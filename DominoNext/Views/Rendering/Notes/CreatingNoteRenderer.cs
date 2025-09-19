using System;
using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Utils;

namespace Lumino.Views.Rendering.Notes
{
    /// <summary>
    /// 创建音符渲染器
    /// </summary>
    public class CreatingNoteRenderer
    {
        // 圆角半径
        private const double CORNER_RADIUS = 3.0;

        /// <summary>
        /// 渲染正在创建的音符
        /// </summary>
        public void Render(DrawingContext context, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect)
        {
            if (viewModel.CreatingNote == null || !viewModel.CreationModule.IsCreatingNote) return;

            var creatingRect = calculateNoteRect(viewModel.CreatingNote);
            if (creatingRect.Width > 0 && creatingRect.Height > 0)
            {
                // 使用预设的颜色和透明度微调一些参数来表示创建状态
                var brush = RenderingUtils.CreateBrushWithOpacity(
                    RenderingUtils.GetResourceBrush("NotePreviewBrush", "#804CAF50"), 0.85);
                var pen = RenderingUtils.GetResourcePen("NotePreviewPenBrush", "#FF689F38", 2);
                
                var roundedRect = new RoundedRect(creatingRect, CORNER_RADIUS);
                context.DrawRectangle(brush, pen, roundedRect);

                // 显示当前时值信息
                if (creatingRect.Width > 30 && creatingRect.Height > 10)
                {
                    var durationText = viewModel.CreatingNote.Duration.ToString();
                    NoteTextRenderer.DrawNoteText(context, durationText, creatingRect, 11);
                }
            }
        }
    }
}