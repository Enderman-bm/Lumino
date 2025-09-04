using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using DominoNext.Services.Interfaces;
using DominoNext.Services.Implementation;
using DominoNext.Views.Rendering.Utils;
using DominoNext.Views.Rendering.Grids;
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
        private readonly VerticalGridRenderer _verticalGridRenderer;

        // 使用预缓存的画刷，提升性能
        private readonly IBrush _backgroundBrush;
        private readonly IBrush _timelineBrush;
        private readonly IPen _horizontalLinePen;

        public EventViewCanvas()
        {
            // 注册到渲染同步服务
            _renderSyncService = RenderSyncService.Instance;
            _renderSyncService.RegisterTarget(this);

            // 初始化渲染器
            _verticalGridRenderer = new VerticalGridRenderer();

            // 初始化缓存画刷
            _backgroundBrush = RenderingUtils.GetResourceBrush("MainCanvasBackgroundBrush", "#FFFFFFFF");
            _timelineBrush = RenderingUtils.GetResourceBrush("VelocityIndicatorBrush", "#FFFF0000");
            _horizontalLinePen = RenderingUtils.GetResourcePen("GridLineBrush", "#FFBAD2F2", 1);
        }

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
            context.DrawRectangle(_backgroundBrush, null, bounds);

            // 基于当前滚动偏移量绘制内容
            var scrollOffset = ViewModel.CurrentScrollOffset;

            DrawHorizontalGridLines(context, bounds);
            
            // 复用VerticalGridRenderer的垂直网格绘制逻辑
            _verticalGridRenderer.RenderVerticalGrid(context, ViewModel, bounds, scrollOffset);
            
            DrawTimeline(context, bounds, scrollOffset);
        }

        private void DrawHorizontalGridLines(DrawingContext context, Rect bounds)
        {
            // 将事件视图高度分为4等份，在1/4、1/2、3/4处画横线
            var quarterHeight = bounds.Height / 4.0;

            // 绘制1/4、1/2、3/4位置的横线
            for (int i = 1; i <= 3; i++)
            {
                var y = i * quarterHeight;
                context.DrawLine(_horizontalLinePen,
                    new Point(0, y), new Point(bounds.Width, y));
            }
        }

        /// <summary>
        /// 绘制时间轴（基于滚动偏移量）
        /// </summary>
        private void DrawTimeline(DrawingContext context, Rect bounds, double scrollOffset)
        {
            var timelinePixelPosition = ViewModel!.TimelinePosition * ViewModel.TimeToPixelScale - scrollOffset;

            if (timelinePixelPosition >= 0 && timelinePixelPosition <= bounds.Width)
            {
                var timelinePen = new Pen(_timelineBrush, 2);
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