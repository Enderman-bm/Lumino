using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Lumino.ViewModels.Editor;
using EnderDebugger;
using Lumino.Views.Controls.Editing.Input;
using Lumino.Services.Interfaces;
using Lumino.Services.Implementation;
using Lumino.Views.Rendering.Notes;
using Lumino.Views.Rendering.Tools;
using Lumino.Views.Rendering.Adapters;

namespace Lumino.Views.Controls.Editing
{
    /// <summary>
    /// 重构后的音符编辑层 - 符合MVVM最佳实践
    /// View层只负责渲染和事件转发，业务逻辑完全委托给ViewModel和模块
    /// </summary>
    public class NoteEditingLayer : Control, IRenderSyncTarget
    {
        #region 依赖属性
        public static readonly StyledProperty<PianoRollViewModel?> ViewModelProperty =
            AvaloniaProperty.Register<NoteEditingLayer, PianoRollViewModel?>(nameof(ViewModel));

        public PianoRollViewModel? ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        #endregion

        #region 渲染组件
        private readonly NoteRenderer _noteRenderer;
        private readonly DragPreviewRenderer _dragPreviewRenderer;
        private readonly ResizePreviewRenderer _resizePreviewRenderer;
        private readonly CreatingNoteRenderer _creatingNoteRenderer;
        private readonly SelectionBoxRenderer _selectionBoxRenderer;
        private readonly OnionSkinRenderer _onionSkinRenderer;
        #endregion

        #region 输入处理组件
        private readonly CursorManager _cursorManager;
        private readonly InputEventRouter _inputEventRouter;
        #endregion

        #region 缓存管理
        private readonly Dictionary<NoteViewModel, Rect> _visibleNoteCache = new();
        private bool _cacheInvalid = true;
        private Rect _lastViewport;
        #endregion

        #region 性能优化 - 同步渲染优化
        private readonly System.Timers.Timer _renderTimer;
        private bool _hasPendingRender = false;
        private bool _isDragging = false;
        private const double RenderInterval = 16.67; // 约60FPS
        private const double DragRenderInterval = 8.33; // 约120FPS for drag operations
        private readonly IRenderSyncService _renderSyncService;
        #endregion

        #region Vulkan渲染支持
        private const bool _useVulkanRendering = true; // 强制使用Vulkan渲染
        #endregion

        #region 构造函数
        public NoteEditingLayer()
        {
            EnderLogger.Instance.Debug("NoteEditingLayer", "NoteEditingLayer constructor - 模块化MVVM版本");

            // 初始化渲染组件
            _noteRenderer = new NoteRenderer();
            _dragPreviewRenderer = new DragPreviewRenderer();
            _dragPreviewRenderer.EnsureInitialized(); // 初始化拖拽预览渲染器
            _resizePreviewRenderer = new ResizePreviewRenderer();
            _creatingNoteRenderer = new CreatingNoteRenderer();
            _selectionBoxRenderer = new SelectionBoxRenderer();
            _onionSkinRenderer = new OnionSkinRenderer();

            // 初始化输入处理组件
            _cursorManager = new CursorManager(this);
            _inputEventRouter = new InputEventRouter();

            // 配置控件
            IsHitTestVisible = true;
            Focusable = true;
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;

            // 初始化性能优化
            _renderTimer = new System.Timers.Timer(RenderInterval);
            _renderTimer.Elapsed += OnRenderTimerElapsed;
            _renderTimer.AutoReset = false;

            // 初始化渲染同步服务
            _renderSyncService = RenderSyncService.Instance;
            _renderSyncService.RegisterTarget(this);

            // 初始化Vulkan渲染支持
            InitializeVulkanRendering();
        }

        /// <summary>
        /// 初始化Vulkan渲染支持 - 强制使用Vulkan
        /// </summary>
        private void InitializeVulkanRendering()
        {
            EnderLogger.Instance.Info("NoteEditingLayer", "音符编辑层: Vulkan渲染已强制启用");
            // Vulkan渲染已强制启用，无需再调用SetVulkanRendering
        }

        static NoteEditingLayer()
        {
            ViewModelProperty.Changed.AddClassHandler<NoteEditingLayer>((layer, e) =>
            {
                EnderLogger.Instance.Debug("NoteEditingLayer", $"ViewModel changed: {e.OldValue} -> {e.NewValue}");
                layer.OnViewModelChanged(e.OldValue as PianoRollViewModel, e.NewValue as PianoRollViewModel);
            });
        }
        #endregion

