using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Utils;
using Lumino.Models.Music;
using System;
using System.Globalization;

namespace Lumino.Views.Controls.Canvas
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

        // 缓存画刷和画笔，提升性能
        private readonly IBrush _backgroundBrush;
        private readonly IBrush _textBrush;
        private readonly IPen _separatorPen;
        private readonly IPen _measureLinePen;

        // 长按移动演奏指示线的相关字段
        private System.Timers.Timer? _longPressTimer;
        private Point _pressPosition;
        private bool _isLongPressActive = false;

        public MeasureHeaderCanvas()
        {
            // 初始化缓存资源
            _backgroundBrush = RenderingUtils.GetResourceBrush("MeasureHeaderBackgroundBrush", "#FFF5F5F5");
            _textBrush = RenderingUtils.GetResourceBrush("MeasureTextBrush", "#FF000000");
            _separatorPen = RenderingUtils.GetResourcePen("SeparatorLineBrush", "#FFCCCCCC", 1);
            _measureLinePen = RenderingUtils.GetResourcePen("MeasureLineBrush", "#FF000080", 1);

            // 注册鼠标事件以支持点击移动演奏指示线
            PointerPressed += OnPointerPressed;
            PointerReleased += OnPointerReleased;
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
                e.PropertyName == nameof(PianoRollViewModel.CurrentScrollOffset) ||
                e.PropertyName == nameof(PianoRollViewModel.PlaybackPosition))
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

            // 绘制演奏指示线的倒三角标记
            DrawPlaybackIndicator(context, bounds, scrollOffset);

            // 绘制底部分隔线
            context.DrawLine(_separatorPen,
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

        /// <summary>
        /// 处理鼠标按下事件，启动长按计时器
        /// </summary>
        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (ViewModel == null) return;

            try
            {
                _pressPosition = e.GetPosition(this);
                _isLongPressActive = false;

                // 启动长按计时器 (200ms)
                _longPressTimer?.Stop();
                _longPressTimer = new System.Timers.Timer(200);
                _longPressTimer.Elapsed += (s, args) =>
                {
                    _longPressTimer?.Stop();
                    _isLongPressActive = true;
                    
                    // 在UI线程上更新演奏位置
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (ViewModel == null) return;
                        
                        var scrollOffset = ViewModel.CurrentScrollOffset;
                        var clickTime = (_pressPosition.X + scrollOffset) / ViewModel.BaseQuarterNoteWidth;
                        var clickPosition = MusicalFraction.FromDouble(clickTime);
                        var quantizedPosition = MusicalFraction.QuantizeToGrid(clickPosition, ViewModel.GridQuantization);
                        var quantizedTime = quantizedPosition.ToDouble();
                        ViewModel.PlaybackPosition = Math.Max(0, quantizedTime);
                    });
                };
                _longPressTimer.AutoReset = false;
                _longPressTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MeasureHeaderCanvas] 处理点击事件错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理鼠标释放事件，取消长按计时器
        /// </summary>
        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _longPressTimer?.Stop();
            _isLongPressActive = false;
        }

        /// <summary>
        /// 绘制演奏指示线的倒三角标记
        /// </summary>
        private void DrawPlaybackIndicator(DrawingContext context, Rect bounds, double scrollOffset)
        {
            if (ViewModel == null) return;

            try
            {
                // 计算演奏指示线的X坐标
                var x = ViewModel.PlaybackPosition * ViewModel.BaseQuarterNoteWidth - scrollOffset;

                // 只在可见范围内绘制
                if (x >= 0 && x <= bounds.Width)
                {
                    // 创建黄色画刷
                    var playbackBrush = new SolidColorBrush(Color.FromRgb(255, 200, 0));

                    // 绘制倒三角（尖端朝下，与指示线对齐）
                    // 三角形高度: 8像素，底边宽度: 12像素
                    var triangleHeight = 8.0;
                    var triangleWidth = 12.0;

                    var geometry = new PathGeometry
                    {
                        Figures = new PathFigures
                        {
                            new PathFigure
                            {
                                StartPoint = new Point(x, 0), // 顶部中心（尖端）
                                Segments = new PathSegments
                                {
                                    new LineSegment { Point = new Point(x - triangleWidth / 2, triangleHeight) }, // 左下角
                                    new LineSegment { Point = new Point(x + triangleWidth / 2, triangleHeight) }, // 右下角
                                },
                                IsClosed = true
                            }
                        }
                    };

                    context.DrawGeometry(playbackBrush, null, geometry);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MeasureHeaderCanvas] 绘制演奏指示线标记错误: {ex.Message}");
            }
        }
    }
}