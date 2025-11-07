using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using Lumino.ViewModels.Editor;
using Lumino.Services.Interfaces;
using Lumino.Services.Implementation;
using Lumino.Views.Rendering.Utils;
using Lumino.Views.Rendering.Grids;
using Lumino.Views.Rendering.Adapters;
using Lumino.Views.Rendering.Background;
using System;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

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
        private readonly Lumino.Views.Rendering.Background.SpectrogramRenderer _spectrogramRenderer;

        // 使用动态画刷获取，确保与主题状态同步
        private IBrush MainBackgroundBrush => RenderingUtils.GetResourceBrush("MainCanvasBackgroundBrush", "#FFFFFFFF");

        // Vulkan渲染支持
        private bool _useVulkanRendering = false;

    // 并行渲染配置（默认开启）。设置为 true 会尝试使用 Parallel 分片并发执行渲染任务。
    // 注意：实际绘制到 DrawingContext 仍会在锁内进行以避免线程安全问题，但每个工作线程会执行自己的分区工作并记录调用堆栈，便于调试。
    public static bool EnableParallelPianoRendering { get; set; } = true;
    public static int ParallelWorkerCount { get; set; } = 16;

    // 用于序列化对 DrawingContext / VulkanAdapter 的实际绘制调用的锁
    private readonly object _drawLock = new object();

        public PianoRollCanvas()
        {
            // 使用全局渲染同步服务
            _renderSyncService = RenderSyncService.Instance;
            _renderSyncService.RegisterTarget(this);

            // 初始化渲染器
            _horizontalGridRenderer = new HorizontalGridRenderer();
            _verticalGridRenderer = new VerticalGridRenderer();
            _playheadRenderer = new PlayheadRenderer();
            _spectrogramRenderer = new Lumino.Views.Rendering.Background.SpectrogramRenderer();

            // 检测是否启用Vulkan渲染
            InitializeVulkanRendering();
        }

        /// <summary>
        /// 初始化Vulkan渲染支持
        /// </summary>
        private void InitializeVulkanRendering()
        {
            try
            {
                // 检查系统是否支持Vulkan
                _useVulkanRendering = VulkanRenderService.Instance.IsSupported;
                if (_useVulkanRendering)
                {
                    System.Diagnostics.Debug.WriteLine("Vulkan渲染已启用");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Vulkan渲染不可用，回退到Skia渲染");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Vulkan初始化失败: {ex.Message}");
                _useVulkanRendering = false;
            }
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
            else if (e.PropertyName == nameof(PianoRollViewModel.SpectrogramData) ||
                     e.PropertyName == nameof(PianoRollViewModel.IsSpectrogramVisible))
            {
                // 频谱图数据或可见性变化时刷新
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
            VulkanDrawingContextAdapter? vulkanAdapter = null;

            try
            {
                // 创建Vulkan适配器（如果启用）
                if (_useVulkanRendering)
                {
                    vulkanAdapter = new VulkanDrawingContextAdapter(context);
                    // 设置视口用于可见性测试
                    vulkanAdapter.SetViewport(bounds);
                }

                // 绘制背景 - 使用动态获取，确保与主题同步
                if (_useVulkanRendering && vulkanAdapter != null)
                {
                    vulkanAdapter.DrawRectangle(MainBackgroundBrush, null, bounds);
                }
                else
                {
                    context.DrawRectangle(MainBackgroundBrush, null, bounds);
                }

                // 获取当前滚动偏移量和缩放参数
                var scrollOffset = ViewModel.CurrentScrollOffset;
                var verticalScrollOffset = ViewModel.VerticalScrollOffset;

                // 分层渲染策略，确保正确的绘制顺序
                // 0. 如果存在频谱图数据且可见，先绘制频谱图背景
                if (ViewModel.HasSpectrogramData && ViewModel.IsSpectrogramVisible && ViewModel.SpectrogramData != null)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"开始渲染频谱: HasSpectrogramData={ViewModel.HasSpectrogramData}, IsVisible={ViewModel.IsSpectrogramVisible}, Opacity={ViewModel.SpectrogramOpacity}");
                        System.Diagnostics.Debug.WriteLine($"频谱数据尺寸: {ViewModel.SpectrogramData.GetLength(0)}x{ViewModel.SpectrogramData.GetLength(1)}, 采样率: {ViewModel.SpectrogramSampleRate}, 时长: {ViewModel.SpectrogramDuration}s");
                        
                        _spectrogramRenderer.RenderSpectrogram(
                            context,
                            ViewModel.SpectrogramData,
                            ViewModel.SpectrogramSampleRate,
                            ViewModel.SpectrogramDuration,
                            ViewModel.SpectrogramMaxFrequency,
                            ViewModel.SpectrogramOpacity,
                            bounds,
                            scrollOffset,
                            verticalScrollOffset,
                            ViewModel.Zoom,
                            ViewModel.VerticalZoom);
                        
                        System.Diagnostics.Debug.WriteLine("频谱渲染完成");
                        
                        // 刷新频谱图批处理
                        if (_useVulkanRendering && vulkanAdapter != null)
                        {
                            vulkanAdapter.FlushBatches();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error rendering spectrogram: {ex.Message}, {ex.StackTrace}");
                        // 出错时不渲染频谱，但继续渲染其他内容
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"跳过频谱渲染: HasData={ViewModel.HasSpectrogramData}, Visible={ViewModel.IsSpectrogramVisible}, Data={ViewModel.SpectrogramData != null}");
                }

                // 1. 绘制底层：水平网格线（键盘区域和分割线）
                _horizontalGridRenderer.RenderHorizontalGrid(context, vulkanAdapter, ViewModel, bounds, verticalScrollOffset);

                // 如果使用Vulkan，刷新第一层批处理
                if (_useVulkanRendering && vulkanAdapter != null)
                {
                    vulkanAdapter.FlushBatches();
                }

                // 支持可选的并行分片渲染。默认关闭以保持与现有逻辑兼容。
                if (EnableParallelPianoRendering && ParallelWorkerCount > 1)
                {
                    try
                    {
                        var workerCount = Math.Max(1, ParallelWorkerCount);
                        var stripeWidth = bounds.Width / workerCount;

                        // 并行执行分片任务，每个任务会记录线程信息与堆栈，然后在锁内执行实际绘制调用
                        ParallelOptions po = new ParallelOptions() { MaxDegreeOfParallelism = workerCount };
                        Parallel.For(0, workerCount, po, i =>
                        {
                            try
                            {
                                // 记录线程 id 与调用堆栈，便于在日志/诊断中查看
                                var tid = Thread.CurrentThread.ManagedThreadId;
                                Debug.WriteLine($"[PianoParallel] Thread {tid} starting stripe {i}/{workerCount}");
                                Debug.WriteLine($"[PianoParallel] Stack (Thread {tid}):\n{Environment.StackTrace}");

                                // 计算分片区域，注意保持浮点安全
                                var x = bounds.X + i * stripeWidth;
                                var w = (i == workerCount - 1) ? (bounds.Right - x) : stripeWidth;
                                var stripe = new Rect(x, bounds.Y, w, bounds.Height);

                                // 在各自线程做一些可并行的预计算（如果有）
                                // TODO: 如果渲染器提供分片/计算接口，应在此处调用。

                                // 实际绘制到 DrawingContext / VulkanAdapter 必须序列化
                                lock (_drawLock)
                                {
                                    // 水平网格已绘制，下面分片绘制垂直网格与播放头的一部分
                                    _verticalGridRenderer.RenderVerticalGrid(context, vulkanAdapter, ViewModel, stripe, scrollOffset);
                                    _playheadRenderer.RenderPlayhead(context, vulkanAdapter, ViewModel, stripe, scrollOffset);

                                    if (_useVulkanRendering && vulkanAdapter != null)
                                    {
                                        vulkanAdapter.FlushBatches();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[PianoParallel] stripe {i} exception: {ex.Message}\n{ex.StackTrace}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"并行渲染失败，回退到串行渲染: {ex.Message}\n{ex.StackTrace}");

                        // 回退到串行渲染以确保不丢失绘制
                        _verticalGridRenderer.RenderVerticalGrid(context, vulkanAdapter, ViewModel, bounds, scrollOffset);
                        if (_useVulkanRendering && vulkanAdapter != null) vulkanAdapter.FlushBatches();
                        _playheadRenderer.RenderPlayhead(context, vulkanAdapter, ViewModel, bounds, scrollOffset);
                        if (_useVulkanRendering && vulkanAdapter != null) vulkanAdapter.FlushBatches();
                    }
                }
                else
                {
                    // 2. 绘制中间层：垂直网格线（小节线和音符线）
                    _verticalGridRenderer.RenderVerticalGrid(context, vulkanAdapter, ViewModel, bounds, scrollOffset);

                    // 如果使用Vulkan，刷新第二层批处理
                    if (_useVulkanRendering && vulkanAdapter != null)
                    {
                        vulkanAdapter.FlushBatches();
                    }

                    // 3. 绘制顶层：播放头（需要在最上层）
                    _playheadRenderer.RenderPlayhead(context, vulkanAdapter, ViewModel, bounds, scrollOffset);

                    // 最后刷新所有批处理
                    if (_useVulkanRendering && vulkanAdapter != null)
                    {
                        vulkanAdapter.FlushBatches();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PianoRollCanvas渲染错误: {ex.Message}");
                // 发生异常时切换回Skia渲染
                _useVulkanRendering = false;
            }
            finally
            {
                // 确保释放Vulkan适配器资源
                vulkanAdapter?.Dispose();
            }
        }

        /// <summary>
        /// 在主题切换时清除内部渲染器的缓存，确保下一次渲染使用新的主题画刷
        /// </summary>
        public void ClearRendererCaches()
        {
            try
            {
                _horizontalGridRenderer?.ClearCache();
            }
            catch { }
            try
            {
                var vgr = _verticalGridRenderer;
                var mi = vgr?.GetType().GetMethod("ClearCache");
                mi?.Invoke(vgr, null);
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
            // 强制重绘
            InvalidateVisual();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            // 从渲染同步服务注销
            _renderSyncService?.UnregisterTarget(this);
            // 清除渲染缓存
            RenderingUtils.ClearBrushCache();
            base.OnDetachedFromVisualTree(e);
        }
    }
}