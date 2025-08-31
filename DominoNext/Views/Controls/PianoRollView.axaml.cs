using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System;
using Avalonia;
using DominoNext.Services.Interfaces;
using DominoNext.Views.Controls.Canvas;
using DominoNext.Views.Controls;
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
            this.SizeChanged += OnSizeChanged;
            
            // 订阅主题变更事件
            SubscribeToThemeChanges();
        }

        private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // 初始化视口尺寸
            UpdateViewportSize();

            // 订阅钢琴键滚动视图的滚动事件
            if (this.FindControl<ScrollViewer>("PianoKeysScrollViewer") is ScrollViewer pianoKeysScrollViewer)
            {
                pianoKeysScrollViewer.ScrollChanged += OnPianoKeysScrollViewerScrollChanged;
            }

            // 事件视图现在使用Canvas渲染，不再需要ScrollViewer同步

            // 订阅底部水平滚动条的值变化事件
            if (this.FindControl<ScrollBar>("HorizontalScrollBar") is ScrollBar horizontalScrollBar)
            {
                horizontalScrollBar.ValueChanged += OnHorizontalScrollBarValueChanged;
            }

            // 订阅右侧垂直滚动条的值变化事件
            if (this.FindControl<ScrollBar>("VerticalScrollBar") is ScrollBar verticalScrollBar)
            {
                verticalScrollBar.ValueChanged += OnVerticalScrollBarValueChanged;
            }
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            UpdateViewportSize();
        }

        /// <summary>
        /// 更新视口尺寸
        /// </summary>
        private void UpdateViewportSize()
        {
            if (DataContext is PianoRollViewModel viewModel)
            {
                var width = Math.Max(400, this.Bounds.Width - 80); // 减去左右边距
                var height = Math.Max(200, this.Bounds.Height - 120); // 减去上下边距
                viewModel.SetViewportSize(width, height);
            }
        }

        private void OnPianoKeysScrollViewerScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            if (_isUpdatingScroll || DataContext is not PianoRollViewModel viewModel) return;

            try
            {
                _isUpdatingScroll = true;

                // 同步垂直滚动
                if (sender is ScrollViewer pianoKeysScrollViewer)
                {
                    viewModel.VerticalScrollOffset = pianoKeysScrollViewer.Offset.Y;
                }
            }
            finally
            {
                _isUpdatingScroll = false;
            }
        }

        private void OnHorizontalScrollBarValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdatingScroll || DataContext is not PianoRollViewModel viewModel) return;

            try
            {
                _isUpdatingScroll = true;

                // 更新ViewModel中的滚动偏移量
                if (sender is ScrollBar scrollBar)
                {
                    viewModel.CurrentScrollOffset = scrollBar.Value;
                }

                // 同步事件视图滚动
                SyncEventViewScroll();
            }
            finally
            {
                _isUpdatingScroll = false;
            }
        }

        private void OnVerticalScrollBarValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdatingScroll || DataContext is not PianoRollViewModel viewModel) return;

            try
            {
                _isUpdatingScroll = true;

                // 更新ViewModel中的垂直滚动偏移量
                if (sender is ScrollBar scrollBar)
                {
                    viewModel.VerticalScrollOffset = scrollBar.Value;
                }

                // 同步钢琴键滚动
                SyncPianoKeysScroll();
            }
            finally
            {
                _isUpdatingScroll = false;
            }
        }

        private void SyncEventViewScroll()
        {
            if (DataContext is not PianoRollViewModel viewModel) return;

            if (this.FindControl<EventViewPanel>("EventViewPanel") is EventViewPanel eventViewPanel)
            {
                // 事件视图现在通过渲染同步服务自动同步，无需手动同步滚动
                eventViewPanel.SyncHorizontalScroll(viewModel.CurrentScrollOffset);
            }
        }

        private void SyncPianoKeysScroll()
        {
            if (DataContext is not PianoRollViewModel viewModel) return;

            if (this.FindControl<ScrollViewer>("PianoKeysScrollViewer") is ScrollViewer pianoKeysScrollViewer)
            {
                pianoKeysScrollViewer.Offset = new Avalonia.Vector(0, viewModel.VerticalScrollOffset);
            }
        }

        /// <summary>
        /// 订阅主题变更事件
        /// </summary>
        private void SubscribeToThemeChanges()
        {
            try
            {
                // 尝试从服务定位器或依赖注入获取设置服务
                _settingsService = GetSettingsService();
                
                if (_settingsService != null)
                {
                    _settingsService.SettingsChanged += OnSettingsChanged;
                }

                // 监听应用程序级别的主题变更
                if (Application.Current != null)
                {
                    Application.Current.PropertyChanged += OnApplicationPropertyChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"订阅主题变更事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取设置服务（简化版本）
        /// </summary>
        private ISettingsService? GetSettingsService()
        {
            try
            {
                // 如果有依赖注入容器，可以从这里获取
                // 这里使用简化的实现
                return new DominoNext.Services.Implementation.SettingsService();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 处理设置变更事件
        /// </summary>
        private void OnSettingsChanged(object? sender, DominoNext.Services.Interfaces.SettingsChangedEventArgs e)
        {
            try
            {
                // 当颜色相关设置变更时，强制刷新当前视图
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
                System.Diagnostics.Debug.WriteLine($"处理设置变更失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理应用程序属性变更事件
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
                System.Diagnostics.Debug.WriteLine($"处理应用程序属性变更失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 强制刷新主题 - 针对钢琴卷帘视图优化
        /// </summary>
        private void ForceRefreshTheme()
        {
            try
            {
                // 强制重新渲染
                this.InvalidateVisual();
                
                // 强制重新测量和排列
                this.InvalidateMeasure();
                this.InvalidateArrange();

                // 特别处理自定义Canvas控件
                RefreshCustomCanvasControls();
                
                // 刷新所有子控件
                RefreshChildControls(this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"强制刷新主题失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新自定义Canvas控件
        /// </summary>
        private void RefreshCustomCanvasControls()
        {
            try
            {
                // 刷新钢琴卷帘Canvas
                if (this.FindControl<DominoNext.Views.Controls.Canvas.PianoRollCanvas>("PianoRollCanvas") is var pianoRollCanvas && pianoRollCanvas != null)
                {
                    pianoRollCanvas.InvalidateVisual();
                }

                // 刷新小节头Canvas
                if (this.FindControl<DominoNext.Views.Controls.Canvas.MeasureHeaderCanvas>("MeasureHeaderCanvas") is var measureHeaderCanvas && measureHeaderCanvas != null)
                {
                    measureHeaderCanvas.InvalidateVisual();
                }

                // 刷新事件视图面板内的Canvas
                if (this.FindControl<EventViewPanel>("EventViewPanel") is EventViewPanel eventViewPanel)
                {
                    eventViewPanel.InvalidateVisual();
                }

                // 刷新钢琴键控件
                if (this.FindControl<PianoKeysCanvas>("PianoKeysCanvas") is var PianoKeysCanvas && PianoKeysCanvas != null)
                {
                    PianoKeysCanvas.InvalidateVisual();
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
        /// 递归刷新子控件
        /// </summary>
        private void RefreshChildControls(Control control)
        {
            try
            {
                // 强制重新渲染
                control.InvalidateVisual();
                
                // 强制重新测量和排列
                control.InvalidateMeasure();
                control.InvalidateArrange();

                // 递归处理子控件
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

        /// <summary>
        /// 释放资源
        /// </summary>
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            try
            {
                // 取消订阅事件
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
                System.Diagnostics.Debug.WriteLine($"释放资源失败: {ex.Message}");
            }

            base.OnDetachedFromVisualTree(e);
        }
    }
}