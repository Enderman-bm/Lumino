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
        public DragPreviewRenderer()
        {
            RenderingUtils.BrushCacheCleared += OnGlobalBrushCacheCleared;
        }

        private void OnGlobalBrushCacheCleared()
        {
            try { ClearCache(); } catch { }
        }
        // 圆角半径
        private const double CORNER_RADIUS = 3.0;

    // 缓存 - 用于拖拽画刷缓存
    private IBrush? _cachedDragBrush;
    private IPen? _cachedDragPen;
    private byte _dragA, _dragR, _dragG, _dragB;

        /// <summary>
        /// 获取缓存的拖拽画刷
        /// </summary>
        private IBrush GetCachedDragBrush()
        {
            if (_cachedDragBrush == null) throw new InvalidOperationException("DragPreviewRenderer not initialized. Call EnsureInitialized() on UI thread before using.");
            return _cachedDragBrush;
        }

        /// <summary>
        /// 获取缓存的拖拽边框画笔
        /// </summary>
        private IPen GetCachedDragPen()
        {
            if (_cachedDragPen == null) throw new InvalidOperationException("DragPreviewRenderer not initialized. Call EnsureInitialized() on UI thread before using.");
            return _cachedDragPen;
        }

        /// <summary>
        /// Ensure brushes/pens are created on UI thread and cache color bytes for Vulkan path.
        /// </summary>
        public void EnsureInitialized()
        {
            try
            {
                _cachedDragBrush = RenderingUtils.CreateBrushWithOpacity(RenderingUtils.GetResourceBrush("NoteDraggingBrush", "#FF2196F3"), 0.9);
                _cachedDragPen = RenderingUtils.GetResourcePen("NoteDraggingPenBrush", "#FF1976D2", 2);

                if (_cachedDragBrush is Avalonia.Media.SolidColorBrush scb)
                {
                    var c = scb.Color;
                    _dragA = c.A; _dragR = c.R; _dragG = c.G; _dragB = c.B;
                }
                else
                {
                    var c = Avalonia.Media.Colors.Transparent;
                    _dragA = c.A; _dragR = c.R; _dragG = c.G; _dragB = c.B;
                }
            }
            catch { }
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
            // 安全性检查
            if (vulkanAdapter == null || viewModel == null || calculateNoteRect == null)
            {
                EnderDebugger.EnderLogger.Instance.Warn("DragPreviewRenderer", "RenderVulkan收到null参数");
                return;
            }

            if (viewModel.DragState?.DraggingNotes == null || viewModel.DragState.DraggingNotes.Count == 0) 
                return;

            var draggingNotes = viewModel.DragState.DraggingNotes;
            
            // 使用缓存的画刷/画笔（EnsureInitialized() 应在 UI 线程上提前被调用以构建这些对象）
            var dragBrush = GetCachedDragBrush();
            var dragPen = GetCachedDragPen();
            
            // GPU批处理优化：收集所有矩形，统一渲染
            var rectangles = new List<RoundedRect>(draggingNotes.Count);
            var textRects = new List<(int pitch, Rect rect)>();
            
            foreach (var note in draggingNotes)
            {
                var noteRect = calculateNoteRect(note);
                if (noteRect.Width > 0 && noteRect.Height > 0)
                {
                    rectangles.Add(new RoundedRect(noteRect, CORNER_RADIUS));
                    
                    // 收集需要绘制文本的音符
                    if (noteRect.Width > 25 && noteRect.Height > 8 && context != null)
                    {
                        textRects.Add((note.Pitch, noteRect));
                    }
                }
            }
            
            // 批量渲染所有矩形
            if (rectangles.Count > 0)
            {
                vulkanAdapter.DrawRoundedRectsInstanced(rectangles, dragBrush, dragPen);
            }
            
            // 绘制文本
            if (context != null)
            {
                foreach (var (pitch, rect) in textRects)
                {
                    NoteTextRenderer.DrawNotePitchText(context, pitch, rect);
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