using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using DominoNext.ViewModels.Editor;
using DominoNext.Services.Interfaces;
using DominoNext.Services.Implementation;
using System;

namespace DominoNext.Views.Controls
{
    /// <summary>
    /// 事件视图面板 - 用于显示MIDI事件和力度信息，支持动态无限长度
    /// </summary>
    public partial class EventViewPanel : UserControl, IRenderSyncTarget
    {
        public static readonly StyledProperty<PianoRollViewModel?> ViewModelProperty =
            AvaloniaProperty.Register<EventViewPanel, PianoRollViewModel?>(nameof(ViewModel));

        public static readonly StyledProperty<bool> IsEventViewVisibleProperty =
            AvaloniaProperty.Register<EventViewPanel, bool>(nameof(IsEventViewVisible), true);

        private readonly IRenderSyncService _renderSyncService;

        public PianoRollViewModel? ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public bool IsEventViewVisible
        {
            get => GetValue(IsEventViewVisibleProperty);
            set => SetValue(IsEventViewVisibleProperty, value);
        }

        public EventViewPanel()
        {
            InitializeComponent();
            
            // 注册到渲染同步服务
            _renderSyncService = RenderSyncService.Instance;
            _renderSyncService.RegisterTarget(this);
            
            // 监听属性变化 - 使用正确的事件订阅方式
            this.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ViewModelProperty && e.NewValue is PianoRollViewModel viewModel)
            {
                DataContext = viewModel;
            }
            else if (e.Property == IsEventViewVisibleProperty && e.NewValue is bool isVisible)
            {
                this.IsVisible = isVisible;
            }
        }

        /// <summary>
        /// 获取内部的滚动视图器，用于与主视图进行滚动同步（已废弃，保留兼容性）
        /// </summary>
        [Obsolete("不再使用ScrollViewer，改为Canvas滚动渲染")]
        public ScrollViewer? GetEventViewScrollViewer()
        {
            return null; // 不再使用ScrollViewer
        }

        /// <summary>
        /// 同步水平滚动位置 - 新实现：通过渲染同步服务
        /// </summary>
        /// <param name="offset">滚动偏移量</param>
        public void SyncHorizontalScroll(double offset)
        {
            // 通过渲染同步服务同步刷新
            _renderSyncService.SyncRefresh();
        }

        /// <summary>
        /// 实现IRenderSyncTarget接口
        /// </summary>
        public void RefreshRender()
        {
            InvalidateVisual();
            
            // 也刷新子Canvas
            if (this.FindControl<Canvas.EventViewCanvas>("EventViewCanvas") is Canvas.EventViewCanvas eventViewCanvas)
            {
                eventViewCanvas.InvalidateVisual();
            }
            
            if (this.FindControl<Canvas.VelocityViewCanvas>("VelocityViewCanvas") is Canvas.VelocityViewCanvas velocityViewCanvas)
            {
                velocityViewCanvas.InvalidateVisual();
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            // 从渲染同步服务注销
            _renderSyncService.UnregisterTarget(this);
            base.OnDetachedFromVisualTree(e);
        }
    }
}