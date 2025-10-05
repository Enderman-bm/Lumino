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
    /// Vulkan拖拽预览渲染器 - 仅使用Vulkan渲染，删除Skia回退逻辑
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
        /// 渲染拖拽预览效果 - 已废弃，使用Vulkan版本
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
        /// Vulkan优化的拖拽预览渲染
        /// </summary>
        public void Render(DrawingContext context, VulkanDrawingContextAdapter? vulkanAdapter, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect)
        {
            // 始终使用Vulkan适配器
            vulkanAdapter ??= new VulkanDrawingContextAdapter(context);
            RenderVulkan(vulkanAdapter, viewModel, calculateNoteRect, context);
        }
        
        /// <summary>
        /// Vulkan优化的拖拽预览渲染核心逻辑
        /// </summary>
        private void RenderVulkan(VulkanDrawingContextAdapter vulkanAdapter, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect, DrawingContext? context = null)
        {
            if (viewModel.DragState.DraggingNotes == null || viewModel.DragState.DraggingNotes.Count == 0) return;

            var draggingNotes = viewModel.DragState.DraggingNotes;
            
            // 使用缓存的画刷
            var dragBrush = GetCachedDragBrush();
            var dragPen = GetCachedDragPen();
            
            // Vulkan批量处理所有拖拽音符
            foreach (var note in draggingNotes)
            {
                var noteRect = calculateNoteRect(note);
                if (noteRect.Width > 0 && noteRect.Height > 0)
                {
                    // 使用Vulkan渲染圆角矩形
                    var roundedRect = new RoundedRect(noteRect, CORNER_RADIUS);
                    vulkanAdapter.DrawRectangle(dragBrush, dragPen, roundedRect);
                    
                    // 只为足够大的音符显示文本
                    if (noteRect.Width > 25 && noteRect.Height > 8)
                    {
                        // 使用Vulkan文本渲染器
                        if (context != null)
                        {
                            NoteTextRenderer.DrawNotePitchText(context, note.Pitch, noteRect);
                        }
                    }
                }
            }
            
            // 刷新批处理
            vulkanAdapter.FlushBatches();
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