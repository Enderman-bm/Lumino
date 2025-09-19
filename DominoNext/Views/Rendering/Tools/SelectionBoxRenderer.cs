using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Utils;
using System;

namespace Lumino.Views.Rendering.Tools
{
    /// <summary>
    /// ѡ�����Ⱦ��
    /// </summary>
    public class SelectionBoxRenderer
    {
        /// <summary>
        /// ��Ⱦѡ���
        /// </summary>
        public void Render(DrawingContext context, PianoRollViewModel viewModel)
        {
            // ����Ƿ����ڽ���ѡ���Լ���ʼ�ͽ����Ƿ񶼴���
            if (!viewModel.SelectionState.IsSelecting || 
                viewModel.SelectionStart == null || 
                viewModel.SelectionEnd == null) 
                return;

            var start = viewModel.SelectionStart.Value;
            var end = viewModel.SelectionEnd.Value;

            var x = Math.Min(start.X, end.X);
            var y = Math.Min(start.Y, end.Y);
            var width = Math.Abs(end.X - start.X);
            var height = Math.Abs(end.Y - start.Y);

            // ֻ�е�ѡ�����һ����Сʱ����Ⱦ�����ⵥ��ʱ��ʾ̫С�Ŀ�
            if (width > 2 || height > 2)
            {
                var selectionRect = new Rect(x, y, width, height);
                
                // ʹ����Դ�е�ѡ�����ɫ
                var selectionBrush = RenderingUtils.GetResourceBrush("SelectionBrush", "#800099FF");
                var selectionPen = RenderingUtils.GetResourcePen("SelectionBrush", "#FF0099FF", 2);
                
                context.DrawRectangle(selectionBrush, selectionPen, selectionRect);
                
                // ���ӵ������
                System.Diagnostics.Debug.WriteLine($"��Ⱦѡ���: {selectionRect}, IsSelecting: {viewModel.SelectionState.IsSelecting}");
            }
        }
    }
}