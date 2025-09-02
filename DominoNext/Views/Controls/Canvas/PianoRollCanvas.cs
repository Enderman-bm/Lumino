using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using DominoNext.ViewModels.Editor;
using DominoNext.Services.Interfaces;
using DominoNext.Services.Implementation;
using System;
using System.Collections.Specialized;
using DominoNext.Views.Rendering.Grids;

namespace DominoNext.Views.Controls.Canvas
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

        // 使用动态资源的画刷
        private IBrush MainBackgroundBrush => GetResourceBrush("MainCanvasBackgroundBrush", "#FFFFFFFF");

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
            if (e.PropertyName == nameof(PianoRollViewModel.Zoom) ||
                e.PropertyName == nameof(PianoRollViewModel.VerticalZoom) ||
                e.PropertyName == nameof(PianoRollViewModel.TimelinePosition) ||
                e.PropertyName == nameof(PianoRollViewModel.CurrentScrollOffset) ||
                e.PropertyName == nameof(PianoRollViewModel.VerticalScrollOffset))
            {
                // 优先使用同步渲染服务
                if (_renderSyncService != null)
                {
                    _renderSyncService.SyncRefresh();
                }
                else
                {
                    InvalidateVisual();
                }
            }
        }

        private void OnNotesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 音符变化时同步刷新
            if (_renderSyncService != null)
            {
                _renderSyncService.SyncRefresh();
            }
            else
            {
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

            var bounds = Bounds;

            // 绘制背景
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
    }
}