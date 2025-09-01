using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using DominoNext.Models.Music;
using System;
using System.Globalization;

namespace DominoNext.Views.Controls.Canvas
{
    public class MeasureHeaderCanvas : Control
    {
        public static readonly StyledProperty<PianoRollViewModel?> ViewModelProperty =
            AvaloniaProperty.Register<MeasureHeaderCanvas, PianoRollViewModel?>(nameof(ViewModel));

        public PianoRollViewModel? ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        // 使用微软雅黑字体系列（更适合中文界面）
        private readonly Typeface _typeface = new Typeface(new FontFamily("Microsoft YaHei"));

        // 资源画刷获取助手方法
        private IBrush GetResourceBrush(string key, string fallbackHex)
        {
            try
            {
                if (Application.Current?.Resources.TryGetResource(key, null, out var obj) == true && obj is IBrush brush)
                    return brush;
            }
            catch { }

            try
            {
                return new SolidColorBrush(Color.Parse(fallbackHex));
            }
            catch
            {
                return Brushes.Transparent;
            }
        }

        private IPen GetResourcePen(string brushKey, string fallbackHex, double thickness = 1)
        {
            var brush = GetResourceBrush(brushKey, fallbackHex);
            return new Pen(brush, thickness);
        }

        static MeasureHeaderCanvas()
        {
            ViewModelProperty.Changed.AddClassHandler<MeasureHeaderCanvas>((canvas, e) =>
            {
                if (e.OldValue is PianoRollViewModel oldVm)
                {
                    oldVm.PropertyChanged -= canvas.OnViewModelPropertyChanged;
                }

                if (e.NewValue is PianoRollViewModel newVm)
                {
                    newVm.PropertyChanged += canvas.OnViewModelPropertyChanged;
                }

                canvas.InvalidateVisual();
            });
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PianoRollViewModel.Zoom) ||
                e.PropertyName == nameof(PianoRollViewModel.CurrentScrollOffset))
            {
                InvalidateVisual();
            }
        }

        public override void Render(DrawingContext context)
        {
            if (ViewModel == null) return;

            var bounds = Bounds;

            // 绘制背景
            var backgroundBrush = GetResourceBrush("MeasureHeaderBackgroundBrush", "#FFF5F5F5");
            context.DrawRectangle(backgroundBrush, null, bounds);

            // 基于当前滚动偏移量绘制小节标题
            var scrollOffset = ViewModel.CurrentScrollOffset;
            DrawMeasureNumbers(context, bounds, scrollOffset);

            // 绘制底部分隔线
            var separatorPen = GetResourcePen("SeparatorLineBrush", "#FFCCCCCC", 1);
            context.DrawLine(separatorPen,
                new Point(0, bounds.Height - 1),
                new Point(bounds.Width, bounds.Height - 1));
        }

        /// <summary>
        /// 绘制小节编号（修复小节间距计算）
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

            var textBrush = GetResourceBrush("MeasureTextBrush", "#FF000000");
            var measureLinePen = GetResourcePen("MeasureLineBrush", "#FF000080", 1);

            for (int measure = startMeasure; measure <= endMeasure; measure++)
            {
                // 小节开始时间：(小节号-1) * 每小节的四分音符数
                var measureStartTime = (measure - 1) * measureInterval;
                var x = measureStartTime * ViewModel.BaseQuarterNoteWidth - scrollOffset;

                if (x >= -measureWidth && x <= bounds.Width)
                {
                    // 绘制小节数字
                    var measureText = new FormattedText(
                        measure.ToString(),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        _typeface,
                        12,
                        textBrush);

                    var textPoint = new Point(x + 5, 5);
                    if (textPoint.X + measureText.Width > 0 && textPoint.X < bounds.Width)
                    {
                        context.DrawText(measureText, textPoint);
                    }

                    // 绘制小节线（除了第一个小节）
                    if (measure > 1 && x >= 0 && x <= bounds.Width)
                    {
                        context.DrawLine(measureLinePen, new Point(x, 0), new Point(x, bounds.Height));
                    }
                }
            }
        }
    }
}