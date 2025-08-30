using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System;
using Avalonia;
using DominoNext.Services.Interfaces;
using DominoNext.ViewModels.Editor;

namespace DominoNext.Views
{
    public partial class PianoRollView : UserControl
    {
        private bool _isUpdatingScroll = false;
        private ISettingsService? _settingsService;

        public PianoRollView()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
            
            // 订阅主题变化
            SubscribeToThemeChanges();
        }

        private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // 订阅主滚动视图的滚动事件
            if (this.FindControl<ScrollViewer>("MainScrollViewer") is ScrollViewer mainScrollViewer)
            {
                mainScrollViewer.ScrollChanged += OnMainScrollViewerScrollChanged;
            }

            // 订阅钢琴键滚动视图的滚动事件
            if (this.FindControl<ScrollViewer>("PianoKeysScrollViewer") is ScrollViewer pianoKeysScrollViewer)
            {
                pianoKeysScrollViewer.ScrollChanged += OnPianoKeysScrollViewerScrollChanged;
            }

            // 订阅事件视图滚动视图的滚动事件
            if (this.FindControl<ScrollViewer>("EventViewScrollViewer") is ScrollViewer eventViewScrollViewer)
            {
                eventViewScrollViewer.ScrollChanged += OnEventViewScrollViewerScrollChanged;
            }

            // 订阅水平滚动条的值变化事件
            if (this.FindControl<ScrollBar>("HorizontalScrollBar") is ScrollBar horizontalScrollBar)
            {
                horizontalScrollBar.ValueChanged += OnHorizontalScrollBarValueChanged;
            }

