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
        private ScrollViewer? _trackListScrollViewer; // 轨道列表 ScrollViewer
        private bool _isUpdatingFromViewModel = false; // 防止重复更新的标志

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
                // 同步垂直滚动 ScrollViewer
                _mainScrollViewer.PropertyChanged += OnMainScrollViewerPropertyChanged;
                
                // 同步ViewModel的CurrentScrollOffset变化到MainScrollViewer的水平偏移
                if (DataContext is TrackOverviewViewModel viewModel)
                {
                    viewModel.PropertyChanged += (s, args) =>
                    {
                        if (args.PropertyName == nameof(TrackOverviewViewModel.CurrentScrollOffset))
                        {
                            SyncScrollViewerHorizontalOffset();
                        }
                    };
                }
            }

            // 更新初始化数据绑定
            InitializeDataBinding();
            
            _logger.Debug("TrackOverviewView", "TrackOverviewView �Ѽ���");
        }

        /// <summary>
        /// ��ʼ�����ݰ󶨣��ڼ���ʱ�������ã�
        /// </summary>
        private void InitializeDataBinding()
        {
            if (DataContext is TrackOverviewViewModel viewModel && _overviewCanvas != null)
            {
                _logger.Debug("TrackOverviewView", "��ʼ��ʼ�����ݰ�");
                
                // ��Ҫ�Ӹ�����ȡ TrackSelector �� PianoRoll
                // ���Զ�㼶���Ҹ�����
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
                    _logger.Debug("TrackOverviewView", "�ҵ� MainWindowViewModel");
                    
                    _overviewCanvas.TrackSelector = mainWindowVM.TrackSelector;
                    _overviewCanvas.PianoRoll = mainWindowVM.PianoRoll;

                    // ͬ��ʱ��
                    if (mainWindowVM.PianoRoll != null)
                    {
                        var duration = mainWindowVM.PianoRoll.MidiFileDuration;
                        viewModel.SetTotalDuration(duration);
                        _logger.Debug("TrackOverviewView", $"ͬ��ʱ��: {duration:F1} �ķ�����");
                    }

                    // ͬ����������
                    if (mainWindowVM.TrackSelector != null)
                    {
                        var trackCount = mainWindowVM.TrackSelector.Tracks.Count;
                        viewModel.SetTrackCount(trackCount);
                        _logger.Debug("TrackOverviewView", $"ͬ����������: {trackCount}");
                        
                        // ���������б��仯
                        mainWindowVM.TrackSelector.Tracks.CollectionChanged += (s, args) =>
                        {
                            var newCount = mainWindowVM.TrackSelector.Tracks.Count;
                            viewModel.SetTrackCount(newCount);
                            _logger.Debug("TrackOverviewView", $"�����б��仯��������: {newCount}");
                        };
                    }
                    else
                    {
                        _logger.Debug("TrackOverviewView", "TrackSelector Ϊ��");
                    }

                    // ���Ҳ��� TrackSelector �� ScrollViewer ��ʵ�ִ�ֱ����ͬ��
                    FindAndBindTrackListScrollViewer();
                    
                    // ǿ��ˢ�»���
                    _overviewCanvas?.InvalidateVisual();
                    _logger.Debug("TrackOverviewView", "ǿ��ˢ�»���");
                }
                else
                {
                    _logger.Debug("TrackOverviewView", "δ�ҵ� MainWindowViewModel");
                }
            }
            else
            {
                _logger.Debug("TrackOverviewView", $"���ݰ�ʧ�� - DataContext: {DataContext?.GetType().Name ?? "null"}, OverviewCanvas: {_overviewCanvas != null}");
            }
        }

        /// <summary>
        /// ���� TrackSelector �е� ScrollViewer ���󶨴�ֱ����ͬ��
        /// </summary>
        private void FindAndBindTrackListScrollViewer()
        {
            // ���ϲ��ҵ�������
            var parent = this.Parent;
            while (parent != null && parent is not Window)
            {
                parent = parent.Parent;
            }

            if (parent is Window window)
            {
                // ���� TrackSelector �ؼ�
                var trackSelector = window.FindControl<TrackSelector>("TrackSelector");
                if (trackSelector != null)
                {
                    // �� TrackSelector �в��� ScrollViewer���ӳٲ��ң��ȴ��ؼ���ʼ����
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        _trackListScrollViewer = FindScrollViewerInVisualTree(trackSelector);
                        if (_trackListScrollViewer != null && _mainScrollViewer != null)
                        {
                            // ��˫�����ͬ��
                            _mainScrollViewer.PropertyChanged += OnMainScrollViewerVerticalScroll;
                            _trackListScrollViewer.PropertyChanged += OnTrackListScrollViewerVerticalScroll;
                            
                            _logger.Debug("TrackOverviewView", "��ֱ����ͬ���ѽ���");
                        }
                    }, Avalonia.Threading.DispatcherPriority.Loaded);
                }
            }
        }

        /// <summary>
        /// �ڿ��ӻ����в��� ScrollViewer
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

        private void OnMainScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == nameof(ScrollViewer.Offset) && 
                _horizontalScrollBar != null && 
                DataContext is TrackOverviewViewModel viewModel &&
                !_isUpdatingFromViewModel) // 防止在从ViewModel更新时触发
            {
                var offset = ((Vector)e.NewValue!).X;
                // MainScrollViewer的水平偏移改变时，同步到ViewModel
                viewModel.SetScrollOffset(offset);
            }
        }

        private void SyncScrollViewerHorizontalOffset()
        {
            if (_mainScrollViewer != null && DataContext is TrackOverviewViewModel viewModel)
            {
                var targetOffset = viewModel.CurrentScrollOffset;
                var currentOffset = _mainScrollViewer.Offset.X;
                
                // 确保MainScrollViewer与CurrentScrollOffset同步
                if (Math.Abs(currentOffset - targetOffset) > 0.1)
                {
                    _isUpdatingFromViewModel = true;
                    try
                    {
                        _mainScrollViewer.Offset = new Vector(targetOffset, _mainScrollViewer.Offset.Y);
                    }
                    finally
                    {
                        _isUpdatingFromViewModel = false;
                    }
                }
            }
        }

        /// <summary>
        /// 从 ScrollViewer 的垂直滚动变化，同步到 TrackList ScrollViewer
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
        /// TrackList ScrollViewer �Ĵ�ֱ�����仯��ͬ������ ScrollViewer
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

            // �����¼�����
            if (_mainScrollViewer != null)
            {
                _mainScrollViewer.PropertyChanged -= OnMainScrollViewerPropertyChanged;
                _mainScrollViewer.PropertyChanged -= OnMainScrollViewerVerticalScroll;
            }

            if (_trackListScrollViewer != null)
            {
                _trackListScrollViewer.PropertyChanged -= OnTrackListScrollViewerVerticalScroll;
            }

            _logger.Debug("TrackOverviewView", "TrackOverviewView ��ж��");
        }
    }
}
