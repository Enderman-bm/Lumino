using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lumino.ViewModels;
using Lumino.Views.Rendering.Utils;
using System;

namespace Lumino.Views.Controls.Canvas
{
    /// <summary>
    /// ����������С�ڱ��⻭��
    /// </summary>
    public class TrackOverviewMeasureHeaderCanvas : Control
    {
        public static readonly StyledProperty<TrackOverviewViewModel?> ViewModelProperty =
            AvaloniaProperty.Register<TrackOverviewMeasureHeaderCanvas, TrackOverviewViewModel?>(nameof(ViewModel));

        public TrackOverviewViewModel? ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        // ���滭ˢ�ͻ��ʣ���������
        private readonly IBrush _backgroundBrush;
        private readonly IBrush _textBrush;
        private readonly IPen _separatorPen;
        private readonly IPen _measureLinePen;

        public TrackOverviewMeasureHeaderCanvas()
        {
            // ��ʼ��������Դ
            _backgroundBrush = RenderingUtils.GetResourceBrush("MeasureHeaderBackgroundBrush", "#FFF5F5F5");
            _textBrush = RenderingUtils.GetResourceBrush("MeasureTextBrush", "#FF000000");
            _separatorPen = RenderingUtils.GetResourcePen("SeparatorLineBrush", "#FFCCCCCC", 1);
            _measureLinePen = RenderingUtils.GetResourcePen("MeasureLineBrush", "#FF000080", 1);
        }

        static TrackOverviewMeasureHeaderCanvas()
        {
            ViewModelProperty.Changed.AddClassHandler<TrackOverviewMeasureHeaderCanvas>((canvas, e) =>
            {
                if (e.OldValue is TrackOverviewViewModel oldVm)
                {
                    oldVm.PropertyChanged -= canvas.OnViewModelPropertyChanged;
                }

                if (e.NewValue is TrackOverviewViewModel newVm)
                {
                    newVm.PropertyChanged += canvas.OnViewModelPropertyChanged;
                }

                canvas.InvalidateVisual();
            });
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TrackOverviewViewModel.Zoom) ||
                e.PropertyName == nameof(TrackOverviewViewModel.CurrentScrollOffset) ||
                e.PropertyName == nameof(TrackOverviewViewModel.TotalWidth))
            {
                InvalidateVisual();
            }
        }

        public override void Render(DrawingContext context)
        {
            if (ViewModel == null) return;

            var bounds = Bounds;

            // ���Ʊ���
            context.DrawRectangle(_backgroundBrush, null, bounds);

            // ���ڵ�ǰ����ƫ��������С�ڱ���
            var scrollOffset = ViewModel.CurrentScrollOffset;
            DrawMeasureNumbers(context, bounds, scrollOffset);

            // ���Ƶײ��ָ���
            context.DrawLine(_separatorPen,
                new Point(0, bounds.Height - 1),
                new Point(bounds.Width, bounds.Height - 1));
        }

        /// <summary>
        /// 绘制小节标头
        /// </summary>
        private void DrawMeasureNumbers(DrawingContext context, Rect bounds, double scrollOffset)
        {
            var scale = ViewModel!.TimeToPixelScale;
            var measureWidth = ViewModel!.MeasureWidth;
            
            // 小节间隔：BeatsPerMeasure个的四分音符数量，4/4拍 = 4.0个四分音符
            var measureInterval = (double)ViewModel.BeatsPerMeasure;

            // 计算可见范围内的起始和结束时间（以四分音符为单位）
            var visibleStartTime = scrollOffset / scale;
            var visibleEndTime = (scrollOffset + bounds.Width) / scale;

            // 修正：确保小节编号从1开始，并且正确计算可见范围
            // 起始小节：向下取整到最近的小节边界，然后+1
            var startMeasure = Math.Max(1, (int)Math.Floor(visibleStartTime / measureInterval) + 1);
            // 结束小节：向上取整，确保覆盖整个可见区域
            var endMeasure = Math.Max(startMeasure, (int)Math.Ceiling(visibleEndTime / measureInterval) + 1);

            for (int measure = startMeasure; measure <= endMeasure; measure++)
            {
                // 小节开始时间：(小节号-1) * 每小节的四分音符数量
                var measureStartTime = (measure - 1) * measureInterval;
                // 计算屏幕坐标：时间 * 缩放比例 - 滚动偏移
                var x = measureStartTime * scale - scrollOffset;

                // 只绘制可见范围内的小节
                if (x >= -measureWidth && x <= bounds.Width + measureWidth)
                {
                    // 绘制小节号文本，位置稍微偏移以避免与线重叠
                    var textPosition = new Point(x + 3, 5);
                    NoteTextRenderer.DrawText(context, measure.ToString(), textPosition,
                        12, _textBrush, useChineseFont: true);

                    // 绘制小节分隔线（除了第一个小节）
                    if (measure > 1 && x >= 0 && x <= bounds.Width)
                    {
                        context.DrawLine(_measureLinePen, new Point(x, 0), new Point(x, bounds.Height));
                    }
                }
            }
        }
    }
}