        #region ViewModel绑定
        private void OnViewModelChanged(PianoRollViewModel? oldViewModel, PianoRollViewModel? newViewModel)
        {
            // 取消旧的绑定
            if (oldViewModel != null)
            {
                UnsubscribeFromViewModelEvents(oldViewModel);
            }

            // 建立新的绑定
                if (newViewModel != null)
                {
                    SubscribeToViewModelEvents(newViewModel);
                    newViewModel.EditorCommands?.SetPianoRollViewModel(newViewModel);
                    EnderLogger.Instance.Info("NoteEditingLayer", $"ViewModel绑定成功. 当前工具: {newViewModel.CurrentTool}, 音符数量: {newViewModel.Notes.Count}");
                }

            InvalidateCache();
        }

        private void SubscribeToViewModelEvents(PianoRollViewModel viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            viewModel.Notes.CollectionChanged += OnNotesCollectionChanged;
            viewModel.CurrentTrackNotes.CollectionChanged += OnCurrentTrackNotesCollectionChanged;

            // 订阅模块事件 - 优化拖拽性能
            viewModel.DragModule.OnDragUpdated += OnDragUpdated;
            viewModel.DragModule.OnDragEnded += OnDragEnded;
            viewModel.DragModule.AnimationModule.OnDragAnimationUpdated += OnDragAnimationUpdated;
            viewModel.ResizeModule.OnResizeUpdated += OnResizeUpdated;
            viewModel.ResizeModule.OnResizeEnded += OnResizeEnded;
            viewModel.CreationModule.OnCreationUpdated += OnCreationUpdated;
            viewModel.PreviewModule.OnPreviewUpdated += OnPreviewUpdated;
            viewModel.SelectionModule.OnSelectionUpdated += OnSelectionUpdated;
        }

        private void UnsubscribeFromViewModelEvents(PianoRollViewModel viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.Notes.CollectionChanged -= OnNotesCollectionChanged;
            viewModel.CurrentTrackNotes.CollectionChanged -= OnCurrentTrackNotesCollectionChanged;

            // 取消订阅模块事件
            viewModel.DragModule.OnDragUpdated -= OnDragUpdated;
            viewModel.DragModule.OnDragEnded -= OnDragEnded;
            viewModel.DragModule.AnimationModule.OnDragAnimationUpdated -= OnDragAnimationUpdated;
            viewModel.ResizeModule.OnResizeUpdated -= OnResizeUpdated;
            viewModel.ResizeModule.OnResizeEnded -= OnResizeEnded;
            viewModel.CreationModule.OnCreationUpdated -= OnCreationUpdated;
            viewModel.PreviewModule.OnPreviewUpdated -= OnPreviewUpdated;
            viewModel.SelectionModule.OnSelectionUpdated -= OnSelectionUpdated;
        }

        /// <summary>
        /// 拖拽更新时的优化处理
        /// </summary>
        private void OnDragUpdated()
        {
            _isDragging = true;
            _renderSyncService.SetDragState(true);

            // 拖拽时使用同步渲染服务进行立即刷新
            _renderSyncService.ImmediateSyncRefresh();
        }

        /// <summary>
        /// 拖拽结束时的处理
        /// </summary>
        private void OnDragEnded()
        {
            _isDragging = false;
            _renderSyncService.SetDragState(false);

            // 拖拽结束后刷新缓存
            InvalidateCache();
        }

        /// <summary>
        /// 拖动动画更新时的处理
        /// </summary>
        private void OnDragAnimationUpdated()
        {
            if (_isDragging)
            {
                _renderSyncService.ImmediateSyncRefresh();
            }
        }

        /// <summary>
        /// 调整大小时的优化处理
        /// </summary>
        private void OnResizeUpdated()
        {
            _isDragging = true; // 调整大小也视为拖拽操作
            _renderSyncService.SetDragState(true);
            _renderSyncService.ImmediateSyncRefresh();
        }

        /// <summary>
        /// 调整大小结束时的处理
        /// </summary>
        private void OnResizeEnded()
        {
            _isDragging = false;
            _renderSyncService.SetDragState(false);
            InvalidateCache();
        }

        /// <summary>
        /// 创建音符时的处理
        /// </summary>
        private void OnCreationUpdated()
        {
            _renderSyncService.SyncRefresh();
        }

        /// <summary>
        /// 预览更新时的处理
        /// </summary>
        private void OnPreviewUpdated()
        {
            // 只在非拖拽状态下进行预览更新
            if (!_isDragging)
            {
                _renderSyncService.SyncRefresh();
            }
        }

