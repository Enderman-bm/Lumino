using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.ViewModels.Editor.Modules;
using Lumino.Services.Interfaces;
using Lumino.Services.Implementation;
using Lumino.Views.Rendering.Utils;
using Lumino.Views.Rendering.Events;
using System;
using EnderDebugger;
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
        private readonly ControllerCurveRenderer _curveRenderer;
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
            _curveRenderer = new ControllerCurveRenderer();
            
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

            // 监听事件曲线绘制模块事件
            if (viewModel.EventCurveDrawingModule != null)
            {
                viewModel.EventCurveDrawingModule.OnCurveUpdated += OnCurveUpdated;
                viewModel.EventCurveDrawingModule.OnCurveCompleted += OnCurveCompleted;
                viewModel.EventCurveDrawingModule.OnCurveCancelled += OnCurveCancelled;
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

            // 取消监听事件曲线绘制模块事件
            if (viewModel.EventCurveDrawingModule != null)
            {
                viewModel.EventCurveDrawingModule.OnCurveUpdated -= OnCurveUpdated;
                viewModel.EventCurveDrawingModule.OnCurveCompleted -= OnCurveCompleted;
                viewModel.EventCurveDrawingModule.OnCurveCancelled -= OnCurveCancelled;
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
            else if (e.PropertyName == nameof(PianoRollViewModel.CurrentEventType) ||
                     e.PropertyName == nameof(PianoRollViewModel.CurrentCCNumber))
            {
                // 事件类型或CC号改变时：
                // 1) 如果正在绘制，取消绘制（防止状态混乱）
                // 2) 强制刷新画布显示新的事件类型
                if (ViewModel?.EventCurveDrawingModule?.IsDrawing == true)
                {
                    EnderLogger.Instance.Debug("VelocityViewCanvas", "[VelocityViewCanvas] 事件类型切换，取消正在进行的绘制");
                    ViewModel.EventCurveDrawingModule.CancelDrawing();
                }
                
                EnderLogger.Instance.Debug("VelocityViewCanvas", $"[VelocityViewCanvas] 事件类型改变: {e.PropertyName}, 刷新画布");
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
        /// 事件曲线更新事件处理
        /// </summary>
        private void OnCurveUpdated()
        {
            // 曲线更新时立即刷新
            EnderLogger.Instance.Debug("VelocityViewCanvas", "[VelocityViewCanvas] OnCurveUpdated triggered, calling SyncRefresh");
            _renderSyncService.SyncRefresh();
        }

        /// <summary>
        /// 事件曲线完成事件处理
        /// </summary>
        private void OnCurveCompleted(List<CurvePoint> curvePoints)
        {
            EnderLogger.Instance.Debug("VelocityViewCanvas", $"[VelocityViewCanvas] OnCurveCompleted: {curvePoints?.Count ?? 0} points, requesting refresh");
            _renderSyncService.SyncRefresh();
        }

        /// <summary>
        /// 事件曲线取消事件处理
        /// </summary>
        private void OnCurveCancelled()
        {
            // 曲线绘制取消时刷新
            EnderLogger.Instance.Debug("VelocityViewCanvas", "[VelocityViewCanvas] OnCurveCancelled triggered, calling SyncRefresh");
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
            
            // 根据事件类型设置画布高度
            double canvasHeight = bounds.Height;
            
            // 设置力度编辑模块的画布高度
            if (ViewModel.VelocityEditingModule != null)
            {
                ViewModel.VelocityEditingModule.SetCanvasHeight(canvasHeight);
            }
            
            // 绘制背景
            context.DrawRectangle(_backgroundBrush, null, bounds);

            // 根据当前事件类型选择相应的绘制方式
            switch (ViewModel.CurrentEventType)
            {
                case Lumino.ViewModels.Editor.Enums.EventType.Velocity:
                    DrawVelocityBars(context, bounds);
                    break;
                case Lumino.ViewModels.Editor.Enums.EventType.PitchBend:
                    DrawPitchBendCurve(context, bounds, canvasHeight);
                    break;
                case Lumino.ViewModels.Editor.Enums.EventType.ControlChange:
                    DrawControlChangeCurve(context, bounds, canvasHeight);
                    break;
                case Lumino.ViewModels.Editor.Enums.EventType.Tempo:
                    DrawTempoCurve(context, bounds, canvasHeight);
                    break;
                default:
                    DrawVelocityBars(context, bounds);
                    break;
            }
            
            // 绘制网格线
            DrawGridLines(context, bounds);
        }

        /// <summary>
        /// 绘制弯音曲线
        /// </summary>
        private void DrawPitchBendCurve(DrawingContext context, Rect bounds, double canvasHeight)
        {
            // 绘制弯音背景
            DrawEventBackground(context, bounds, "弯音范围: -8192～8191");
            
            // 绘制每个音符的弯音指示块（基于 PitchBendValue）
            if (ViewModel?.CurrentTrackNotes != null)
            {
                var scrollOffset = ViewModel.CurrentScrollOffset;
                var visibleNotes = GetVisibleNotes(ViewModel.CurrentTrackNotes.AsEnumerable(), bounds, scrollOffset);
                
                foreach (var note in visibleNotes)
                {
                    try
                    {
                        // 根据 PitchBendValue 计算指示块高度
                        var pitchBendValue = note.GetModel().PitchBendValue;
                        var heightRatio = (pitchBendValue + 8192.0) / (8192.0 * 2.0); // 映射到 0-1
                        heightRatio = Math.Max(0, Math.Min(1, heightRatio)); // 限制在 0-1
                        
                        // 计算屏幕坐标
                        var noteX = note.GetX(ViewModel.TimeToPixelScale) - scrollOffset;
                        var noteWidth = note.GetWidth(ViewModel.TimeToPixelScale);
                        var barHeight = bounds.Height * heightRatio;
                        var barY = bounds.Height - barHeight;
                        
                        // 绘制指示块
                        var barBrush = RenderingUtils.GetResourceBrush("PitchBendBrush", "#FFA500"); // 橙色
                        var barRect = new Rect(noteX, barY, noteWidth, barHeight);
                        context.DrawRectangle(barBrush, null, barRect);
                    }
                        catch (Exception ex)
                    {
                        EnderLogger.Instance.LogException(ex, "VelocityViewCanvas", "绘制弯音指示块错误");
                    }
                }
            }
            
            // 如果正在绘制事件曲线
            if (ViewModel?.EventCurveDrawingModule?.IsDrawing == true)
            {
                var curvePoints = ViewModel.EventCurveDrawingModule.CurrentCurvePoints
                    .Select(p => new Point(p.ScreenPosition.X - ViewModel.CurrentScrollOffset, p.ScreenPosition.Y))
                    .ToList();
                _curveRenderer.DrawPitchBendCurve(context, curvePoints, bounds, ViewModel.CurrentScrollOffset);
            }
        }

        /// <summary>
        /// 绘制CC控制器曲线
        /// </summary>
        private void DrawControlChangeCurve(DrawingContext context, Rect bounds, double canvasHeight)
        {
            // 绘制CC背景
            DrawEventBackground(context, bounds, $"CC{ViewModel?.CurrentCCNumber} 范围: 0-127");
            
            if (ViewModel?.CurrentTrackControllerEvents != null)
            {
                try
                {
                    var scrollOffset = ViewModel.CurrentScrollOffset;
                    var timeToPixelScale = ViewModel.TimeToPixelScale;

                    var visibleStart = scrollOffset / timeToPixelScale;
                    var visibleEnd = (scrollOffset + bounds.Width) / timeToPixelScale;

                    var events = ViewModel.CurrentTrackControllerEvents
                        .Where(evt =>
                        {
                            var time = evt.Time.ToDouble();
                            return time >= visibleStart && time <= visibleEnd;
                        })
                        .OrderBy(evt => evt.Time.ToDouble())
                        .ToList();

                    if (events.Count == 0 && ViewModel.CurrentTrackNotes != null)
                    {
                        // 向后兼容：若无事件，则从音符默认值推断
                        var fallback = GetVisibleNotes(ViewModel.CurrentTrackNotes.AsEnumerable(), bounds, scrollOffset)
                            .OrderBy(n => n.StartPosition.ToDouble());

                        foreach (var note in fallback)
                        {
                            events.Add(new ControllerEventViewModel
                            {
                                TrackIndex = note.TrackIndex,
                                ControllerNumber = ViewModel.CurrentCCNumber,
                                Time = note.StartPosition,
                                Value = note.GetModel().ControlChangeValue
                            });
                        }
                    }

                    if (events.Count > 0)
                    {
                        var ccPoints = new List<Point>(events.Count * 2);
                        for (int i = 0; i < events.Count; i++)
                        {
                            var evt = events[i];
                            var worldTime = evt.Time.ToDouble();
                            var x = worldTime * timeToPixelScale - scrollOffset;
                            var heightRatio = Math.Clamp(evt.Value / 127.0, 0, 1);
                            var y = bounds.Height - (heightRatio * bounds.Height);
                            ccPoints.Add(new Point(x, y));

                            if (i < events.Count - 1)
                            {
                                var nextTime = events[i + 1].Time.ToDouble();
                                var midX = ((worldTime + nextTime) / 2.0) * timeToPixelScale - scrollOffset;
                                var midY = (y + (bounds.Height - Math.Clamp(events[i + 1].Value / 127.0, 0, 1) * bounds.Height)) / 2.0;
                                ccPoints.Add(new Point(midX, midY));
                            }
                        }

                        _curveRenderer.DrawControlChangeCurve(context, ccPoints, ViewModel.CurrentCCNumber, bounds, ViewModel.CurrentScrollOffset);
                    }
                }
                catch (Exception ex)
                {
                    EnderLogger.Instance.LogException(ex, "VelocityViewCanvas", "绘制CC曲线错误");
                }
            }
            
            // 如果正在绘制事件曲线
            if (ViewModel?.EventCurveDrawingModule?.IsDrawing == true)
            {
                var curvePoints = ViewModel.EventCurveDrawingModule.CurrentCurvePoints
                    .Select(p => new Point(p.ScreenPosition.X - ViewModel.CurrentScrollOffset, p.ScreenPosition.Y))
                    .ToList();
                _curveRenderer.DrawControlChangeCurve(context, curvePoints, ViewModel.CurrentCCNumber, bounds, ViewModel.CurrentScrollOffset);
            }
        }

        /// <summary>
        /// 绘制速度（Tempo）曲线
        /// </summary>
        private void DrawTempoCurve(DrawingContext context, Rect bounds, double canvasHeight)
        {
            // 绘制Tempo背景
            DrawEventBackground(context, bounds, "Tempo 范围: 1-300 BPM");
            
            // 如果正在绘制事件曲线
            if (ViewModel?.EventCurveDrawingModule?.IsDrawing == true)
            {
                var curvePoints = ViewModel.EventCurveDrawingModule.CurrentCurvePoints
                    .Select(p => new Point(p.ScreenPosition.X - ViewModel.CurrentScrollOffset, p.ScreenPosition.Y))
                    .ToList();
                // 使用通用曲线渲染器绘制速度曲线
                _curveRenderer.DrawEventCurve(context, Lumino.ViewModels.Editor.Enums.EventType.Tempo, curvePoints, bounds, ViewModel.CurrentScrollOffset);
            }
        }

        /// <summary>
        /// 绘制事件背景信息
        /// </summary>
        private void DrawEventBackground(DrawingContext context, Rect bounds, string label)
        {
            // 绘制标签文本
            var textBrush = RenderingUtils.GetResourceBrush("TextBrush", "#808080");
            var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal, FontStretch.Normal);
            var formattedText = new FormattedText(label, 
                System.Globalization.CultureInfo.InvariantCulture,
                Avalonia.Media.FlowDirection.LeftToRight, 
                typeface, 12, textBrush);
            
            context.DrawText(formattedText, new Point(10, bounds.Height / 2 - 6));
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

            // 如果正在绘制力度曲线，在力度条之上绘制曲线预览
            if (ViewModel.EventCurveDrawingModule?.IsDrawing == true)
            {
                var curvePoints = ViewModel.EventCurveDrawingModule.CurrentCurvePoints
                    .Select(p => new Point(p.ScreenPosition.X - scrollOffset, p.ScreenPosition.Y))
                    .ToList();
                _curveRenderer.DrawEventCurve(context, Lumino.ViewModels.Editor.Enums.EventType.Velocity, curvePoints, bounds, scrollOffset);
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
            var position = e.GetPosition(this);
            var properties = e.GetCurrentPoint(this).Properties;

            EnderLogger.Instance.Debug("VelocityViewCanvas", $"[VelocityViewCanvas] OnPointerPressed: Position=({position.X}, {position.Y}), LeftButtonPressed={properties.IsLeftButtonPressed}");

            if (properties.IsLeftButtonPressed && ViewModel != null)
            {
                EnderLogger.Instance.Debug("VelocityViewCanvas", $"[VelocityViewCanvas] CurrentTool={ViewModel.CurrentTool}, EventCurveDrawingModule={ViewModel.EventCurveDrawingModule != null}");

                // 屏幕坐标转换为世界坐标（Y坐标限制在画布范围内）
                var worldPosition = new Point(
                    position.X + ViewModel.CurrentScrollOffset,
                    Math.Max(0, Math.Min(Bounds.Height, position.Y))
                );
                
                // 根据当前工具选择相应的编辑方式
                if (ViewModel.CurrentTool == Lumino.ViewModels.Editor.EditorTool.Pencil)
                {
                    // 铅笔工具：绘制事件曲线
                    EnderLogger.Instance.Debug("VelocityViewCanvas", $"[VelocityViewCanvas] Starting curve drawing at ({worldPosition.X}, {worldPosition.Y}), CanvasHeight={Bounds.Height}");
                    ViewModel.StartDrawingEventCurve(worldPosition, Bounds.Height);
                    EnderLogger.Instance.Debug("VelocityViewCanvas", $"[VelocityViewCanvas] IsDrawing={ViewModel.EventCurveDrawingModule?.IsDrawing}");
                }
                else if (ViewModel.CurrentEventType == Lumino.ViewModels.Editor.Enums.EventType.Velocity &&
                         ViewModel.VelocityEditingModule != null)
                {
                    // 选择工具 + 力度模式：编辑力度
                    EnderLogger.Instance.Debug("VelocityViewCanvas", $"[VelocityViewCanvas] Starting velocity editing");
                    ViewModel.VelocityEditingModule.StartEditing(worldPosition);
                }
                
                e.Handled = true;
            }

            base.OnPointerPressed(e);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            if (ViewModel == null) return;

            var position = e.GetPosition(this);
            
            // 屏幕坐标转换为世界坐标
            var worldPosition = new Point(
                position.X + ViewModel.CurrentScrollOffset,
                Math.Max(0, Math.Min(Bounds.Height, position.Y))
            );
            
            // 判断当前在编辑或绘制什么
            if (ViewModel.CurrentTool == Lumino.ViewModels.Editor.EditorTool.Pencil && 
                ViewModel.EventCurveDrawingModule?.IsDrawing == true)
            {
                // 绘制曲线中
                EnderLogger.Instance.Debug("VelocityViewCanvas", $"[VelocityViewCanvas] UpdateDrawingEventCurve: ({worldPosition.X}, {worldPosition.Y}), PointCount={ViewModel.EventCurveDrawingModule.CurrentCurvePoints?.Count}");
                ViewModel.UpdateDrawingEventCurve(worldPosition);
            }
            else if (ViewModel.VelocityEditingModule?.IsEditingVelocity == true)
            {
                // 编辑力度中
                ViewModel.VelocityEditingModule.UpdateEditing(worldPosition);
            }

            base.OnPointerMoved(e);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (ViewModel != null)
            {
                // 根据当前工具决定如何结束操作
                if (ViewModel.CurrentTool == Lumino.ViewModels.Editor.EditorTool.Pencil && 
                    ViewModel.EventCurveDrawingModule?.IsDrawing == true)
                {
                    EnderLogger.Instance.Debug("VelocityViewCanvas", $"[VelocityViewCanvas] FinishDrawingEventCurve: PointCount={ViewModel.EventCurveDrawingModule.CurrentCurvePoints?.Count}");
                    ViewModel.FinishDrawingEventCurve();
                }
                else if (ViewModel.VelocityEditingModule?.IsEditingVelocity == true)
                {
                    ViewModel.VelocityEditingModule.EndEditing();
                }
            }
            
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
            EnderLogger.Instance.Debug("VelocityViewCanvas", $"[VelocityViewCanvas] RefreshRender called, calling InvalidateVisual");
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