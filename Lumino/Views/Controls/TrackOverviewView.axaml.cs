using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Lumino.ViewModels;
using Lumino.ViewModels.Editor;
using EnderDebugger;

namespace Lumino.Views.Controls
{
    public partial class TrackOverviewView : UserControl
    {
        private readonly EnderLogger _logger = EnderLogger.Instance;
        private ScrollViewer? _mainScrollViewer;
        private ScrollBar? _horizontalScrollBar;
        private Canvas.TrackOverviewCanvas? _overviewCanvas;
        private ScrollViewer? _trackListScrollViewer; // 音轨列表的 ScrollViewer

        public TrackOverviewView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // 获取控件引用
            _mainScrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
            _horizontalScrollBar = this.FindControl<ScrollBar>("HorizontalScrollBar");
            _overviewCanvas = this.FindControl<Canvas.TrackOverviewCanvas>("OverviewCanvas");

            if (_mainScrollViewer != null && _horizontalScrollBar != null)
            {
                // 同步滚动条和 ScrollViewer
                _horizontalScrollBar.Scroll += OnHorizontalScrollBarScroll;
                _mainScrollViewer.PropertyChanged += OnMainScrollViewerPropertyChanged;
            }

            // 立即初始化数据绑定
            InitializeDataBinding();
            
            _logger.Debug("TrackOverviewView", "TrackOverviewView 已加载");
        }

        /// <summary>
        /// 初始化数据绑定（在加载时主动调用）
        /// </summary>
        private void InitializeDataBinding()
        {
            if (DataContext is TrackOverviewViewModel viewModel && _overviewCanvas != null)
            {
                _logger.Debug("TrackOverviewView", "开始初始化数据绑定");
                
                // 需要从父级获取 TrackSelector 和 PianoRoll
                // 尝试多层级查找父窗口
                var parent = this.Parent;
                MainWindowViewModel? mainWindowVM = null;

                while (parent != null)
                {
                    if (parent is Window window && window.DataContext is MainWindowViewModel vm)
                    {
                        mainWindowVM = vm;
                        break;
                    }
                    if (parent is Control control && control.DataContext is MainWindowViewModel vm2)
                    {
                        mainWindowVM = vm2;
                        break;
                    }
                    parent = parent.Parent;
                }

                if (mainWindowVM != null)
                {
                    _logger.Debug("TrackOverviewView", "找到 MainWindowViewModel");
                    
                    _overviewCanvas.TrackSelector = mainWindowVM.TrackSelector;
                    _overviewCanvas.PianoRoll = mainWindowVM.PianoRoll;

                    // 同步时长
                    if (mainWindowVM.PianoRoll != null)
                    {
                        var duration = mainWindowVM.PianoRoll.MidiFileDuration;
                        viewModel.SetTotalDuration(duration);
                        _logger.Debug("TrackOverviewView", $"同步时长: {duration:F1} 四分音符");
                    }

                    // 同步音轨数量
                    if (mainWindowVM.TrackSelector != null)
                    {
                        var trackCount = mainWindowVM.TrackSelector.Tracks.Count;
                        viewModel.SetTrackCount(trackCount);
                        _logger.Debug("TrackOverviewView", $"同步音轨数量: {trackCount}");
                        
                        // 监听音轨列表变化
                        mainWindowVM.TrackSelector.Tracks.CollectionChanged += (s, args) =>
                        {
                            var newCount = mainWindowVM.TrackSelector.Tracks.Count;
                            viewModel.SetTrackCount(newCount);
                            _logger.Debug("TrackOverviewView", $"音轨列表变化，新数量: {newCount}");
                        };
                    }
                    else
                    {
                        _logger.Debug("TrackOverviewView", "TrackSelector 为空");
                    }

                    // 查找并绑定 TrackSelector 的 ScrollViewer 以实现垂直滚动同步
                    FindAndBindTrackListScrollViewer();
                    
                    // 强制刷新画布
                    _overviewCanvas?.InvalidateVisual();
                    _logger.Debug("TrackOverviewView", "强制刷新画布");
                }
                else
                {
                    _logger.Debug("TrackOverviewView", "未找到 MainWindowViewModel");
                }
            }
            else
            {
                _logger.Debug("TrackOverviewView", $"数据绑定失败 - DataContext: {DataContext?.GetType().Name ?? "null"}, OverviewCanvas: {_overviewCanvas != null}");
            }
        }

