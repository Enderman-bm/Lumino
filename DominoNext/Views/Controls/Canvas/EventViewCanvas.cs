using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using DominoNext.Services.Interfaces;
using DominoNext.Services.Implementation;
using System;
using System.Collections.Specialized;

namespace DominoNext.Views.Controls.Canvas
{
    public class EventViewCanvas : Control, IRenderSyncTarget
    {
        public static readonly StyledProperty<PianoRollViewModel?> ViewModelProperty =
            AvaloniaProperty.Register<EventViewCanvas, PianoRollViewModel?>(nameof(ViewModel));

        public PianoRollViewModel? ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        private readonly IRenderSyncService _renderSyncService;

        public EventViewCanvas()
        {
            // 注册到渲染同步服务
            _renderSyncService = RenderSyncService.Instance;
            _renderSyncService.RegisterTarget(this);
        }

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

        private IPen GetResourcePen(string brushKey, string fallbackHex, double thickness = 1, DashStyle? dashStyle = null)
        {
            var brush = GetResourceBrush(brushKey, fallbackHex);
            var pen = new Pen(brush, thickness);
            if (dashStyle != null)
                pen.DashStyle = dashStyle;
            return pen;
        }

        // 使用动态资源的画刷
        private IBrush TimelineBrush => GetResourceBrush("VelocityIndicatorBrush", "#FFFF0000");
        private IBrush BackgroundBrush => GetResourceBrush("MainCanvasBackgroundBrush", "#FFFFFFFF");

        static EventViewCanvas()
        {
            ViewModelProperty.Changed.AddClassHandler<EventViewCanvas>((canvas, e) =>
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
                e.PropertyName == nameof(PianoRollViewModel.VerticalZoom) ||
                e.PropertyName == nameof(PianoRollViewModel.TimelinePosition) ||
                e.PropertyName == nameof(PianoRollViewModel.CurrentScrollOffset))
            {
                // 使用渲染同步服务
                _renderSyncService.SyncRefresh();
            }
        }

        public override void Render(DrawingContext context)
        {
            if (ViewModel == null) return;

            var bounds = Bounds;

            // 绘制背景
            context.DrawRectangle(BackgroundBrush, null, bounds);

            // 基于当前滚动偏移量绘制内容
            var scrollOffset = ViewModel.CurrentScrollOffset;

            DrawHorizontalGridLines(context, bounds);
            DrawVerticalGridLines(context, bounds, scrollOffset);
            DrawTimeline(context, bounds, scrollOffset);
        }

        private void DrawHorizontalGridLines(DrawingContext context, Rect bounds)
        {
            // 将事件视图高度分为4等份，在1/4、1/2、3/4处画横线
            var quarterHeight = bounds.Height / 4.0;

            var horizontalLinePen = GetResourcePen("GridLineBrush", "#FFBAD2F2", 1);

            // 绘制1/4、1/2、3/4位置的横线
            for (int i = 1; i <= 3; i++)
            {
                var y = i * quarterHeight;
                context.DrawLine(horizontalLinePen,
                    new Point(0, y), new Point(bounds.Width, y));
            }
        }

