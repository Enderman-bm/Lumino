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