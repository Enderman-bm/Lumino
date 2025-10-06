using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using System;
using Avalonia;
using Lumino.Services.Interfaces;
using Lumino.Views.Controls.Canvas;
using Lumino.Views.Controls;
using Lumino.ViewModels.Editor;
using System.Threading.Tasks;
using EnderDebugger;
using Lumino.Views.Controls.Editing.Input;

namespace Lumino.Views
{
    public partial class PianoRollView : UserControl
    {
        private bool _isUpdatingScroll = false;
        private ISettingsService? _settingsService;
        private readonly EnderLogger _logger;
        private readonly InputEventRouter _inputEventRouter;

        public PianoRollView()
        {
            _logger = EnderLogger.Instance;
            _inputEventRouter = new InputEventRouter();
            InitializeComponent();
            this.Loaded += OnLoaded;
            this.SizeChanged += OnSizeChanged;
            
            // 添加键盘事件处理
            this.KeyDown += OnKeyDown;
            
            // 添加鼠标滚轮事件处理
            this.PointerWheelChanged += OnPointerWheelChanged;
            
            // 订阅主题变更事件
            SubscribeToThemeChanges();
        }

        private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // 初始化视口尺寸
            UpdateViewportSize();

            // 设置工具栏的ViewModel
            if (this.FindControl<Controls.Toolbar>("ToolbarControl") is Controls.Toolbar toolbar &&
                DataContext is PianoRollViewModel viewModel)
            {
                _logger.Info("PianoRollView", $"设置Toolbar DataContext, Toolbar: {toolbar != null}, ViewModel: {viewModel != null}");
                _logger.Info("PianoRollView", $"ViewModel.Toolbar: {viewModel.Toolbar != null}");
                toolbar.SetViewModel(viewModel.Toolbar);
                _logger.Info("PianoRollView", $"Toolbar DataContext已设置: {toolbar.DataContext != null}");
            }
            else
            {
                _logger.Error("PianoRollView", "警告: 未能设置Toolbar DataContext!");
            }

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
                
                // 初始化滚动条的最大值，基于实际渲染高度
                if (DataContext is PianoRollViewModel vm)
                {
                    var actualPianoRenderHeight = GetActualPianoRenderHeight();
                    var maxScrollValue = Math.Max(0, vm.TotalHeight - actualPianoRenderHeight);
                    verticalScrollBar.Maximum = maxScrollValue;
                    
                    // 如果当前滚动位置超过了新的最大值，则设置到最大值
                    if (vm.VerticalScrollOffset > maxScrollValue)
                    {
                        vm.SetVerticalScrollOffset(maxScrollValue);
                        verticalScrollBar.Value = maxScrollValue;
                    }
                }
            }
            
