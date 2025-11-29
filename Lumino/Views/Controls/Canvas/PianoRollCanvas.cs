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
using EnderDebugger;

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

    // 并行渲染配置（默认关闭）。启用并行渲染时需确保不会在后台线程直接调用 DrawingContext/Avalonia API。
    // 默认设为 false，以避免后台线程触发 Avalonia 的 "Call from invalid thread" 异常。
    public static bool EnableParallelPianoRendering { get; set; } = false;
    public static int ParallelWorkerCount { get; set; } = 16;

    // 性能监控开关（默认启用以收集基准数据）
    public static bool EnablePerformanceMonitoring
    {
        get => PerformanceMonitor.Enabled;
        set => PerformanceMonitor.Enabled = value;
    }

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
                    EnderLogger.Instance.Info("PianoRollCanvas", "Vulkan渲染已启用");
                }
                else
                {
                    EnderLogger.Instance.Info("PianoRollCanvas", "Vulkan渲染不可用，回退到Skia渲染");
                }
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "PianoRollCanvas", "Vulkan初始化失败");
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
            using var _ = PerformanceMonitor.Measure("PianoRollCanvas.Render");
            
            if (ViewModel == null) return;

            // 在 UI 线程读取并缓存对 ViewModel 的引用，避免在并行工作线程中通过 Avalonia 的 GetValue 访问 StyledProperty
            var vm = ViewModel;

            var bounds = Bounds;
            VulkanDrawingContextAdapter? vulkanAdapter = null;

            try
            {
                // 检查Vulkan服务状态，如果已初始化但_useVulkanRendering为false，则尝试启用
                // 为了确保稳定性，暂时禁用自动切换到Vulkan渲染
                /*
                if (!_useVulkanRendering && VulkanRenderService.Instance.IsInitialized)
                {
                    _useVulkanRendering = true;
                    EnderLogger.Instance.Info("PianoRollCanvas", "检测到Vulkan服务已就绪，启用Vulkan渲染");
                }
                */

                // 创建Vulkan适配器（如果启用）
                if (_useVulkanRendering)
                {
                    if (VulkanRenderService.Instance.IsInitialized)
                    {
                        using var _vk = PerformanceMonitor.Measure("CreateVulkanAdapter");
                        vulkanAdapter = new VulkanDrawingContextAdapter(context);
                        // 设置视口用于可见性测试
                        vulkanAdapter.SetViewport(bounds);
                    }
                    else
                    {
                        // 虽然启用了Vulkan，但服务未就绪，回退到Skia
                        // 这种情况可能发生在初始化过程中
                    }
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

                // 获取当前滚动偏移量和缩放参数（从缓存的 vm 读取，已在 UI 线程完成）
                var scrollOffset = vm.CurrentScrollOffset;
                var verticalScrollOffset = vm.VerticalScrollOffset;

                // 分层渲染策略，确保正确的绘制顺序
                // 0. 如果存在频谱图数据且可见，先绘制频谱图背景
                if (vm.HasSpectrogramData && vm.IsSpectrogramVisible && vm.SpectrogramData != null)
                {
                    try
                    {
                        EnderLogger.Instance.Debug("PianoRollCanvas", $"开始渲染频谱: HasSpectrogramData={vm.HasSpectrogramData}, IsVisible={vm.IsSpectrogramVisible}, Opacity={vm.SpectrogramOpacity}");
                        EnderLogger.Instance.Debug("PianoRollCanvas", $"频谱数据尺寸: {vm.SpectrogramData.GetLength(0)}x{vm.SpectrogramData.GetLength(1)}, 采样率: {vm.SpectrogramSampleRate}, 时长: {vm.SpectrogramDuration}s");
                        
                        _spectrogramRenderer.RenderSpectrogram(
                            context,
                            vm.SpectrogramData,
                            vm.SpectrogramSampleRate,
                            vm.SpectrogramDuration,
                            vm.SpectrogramMaxFrequency,
                            vm.SpectrogramOpacity,
                            bounds,
                            scrollOffset,
                            verticalScrollOffset,
                            vm.Zoom,
                            vm.VerticalZoom);
                        
                        EnderLogger.Instance.Debug("PianoRollCanvas", "频谱渲染完成");
                        
                        // 刷新频谱图批处理
                        if (_useVulkanRendering && vulkanAdapter != null)
                        {
                            vulkanAdapter.FlushBatches();
                        }
                    }
                    catch (Exception ex)
                    {
                        EnderLogger.Instance.LogException(ex, "PianoRollCanvas", "Error rendering spectrogram");
                        // 出错时不渲染频谱，但继续渲染其他内容
                    }
                }
                else
                {
                    EnderLogger.Instance.Debug("PianoRollCanvas", $"跳过频谱渲染: HasData={vm.HasSpectrogramData}, Visible={vm.IsSpectrogramVisible}, Data={vm.SpectrogramData != null}");
                }

                // 1. 绘制底层：水平网格线（键盘区域和分割线）
                // 在渲染开始时确保垂直网格渲染器在 UI 线程上初始化所需的 Avalonia 画笔/资源，
                // 以便并行工作线程可以安全地使用这些已缓存的画笔而不会触发跨线程异常。
                try
                {
                    _verticalGridRenderer?.EnsurePensInitialized();
                    _playheadRenderer?.EnsureInitialized();
                }
                catch (Exception ex)
                {
                    EnderLogger.Instance.LogException(ex, "PianoRollCanvas", "EnsurePensInitialized failed");
                }

                _horizontalGridRenderer.RenderHorizontalGrid(context, vulkanAdapter, vm, bounds, verticalScrollOffset);

                // 如果使用Vulkan，刷新第一层批处理
                if (_useVulkanRendering && vulkanAdapter != null)
                {
                    vulkanAdapter.FlushBatches();
                }

                // 并行渲染（实验性）：当前实现中后台线程可能会尝试直接调用 Avalonia 的 DrawingContext/VulkanAdapter，
                // 导致 "Call from invalid thread" 错误。为安全起见默认回退到串行渲染。若将来实现了纯数据分片计算并在 UI 线程进行绘制，
                // 可在此处重新启用并行逻辑。
                if (EnableParallelPianoRendering && ParallelWorkerCount > 1)
                {
                    EnderLogger.Instance.Debug("PianoRollCanvas", "[PianoParallel] Parallel rendering requested but disabled in this build to avoid Avalonia cross-thread calls; falling back to serial rendering.");

                    // 安全地回退到串行渲染（UI 线程执行所有绘制调用）
                    _verticalGridRenderer!.RenderVerticalGrid(context, vulkanAdapter, vm, bounds, scrollOffset);
                    if (_useVulkanRendering && vulkanAdapter != null) vulkanAdapter.FlushBatches();
                    _playheadRenderer!.RenderPlayhead(context, vulkanAdapter, vm, bounds, scrollOffset);
                    if (_useVulkanRendering && vulkanAdapter != null) vulkanAdapter.FlushBatches();
                }
                else
                {
                    // 2. 绘制中间层：垂直网格线（小节线和音符线）
                    _verticalGridRenderer!.RenderVerticalGrid(context, vulkanAdapter, vm, bounds, scrollOffset);

                    // 如果使用Vulkan，刷新第二层批处理
                    if (_useVulkanRendering && vulkanAdapter != null)
                    {
                        vulkanAdapter.FlushBatches();
                    }

                    // 3. 绘制顶层：播放头（需要在最上层）
                    _playheadRenderer!.RenderPlayhead(context, vulkanAdapter, vm, bounds, scrollOffset);

                    // 最后刷新所有批处理
                    if (_useVulkanRendering && vulkanAdapter != null)
                    {
                        vulkanAdapter.FlushBatches();
                    }
                }
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "PianoRollCanvas", "PianoRollCanvas 渲染异常");
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