            // 订阅垂直滚动条的值变化事件
            if (this.FindControl<ScrollBar>("VerticalScrollBar") is ScrollBar verticalScrollBar)
            {
                verticalScrollBar.ValueChanged += OnVerticalScrollBarValueChanged;
            }
        }

        /// <summary>
        /// 订阅主题变化
        /// </summary>
        private void SubscribeToThemeChanges()
        {
            try
            {
                // 从资源字典中获取设置服务的实例
                _settingsService = GetSettingsService();
                
                if (_settingsService != null)
                {
                    _settingsService.SettingsChanged += OnSettingsChanged;
                }

                // 订阅应用程序的属性变化事件
                if (Application.Current != null)
                {
                    Application.Current.PropertyChanged += OnApplicationPropertyChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"订阅主题变化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取设置服务实例
        /// </summary>
        private ISettingsService? GetSettingsService()
        {
            try
            {
                // 从资源字典中获取设置服务的实例
                // 如果不存在则使用默认实现
                return new DominoNext.Services.Implementation.SettingsService();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 处理设置变化事件
        /// </summary>
        private void OnSettingsChanged(object? sender, DominoNext.Services.Interfaces.SettingsChangedEventArgs e)
        {
            try
            {
                // 当颜色设置或主题变化时，强制刷新当前视图
                if (e.PropertyName?.EndsWith("Color") == true || e.PropertyName == "Theme")
                {
                    Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ForceRefreshTheme();
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理设置变化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理应用程序属性变化事件
        /// </summary>
        private void OnApplicationPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            try
            {
                if (e.Property.Name == nameof(Application.RequestedThemeVariant))
                {
                    Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ForceRefreshTheme();
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理应用程序属性变化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 强制刷新视图 - 重新绘制所有控件
        /// </summary>
        private void ForceRefreshTheme()
        {
            try
            {
                // 强制重绘
                this.InvalidateVisual();
                
                // 强制重新测量和排列
                this.InvalidateMeasure();
                this.InvalidateArrange();

                // 刷新自定义Canvas控件
                RefreshCustomCanvasControls();
                
                // 刷新所有子控件
                RefreshChildControls(this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"强制刷新视图失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新自定义Canvas控件
        /// </summary>
        private void RefreshCustomCanvasControls()
        {
            try
            {
                // 刷新钢琴卷Canvas
                if (this.FindControl<DominoNext.Views.Controls.Canvas.PianoRollCanvas>("PianoRollCanvas") is var pianoRollCanvas && pianoRollCanvas != null)
                {
                    pianoRollCanvas.InvalidateVisual();
                }

                // 刷新小节头Canvas
                if (this.FindControl<DominoNext.Views.Controls.Canvas.MeasureHeaderCanvas>("MeasureHeaderCanvas") is var measureHeaderCanvas && measureHeaderCanvas != null)
                {
                    measureHeaderCanvas.InvalidateVisual();
                }

                // 刷新事件视图Canvas
                if (this.FindControl<DominoNext.Views.Controls.Canvas.EventViewCanvas>("EventViewCanvas") is var eventViewCanvas && eventViewCanvas != null)
                {
                    eventViewCanvas.InvalidateVisual();
                }

                // 刷新力度视图Canvas
                if (this.FindControl<DominoNext.Views.Controls.Canvas.VelocityViewCanvas>("VelocityViewCanvas") is var velocityViewCanvas && velocityViewCanvas != null)
                {
                    velocityViewCanvas.InvalidateVisual();
                }

                // 刷新钢琴键控件
                if (this.FindControl<DominoNext.Views.Controls.PianoKeysControl>("PianoKeysControl") is var pianoKeysControl && pianoKeysControl != null)
                {
                    pianoKeysControl.InvalidateVisual();
                }

                // 刷新音符编辑层
                if (this.FindControl<DominoNext.Views.Controls.Editing.NoteEditingLayer>("NoteEditingLayer") is var noteEditingLayer && noteEditingLayer != null)
                {
                    noteEditingLayer.InvalidateVisual();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"刷新自定义Canvas控件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 递归刷新所有子控件
        /// </summary>
        private void RefreshChildControls(Control control)
        {
            try
            {
                // 强制重绘
                control.InvalidateVisual();
                
                // 强制重新测量和排列
                control.InvalidateMeasure();
                control.InvalidateArrange();

                // 递归刷新子控件
                if (control is Panel panel)
                {
                    foreach (Control child in panel.Children)
                    {
                        RefreshChildControls(child);
                    }
                }
                else if (control is ContentControl contentControl && contentControl.Content is Control childControl)
                {
                    RefreshChildControls(childControl);
                }
                else if (control is ScrollViewer scrollViewer && scrollViewer.Content is Control scrollContent)
                {
                    RefreshChildControls(scrollContent);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"刷新子控件失败: {ex.Message}");
            }
        }

        private void OnMainScrollViewerScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            if (_isUpdatingScroll) return;

            try
            {
                _isUpdatingScroll = true;

                // 检查是否接近末尾，如果是则自动扩展小节数量
                if (sender is ScrollViewer mainScrollViewer)
                {
                    var horizontalOffset = mainScrollViewer.Offset.X;
                    var viewportWidth = mainScrollViewer.Viewport.Width;
                    var extentWidth = mainScrollViewer.Extent.Width;

                    // 当滚动到末尾90%以上时，自动添加10个小节
                    if (horizontalOffset + viewportWidth >= extentWidth * 0.9)
                    {
                        if (DataContext is PianoRollViewModel pianoRoll)
                        {
                            pianoRoll.TotalMeasures += 10;
                        }
                    }
                }

                // 同步小节头的水平滚动
                SyncMeasureHeaderScroll();

                // 同步事件视图的水平滚动
                SyncEventViewScroll();

                // 同步钢琴键的垂直滚动
                SyncPianoKeysScroll();
            }
            finally
            {
                _isUpdatingScroll = false;
            }
        }

        private void OnPianoKeysScrollViewerScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            if (_isUpdatingScroll) return;

            try
            {
                _isUpdatingScroll = true;

                // 同步主视图的垂直滚动
                if (this.FindControl<ScrollViewer>("MainScrollViewer") is ScrollViewer mainScrollViewer &&
                    sender is ScrollViewer pianoKeysScrollViewer)
                {
                    var newOffset = new Avalonia.Vector(mainScrollViewer.Offset.X, pianoKeysScrollViewer.Offset.Y);
                    mainScrollViewer.Offset = newOffset;
                }
            }
            finally
            {
                _isUpdatingScroll = false;
            }
        }

        private void OnEventViewScrollViewerScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            if (_isUpdatingScroll) return;

            try
            {
                _isUpdatingScroll = true;

                // 同步主视图的水平滚动
                if (this.FindControl<ScrollViewer>("MainScrollViewer") is ScrollViewer mainScrollViewer &&
                    sender is ScrollViewer eventViewScrollViewer)
                {
                    var newOffset = new Avalonia.Vector(eventViewScrollViewer.Offset.X, mainScrollViewer.Offset.Y);
                    mainScrollViewer.Offset = newOffset;
                }

                // 同步小节头的水平滚动
                SyncMeasureHeaderScroll();
            }
            finally
            {
                _isUpdatingScroll = false;
            }
        }

        private void OnHorizontalScrollBarValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdatingScroll) return;

            try
            {
                _isUpdatingScroll = true;

                // 当水平滚动条的值变化时，更新主视图的水平偏移
                if (this.FindControl<ScrollViewer>("MainScrollViewer") is ScrollViewer mainScrollViewer &&
                    sender is ScrollBar scrollBar)
                {
                    var newOffset = new Avalonia.Vector(scrollBar.Value, mainScrollViewer.Offset.Y);
                    mainScrollViewer.Offset = newOffset;
                }

                SyncMeasureHeaderScroll();
                SyncEventViewScroll();
            }
            finally
            {
                _isUpdatingScroll = false;
            }
        }

        private void OnVerticalScrollBarValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdatingScroll) return;

            try
            {
                _isUpdatingScroll = true;

                // 当垂直滚动条的值变化时，更新主视图的垂直偏移
                if (this.FindControl<ScrollViewer>("MainScrollViewer") is ScrollViewer mainScrollViewer &&
                    sender is ScrollBar scrollBar)
                {
                    var newOffset = new Avalonia.Vector(mainScrollViewer.Offset.X, scrollBar.Value);
                    mainScrollViewer.Offset = newOffset;
                }

                SyncPianoKeysScroll();
            }
            finally
            {
                _isUpdatingScroll = false;
            }
        }

        private void SyncMeasureHeaderScroll()
        {
            if (this.FindControl<ScrollViewer>("MainScrollViewer") is ScrollViewer mainScrollViewer &&
                this.FindControl<ScrollViewer>("MeasureHeaderScrollViewer") is ScrollViewer measureHeaderScrollViewer)
            {
                measureHeaderScrollViewer.Offset = new Avalonia.Vector(mainScrollViewer.Offset.X, 0);
            }
        }

        private void SyncEventViewScroll()
        {
            if (this.FindControl<ScrollViewer>("MainScrollViewer") is ScrollViewer mainScrollViewer &&
                this.FindControl<ScrollViewer>("EventViewScrollViewer") is ScrollViewer eventViewScrollViewer)
            {
                eventViewScrollViewer.Offset = new Avalonia.Vector(mainScrollViewer.Offset.X, 0);
            }
        }

        private void SyncPianoKeysScroll()
        {
            if (this.FindControl<ScrollViewer>("MainScrollViewer") is ScrollViewer mainScrollViewer &&
                this.FindControl<ScrollViewer>("PianoKeysScrollViewer") is ScrollViewer pianoKeysScrollViewer)
            {
                pianoKeysScrollViewer.Offset = new Avalonia.Vector(0, mainScrollViewer.Offset.Y);
            }
        }

        /// <summary>
        /// 断开资源
        /// </summary>
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            try
            {
                // 取消订阅设置变化
                if (_settingsService != null)
                {
                    _settingsService.SettingsChanged -= OnSettingsChanged;
                }

                if (Application.Current != null)
                {
                    Application.Current.PropertyChanged -= OnApplicationPropertyChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"断开资源失败: {ex.Message}");
            }

            base.OnDetachedFromVisualTree(e);
        }
    }
}