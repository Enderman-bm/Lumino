using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using Lumino.ViewModels.Editor;
using Lumino.Services.Interfaces;
using Lumino.Services.Implementation;
using Lumino.Views.Rendering.Utils;
using Lumino.Views.Rendering.Grids;
using System;
using System.Collections.Specialized;

namespace Lumino.Views.Controls.Canvas
{
    public class PianoRollCanvas : Control, IRenderSyncTarget
    {
        public static readonly StyledProperty<PianoRollViewModel?> ViewModelProperty =
            AvaloniaProperty.Register<PianoRollCanvas, PianoRollViewModel?>(nameof(ViewModel));

        public PianoRollViewModel? ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        private const double PianoKeyWidth = 60;
        private readonly IRenderSyncService? _renderSyncService;

        // 独立的渲染器
        private readonly HorizontalGridRenderer _horizontalGridRenderer;
        private readonly VerticalGridRenderer _verticalGridRenderer;
        private readonly PlayheadRenderer _playheadRenderer;

        // 使用动态画刷获取，确保与主题状态同步
        private IBrush MainBackgroundBrush => RenderingUtils.GetResourceBrush("MainCanvasBackgroundBrush", "#FFFFFFFF");

        public PianoRollCanvas()
        {
            // 使用全局渲染同步服务
            _renderSyncService = RenderSyncService.Instance;
            _renderSyncService.RegisterTarget(this);

            // 初始化渲染器
            _horizontalGridRenderer = new HorizontalGridRenderer();
            _verticalGridRenderer = new VerticalGridRenderer();
            _playheadRenderer = new PlayheadRenderer();
        }

        static PianoRollCanvas()
        {
            ViewModelProperty.Changed.AddClassHandler<PianoRollCanvas>((canvas, e) =>
            {
                if (e.OldValue is PianoRollViewModel oldVm)
                {
                    oldVm.PropertyChanged -= canvas.OnViewModelPropertyChanged;
                    if (oldVm.Notes is INotifyCollectionChanged oldCollection)
                        oldCollection.CollectionChanged -= canvas.OnNotesCollectionChanged;
                }

                if (e.NewValue is PianoRollViewModel newVm)
                {
                    newVm.PropertyChanged += canvas.OnViewModelPropertyChanged;
                    if (newVm.Notes is INotifyCollectionChanged newCollection)
                        newCollection.CollectionChanged += canvas.OnNotesCollectionChanged;
                }

                canvas.InvalidateVisual();
            });
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 根据属性类型决定刷新策略
            if (e.PropertyName == nameof(PianoRollViewModel.Zoom) ||
                e.PropertyName == nameof(PianoRollViewModel.VerticalZoom))
            {
                // 缩放变化需要同步刷新所有Canvas
                if (_renderSyncService != null)
                {
                    _renderSyncService.SyncRefresh();
                }
                else
                {
                    InvalidateVisual();
                }
            }
            else if (e.PropertyName == nameof(PianoRollViewModel.TimelinePosition) ||
                     e.PropertyName == nameof(PianoRollViewModel.CurrentScrollOffset) ||
                     e.PropertyName == nameof(PianoRollViewModel.VerticalScrollOffset))
            {
                // 位置变化只需要刷新自己，避免不必要的EventView刷新
                InvalidateVisual();
            }
        }

        private void OnNotesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 音符变化时只刷新自己，EventView不需要重绘
            InvalidateVisual();
        }

        /// <summary>
        /// 实现IRenderSyncTarget接口
        /// </summary>
        public void RefreshRender()
        {
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            if (ViewModel == null) return;

            var bounds = Bounds;

            // 绘制背景 - 使用动态获取，确保与主题同步
            context.DrawRectangle(MainBackgroundBrush, null, bounds);

            // 获取当前滚动偏移量和缩放参数
            var scrollOffset = ViewModel.CurrentScrollOffset;
            var verticalScrollOffset = ViewModel.VerticalScrollOffset;

            // 稳定渲染策略：总是绘制所有组件，确保显示完整性
            // 渲染器内部会自行优化计算，避免不必要的重复计算
            
            // 绘制水平网格线（键盘区域和分割线）
            _horizontalGridRenderer.RenderHorizontalGrid(context, ViewModel, bounds, verticalScrollOffset);
            
            // 绘制垂直网格线（小节线和音符线）
            _verticalGridRenderer.RenderVerticalGrid(context, ViewModel, bounds, scrollOffset);
            
            // 绘制播放头
            _playheadRenderer.RenderPlayhead(context, ViewModel, bounds, scrollOffset);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            // 从渲染同步服务注销
            _renderSyncService?.UnregisterTarget(this);
            base.OnDetachedFromVisualTree(e);
        }
    }
}