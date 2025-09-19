using System;
using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor.Components;

namespace Lumino.Views.Controls.Canvas
{
    /// <summary>
    /// �����Զ��������Canvas
    /// </summary>
    public class HorizontalScrollBarCanvas : CustomScrollBarCanvas
    {
        #region ���캯��
        public HorizontalScrollBarCanvas()
        {
            Height = 20; // Ĭ�ϸ߶�
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom;
        }
        #endregion

        #region ʵ�ֳ��󷽷�
        protected override double GetPositionFromPoint(Point point)
        {
            return point.X;
        }

        protected override Size GetDesiredSize(Size availableSize)
        {
            return new Size(availableSize.Width, 20);
        }

        protected override double GetTrackLength(Size size)
        {
            return size.Width;
        }

        protected override Rect GetTrackRect(Rect bounds)
        {
            var trackHeight = Math.Min(bounds.Height * 0.6, 8);
            var trackY = (bounds.Height - trackHeight) / 2;
            return new Rect(0, trackY, bounds.Width, trackHeight);
        }

        protected override Rect GetThumbRect(Rect bounds)
        {
            if (ViewModel == null) return new Rect();

            var trackHeight = Math.Min(bounds.Height * 0.8, 12);
            var trackY = (bounds.Height - trackHeight) / 2;
            
            var thumbX = ViewModel.ThumbPosition;
            var thumbWidth = ViewModel.ThumbLength;
            
            return new Rect(thumbX, trackY, thumbWidth, trackHeight);
        }
        #endregion

        #region ������ȾЧ��
        protected override void DrawThumb(DrawingContext context, Rect bounds)
        {
            base.DrawThumb(context, bounds);
            
            // ���������ק��Ե�����Ʊ�Եָʾ��
            if (ViewModel?.IsDraggingStartEdge == true || ViewModel?.IsDraggingEndEdge == true)
            {
                DrawEdgeIndicators(context, bounds);
            }
        }

        private void DrawEdgeIndicators(DrawingContext context, Rect bounds)
        {
            if (ViewModel == null) return;

            var thumbRect = GetThumbRect(bounds);
            var indicatorBrush = ThumbPressedBrush ?? Brushes.Blue;
            var indicatorWidth = 2;

            if (ViewModel.IsDraggingStartEdge)
            {
                // ���Եָʾ��
                var leftIndicator = new Rect(thumbRect.Left - indicatorWidth / 2, thumbRect.Top - 2, 
                    indicatorWidth, thumbRect.Height + 4);
                context.FillRectangle(indicatorBrush, leftIndicator);
            }

            if (ViewModel.IsDraggingEndEdge)
            {
                // �ұ�Եָʾ��
                var rightIndicator = new Rect(thumbRect.Right - indicatorWidth / 2, thumbRect.Top - 2, 
                    indicatorWidth, thumbRect.Height + 4);
                context.FillRectangle(indicatorBrush, rightIndicator);
            }
        }
        #endregion
    }
}