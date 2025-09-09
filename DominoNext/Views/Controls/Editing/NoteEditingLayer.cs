using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using DominoNext.Views.Controls.Editing.Input;
using DominoNext.Services.Interfaces;
using DominoNext.Services.Implementation;
using DominoNext.Views.Rendering.Notes;
using DominoNext.Views.Rendering.Tools;

namespace DominoNext.Views.Controls.Editing
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
        #endregion

        #region 输入处理组件
        private readonly CursorManager _cursorManager;
        private readonly InputEventRouter _inputEventRouter;
        #endregion

        #region 缓存管理
        private readonly Dictionary<NoteViewModel, Rect> _visibleNoteCache = new();
        private bool _cacheInvalid = true;
        private Rect _lastViewport;

        // 空间索引优化
        private readonly SpatialIndex<NoteViewModel> _spatialIndex = new();
        private bool _spatialIndexDirty = true;
        private double _lastTimeToPixelScale = double.NaN;
        private double _lastKeyHeight = double.NaN;
        private double _lastScrollX = double.NaN;
        private double _lastScrollY = double.NaN;

        // 性能监控
        private readonly System.Diagnostics.Stopwatch _cacheUpdateStopwatch = new();
        private readonly Queue<double> _cacheUpdateTimes = new();
        private const int PERF_SAMPLE_SIZE = 30;
        #endregion

        #region 性能优化 - 同步渲染优化
        private readonly System.Timers.Timer _renderTimer;
        private bool _hasPendingRender = false;
        private bool _isDragging = false;
        private const double RenderInterval = 16.67; // 约60FPS
        private const double DragRenderInterval = 8.33; // 约120FPS for drag operations
        private readonly IRenderSyncService _renderSyncService;
        #endregion

        #region 构造函数
        public NoteEditingLayer()
        {
            Debug.WriteLine("NoteEditingLayer constructor - 模块化MVVM版本");

            // 初始化渲染组件
            _noteRenderer = new NoteRenderer();
            _dragPreviewRenderer = new DragPreviewRenderer();
            _resizePreviewRenderer = new ResizePreviewRenderer();
            _creatingNoteRenderer = new CreatingNoteRenderer();
            _selectionBoxRenderer = new SelectionBoxRenderer();

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
        }

        static NoteEditingLayer()
        {
            ViewModelProperty.Changed.AddClassHandler<NoteEditingLayer>((layer, e) =>
            {
                Debug.WriteLine($"ViewModel changed: {e.OldValue} -> {e.NewValue}");
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
                Debug.WriteLine($"ViewModel绑定成功. 当前工具: {newViewModel.CurrentTool}, 音符数量: {newViewModel.Notes.Count}");
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

            // 绘制透明背景以确保接收指针事件
            context.DrawRectangle(Brushes.Transparent, null, viewport);

            // 智能缓存更新策略
            bool needsUpdate = _cacheInvalid || !viewport.Equals(_lastViewport);
            bool isViewportChange = !_cacheInvalid && !viewport.Equals(_lastViewport);

            if (needsUpdate)
            {
                // 如果只是视口变化，使用增量更新
                UpdateVisibleNotesCache(viewport, isViewportChange);
                _lastViewport = viewport;
                _cacheInvalid = false;
            }

            // 使用渲染器进行渲染
            _noteRenderer.RenderNotes(context, ViewModel, _visibleNoteCache);

            if (ViewModel.DragState.IsDragging)
            {
                _dragPreviewRenderer.Render(context, ViewModel, CalculateNoteRect);
            }

            if (ViewModel.ResizeState.IsResizing)
            {
                _resizePreviewRenderer.Render(context, ViewModel, CalculateNoteRect);
            }

            if (ViewModel.CreationModule.IsCreatingNote)
            {
                _creatingNoteRenderer.Render(context, ViewModel, CalculateNoteRect);
            }

            // 只有在不悬停在音符上且不在进行其他操作时才显示预览音符
            if (!ViewModel.CreationModule.IsCreatingNote &&
                !_cursorManager.IsHoveringResizeEdge &&
                !_cursorManager.IsHoveringNote &&
                !ViewModel.DragState.IsDragging &&
                !ViewModel.ResizeState.IsResizing)
            {
                _noteRenderer.RenderPreviewNote(context, ViewModel, CalculateNoteRect);
            }

            _selectionBoxRenderer.Render(context, ViewModel);
        }
        #endregion

        #region 缓存管理
        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 智能缓存失效策略 - 只处理真正影响渲染的属性变化
            switch (e.PropertyName)
            {
                case nameof(PianoRollViewModel.Zoom):
                case nameof(PianoRollViewModel.VerticalZoom):
                    // 缩放变化需要重建空间索引和清空所有缓存
                    _spatialIndexDirty = true;
                    _visibleNoteCache.Clear();
                    // 清空所有音符的矩形缓存
                    if (ViewModel?.CurrentTrackNotes != null)
                    {
                        foreach (var note in ViewModel.CurrentTrackNotes)
                        {
                            note.InvalidateCache();
                        }
                    }
                    ThrottledInvalidateVisual();
                    break;

                case nameof(PianoRollViewModel.CurrentScrollOffset):
                case nameof(PianoRollViewModel.VerticalScrollOffset):
                    // 滚动变化只需要更新可见缓存，不需要重建空间索引
                    if (_lastViewport.Width > 0 && _lastViewport.Height > 0)
                    {
                        UpdateVisibleNotesCache(_lastViewport, true);
                    }
                    ThrottledInvalidateVisual();
                    break;

                case nameof(PianoRollViewModel.CurrentTool):
                case nameof(PianoRollViewModel.GridQuantization):
                    // 这些变化不影响音符位置，只需要重绘
                    ThrottledInvalidateVisual();
                    break;

                default:
                    // 未知属性变化，保守处理
                    InvalidateCache();
                    break;
            }
        }

        private void OnNotesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // 智能缓存更新：只处理真正影响可见区域的变化
            bool needsFullRebuild = false;
            bool needsIncrementalUpdate = false;

            switch (e.Action)
            {
                case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    if (e.NewItems != null)
                    {
                        foreach (NoteViewModel note in e.NewItems)
                        {
                            AddNoteToSpatialIndex(note);
                        }
                        needsIncrementalUpdate = true;
                    }
                    break;

                case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                    if (e.OldItems != null)
                    {
                        foreach (NoteViewModel note in e.OldItems)
                        {
                            RemoveNoteFromSpatialIndex(note);
                            _visibleNoteCache.Remove(note);
                        }
                        needsIncrementalUpdate = true;
                    }
                    break;

                case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                    needsFullRebuild = true;
                    break;

                case System.Collections.Specialized.NotifyCollectionChangedAction.Move:
                    // 移动操作不影响渲染
                    break;
            }

            if (needsFullRebuild)
            {
                InvalidateCache();
            }
            else if (needsIncrementalUpdate)
            {
                // 只更新可见区域缓存，不重建整个空间索引
                if (_lastViewport.Width > 0 && _lastViewport.Height > 0)
                {
                    UpdateVisibleNotesCache(_lastViewport, true); // true表示增量更新
                }
                ThrottledInvalidateVisual();
            }
        }

        private void OnCurrentTrackNotesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // 当前音轨音符变化时的处理
            switch (e.Action)
            {
                case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    if (e.NewItems != null)
                    {
                        foreach (NoteViewModel note in e.NewItems)
                        {
                            AddNoteToSpatialIndex(note);
                        }
                    }
                    break;

                case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                    if (e.OldItems != null)
                    {
                        foreach (NoteViewModel note in e.OldItems)
                        {
                            RemoveNoteFromSpatialIndex(note);
                            _visibleNoteCache.Remove(note);
                        }
                    }
                    break;

                default:
                    InvalidateCache();
                    return;
            }

            // 增量更新可见缓存
            if (_lastViewport.Width > 0 && _lastViewport.Height > 0)
            {
                UpdateVisibleNotesCache(_lastViewport, true);
            }
            ThrottledInvalidateVisual();
        }

        private void InvalidateCache()
        {
            _cacheInvalid = true;
            _spatialIndexDirty = true;
            ThrottledInvalidateVisual();
        }

        private void UpdateVisibleNotesCache(Rect viewport, bool incrementalUpdate = false)
        {
            _cacheUpdateStopwatch.Restart();

            if (ViewModel?.CurrentTrackNotes == null)
            {
                _visibleNoteCache.Clear();
                return;
            }

            var currentTimeToPixelScale = ViewModel.TimeToPixelScale;
            var currentKeyHeight = ViewModel.KeyHeight;
            var currentScrollX = ViewModel.CurrentScrollOffset;
            var currentScrollY = ViewModel.VerticalScrollOffset;

            // 检查是否需要重建空间索引
            bool needsRebuildSpatialIndex = _spatialIndexDirty ||
                !AreValuesEqual(_lastTimeToPixelScale, currentTimeToPixelScale) ||
                !AreValuesEqual(_lastKeyHeight, currentKeyHeight);

            if (needsRebuildSpatialIndex)
            {
                RebuildSpatialIndex();
                _lastTimeToPixelScale = currentTimeToPixelScale;
                _lastKeyHeight = currentKeyHeight;
            }

            // 检查是否只是滚动变化（不需要重建空间索引）
            bool isOnlyScroll = !needsRebuildSpatialIndex &&
                AreValuesEqual(_lastTimeToPixelScale, currentTimeToPixelScale) &&
                AreValuesEqual(_lastKeyHeight, currentKeyHeight) &&
                (!AreValuesEqual(_lastScrollX, currentScrollX) || !AreValuesEqual(_lastScrollY, currentScrollY));

            if (!incrementalUpdate || needsRebuildSpatialIndex || !isOnlyScroll)
            {
                _visibleNoteCache.Clear();
            }

            var expandedViewport = viewport.Inflate(50); // 减少扩展区域

            // 使用空间索引快速查询可见音符
            var potentiallyVisibleNotes = _spatialIndex.Query(expandedViewport);

            foreach (var note in potentiallyVisibleNotes)
            {
                // 首先尝试从缓存获取矩形
                var cachedRect = note.GetCachedScreenRect(currentTimeToPixelScale, currentKeyHeight, currentScrollX, currentScrollY);

                Rect noteRect;
                if (cachedRect.HasValue)
                {
                    noteRect = cachedRect.Value;
                }
                else
                {
                    // 缓存未命中，计算并缓存
                    noteRect = CalculateNoteRect(note);
                    note.SetCachedScreenRect(noteRect, currentTimeToPixelScale, currentKeyHeight, currentScrollX, currentScrollY);

                    // 更新空间索引中的位置（如果位置发生了变化）
                    if (needsRebuildSpatialIndex)
                    {
                        _spatialIndex.Update(note, noteRect);
                    }
                }

                if (noteRect.Intersects(expandedViewport))
                {
                    _visibleNoteCache[note] = noteRect;
                }
            }

            _lastScrollX = currentScrollX;
            _lastScrollY = currentScrollY;

            // 性能监控
            _cacheUpdateStopwatch.Stop();
            _cacheUpdateTimes.Enqueue(_cacheUpdateStopwatch.Elapsed.TotalMilliseconds);
            if (_cacheUpdateTimes.Count > PERF_SAMPLE_SIZE)
                _cacheUpdateTimes.Dequeue();

            if (_cacheUpdateTimes.Count == PERF_SAMPLE_SIZE)
            {
                var avgTime = _cacheUpdateTimes.Average();
                Debug.WriteLine($"缓存更新性能: 平均{avgTime:F2}ms, 可见音符: {_visibleNoteCache.Count}, 空间索引: {_spatialIndex.GetDebugInfo()}");
            }
        }

        /// <summary>
        /// 重建空间索引
        /// </summary>
        private void RebuildSpatialIndex()
        {
            _spatialIndex.Clear();
            if (ViewModel?.CurrentTrackNotes == null) return;

            foreach (var note in ViewModel.CurrentTrackNotes)
            {
                AddNoteToSpatialIndex(note);
            }

            _spatialIndexDirty = false;

            // 定期优化空间索引
            if (ViewModel.CurrentTrackNotes.Count > 1000)
            {
                _spatialIndex.Optimize();
            }
        }

        /// <summary>
        /// 添加音符到空间索引
        /// </summary>
        private void AddNoteToSpatialIndex(NoteViewModel note)
        {
            if (ViewModel == null) return;

            var rect = CalculateNoteRect(note);
            if (rect.Width > 0 && rect.Height > 0)
            {
                _spatialIndex.Insert(note, rect);

                // 缓存矩形到音符本身
                note.SetCachedScreenRect(rect, ViewModel.TimeToPixelScale, ViewModel.KeyHeight,
                    ViewModel.CurrentScrollOffset, ViewModel.VerticalScrollOffset);
            }
        }

        /// <summary>
        /// 从空间索引移除音符
        /// </summary>
        private void RemoveNoteFromSpatialIndex(NoteViewModel note)
        {
            _spatialIndex.Remove(note);
        }

        /// <summary>
        /// 检查两个浮点数是否相等（考虑精度）
        /// </summary>
        private static bool AreValuesEqual(double a, double b)
        {
            if (double.IsNaN(a) && double.IsNaN(b)) return true;
            if (double.IsNaN(a) || double.IsNaN(b)) return false;
            return Math.Abs(a - b) < 1e-10;
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