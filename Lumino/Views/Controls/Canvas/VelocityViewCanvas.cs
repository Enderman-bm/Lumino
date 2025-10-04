using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Services.Interfaces;
using Lumino.Services.Implementation;
using Lumino.Views.Rendering.Utils;
using Lumino.Views.Rendering.Events;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace Lumino.Views.Controls.Canvas
{
    /// <summary>
    /// 力度视图画布 - 显示和编辑音符力度，支持动态缓存和后台预计算
    /// </summary>
    public class VelocityViewCanvas : Control, IRenderSyncTarget
    {
        public static readonly StyledProperty<PianoRollViewModel?> ViewModelProperty =
            AvaloniaProperty.Register<VelocityViewCanvas, PianoRollViewModel?>(nameof(ViewModel));

        public PianoRollViewModel? ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        private readonly VelocityBarRenderer _velocityRenderer;
        private readonly IRenderSyncService _renderSyncService;

        // 缓存画刷实例，确保渲染一致性
        private readonly IBrush _backgroundBrush;
        private readonly IPen _gridLinePen;

        // 性能优化相关
        private DateTime _lastPrecomputeTime = DateTime.MinValue;
        private readonly TimeSpan _precomputeInterval = TimeSpan.FromMilliseconds(500); // 预计算间隔
        private volatile bool _precomputeScheduled = false;

        public VelocityViewCanvas()
        {
            _velocityRenderer = new VelocityBarRenderer();
            
            // 注册到渲染同步服务
            _renderSyncService = RenderSyncService.Instance;
            _renderSyncService.RegisterTarget(this);
            
            // 启用鼠标事件
            IsHitTestVisible = true;

            // 初始化缓存画刷
            _backgroundBrush = RenderingUtils.GetResourceBrush("VelocityViewBackgroundBrush", "#20000000");
            _gridLinePen = RenderingUtils.GetResourcePen("VelocityGridLineBrush", "#30808080", 1);

            // 启用后台预计算（对于大数据集）
            _velocityRenderer.SetBackgroundPrecomputationEnabled(true);
            _velocityRenderer.SetPrecomputationThreshold(500); // 超过500个音符时启用
        }

        static VelocityViewCanvas()
        {
            ViewModelProperty.Changed.AddClassHandler<VelocityViewCanvas>((canvas, e) =>
            {
                if (e.OldValue is PianoRollViewModel oldVm)
                {
                    canvas.UnsubscribeFromViewModel(oldVm);
                }

                if (e.NewValue is PianoRollViewModel newVm)
                {
                    canvas.SubscribeToViewModel(newVm);
                }

                // 清除预计算缓存，因为ViewModel已变更
                canvas._velocityRenderer.ClearPrecomputedCache();
                canvas.InvalidateVisual();
            });
        }

        private void SubscribeToViewModel(PianoRollViewModel viewModel)
        {
            // 监听ViewModel属性变化
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            
            // 监听音符集合变化
            if (viewModel.Notes is INotifyCollectionChanged notesCollection)
            {
                notesCollection.CollectionChanged += OnNotesCollectionChanged;
            }
            
            // 监听当前轨道音符集合变化
            if (viewModel.CurrentTrackNotes is INotifyCollectionChanged currentTrackNotesCollection)
            {
                currentTrackNotesCollection.CollectionChanged += OnCurrentTrackNotesCollectionChanged;
            }

            // 监听每个音符的属性变化
            foreach (var note in viewModel.CurrentTrackNotes)
            {
                note.PropertyChanged += OnNotePropertyChanged;
            }

            // 监听力度编辑模块事件
            if (viewModel.VelocityEditingModule != null)
            {
                viewModel.VelocityEditingModule.OnVelocityUpdated += OnVelocityUpdated;
            }

            // 触发初始预计算
            SchedulePrecompute();
        }

        private void UnsubscribeFromViewModel(PianoRollViewModel viewModel)
        {
            // 取消监听ViewModel属性变化
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            
            // 取消监听音符集合变化
            if (viewModel.Notes is INotifyCollectionChanged notesCollection)
            {
                notesCollection.CollectionChanged -= OnNotesCollectionChanged;
            }
            
            // 取消监听当前轨道音符集合变化
            if (viewModel.CurrentTrackNotes is INotifyCollectionChanged currentTrackNotesCollection)
            {
                currentTrackNotesCollection.CollectionChanged -= OnCurrentTrackNotesCollectionChanged;
            }

            // 取消监听每个音符的属性变化
            foreach (var note in viewModel.CurrentTrackNotes)
            {
                note.PropertyChanged -= OnNotePropertyChanged;
            }

            // 取消监听力度编辑模块事件
            if (viewModel.VelocityEditingModule != null)
            {
                viewModel.VelocityEditingModule.OnVelocityUpdated -= OnVelocityUpdated;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PianoRollViewModel.Zoom) ||
                e.PropertyName == nameof(PianoRollViewModel.VerticalZoom))
            {
                // 缩放变化时清除预计算缓存并重新预计算
                _velocityRenderer.ClearPrecomputedCache();
                SchedulePrecompute();
                _renderSyncService.SyncRefresh();
            }
            else if (e.PropertyName == nameof(PianoRollViewModel.TimelinePosition) ||
                     e.PropertyName == nameof(PianoRollViewModel.CurrentScrollOffset))
            {
                // 滚动时清除预计算缓存并重新预计算
                _velocityRenderer.ClearPrecomputedCache();
                SchedulePrecompute();
                _renderSyncService.SyncRefresh();
            }
            else if (e.PropertyName == nameof(PianoRollViewModel.CurrentTrackIndex))
            {
                // 轨道切换时清除缓存
                _velocityRenderer.ClearPrecomputedCache();
                SchedulePrecompute();
                _renderSyncService.SyncRefresh();
            }
        }

        private void OnNotesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 音符集合发生变化时需要更新事件监听
            if (e.OldItems != null)
            {
                foreach (NoteViewModel note in e.OldItems)
                {
                    note.PropertyChanged -= OnNotePropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (NoteViewModel note in e.NewItems)
                {
                    note.PropertyChanged += OnNotePropertyChanged;
                }
            }

            // 清除预计算缓存并重新预计算
            _velocityRenderer.ClearPrecomputedCache();
            SchedulePrecompute();
            _renderSyncService.SyncRefresh();
        }

        private void OnCurrentTrackNotesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 当前轨道音符集合发生变化时需要更新事件监听
            if (e.OldItems != null)
            {
                foreach (NoteViewModel note in e.OldItems)
                {
                    note.PropertyChanged -= OnNotePropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (NoteViewModel note in e.NewItems)
                {
                    note.PropertyChanged += OnNotePropertyChanged;
                }
            }

            // 清除预计算缓存并重新预计算
            _velocityRenderer.ClearPrecomputedCache();
            SchedulePrecompute();
            _renderSyncService.SyncRefresh();
        }

        private void OnNotePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 任何音符属性发生变化时，刷新力度视图
            if (e.PropertyName == nameof(NoteViewModel.Velocity) ||
                e.PropertyName == nameof(NoteViewModel.StartPosition) ||
                e.PropertyName == nameof(NoteViewModel.Duration) ||
                e.PropertyName == nameof(NoteViewModel.Pitch) ||
                e.PropertyName == nameof(NoteViewModel.IsSelected))
            {
                // 只有影响渲染的属性变化时才清除缓存
                if (e.PropertyName == nameof(NoteViewModel.Velocity) ||
                    e.PropertyName == nameof(NoteViewModel.StartPosition) ||
                    e.PropertyName == nameof(NoteViewModel.Duration))
                {
                    _velocityRenderer.ClearPrecomputedCache();
                    SchedulePrecompute();
                }
                
                _renderSyncService.SyncRefresh();
            }
        }

        private void OnVelocityUpdated()
        {
            // 力度更新时立即刷新，但不清除全部缓存（因为这可能是批量更新）
            _renderSyncService.SyncRefresh();
        }

        /// <summary>
        /// 调度后台预计算任务
        /// </summary>
        private void SchedulePrecompute()
        {
            if (_precomputeScheduled) return;
            
            var now = DateTime.Now;
            if (now - _lastPrecomputeTime < _precomputeInterval) return;

            _precomputeScheduled = true;
            
            // 使用Dispatcher延迟执行，避免在UI操作期间进行
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    await Task.Delay(100); // 短暂延迟确保UI操作完成
                    await PerformPrecompute();
                }
                catch (Exception)
                {
                    // 预计算任务失败，静默处理
                }
                finally
                {
                    _precomputeScheduled = false;
                    _lastPrecomputeTime = DateTime.Now;
                }
            });
        }

        /// <summary>
        /// 执行后台预计算
        /// </summary>
        private async Task PerformPrecompute()
        {
            if (ViewModel?.CurrentTrackNotes == null || double.IsNaN(Bounds.Width) || double.IsNaN(Bounds.Height))
                return;

            var bounds = Bounds;
            var scrollOffset = ViewModel.CurrentScrollOffset;
            var timeToPixelScale = ViewModel.TimeToPixelScale;
            var notes = ViewModel.CurrentTrackNotes.ToList(); // 创建快照避免集合修改

            await _velocityRenderer.PrecomputeVelocityBarsAsync(notes, bounds, timeToPixelScale, scrollOffset);
        }

        public override void Render(DrawingContext context)
        {
            if (ViewModel == null) return;

            var bounds = Bounds;
            
            // 设置力度编辑模块的画布高度
            if (ViewModel.VelocityEditingModule != null)
            {
                ViewModel.VelocityEditingModule.SetCanvasHeight(bounds.Height);
            }
            
            // 绘制背景
            context.DrawRectangle(_backgroundBrush, null, bounds);

            // 绘制力度条
            DrawVelocityBars(context, bounds);
            
            // 绘制网格线（可选）
            DrawGridLines(context, bounds);
        }

        private void DrawVelocityBars(DrawingContext context, Rect bounds)
        {
            if (ViewModel?.CurrentTrackNotes == null) return;

            var scrollOffset = ViewModel.CurrentScrollOffset;
            var noteCount = ViewModel.CurrentTrackNotes.Count;

            // 对于大量音符，只渲染可见区域内的音符以提升性能
            var visibleNotes = noteCount > 1000 
                ? GetVisibleNotes(ViewModel.CurrentTrackNotes.AsEnumerable(), bounds, scrollOffset)
                : ViewModel.CurrentTrackNotes.AsEnumerable();

            foreach (var note in visibleNotes)
            {
                // 确定渲染类型
                var renderType = GetVelocityRenderType(note);
                
                _velocityRenderer.DrawVelocityBar(context, note, bounds, 
                    ViewModel.TimeToPixelScale, renderType, scrollOffset);
            }

            // 渲染正在编辑的力度预览
            if (ViewModel.VelocityEditingModule?.IsEditingVelocity == true)
            {
                _velocityRenderer.DrawEditingPreview(context, bounds, 
                    ViewModel.VelocityEditingModule, ViewModel.TimeToPixelScale, scrollOffset);
            }
        }

        /// <summary>
        /// 获取可见区域内的音符（性能优化）
        /// </summary>
        private IEnumerable<NoteViewModel> GetVisibleNotes(IEnumerable<NoteViewModel> notes, Rect bounds, double scrollOffset)
        {
            var visibleTimeStart = scrollOffset / ViewModel!.TimeToPixelScale;
            var visibleTimeEnd = (scrollOffset + bounds.Width) / ViewModel.TimeToPixelScale;

            return notes.Where(note =>
            {
                var noteStart = note.StartPosition.ToDouble();
                var noteEnd = noteStart + note.Duration.ToDouble();
                return noteEnd >= visibleTimeStart && noteStart <= visibleTimeEnd;
            });
        }

        private VelocityRenderType GetVelocityRenderType(NoteViewModel note)
        {
            if (ViewModel?.VelocityEditingModule?.EditingNotes?.Contains(note) == true)
                return VelocityRenderType.Editing;
            
            if (note.IsSelected)
                return VelocityRenderType.Selected;
                
            if (ViewModel?.DragState?.DraggingNotes?.Contains(note) == true)
                return VelocityRenderType.Dragging;
                
            return VelocityRenderType.Normal;
        }

        private void DrawGridLines(DrawingContext context, Rect bounds)
        {
            if (ViewModel == null) return;

            // 绘制水平参考线 (25%, 50%, 75%, 100%)
            var quarterHeight = bounds.Height / 4.0;
            for (int i = 1; i <= 3; i++)
            {
                var y = bounds.Height - (i * quarterHeight);
                context.DrawLine(_gridLinePen, new Point(0, y), new Point(bounds.Width, y));
            }
        }

        #region 用户事件处理

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (ViewModel?.VelocityEditingModule == null) return;

            var position = e.GetPosition(this);
            var properties = e.GetCurrentPoint(this).Properties;

            if (properties.IsLeftButtonPressed)
            {
                // 屏幕坐标转换为世界坐标
                var worldPosition = new Point(
                    position.X + ViewModel.CurrentScrollOffset,
                    position.Y
                );
                
                ViewModel.VelocityEditingModule.StartEditing(worldPosition);
                e.Handled = true;
            }

            base.OnPointerPressed(e);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            if (ViewModel?.VelocityEditingModule == null) return;

            var position = e.GetPosition(this);
            
            // 只在编辑时处理移动事件
            if (ViewModel.VelocityEditingModule.IsEditingVelocity)
            {
                // 限制位置在画布范围内
                var clampedPosition = new Point(
                    Math.Max(0, Math.Min(Bounds.Width, position.X)),
                    Math.Max(0, Math.Min(Bounds.Height, position.Y))
                );
                
                // 屏幕坐标转换为世界坐标
                var worldPosition = new Point(
                    clampedPosition.X + ViewModel.CurrentScrollOffset,
                    clampedPosition.Y
                );
                
                ViewModel.VelocityEditingModule.UpdateEditing(worldPosition);
            }

            base.OnPointerMoved(e);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (ViewModel?.VelocityEditingModule == null) return;
            
            ViewModel.VelocityEditingModule.EndEditing();
            e.Handled = true;

            base.OnPointerReleased(e);
        }

        #endregion

        #region IRenderSyncTarget接口实现

        /// <summary>
        /// 实现IRenderSyncTarget接口
        /// </summary>
        public void RefreshRender()
        {
            InvalidateVisual();
        }

        #endregion

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            // 从渲染同步服务注销
            _renderSyncService.UnregisterTarget(this);
            
            // 清除所有缓存释放内存
            _velocityRenderer.ClearAllCaches();
            
            base.OnDetachedFromVisualTree(e);
        }

        #region 性能诊断

        /// <summary>
        /// 获取性能统计信息（调试用）
        /// </summary>
        public string GetPerformanceStatistics()
        {
            var noteCount = ViewModel?.CurrentTrackNotes?.Count ?? 0;
            var cacheStats = _velocityRenderer.GetCacheStatistics();
            return $"音符数量: {noteCount}, {cacheStats}";
        }

        #endregion
    }
}