        /// <summary>
        /// 选择更新时的处理
        /// </summary>
        private void OnSelectionUpdated()
        {
            _renderSyncService.SyncRefresh();
        }
        #endregion

        #region 事件处理 - 委托给输入路由器
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            _inputEventRouter.HandlePointerPressed(e, ViewModel, this);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            var position = e.GetPosition(this);
            _cursorManager.UpdateCursorForPosition(position, ViewModel);

            _inputEventRouter.HandlePointerMoved(e, ViewModel);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            _inputEventRouter.HandlePointerReleased(e, ViewModel);
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            _cursorManager.Reset();
            ViewModel?.PreviewModule?.ClearPreview();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            _inputEventRouter.HandleKeyDown(e, ViewModel);
        }
        #endregion

        #region 渲染 - 委托给渲染器
        public override void Render(DrawingContext context)
        {
            if (ViewModel == null) return;

            var bounds = Bounds;
            var viewport = new Rect(0, 0, bounds.Width, bounds.Height);
            VulkanDrawingContextAdapter? vulkanAdapter = null;

            try
            {
                // 始终创建Vulkan适配器
                vulkanAdapter = new VulkanDrawingContextAdapter(context);
                // 设置视口用于可见性测试
                vulkanAdapter.SetViewport(viewport);

                // 绘制透明背景以确保接收指针事件 - 始终使用Vulkan
                vulkanAdapter.DrawRectangle(Brushes.Transparent, null, viewport);

                if (_cacheInvalid || !viewport.Equals(_lastViewport))
                {
                    UpdateVisibleNotesCache(viewport);
                    _lastViewport = viewport;
                    _cacheInvalid = false;
                }

                // 渲染洋葱皮效果（其他音轨的音符）
                if (ViewModel.IsOnionSkinEnabled)
                {
                    _onionSkinRenderer.Render(context, vulkanAdapter, ViewModel, CalculateNoteRect, viewport);
                }

                // 刷新第一层批处理
                vulkanAdapter.FlushBatches();

                // 使用渲染器进行渲染
                _noteRenderer.RenderNotes(context, ViewModel, _visibleNoteCache, vulkanAdapter);

                // 渲染拖动动画音符（如果有的话）
                if (ViewModel.DragState.IsDragging && ViewModel.DragModule.AnimationModule != null)
                {
                    var animatedNotes = ViewModel.DragModule.AnimationModule.GetAllAnimatedNotes();
                    if (animatedNotes.Any())
                    {
                        _noteRenderer.RenderAnimatedNotes(context, ViewModel, animatedNotes, CalculateNoteRect, vulkanAdapter);
                    }
                }

                // 第二层批处理：特殊状态渲染
                // 拖拽预览
                if (ViewModel.DragState.IsDragging)
                {
                    _dragPreviewRenderer.Render(context, vulkanAdapter, ViewModel, CalculateNoteRect);
                }

                // 调整大小预览
                if (ViewModel.ResizeState.IsResizing)
                {
                    _resizePreviewRenderer.Render(context, vulkanAdapter, ViewModel, CalculateNoteRect);
                }

                // 创建中音符
                if (ViewModel.CreationModule.IsCreatingNote)
                {
                    _creatingNoteRenderer.Render(context, vulkanAdapter, ViewModel, CalculateNoteRect);
                }

                // 预览音符（保持原有条件）
                if (!ViewModel.CreationModule.IsCreatingNote &&
                    !_cursorManager.IsHoveringResizeEdge &&
                    !_cursorManager.IsHoveringNote &&
                    !ViewModel.DragState.IsDragging &&
                    !ViewModel.ResizeState.IsResizing)
                {
                    _noteRenderer.RenderPreviewNote(context, ViewModel, CalculateNoteRect, vulkanAdapter);
                }

                // 选择框
                _selectionBoxRenderer.Render(context, vulkanAdapter, ViewModel);

                // 最后刷新所有批处理
                vulkanAdapter.FlushBatches();
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "NoteEditingLayer", "NoteEditingLayer 渲染错误");
                // 仅记录错误，不切换渲染模式（强制使用Vulkan）
            }
            finally
            {
                // 确保释放Vulkan适配器资源
                vulkanAdapter?.Dispose();
            }
        }
        #endregion

        #region 缓存管理
        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var scrollingProperties = new[]
            {
        nameof(PianoRollViewModel.CurrentScrollOffset),
        nameof(PianoRollViewModel.VerticalScrollOffset)
    };

            if (Array.Exists(scrollingProperties, prop => prop == e.PropertyName))
            {
                // 滚动时清除音符坐标缓存并使用同步渲染，避免延迟
                ClearAllNoteCaches();
                InvalidateCache();
                InvalidateVisual(); // 直接同步触发，不通过异步调度
                return;
            }

            // 缩放变化时也需要清除音符坐标缓存
            var zoomProperties = new[]
            {
        nameof(PianoRollViewModel.Zoom),
        nameof(PianoRollViewModel.VerticalZoom)
    };

            if (Array.Exists(zoomProperties, prop => prop == e.PropertyName))
            {
                ClearAllNoteCaches();
                InvalidateCache();
            }

            // 其他属性仍使用异步渲染
            var renderingProperties = new[]
            {
        nameof(PianoRollViewModel.CurrentTool),
        nameof(PianoRollViewModel.GridQuantization)
    };

            if (Array.Exists(renderingProperties, prop => prop == e.PropertyName))
            {
                InvalidateCache();
            }
        }

        private void OnNotesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            InvalidateCache();
        }

        private void OnCurrentTrackNotesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            InvalidateCache();
        }

        private void InvalidateCache()
        {
            _cacheInvalid = true;
            ThrottledInvalidateVisual();
        }

        private void UpdateVisibleNotesCache(Rect viewport)
        {
            _visibleNoteCache.Clear();
            if (ViewModel?.CurrentTrackNotes == null) return;

            var expandedViewport = viewport.Inflate(100);

            foreach (var note in ViewModel.CurrentTrackNotes)
            {
                var noteRect = CalculateNoteRect(note);
                if (noteRect.Intersects(expandedViewport))
                {
                    _visibleNoteCache[note] = noteRect;
                }
            }
        }

        /// <summary>
        /// 优化的音符矩形计算 - 应用滚动偏移量
        /// </summary>
        private Rect CalculateNoteRect(NoteViewModel note)
        {
            if (ViewModel == null) return default;

            // 计算音符的绝对位置
            var absoluteX = note.GetX(ViewModel.TimeToPixelScale);
            var absoluteY = note.GetY(ViewModel.KeyHeight);
            var width = Math.Max(4, note.GetWidth(ViewModel.TimeToPixelScale));
            var height = Math.Max(2, note.GetHeight(ViewModel.KeyHeight) - 1);

            // 应用滚动偏移量
            var x = absoluteX - ViewModel.CurrentScrollOffset;
            var y = absoluteY - ViewModel.VerticalScrollOffset;

            return new Rect(x, y, width, height);
        }

        /// <summary>
        /// 清除所有音符的坐标缓存 - 用于缩放或滚动变化时
        /// </summary>
        private void ClearAllNoteCaches()
        {
            if (ViewModel?.CurrentTrackNotes == null) return;

            foreach (var note in ViewModel.CurrentTrackNotes)
            {
                // 清除NoteViewModel的坐标缓存
                note.InvalidateCoordinateCache();
            }
        }
        #endregion

        #region 性能优化 - 智能渲染策略
        /// <summary>
        /// 直接触发重绘，不使用频率限制（用于拖拽等实时操作）
        /// </summary>
        private void DirectInvalidateVisual()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual, Avalonia.Threading.DispatcherPriority.Render);
        }

        /// <summary>
        /// 限制频率的重绘（用于普通操作）
        /// </summary>
        private void ThrottledInvalidateVisual()
        {
            // 优先使用同步渲染服务
            _renderSyncService.SyncRefresh();
        }

        private void OnRenderTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (_hasPendingRender)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (_hasPendingRender)
                    {
                        InvalidateVisual();
                        _hasPendingRender = false;
                    }
                }, Avalonia.Threading.DispatcherPriority.Render);
            }
        }

        /// <summary>
        /// 实现IRenderSyncTarget接口
        /// </summary>
        public void RefreshRender()
        {
            InvalidateVisual();
        }
        #endregion
    }

    #region Command参数类

    /// <summary>
    /// 编辑器交互参数
    /// </summary>
    public class EditorInteractionArgs
    {
        public Point Position { get; set; }
        public EditorTool Tool { get; set; }
        public KeyModifiers Modifiers { get; set; }
        public EditorInteractionType InteractionType { get; set; }
    }

    /// <summary>
    /// 键盘命令参数
    /// </summary>
    public class KeyCommandArgs
    {
        public Key Key { get; set; }
        public KeyModifiers Modifiers { get; set; }
    }

    /// <summary>
    /// 交互类型
    /// </summary>
    public enum EditorInteractionType
    {
        Press,
        Move,
        Release
    }

    #endregion
}