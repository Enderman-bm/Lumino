using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using DominoNext.Views.Controls.Editing.Input;
using DominoNext.Views.Controls.Editing.Rendering;
using DominoNext.Services.Implementation;

namespace DominoNext.Views.Controls.Editing
{
    /// <summary>
    /// 音符编辑层 - 纯MVVM模式实现
    /// View只负责渲染和事件转发，业务逻辑完全委托给ViewModel模块
    /// 集成统一的渲染刷新服务，解决拖拽影子问题
    /// </summary>
    public class NoteEditingLayer : Control
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

        #region 渲染器
        private readonly NoteRenderer _noteRenderer;
        private readonly DragPreviewRenderer _dragPreviewRenderer;
        private readonly ResizePreviewRenderer _resizePreviewRenderer;
        private readonly CreatingNoteRenderer _creatingNoteRenderer;
        private readonly SelectionBoxRenderer _selectionBoxRenderer;
        #endregion

        #region 输入处理
        private readonly CursorManager _cursorManager;
        private readonly InputEventRouter _inputEventRouter;
        #endregion

        #region 缓存优化
        private readonly Dictionary<NoteViewModel, Rect> _visibleNoteCache = new();
        private bool _cacheInvalid = true;
        private Rect _lastViewport;
        #endregion

        #region 渲染刷新服务
        private readonly RenderRefreshService _renderRefreshService;
        #endregion

        #region 构造函数
        public NoteEditingLayer()
        {
            Debug.WriteLine("NoteEditingLayer constructor - 集成渲染刷新服务版本");

            // 初始化渲染器
            _noteRenderer = new NoteRenderer();
            _dragPreviewRenderer = new DragPreviewRenderer();
            _resizePreviewRenderer = new ResizePreviewRenderer();
            _creatingNoteRenderer = new CreatingNoteRenderer();
            _selectionBoxRenderer = new SelectionBoxRenderer();

            // 初始化输入处理器
            _cursorManager = new CursorManager(this);
            _inputEventRouter = new InputEventRouter();

            // 初始化渲染刷新服务
            _renderRefreshService = new RenderRefreshService();
            _renderRefreshService.OnRefreshRequested += OnRefreshRequested;
            _renderRefreshService.OnForceRefreshRequested += OnForceRefreshRequested;

            // 设置控件
            IsHitTestVisible = true;
            Focusable = true;
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
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
            // 取消订阅旧的绑定
            if (oldViewModel != null)
            {
                UnsubscribeFromViewModelEvents(oldViewModel);
            }

            // 订阅新的绑定
            if (newViewModel != null)
            {
                SubscribeToViewModelEvents(newViewModel);
                newViewModel.EditorCommands?.SetPianoRollViewModel(newViewModel);
                Debug.WriteLine($"ViewModel绑定成功. 当前工具: {newViewModel.CurrentTool}, 音符数量: {newViewModel.Notes.Count}");
            }

            // 强制完全刷新
            _renderRefreshService.ForceCompleteRefresh();
        }

        private void SubscribeToViewModelEvents(PianoRollViewModel viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            viewModel.Notes.CollectionChanged += OnNotesCollectionChanged;

            // 订阅模块事件 - 使用统一的刷新服务
            viewModel.DragModule.OnDragUpdated += () => _renderRefreshService.RealtimeRefresh();
            viewModel.DragModule.OnDragEnded += () => _renderRefreshService.ForceCompleteRefresh();
            viewModel.ResizeModule.OnResizeUpdated += () => _renderRefreshService.RealtimeRefresh();
            viewModel.ResizeModule.OnResizeEnded += () => _renderRefreshService.ForceCompleteRefresh();
            viewModel.CreationModule.OnCreationUpdated += () => _renderRefreshService.RealtimeRefresh();
            viewModel.CreationModule.OnCreationCompleted += () => _renderRefreshService.ForceCompleteRefresh();
            viewModel.PreviewModule.OnPreviewUpdated += () => _renderRefreshService.ThrottledRefresh();
            viewModel.SelectionModule.OnSelectionUpdated += () => _renderRefreshService.RealtimeRefresh();
        }

        private void UnsubscribeFromViewModelEvents(PianoRollViewModel viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.Notes.CollectionChanged -= OnNotesCollectionChanged;

            // 注意：由于使用了lambda表达式，这里无法直接取消订阅
            // 但由于ViewModel生命周期管理，这通常不是问题
        }
        #endregion

        #region 刷新服务事件处理
        private void OnRefreshRequested()
        {
            InvalidateCache();
            InvalidateVisual();
        }

