using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
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
            // 立即响应所有相关属性的变化，确保小节显示与缩放同步
            switch (e.PropertyName)
            {
                case nameof(PianoRollViewModel.Zoom):
                case nameof(PianoRollViewModel.ContentWidth):
                case nameof(PianoRollViewModel.MeasureWidth):
                case nameof(PianoRollViewModel.TicksPerBeat):
                case nameof(PianoRollViewModel.BeatsPerMeasure):
                case nameof(PianoRollViewModel.TotalMeasures):
                    // 确保在UI线程上执行InvalidateVisual
                    if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                    {
                        InvalidateVisual();
                    }
                    else
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => InvalidateVisual());
                    }
                    break;
            }
        }

        public override void Render(DrawingContext context)
        {
            if (ViewModel == null) return;

            var bounds = Bounds;

            // 绘制背景 - 使用资源中的颜色
            var backgroundBrush = GetResourceBrush("MeasureHeaderBackgroundBrush", "#FFF5F5F5");
            context.DrawRectangle(backgroundBrush, null, bounds);

            // 修复：正确计算小节宽度和起始/结束小节
            var measureWidth = ViewModel.MeasureWidth;
            var startMeasure = Math.Max(1, (int)(bounds.X / measureWidth) + 1);
            var endMeasure = (int)((bounds.X + bounds.Width) / measureWidth) + 2;

            for (int measure = startMeasure; measure <= endMeasure; measure++)
            {
                var x = (measure - 1) * measureWidth;
                // 修复：检查小节线是否在可视区域内
                if (x >= bounds.X && x <= bounds.X + bounds.Width)
                {
                    // 绘制小节数字 - 使用资源中的文字颜色
                    var textBrush = GetResourceBrush("MeasureTextBrush", "#FF000000");
                    var measureText = new FormattedText(
                        measure.ToString(),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        _typeface,
                        12,
                        textBrush);

                    var textPoint = new Point(x + 5, 5);
                    context.DrawText(measureText, textPoint);

                    // 绘制小节线 - 使用资源中的小节线颜色
                    if (measure > 1)
                    {
                        var measureLinePen = GetResourcePen("MeasureLineBrush", "#FFCCCCCC", 1.0); // 使用更浅的颜色和适中的粗细
                        context.DrawLine(measureLinePen, new Point(x, 0), new Point(x, bounds.Height));
                    }
                }
            }

            // 绘制底部分隔线 - 使用资源中的分隔线颜色
            var separatorPen = GetResourcePen("SeparatorLineBrush", "#FFE0E0E0", 0.8); // 更浅的分隔线
            context.DrawLine(separatorPen,
                new Point(0, bounds.Height - 1),
                new Point(bounds.Width, bounds.Height - 1));
        }
    }
}