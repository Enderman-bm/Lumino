using System;
using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Utils;
using Lumino.Views.Rendering.Adapters;

namespace Lumino.Views.Rendering.Notes
{
    /// <summary>
    /// Vulkan创建音符渲染器 - 仅使用Vulkan渲染，删除Skia回退逻辑
    /// </summary>
    public class CreatingNoteRenderer
    {
        // 圆角半径
        private const double CORNER_RADIUS = 3.0;

        /// <summary>
        /// 渲染正在创建的音符 - 已废弃，使用Vulkan版本
        /// </summary>
        [Obsolete("请使用带Vulkan适配器的重载方法")]
        public void Render(DrawingContext context, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect)
        {
            // 创建Vulkan适配器并调用Vulkan版本
            var vulkanAdapter = new VulkanDrawingContextAdapter(context);
            try
            {
                RenderVulkan(vulkanAdapter, viewModel, calculateNoteRect, context);
            }
            finally
            {
                vulkanAdapter.Dispose();
            }
        }

        /// <summary>
        /// Vulkan优化的创建音符渲染
        /// </summary>
        public void Render(DrawingContext context, VulkanDrawingContextAdapter? vulkanAdapter, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect)
        {
            // 始终使用Vulkan适配器
            vulkanAdapter ??= new VulkanDrawingContextAdapter(context);
            RenderVulkan(vulkanAdapter, viewModel, calculateNoteRect, context);
        }
        
        /// <summary>
        /// Vulkan优化的创建音符渲染核心逻辑
        /// </summary>
        private void RenderVulkan(VulkanDrawingContextAdapter vulkanAdapter, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect, DrawingContext? context = null)
        {
            if (viewModel.CreatingNote == null || !viewModel.CreationModule.IsCreatingNote) return;

            var creatingRect = calculateNoteRect(viewModel.CreatingNote);
            if (creatingRect.Width > 0 && creatingRect.Height > 0)
            {
                // 使用预设的颜色和透明度微调一些参数来表示创建状态
                var brush = RenderingUtils.CreateBrushWithOpacity(
                    RenderingUtils.GetResourceBrush("NotePreviewBrush", "#804CAF50"), 0.85);
                var pen = RenderingUtils.GetResourcePen("NotePreviewPenBrush", "#FF689F38", 2);
                
                // 使用Vulkan渲染圆角矩形
                var roundedRect = new RoundedRect(creatingRect, CORNER_RADIUS);
                vulkanAdapter.DrawRectangle(brush, pen, roundedRect);

                // 显示当前时值信息
                if (creatingRect.Width > 30 && creatingRect.Height > 10)
                {
                    var durationText = viewModel.CreatingNote.Duration.ToString();
                    if (context != null)
                    {
                        NoteTextRenderer.DrawNoteText(context, durationText, creatingRect, 11);
                    }
                }
            }
            
            // 刷新批处理
            vulkanAdapter.FlushBatches();
        }
    }
}