        private void OnForceRefreshRequested()
        {
            // 强制刷新：清除所有缓存并重建
            ForceCompleteCacheRefresh();
            InvalidateVisual();
        }

        /// <summary>
        /// 强制完全刷新缓存
        /// 清除所有音符缓存，强制重新计算所有位置
        /// </summary>
        private void ForceCompleteCacheRefresh()
        {
            _cacheInvalid = true;
            _visibleNoteCache.Clear();
            
            // 清除所有音符的内部缓存
            if (ViewModel?.Notes != null)
            {
                foreach (var note in ViewModel.Notes)
                {
                    note.InvalidateCache();
                }
            }
            
            Debug.WriteLine("强制完全刷新缓存完成");
        }
        #endregion

        #region 事件处理 - 委托给路由处理器
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
            
            // 当悬停状态变化时使用节流刷新
            if (_cursorManager.HoveringStateChanged)
            {
                _renderRefreshService.ThrottledRefresh();
            }

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

            // 绘制透明背景以确保正确接收指针事件
            context.DrawRectangle(Brushes.Transparent, null, viewport);

            // 更新可见音符缓存
            if (_cacheInvalid || !viewport.Equals(_lastViewport))
            {
                UpdateVisibleNotesCache(viewport);
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

            // 只有在非创建状态且未在调整大小边缘或音符上悬停时才显示预览音符
            if (!ViewModel.CreationModule.IsCreatingNote && 
                !_cursorManager.IsHoveringResizeEdge && 
                !_cursorManager.IsHoveringNote &&  // 在非创建状态下悬停时显示预览
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
            // 只处理影响渲染的属性变化
            var renderingProperties = new[]
            {
                nameof(PianoRollViewModel.Zoom),
                nameof(PianoRollViewModel.VerticalZoom),
                nameof(PianoRollViewModel.CurrentTool),
                nameof(PianoRollViewModel.GridQuantization),
                "Visual", // 直接的视觉刷新通知
                "ForceRefresh" // 强制刷新通知
            };

            if (Array.Exists(renderingProperties, prop => prop == e.PropertyName))
            {
                // 对于强制刷新和视觉更新，使用强制完全刷新
                if (e.PropertyName == "Visual" || e.PropertyName == "ForceRefresh")
                {
                    _renderRefreshService.ForceCompleteRefresh();
                    return;
                }

                // 对于缩放属性变化，使用强制完全刷新
                if (e.PropertyName == nameof(PianoRollViewModel.Zoom) || 
                    e.PropertyName == nameof(PianoRollViewModel.VerticalZoom))
                {
                    _renderRefreshService.ForceCompleteRefresh();
                }
                else
                {
                    _renderRefreshService.ThrottledRefresh();
                }
            }
        }

        private void OnNotesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // 音符集合变化时，使用强制完全刷新
            _renderRefreshService.ForceCompleteRefresh();
        }

        private void InvalidateCache()
        {
            _cacheInvalid = true;
        }

        private void UpdateVisibleNotesCache(Rect viewport)
        {
            _visibleNoteCache.Clear();
            if (ViewModel?.Notes == null) return;

            var expandedViewport = viewport.Inflate(100);

            foreach (var note in ViewModel.Notes)
            {
                var noteRect = CalculateNoteRect(note);
                if (noteRect.Intersects(expandedViewport))
                {
                    _visibleNoteCache[note] = noteRect;
                }
            }
        }

        private Rect CalculateNoteRect(NoteViewModel note)
        {
            if (ViewModel == null) return default;

            var x = note.GetX(ViewModel.Zoom, ViewModel.PixelsPerTick);
            var y = note.GetY(ViewModel.KeyHeight);
            var width = Math.Max(4, note.GetWidth(ViewModel.Zoom, ViewModel.PixelsPerTick));
            var height = Math.Max(2, note.GetHeight(ViewModel.KeyHeight) - 1);

            return new Rect(x, y, width, height);
        }
        #endregion
    }

    #region Command参数定义

    /// <summary>
    /// 编辑器交互参数
    /// </summary>
    public class EditorInteractionArgs
    {
        public Point Position { get; set; }
        public EditorTool Tool { get; set; }
        public KeyModifiers Modifiers { get; set; }
        public EditorInteractionType InteractionType { get; set; }
        public MouseButtons MouseButtons { get; set; }
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

    /// <summary>
    /// 鼠标按钮枚举
    /// </summary>
    public enum MouseButtons
    {
        None = 0,
        Left = 1,
        Right = 2,
        Middle = 4
    }

    #endregion
}