            // 订阅ViewModel的属性变化，特别是事件视图可见性
            if (DataContext is PianoRollViewModel viewModel2)
            {
                viewModel2.PropertyChanged += OnViewModelPropertyChanged;
                viewModel2.InvalidateRequested += InvalidateVisual;
            }
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            UpdateViewportSize();
        }

        /// <summary>
        /// 更新视口尺寸 - 增强事件视图切换处理
        /// </summary>
        private void UpdateViewportSize()
        {
            if (DataContext is PianoRollViewModel viewModel)
            {
                var width = Math.Max(400, this.Bounds.Width - 80); // 减去左右边距
                var height = Math.Max(200, this.Bounds.Height - 120); // 减去上下边距
                
                // 检查事件视图的实际高度 - 增加稳定性检查
                var eventViewActualHeight = 0.0;
                if (viewModel.IsEventViewVisible)
                {
                    if (this.FindControl<EventViewPanel>("EventViewPanel") is EventViewPanel eventViewPanel && 
                        eventViewPanel.Bounds.Height > 10) // 增加最小高度检查
                    {
                        eventViewActualHeight = Math.Max(0, eventViewPanel.Bounds.Height);
                    }
                    else
                    {
                        // 使用稳定的事件视图高度估算
                        eventViewActualHeight = Math.Max(height * 0.25, 80);
                    }
                }
                
                // 钢琴卷帘的实际可用高度需要减去事件视图占用的高度
                var pianoRollAvailableHeight = height - eventViewActualHeight;
                if (eventViewActualHeight > 0 && pianoRollAvailableHeight > 100) // 增加最小可用高度检查
                {
                    viewModel.SetViewportSize(width, pianoRollAvailableHeight);
                }
                else
                {
                    viewModel.SetViewportSize(width, height);
                }
                
                // 延迟更新垂直滚动条，确保布局已完成 - 增加防抖处理
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (DataContext is PianoRollViewModel vm && this.FindControl<ScrollBar>("VerticalScrollBar") is ScrollBar verticalScrollBar)
                    {
                        var actualPianoRenderHeight = GetActualPianoRenderHeight();
                        var maxScrollValue = Math.Max(0, vm.TotalHeight - actualPianoRenderHeight);
                        
                        // 只有当最大值真正改变时才更新，避免触发不必要的事件
                        if (Math.Abs(verticalScrollBar.Maximum - maxScrollValue) > 0.5) // 增加阈值
                        {
                            verticalScrollBar.Maximum = maxScrollValue;
                        }
                        
                        // 如果当前滚动位置超出了新的最大值，调整它 - 增加保护逻辑
                        if (vm.VerticalScrollOffset > maxScrollValue && maxScrollValue > 1.0)
                        {
                            var clampedValue = maxScrollValue;
                            
                            // 只有在确实需要调整时才设置，避免不必要的重置
                            if (Math.Abs(vm.VerticalScrollOffset - clampedValue) > 1.0)
                            {
                                vm.SetVerticalScrollOffset(clampedValue);
                                
                                // 延迟设置滚动条值，避免递归
                                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                {
                                    if (Math.Abs(verticalScrollBar.Value - clampedValue) > 0.5)
                                    {
                                        verticalScrollBar.Value = clampedValue;
                                    }
                                });
                            }
                        }
                    }
                }, Avalonia.Threading.DispatcherPriority.Render);
            }
        }

        /// <summary>
        /// 处理钢琴键滚动视图滚动事件 - 增强防抖和上下文验证
        /// </summary>
        private void OnPianoKeysScrollViewerScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            if (DataContext is PianoRollViewModel viewModel && !_isUpdatingScroll && sender is ScrollViewer scrollViewer)
            {
                // 增强垂直方向滚动变化检测 - 使用ScrollViewer的当前偏移
                var currentVerticalOffset = scrollViewer.Offset.Y;
                
                // 获取实际渲染高度进行上下文验证
                var actualRenderHeight = GetActualPianoRenderHeight();
                var maxPossibleOffset = Math.Max(0, viewModel.TotalHeight - actualRenderHeight);
                
                // 将滚动偏移转换为ViewModel的垂直滚动偏移 - 增加范围验证
                var newVerticalOffset = Math.Max(0, Math.Min(maxPossibleOffset, currentVerticalOffset));
                
                // 只有当偏移量真正变化时才更新 - 增加阈值避免微小更新
                if (Math.Abs(viewModel.VerticalScrollOffset - newVerticalOffset) > 0.5)
                {
                    _isUpdatingScroll = true;
                    try
                    {
                        viewModel.SetVerticalScrollOffset(newVerticalOffset);
                        
                        // 延迟同步垂直滚动条，避免递归更新 - 增加防抖处理
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            if (!_isUpdatingScroll && this.FindControl<ScrollBar>("VerticalScrollBar") is ScrollBar verticalScrollBar)
                            {
                                var maxValue = verticalScrollBar.Maximum;
                                var clampedValue = Math.Min(maxValue, newVerticalOffset);
                                
                                if (Math.Abs(verticalScrollBar.Value - clampedValue) > 0.5) // 增加阈值
                                {
                                    verticalScrollBar.Value = clampedValue;
                                }
                            }
                        }, Avalonia.Threading.DispatcherPriority.Render);
                    }
                    finally
                    {
                        _isUpdatingScroll = false;
                    }
                }
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
                    viewModel.SetCurrentScrollOffset(scrollBar.Value);
                }

                // 同步事件视图滚动
                SyncEventViewScroll();
            }
            finally
            {
                _isUpdatingScroll = false;
            }
        }

        /// <summary>
        /// 处理垂直滚动条值变化事件
        /// </summary>
        private void OnVerticalScrollBarValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (DataContext is PianoRollViewModel viewModel && !_isUpdatingScroll)
            {
                // 检查滚动条的最大值，确保值有效
                if (sender is ScrollBar scrollBar && e.NewValue > scrollBar.Maximum)
                {
                    return; // 值超出范围，忽略
                }

                // 只有当值真正改变时才更新，避免递归
                if (Math.Abs(e.NewValue - e.OldValue) < 0.01)
                    return;

                _isUpdatingScroll = true;
                try
                {
                    // 更新ViewModel的垂直滚动偏移
                    viewModel.SetVerticalScrollOffset(e.NewValue);
                    
                    // 延迟同步钢琴键滚动，避免递归更新
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (!_isUpdatingScroll)
                        {
                            SyncPianoKeysScroll();
                        }
                    });
                }
                finally
                {
                    _isUpdatingScroll = false;
                }
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
            if (_isUpdatingScroll || DataContext is not PianoRollViewModel viewModel) return;

            try
            {
                _isUpdatingScroll = true;

                if (this.FindControl<ScrollViewer>("PianoKeysScrollViewer") is ScrollViewer pianoKeysScrollViewer)
                {
                    // 获取实际渲染高度
                    var actualPianoRenderHeight = GetActualPianoRenderHeight();
                    var maxScrollValue = Math.Max(0, viewModel.TotalHeight - actualPianoRenderHeight);
                    var clampedOffset = Math.Max(0, Math.Min(maxScrollValue, viewModel.VerticalScrollOffset));
                    
                    // 只有当偏移量真正改变时才更新，避免不必要的布局更新
                    var currentOffset = pianoKeysScrollViewer.Offset.Y;
                    if (Math.Abs(clampedOffset - currentOffset) > 0.1)
                    {
                        pianoKeysScrollViewer.Offset = new Avalonia.Vector(0, clampedOffset);
                        
                        // 延迟同步垂直滚动条，确保滚动已完成，但避免递归更新
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            if (!_isUpdatingScroll && this.FindControl<ScrollBar>("VerticalScrollBar") is ScrollBar verticalScrollBar)
                            {
                                var scrollbarMaxValue = Math.Max(0, verticalScrollBar.Maximum);
                                var clampedScrollValue = Math.Max(0, Math.Min(scrollbarMaxValue, clampedOffset));
                                
                                // 只有当滚动条值真正改变时才更新
                                if (Math.Abs(verticalScrollBar.Value - clampedScrollValue) > 0.1)
                                {
                                    verticalScrollBar.Value = clampedScrollValue;
                                }
                            }
                        });
                    }
                }
            }
            finally
            {
                _isUpdatingScroll = false;
            }
        }

        /// <summary>
        /// 处理ViewModel属性变化
        /// </summary>
        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PianoRollViewModel.IsEventViewVisible))
            {
                // 当事件视图可见性改变时，延迟更新视口尺寸以确保布局已完成
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    UpdateViewportSize();
                    
                    // 延迟更新滚动条状态
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (DataContext is PianoRollViewModel vm && this.FindControl<ScrollBar>("VerticalScrollBar") is ScrollBar verticalScrollBar)
                        {
                            var actualPianoRenderHeight = GetActualPianoRenderHeight();
                            var maxScrollValue = Math.Max(0, vm.TotalHeight - actualPianoRenderHeight);
                            
                            // 只有当最大值真正改变时才更新
                            if (Math.Abs(verticalScrollBar.Maximum - maxScrollValue) > 0.1)
                            {
                                verticalScrollBar.Maximum = maxScrollValue;
                            }
                            
                            // 确保当前值在有效范围内
                            if (verticalScrollBar.Value > maxScrollValue)
                            {
                                var clampedValue = maxScrollValue;
                                verticalScrollBar.Value = clampedValue;
                                vm.SetVerticalScrollOffset(clampedValue);
                            }
                        }
                    });
                });
            }
            else if (e.PropertyName == nameof(PianoRollViewModel.VerticalZoom) || 
                     e.PropertyName == nameof(PianoRollViewModel.KeyHeight) ||
                     e.PropertyName == nameof(PianoRollViewModel.TotalHeight) ||
                     e.PropertyName == nameof(PianoRollViewModel.VerticalScrollOffset))
            {
                // 当垂直相关属性变化时，延迟更新滚动条范围并同步钢琴键盘
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (DataContext is PianoRollViewModel viewModel)
                    {
                        var actualPianoRenderHeight = GetActualPianoRenderHeight();
                        var maxScrollValue = Math.Max(0, viewModel.TotalHeight - actualPianoRenderHeight);
                        
                        // 更新垂直滚动条
                        if (this.FindControl<ScrollBar>("VerticalScrollBar") is ScrollBar verticalScrollBar)
                        {
                            // 只有当最大值真正改变时才更新
                            if (Math.Abs(verticalScrollBar.Maximum - maxScrollValue) > 0.1)
                            {
                                verticalScrollBar.Maximum = maxScrollValue;
                            }
                            
                            // 如果当前滚动位置超出了新的范围，调整它
                            if (viewModel.VerticalScrollOffset > maxScrollValue)
                            {
                                var clampedValue = maxScrollValue;
                                viewModel.SetVerticalScrollOffset(clampedValue);
                                
                                // 延迟设置滚动条值
                                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                {
                                    if (Math.Abs(verticalScrollBar.Value - clampedValue) > 0.1)
                                    {
                                        verticalScrollBar.Value = clampedValue;
                                    }
                                });
                            }
                        }
                        
                        // 延迟同步钢琴键盘滚动
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            if (!_isUpdatingScroll && DataContext is PianoRollViewModel vm)
                            {
                                SyncPianoKeysScroll();
                            }
                        });
                    }
                });
            }
            else if (e.PropertyName == nameof(PianoRollViewModel.IsOnionSkinEnabled))
            {
                // 当洋葱皮模式切换时，立即刷新音符编辑层
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (this.FindControl<Lumino.Views.Controls.Editing.NoteEditingLayer>("NoteEditingLayer") is var noteEditingLayer && noteEditingLayer != null)
                    {
                        noteEditingLayer.InvalidateVisual();
                    }
                }, Avalonia.Threading.DispatcherPriority.Render);
            }
            else if (e.PropertyName == nameof(PianoRollViewModel.PlaybackPosition))
            {
                // 当演奏位置变化时，刷新音符编辑层和小节头部
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (this.FindControl<Lumino.Views.Controls.Editing.NoteEditingLayer>("NoteEditingLayer") is var noteEditingLayer && noteEditingLayer != null)
                    {
                        noteEditingLayer.InvalidateVisual();
                    }
                    if (this.FindControl<Lumino.Views.Controls.Canvas.MeasureHeaderCanvas>("MeasureHeaderCanvas") is var measureHeaderCanvas && measureHeaderCanvas != null)
                    {
                        measureHeaderCanvas.InvalidateVisual();
                    }
                }, Avalonia.Threading.DispatcherPriority.Render);
            }
        }

        /// <summary>
        /// 处理鼠标滚轮事件
        /// </summary>
        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (DataContext is not PianoRollViewModel viewModel) return;

            var delta = e.Delta;
            var isShiftPressed = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            var isCtrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);

            // 阻止事件冒泡，避免页面滚动
            e.Handled = true;

            try
            {
                _isUpdatingScroll = true;

                if (isCtrlPressed)
                {
                    // Ctrl + 滚轮：缩放功能
                    HandleZoomWithWheel(delta, viewModel);
                }
                else if (isShiftPressed || Math.Abs(delta.X) > Math.Abs(delta.Y))
                {
                    // Shift + 滚轮 或 水平滚轮：水平滚动
                    HandleHorizontalScroll(delta, viewModel);
                }
                else
                {
                    // 普通滚轮：垂直滚动
                    HandleVerticalScroll(delta, viewModel);
                }
            }
            finally
            {
                _isUpdatingScroll = false;
            }
        }

        /// <summary>
        /// 处理缩放操作
        /// </summary>
        private void HandleZoomWithWheel(Vector delta, PianoRollViewModel viewModel)
        {
            // 水平缩放
            if (Math.Abs(delta.Y) > 0.01)
            {
                var currentZoom = viewModel.ZoomSliderValue;
                var zoomDelta = delta.Y * 5; // 调整缩放灵敏度
                var newZoom = Math.Max(0, Math.Min(100, currentZoom + zoomDelta));
                viewModel.SetZoomSliderValue(newZoom);
            }

            // 垂直缩放（如果有水平滚轮）
            if (Math.Abs(delta.X) > 0.01)
            {
                var currentVerticalZoom = viewModel.VerticalZoomSliderValue;
                var zoomDelta = delta.X * 5; // 调整缩放灵敏度
                var newVerticalZoom = Math.Max(0, Math.Min(100, currentVerticalZoom + zoomDelta));
                viewModel.SetVerticalZoomSliderValue(newVerticalZoom);
            }
        }

        /// <summary>
        /// 处理水平滚动
        /// </summary>
        private void HandleHorizontalScroll(Vector delta, PianoRollViewModel viewModel)
        {
            var scrollDelta = (Math.Abs(delta.X) > Math.Abs(delta.Y) ? delta.X : delta.Y) * 50; // 调整滚动灵敏度
            var newOffset = Math.Max(0, Math.Min(viewModel.MaxScrollExtent, viewModel.CurrentScrollOffset - scrollDelta));
            
            if (Math.Abs(newOffset - viewModel.CurrentScrollOffset) > 0.1)
            {
                viewModel.SetCurrentScrollOffset(newOffset);
                
                // 更新水平滚动条
                if (this.FindControl<ScrollBar>("HorizontalScrollBar") is ScrollBar horizontalScrollBar)
                {
                    horizontalScrollBar.Value = newOffset;
                }
            }
        }

        /// <summary>
        /// 处理垂直滚动
        /// </summary>
        private void HandleVerticalScroll(Vector delta, PianoRollViewModel viewModel)
        {
            var scrollDelta = delta.Y * 30; // 调整滚动灵敏度
            
            // 获取实际的钢琴渲染高度
            var actualPianoRenderHeight = GetActualPianoRenderHeight();
            
            // 计算有效的滚动范围：基于实际渲染区域
            var pianoContentHeight = viewModel.TotalHeight;
            var maxScrollValue = Math.Max(0, pianoContentHeight - actualPianoRenderHeight);
            
            // 修复重置逻辑：只有当内容高度小于渲染高度时才重置
            if (pianoContentHeight <= actualPianoRenderHeight)
            {
                if (viewModel.VerticalScrollOffset != 0)
                {
                    viewModel.SetVerticalScrollOffset(0);
                }
                return;
            }
            
            // 应用滚动增量并限制在有效范围内
            var newOffset = Math.Max(0, Math.Min(maxScrollValue, viewModel.VerticalScrollOffset - scrollDelta));
            
            // 只有当偏移量真正改变时才更新（增加阈值避免微小抖动）
            if (Math.Abs(newOffset - viewModel.VerticalScrollOffset) > 0.5)
            {
                viewModel.SetVerticalScrollOffset(newOffset);
                
                // 延迟更新垂直滚动条，避免递归更新
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (!_isUpdatingScroll && this.FindControl<ScrollBar>("VerticalScrollBar") is ScrollBar verticalScrollBar)
                    {
                        // 只有当最大值真正改变时才更新
                        if (Math.Abs(verticalScrollBar.Maximum - maxScrollValue) > 0.1)
                        {
                            verticalScrollBar.Maximum = maxScrollValue;
                        }
                        
                        // 只有当值真正不同时才更新
                        if (Math.Abs(verticalScrollBar.Value - newOffset) > 0.1)
                        {
                            verticalScrollBar.Value = newOffset;
                        }
                    }
                });
                
                // 延迟同步钢琴键滚动
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (!_isUpdatingScroll)
                    {
                        SyncPianoKeysScroll();
                    }
                });
            }
        }

        /// <summary>
        /// 获取钢琴的实际渲染高度
        /// </summary>
        private double GetActualPianoRenderHeight()
        {
            // 优先使用钢琴渲染区域的实际高度
            if (this.FindControl<Border>("PianoRenderArea") is Border pianoRenderArea && pianoRenderArea.Bounds.Height > 0)
            {
                return Math.Max(100, pianoRenderArea.Bounds.Height);
            }
            
            // 从主内容区域计算
            var totalHeight = this.Bounds.Height;
            if (totalHeight <= 0) return 200; // 返回默认高度
            
            var toolbarHeight = 44; // 工具栏高度
            var measureHeaderHeight = 30; // 小节标题高度
            var bottomControlHeight = 20; // 底部控制栏高度
            var eventViewHeight = 0.0;
            
            // 计算事件视图占用的高度 - 增加稳定性检查
            if (DataContext is PianoRollViewModel viewModel && viewModel.IsEventViewVisible)
            {
                if (this.FindControl<EventViewPanel>("EventViewPanel") is EventViewPanel eventViewPanel && eventViewPanel.Bounds.Height > 10)
                {
                    eventViewHeight = Math.Max(0, eventViewPanel.Bounds.Height) + 4; // 加上分隔条高度
                }
                else
                {
                    // 使用稳定的事件视图高度（总高度的1/3），但确保最小值
                    eventViewHeight = Math.Max(totalHeight * 0.3, 100);
                }
            }
            
            // 钢琴实际可用的渲染高度 - 增加最小高度保护
            var actualHeight = totalHeight - toolbarHeight - measureHeaderHeight - bottomControlHeight - eventViewHeight;
            return Math.Max(200, actualHeight); // 提高最小高度要求，避免过小导致重置
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
                return new Lumino.Services.Implementation.SettingsService();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 处理设置变更事件
        /// </summary>
        private void OnSettingsChanged(object? sender, Lumino.Services.Interfaces.SettingsChangedEventArgs e)
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
                if (this.FindControl<Lumino.Views.Controls.Canvas.PianoRollCanvas>("PianoRollCanvas") is var pianoRollCanvas && pianoRollCanvas != null)
                {
                    pianoRollCanvas.InvalidateVisual();
                }

                // 刷新小节头Canvas
                if (this.FindControl<Lumino.Views.Controls.Canvas.MeasureHeaderCanvas>("MeasureHeaderCanvas") is var measureHeaderCanvas && measureHeaderCanvas != null)
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
                if (this.FindControl<Lumino.Views.Controls.Editing.NoteEditingLayer>("NoteEditingLayer") is var noteEditingLayer && noteEditingLayer != null)
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
                
                // 取消订阅ViewModel事件
                if (DataContext is PianoRollViewModel viewModel)
                {
                    viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                    viewModel.InvalidateRequested -= InvalidateVisual;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"释放资源失败: {ex.Message}");
            }
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is PianoRollViewModel viewModel)
            {
                _inputEventRouter.HandleKeyDown(e, viewModel);
            }
        }
    }
}