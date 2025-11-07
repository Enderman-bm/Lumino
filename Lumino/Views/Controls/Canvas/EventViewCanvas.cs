using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Services.Interfaces;
using Lumino.Services.Implementation;
using Lumino.Views.Rendering.Utils;
using Lumino.Views.Rendering.Grids;
using Lumino.Views.Rendering.Events;
using System;
using System.Collections.Specialized;

namespace Lumino.Views.Controls.Canvas
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

        private readonly IRenderSyncService? _renderSyncService;

        // 独立的渲染器
        private readonly EventViewHorizontalGridRenderer _horizontalGridRenderer;
        private readonly VerticalGridRenderer _verticalGridRenderer;
        private readonly PlayheadRenderer _playheadRenderer;

        // 使用动态画刷获取，确保与主题状态同步
        private IBrush MainBackgroundBrush => RenderingUtils.GetResourceBrush("MainCanvasBackgroundBrush", "#FFFFFFFF");

        public EventViewCanvas()
        {
            // 使用全局渲染同步服务
            _renderSyncService = RenderSyncService.Instance;
            _renderSyncService.RegisterTarget(this);

            // 初始化渲染器
            _horizontalGridRenderer = new EventViewHorizontalGridRenderer();
            _verticalGridRenderer = new VerticalGridRenderer();
            _playheadRenderer = new PlayheadRenderer();
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
                     e.PropertyName == nameof(PianoRollViewModel.CurrentScrollOffset))
            {
                // 位置变化刷新自己
                InvalidateVisual();
            }
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

            // 资源状态检查保护机制
            if (!ResourcePreloadService.Instance.ResourcesLoaded)
            {
                // 短暂延迟后重新尝试渲染
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(100);
                    InvalidateVisual();
                });
                return;
            }

            var bounds = Bounds;

            // 绘制背景 - 使用动态获取，确保与主题同步
            context.DrawRectangle(MainBackgroundBrush, null, bounds);

            // 获取当前滚动偏移量
            var scrollOffset = ViewModel.CurrentScrollOffset;

            // 稳定渲染策略：总是绘制所有组件，确保显示完整性
            // 渲染器内部会自行优化计算，避免不必要的重复计算
            
            // 绘制水平网格线（事件视图的分割线）
            _horizontalGridRenderer.RenderEventViewHorizontalGrid(context, ViewModel, bounds);
            
            // 绘制垂直网格线（小节线和音符线）
            _verticalGridRenderer.RenderVerticalGrid(context, ViewModel, bounds, scrollOffset);
            
            // 绘制播放头
            _playheadRenderer.RenderPlayhead(context, ViewModel, bounds, scrollOffset);
        }

        /// <summary>
        /// 在主题切换时清除内部渲染器的缓存，确保下一次渲染使用新的主题画刷
        /// </summary>
        public void ClearRendererCaches()
        {
            try
            {
                var mi = _horizontalGridRenderer?.GetType().GetMethod("ClearCache");
                mi?.Invoke(_horizontalGridRenderer, null);
            }
            catch { }

            try
            {
                var mi2 = _verticalGridRenderer?.GetType().GetMethod("ClearCache");
                mi2?.Invoke(_verticalGridRenderer, null);
            }
            catch { }

            try
            {
                _playheadRenderer?.ClearCache();
            }
            catch { }

            try
            {
                RenderingUtils.ClearBrushCache();
            }
            catch { }

            InvalidateVisual();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            // 从渲染同步服务注销
            _renderSyncService?.UnregisterTarget(this);
            base.OnDetachedFromVisualTree(e);
        }
    }
}