        /// <summary>
        /// 绘制垂直网格线（基于滚动偏移量）
        /// </summary>
        private void DrawVerticalGridLines(DrawingContext context, Rect bounds, double scrollOffset)
        {
            var measureWidth = ViewModel!.MeasureWidth;
            var beatWidth = ViewModel.BeatWidth;
            var eighthWidth = ViewModel.EighthNoteWidth;
            var sixteenthWidth = ViewModel.SixteenthNoteWidth;

            var startY = 0;
            var endY = bounds.Height;

            // 计算可见范围内的网格线
            var visibleStartTime = scrollOffset / (ViewModel.PixelsPerTick * ViewModel.Zoom);
            var visibleEndTime = (scrollOffset + bounds.Width) / (ViewModel.PixelsPerTick * ViewModel.Zoom);

            // 绘制十六分音符线（最稀疏的虚线）
            if (sixteenthWidth > 5)
            {
                var sixteenthTicks = ViewModel.TicksPerBeat / 4;
                var startSixteenth = (int)(visibleStartTime / sixteenthTicks);
                var endSixteenth = (int)(visibleEndTime / sixteenthTicks) + 1;

                var sixteenthNotePen = GetResourcePen("GridLineBrush", "#FFafafaf", 1, new DashStyle(new double[] { 1, 3 }, 0));

                for (int i = startSixteenth; i <= endSixteenth; i++)
                {
                    if (i % 4 == 0) continue; // 跳过拍线位置

                    var time = i * sixteenthTicks;
                    var x = time * ViewModel.PixelsPerTick * ViewModel.Zoom - scrollOffset;
                    
                    if (x >= 0 && x <= bounds.Width)
                    {
                        context.DrawLine(sixteenthNotePen, new Point(x, startY), new Point(x, endY));
                    }
                }
            }

            // 绘制八分音符线（虚线）
            if (eighthWidth > 10)
            {
                var eighthTicks = ViewModel.TicksPerBeat / 2;
                var startEighth = (int)(visibleStartTime / eighthTicks);
                var endEighth = (int)(visibleEndTime / eighthTicks) + 1;

                var eighthNotePen = GetResourcePen("GridLineBrush", "#FFafafaf", 1, new DashStyle(new double[] { 2, 2 }, 0));

                for (int i = startEighth; i <= endEighth; i++)
                {
                    if (i % 2 == 0) continue; // 跳过拍线位置

                    var time = i * eighthTicks;
                    var x = time * ViewModel.PixelsPerTick * ViewModel.Zoom - scrollOffset;
                    
                    if (x >= 0 && x <= bounds.Width)
                    {
                        context.DrawLine(eighthNotePen, new Point(x, startY), new Point(x, endY));
                    }
                }
            }

            // 绘制二分音符和四分音符线（实线）
            var beatTicks = ViewModel.TicksPerBeat;
            var startBeat = (int)(visibleStartTime / beatTicks);
            var endBeat = (int)(visibleEndTime / beatTicks) + 1;

            var beatLinePen = GetResourcePen("GridLineBrush", "#FFafafaf", 1);

            for (int i = startBeat; i <= endBeat; i++)
            {
                if (i % ViewModel.BeatsPerMeasure == 0) continue; // 跳过小节线位置

                var time = i * beatTicks;
                var x = time * ViewModel.PixelsPerTick * ViewModel.Zoom - scrollOffset;
                
                if (x >= 0 && x <= bounds.Width)
                {
                    context.DrawLine(beatLinePen, new Point(x, startY), new Point(x, endY));
                }
            }

            // 绘制小节线（最后绘制，覆盖其他线条）
            var measureTicks = ViewModel.BeatsPerMeasure * ViewModel.TicksPerBeat;
            var startMeasure = (int)(visibleStartTime / measureTicks);
            var endMeasure = (int)(visibleEndTime / measureTicks) + 1;

            var measureLinePen = GetResourcePen("MeasureLineBrush", "#FF000080", 1);

            for (int i = startMeasure; i <= endMeasure; i++)
            {
                var time = i * measureTicks;
                var x = time * ViewModel.PixelsPerTick * ViewModel.Zoom - scrollOffset;
                
                if (x >= 0 && x <= bounds.Width)
                {
                    context.DrawLine(measureLinePen, new Point(x, startY), new Point(x, endY));
                }
            }
        }

        /// <summary>
        /// 绘制时间轴（基于滚动偏移量）
        /// </summary>
        private void DrawTimeline(DrawingContext context, Rect bounds, double scrollOffset)
        {
            var timelinePixelPosition = ViewModel!.TimelinePosition * ViewModel.PixelsPerTick * ViewModel.Zoom - scrollOffset;

            if (timelinePixelPosition >= 0 && timelinePixelPosition <= bounds.Width)
            {
                var timelinePen = new Pen(TimelineBrush, 2);
                context.DrawLine(timelinePen, 
                    new Point(timelinePixelPosition, 0), 
                    new Point(timelinePixelPosition, bounds.Height));
            }
        }

        /// <summary>
        /// 实现IRenderSyncTarget接口
        /// </summary>
        public void RefreshRender()
        {
            InvalidateVisual();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            // 从渲染同步服务注销
            _renderSyncService.UnregisterTarget(this);
            base.OnDetachedFromVisualTree(e);
        }
    }
}