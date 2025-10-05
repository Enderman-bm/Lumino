using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Utils;
using Lumino.Views.Rendering.Adapters;

namespace Lumino.Views.Rendering.Notes
{
    /// <summary>
    /// 调整大小预览渲染器
    /// </summary>
    public class ResizePreviewRenderer
    {
        // 圆角半径
        private const double CORNER_RADIUS = 3.0;

        /// <summary>
        /// 渲染调整大小预览效果
        /// </summary>
        public void Render(DrawingContext context, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect)
        {
            Render(context, null, viewModel, calculateNoteRect);
        }

        /// <summary>
        /// 渲染调整大小预览效果，支持Vulkan适配器
        /// </summary>
        public void Render(DrawingContext context, VulkanDrawingContextAdapter? vulkanAdapter, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect)
        {
            if (viewModel.ResizeState.ResizingNotes == null || viewModel.ResizeState.ResizingNotes.Count == 0) return;

            // 获取调整大小预览颜色 - 使用选中状态颜色的变体来表示调整大小状态
            var resizeBrush = RenderingUtils.CreateBrushWithOpacity(
                RenderingUtils.GetResourceBrush("NoteSelectedBrush", "#FFFF9800"), 0.8);
            var resizePen = RenderingUtils.GetResourcePen("NoteSelectedPenBrush", "#FFF57C00", 2);

            // 为每个音符的调整大小操作绘制预览
            foreach (var note in viewModel.ResizeState.ResizingNotes)
            {
                var noteRect = calculateNoteRect(note);
                if (noteRect.Width > 0 && noteRect.Height > 0)
                {
                    // 使用圆角矩形标识调整大小预览，增加透明度突出显示
                    var roundedRect = new RoundedRect(noteRect, CORNER_RADIUS);
                    context.DrawRectangle(resizeBrush, resizePen, roundedRect);

                    // 显示当前时值信息，供编辑者查看
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