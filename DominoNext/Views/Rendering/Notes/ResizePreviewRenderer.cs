using System;
using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using DominoNext.Views.Rendering.Utils;

namespace DominoNext.Views.Rendering.Notes
{
    /// <summary>
    /// 调整大小预览渲染器
    /// </summary>
    public class ResizePreviewRenderer
    {
        /// <summary>
        /// 渲染调整大小预览效果
        /// </summary>
        public void Render(DrawingContext context, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect)
        {
            if (viewModel.ResizeState.ResizingNotes == null || viewModel.ResizeState.ResizingNotes.Count == 0) return;

            // 获取调整大小预览颜色 - 使用选中音符颜色的变体来表示调整大小状态
            var resizeBrush = RenderingUtils.CreateBrushWithOpacity(
                RenderingUtils.GetResourceBrush("NoteSelectedBrush", "#FFFF9800"), 0.8);
            var resizePen = RenderingUtils.GetResourcePen("NoteSelectedPenBrush", "#FFF57C00", 2);

            // 为每个正在调整大小的音符绘制预览
            foreach (var note in viewModel.ResizeState.ResizingNotes)
            {
                var noteRect = calculateNoteRect(note);
                if (noteRect.Width > 0 && noteRect.Height > 0)
                {
                    // 使用亮色标识调整大小预览，高透明度突出显示
                    context.DrawRectangle(resizeBrush, resizePen, noteRect);

                    // 显示当前时值信息，便于编辑者查看
                    if (noteRect.Width > 25 && noteRect.Height > 8)
                    {
                        var durationText = note.Duration.ToString();
                        NoteTextRenderer.DrawNoteText(context, durationText, noteRect, 10, useChineseFont: true);
                    }
                }
            }
        }
    }
}