        /// <summary>
        /// 查找 TrackSelector 中的 ScrollViewer 并绑定垂直滚动同步
        /// </summary>
        private void FindAndBindTrackListScrollViewer()
        {
            // 向上查找到主窗口
            var parent = this.Parent;
            while (parent != null && parent is not Window)
            {
                parent = parent.Parent;
            }

            if (parent is Window window)
            {
                // 查找 TrackSelector 控件
                var trackSelector = window.FindControl<TrackSelector>("TrackSelector");
                if (trackSelector != null)
                {
                    // 在 TrackSelector 中查找 ScrollViewer（延迟查找，等待控件初始化）
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        _trackListScrollViewer = FindScrollViewerInVisualTree(trackSelector);
                        if (_trackListScrollViewer != null && _mainScrollViewer != null)
                        {
                            // 绑定双向滚动同步
                            _mainScrollViewer.PropertyChanged += OnMainScrollViewerVerticalScroll;
                            _trackListScrollViewer.PropertyChanged += OnTrackListScrollViewerVerticalScroll;
                            
                            _logger.Debug("TrackOverviewView", "垂直滚动同步已建立");
                        }
                    }, Avalonia.Threading.DispatcherPriority.Loaded);
                }
            }
        }

        /// <summary>
        /// 在可视化树中查找 ScrollViewer
        /// </summary>
        private ScrollViewer? FindScrollViewerInVisualTree(Control control)
        {
            if (control is ScrollViewer scrollViewer)
                return scrollViewer;

            if (control is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (child is Control childControl)
                    {
                        var result = FindScrollViewerInVisualTree(childControl);
                        if (result != null)
                            return result;
                    }
                }
            }

            if (control is Decorator decorator && decorator.Child is Control decoratedChild)
            {
                return FindScrollViewerInVisualTree(decoratedChild);
            }

            if (control is ContentControl contentControl && contentControl.Content is Control contentChild)
            {
                return FindScrollViewerInVisualTree(contentChild);
            }

            return null;
        }

        private void OnHorizontalScrollBarScroll(object? sender, ScrollEventArgs e)
        {
            if (_mainScrollViewer != null && DataContext is TrackOverviewViewModel viewModel)
            {
                var offset = e.NewValue;
                _mainScrollViewer.Offset = new Vector(offset, _mainScrollViewer.Offset.Y);
                viewModel.SetScrollOffset(offset);
            }
        }

        private void OnMainScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == nameof(ScrollViewer.Offset) && 
                _horizontalScrollBar != null && 
                DataContext is TrackOverviewViewModel viewModel)
            {
                var offset = ((Vector)e.NewValue!).X;
                if (Math.Abs(_horizontalScrollBar.Value - offset) > 0.01)
                {
                    _horizontalScrollBar.Value = offset;
                    viewModel.SetScrollOffset(offset);
                }
            }
        }

        /// <summary>
        /// 主 ScrollViewer 的垂直滚动变化，同步到 TrackList ScrollViewer
        /// </summary>
        private void OnMainScrollViewerVerticalScroll(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == nameof(ScrollViewer.Offset) && _trackListScrollViewer != null && _mainScrollViewer != null)
            {
                var verticalOffset = ((Vector)e.NewValue!).Y;
                if (Math.Abs(_trackListScrollViewer.Offset.Y - verticalOffset) > 0.01)
                {
                    _trackListScrollViewer.Offset = new Vector(_trackListScrollViewer.Offset.X, verticalOffset);
                }
            }
        }

        /// <summary>
        /// TrackList ScrollViewer 的垂直滚动变化，同步到主 ScrollViewer
        /// </summary>
        private void OnTrackListScrollViewerVerticalScroll(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == nameof(ScrollViewer.Offset) && _trackListScrollViewer != null && _mainScrollViewer != null)
            {
                var verticalOffset = ((Vector)e.NewValue!).Y;
                if (Math.Abs(_mainScrollViewer.Offset.Y - verticalOffset) > 0.01)
                {
                    _mainScrollViewer.Offset = new Vector(_mainScrollViewer.Offset.X, verticalOffset);
                }
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            // 清理事件订阅
            if (_horizontalScrollBar != null)
            {
                _horizontalScrollBar.Scroll -= OnHorizontalScrollBarScroll;
            }

            if (_mainScrollViewer != null)
            {
                _mainScrollViewer.PropertyChanged -= OnMainScrollViewerPropertyChanged;
                _mainScrollViewer.PropertyChanged -= OnMainScrollViewerVerticalScroll;
            }

            if (_trackListScrollViewer != null)
            {
                _trackListScrollViewer.PropertyChanged -= OnTrackListScrollViewerVerticalScroll;
            }

            _logger.Debug("TrackOverviewView", "TrackOverviewView 已卸载");
        }
    }
}
