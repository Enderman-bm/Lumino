using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lumino.ViewModels;
using Lumino.Views.Rendering.Utils;
using System;

namespace Lumino.Views.Controls.Canvas
{
    /// <summary>
    /// 音轨总览的小节标题画布
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

        // 缓存画刷和画笔，提升性能
        private readonly IBrush _backgroundBrush;
        private readonly IBrush _textBrush;
        private readonly IPen _separatorPen;
        private readonly IPen _measureLinePen;

        public TrackOverviewMeasureHeaderCanvas()
        {
            // 初始化缓存资源
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
                e.PropertyName == nameof(TrackOverviewViewModel.CurrentScrollOffset))
            {
                InvalidateVisual();
            }
        }

        public override void Render(DrawingContext context)
        {
            if (ViewModel == null) return;

            var bounds = Bounds;

            // 绘制背景
            context.DrawRectangle(_backgroundBrush, null, bounds);

            // 基于当前滚动偏移量绘制小节标题
            var scrollOffset = ViewModel.CurrentScrollOffset;
            DrawMeasureNumbers(context, bounds, scrollOffset);

            // 绘制底部分隔线
            context.DrawLine(_separatorPen,
                new Point(0, bounds.Height - 1),
                new Point(bounds.Width, bounds.Height - 1));
        }

        /// <summary>
        /// 绘制小节编号
        /// </summary>
        private void DrawMeasureNumbers(DrawingContext context, Rect bounds, double scrollOffset)
        {
            var measureWidth = ViewModel!.MeasureWidth;
            
            // 小节间距：BeatsPerMeasure个四分音符（4/4拍 = 4.0个四分音符）
            var measureInterval = (double)ViewModel.BeatsPerMeasure;

            // 计算可见范围内的小节（以四分音符为单位）
            var visibleStartTime = scrollOffset / ViewModel.BaseQuarterNoteWidth;
            var visibleEndTime = (scrollOffset + bounds.Width) / ViewModel.BaseQuarterNoteWidth;

            var startMeasure = Math.Max(1, (int)(visibleStartTime / measureInterval) + 1);
            var endMeasure = (int)(visibleEndTime / measureInterval) + 2;

            for (int measure = startMeasure; measure <= endMeasure; measure++)
            {
                // 小节开始时间：(小节号-1) * 每小节的四分音符数
                var measureStartTime = (measure - 1) * measureInterval;
                var x = measureStartTime * ViewModel.BaseQuarterNoteWidth - scrollOffset;

                if (x >= -measureWidth && x <= bounds.Width)
                {
                    // 使用统一的文本渲染器绘制小节数字
                    var textPosition = new Point(x + 5, 5);
                    NoteTextRenderer.DrawText(context, measure.ToString(), textPosition, 
                        12, _textBrush, useChineseFont: true);

                    // 绘制小节线（除了第一个小节）
                    if (measure > 1 && x >= 0 && x <= bounds.Width)
                    {
                        context.DrawLine(_measureLinePen, new Point(x, 0), new Point(x, bounds.Height));
                    }
                }
            }
        }
    }
}
