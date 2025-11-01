using System;
using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Utils;
using Lumino.Views.Rendering.Adapters;

namespace Lumino.Views.Rendering.Tools
{
    /// <summary>
    /// 选择框渲染器
    /// </summary>
    public class SelectionBoxRenderer
    {
        /// <summary>
        /// 渲染选择框
        /// </summary>
        public void Render(DrawingContext context, PianoRollViewModel viewModel)
        {
            Render(context, null, viewModel);
        }

        /// <summary>
        /// 渲染选择框，支持Vulkan适配器
        /// </summary>
        public void Render(DrawingContext context, VulkanDrawingContextAdapter? vulkanAdapter, PianoRollViewModel viewModel)
        {
            // 检查是否正在进行选择以及起始和结束是否都存在
            if (!(viewModel.SelectionState?.IsSelecting ?? false) || 
                viewModel.SelectionStart == null || 
                viewModel.SelectionEnd == null) 
                return;

            var start = viewModel.SelectionStart.Value;
            var end = viewModel.SelectionEnd.Value;

            var x = Math.Min(start.X, end.X);
            var y = Math.Min(start.Y, end.Y);
            var width = Math.Abs(end.X - start.X);
            var height = Math.Abs(end.Y - start.Y);

            // 只有当选择框有一定大小时才渲染，避免单点时显示太小的框
            if (width > 2 || height > 2)
            {
                var selectionRect = new Rect(x, y, width, height);
                
                // 使用资源中的选择框颜色
                var selectionBrush = RenderingUtils.GetResourceBrush("SelectionBrush", "#800099FF");
                var selectionPen = RenderingUtils.GetResourcePen("SelectionBrush", "#FF0099FF", 2);
                
                context.DrawRectangle(selectionBrush, selectionPen, selectionRect);
                
                // 添加调试输出
                System.Diagnostics.Debug.WriteLine($"渲染选择框: {selectionRect}, IsSelecting: {viewModel.SelectionState.IsSelecting}");
            }
        }
    }
}