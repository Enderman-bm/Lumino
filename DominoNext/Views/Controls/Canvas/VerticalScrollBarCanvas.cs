using System;
using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor.Components;

namespace DominoNext.Views.Controls.Canvas
{
    /// <summary>
    /// 纵向自定义滚动条Canvas
    /// </summary>
    public class VerticalScrollBarCanvas : CustomScrollBarCanvas
    {
        #region 构造函数
        public VerticalScrollBarCanvas()
        {
            Width = 20; // 默认宽度
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
        }
        #endregion

        #region 实现抽象方法
        protected override double GetPositionFromPoint(Point point)
        {
            return point.Y;
        }

        protected override Size GetDesiredSize(Size availableSize)
        {
            return new Size(20, availableSize.Height);
        }

        protected override double GetTrackLength(Size size)
        {
            return size.Height;
        }

        protected override Rect GetTrackRect(Rect bounds)
        {
            var trackWidth = Math.Min(bounds.Width * 0.6, 8);
            var trackX = (bounds.Width - trackWidth) / 2;
            return new Rect(trackX, 0, trackWidth, bounds.Height);
        }

        protected override Rect GetThumbRect(Rect bounds)
        {
            if (ViewModel == null) return new Rect();

            var trackWidth = Math.Min(bounds.Width * 0.8, 12);
            var trackX = (bounds.Width - trackWidth) / 2;
            
            var thumbY = ViewModel.ThumbPosition;
            var thumbHeight = ViewModel.ThumbLength;
            
            return new Rect(trackX, thumbY, trackWidth, thumbHeight);
        }
        #endregion

        #region 特殊渲染效果
        protected override void DrawThumb(DrawingContext context, Rect bounds)
        {
            base.DrawThumb(context, bounds);
            
            // 如果正在拖拽边缘，绘制边缘指示器
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
            var indicatorHeight = 2;

            if (ViewModel.IsDraggingStartEdge)
            {
                // 上边缘指示器
                var topIndicator = new Rect(thumbRect.Left - 2, thumbRect.Top - indicatorHeight / 2, 
                    thumbRect.Width + 4, indicatorHeight);
                context.FillRectangle(indicatorBrush, topIndicator);
            }

            if (ViewModel.IsDraggingEndEdge)
            {
                // 下边缘指示器
                var bottomIndicator = new Rect(thumbRect.Left - 2, thumbRect.Bottom - indicatorHeight / 2, 
                    thumbRect.Width + 4, indicatorHeight);
                context.FillRectangle(indicatorBrush, bottomIndicator);
            }
        }
        #endregion
    }
}