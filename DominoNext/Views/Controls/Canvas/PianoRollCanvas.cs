using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using DominoNext.ViewModels.Editor;
using DominoNext.Renderers;
using System;
using System.Collections.Specialized;
using DominoNext.Models.Music;

namespace DominoNext.Views.Controls.Canvas
{
    public class PianoRollCanvas : Control
    {
        public static readonly StyledProperty<PianoRollViewModel?> ViewModelProperty =
            AvaloniaProperty.Register<PianoRollCanvas, PianoRollViewModel?>(nameof(ViewModel));

        public PianoRollViewModel? ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        // 各个专门的渲染器
        private readonly HorizontalGridRenderer _horizontalGridRenderer = new();
        private readonly VerticalGridRenderer _verticalGridRenderer = new();
        private readonly PlayheadRenderer _playheadRenderer = new();
        private readonly OnionSkinRenderer _onionSkinRenderer = new();

        // 智能更新策略：只有真正需要时才重绘
        private Rect _dirtyRegion = new Rect();
        private bool _hasDirtyRegion = false;
        private bool _needsFullRedraw = true;
        
        // 缓存上次的关键参数，用于检测变化
        private double _lastZoom = double.NaN;
        private double _lastVerticalZoom = double.NaN;
        private double _lastTimelinePosition = double.NaN;

        // 资源画刷获取助手方法 - 优化版本
        private IBrush GetResourceBrush(string key, string fallbackHex)
        {
            try
            {
                if (Application.Current?.Resources.TryGetResource(key, null, out var obj) == true && obj is IBrush brush)
                    return brush;
            }
            catch { }

            return new SolidColorBrush(Color.Parse(fallbackHex));
        }

        // 使用动态资源的画刷
        private IBrush MainBackgroundBrush => GetResourceBrush("MainCanvasBackgroundBrush", "#FFFFFFFF");

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

                canvas._needsFullRedraw = true;
                canvas.InvalidateVisual();
            });
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 智能更新策略：根据变化的属性决定更新范围
            switch (e.PropertyName)
            {
                case nameof(PianoRollViewModel.Zoom):
                    if (ViewModel != null && Math.Abs(_lastZoom - ViewModel.Zoom) > 0.001)
                    {
                        _lastZoom = ViewModel.Zoom;
                        _needsFullRedraw = true;
                        InvalidateVisual();
                    }
                    break;
                    
                case nameof(PianoRollViewModel.VerticalZoom):
                    if (ViewModel != null && Math.Abs(_lastVerticalZoom - ViewModel.VerticalZoom) > 0.001)
                    {
                        _lastVerticalZoom = ViewModel.VerticalZoom;
                        _needsFullRedraw = true;
                        InvalidateVisual();
                    }
                    break;
                    
                case nameof(PianoRollViewModel.TimelinePosition):
                case nameof(PianoRollViewModel.PlaybackPosition):
                    if (ViewModel != null && Math.Abs(_lastTimelinePosition - ViewModel.TimelinePosition) > 1.0)
                    {
                        // 播放指示线移动：只重绘受影响的区域
                        InvalidateTimelineRegion(_lastTimelinePosition, ViewModel.TimelinePosition);
                        _lastTimelinePosition = ViewModel.TimelinePosition;
                        InvalidateVisual();
                    }
                    break;
                    
                // 网格相关属性变化
                case nameof(PianoRollViewModel.IsOnionSkinEnabled):
                case nameof(PianoRollViewModel.OnionSkinOpacity):
                case nameof(PianoRollViewModel.OnionSkinPreviousFrames):
                case nameof(PianoRollViewModel.OnionSkinNextFrames):
                case nameof(PianoRollViewModel.TicksPerBeat):
                case nameof(PianoRollViewModel.BeatsPerMeasure):
                case nameof(PianoRollViewModel.TotalMeasures):
                case nameof(PianoRollViewModel.GridQuantization):
                case nameof(PianoRollViewModel.MeasureWidth):
                case nameof(PianoRollViewModel.SubdivisionLevel):
                    _needsFullRedraw = true;
                    InvalidateVisual();
                    break;
                    
                case nameof(PianoRollViewModel.ContentWidth):
                case nameof(PianoRollViewModel.TotalHeight):
                    _needsFullRedraw = true;
                    InvalidateMeasure();
                    InvalidateVisual();
                    break;
            }
        }

        private void OnNotesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 音符集合变化：需要完全重绘
            _needsFullRedraw = true;
            InvalidateVisual();
            
            // 同时更新MIDI事件
            if (ViewModel != null)
            {
                ViewModel.UpdateMidiEvents();
            }
        }

        private void InvalidateTimelineRegion(double oldPosition, double newPosition)
        {
            var minX = Math.Min(oldPosition, newPosition) - 5;
            var maxX = Math.Max(oldPosition, newPosition) + 5;
            
            _dirtyRegion = new Rect(minX, 0, maxX - minX, Bounds.Height);
            _hasDirtyRegion = true;
        }

        public override void Render(DrawingContext context)
        {
            if (ViewModel == null) return;

            var bounds = Bounds;
            RenderOptimized(context, bounds);
            
            // 重置脏区域标记
            _hasDirtyRegion = false;
            _needsFullRedraw = false;
        }

        private void RenderOptimized(DrawingContext context, Rect bounds)
        {
            // 绘制背景
            context.DrawRectangle(MainBackgroundBrush, null, bounds);

            // 根据更新策略选择渲染区域
            Rect renderRegion;
            if (_needsFullRedraw)
            {
                renderRegion = bounds;
            }
            else if (_hasDirtyRegion)
            {
                renderRegion = _dirtyRegion;
                // 使用裁剪以提高性能
                using (context.PushClip(_dirtyRegion))
                {
                    RenderContent(context, bounds, renderRegion);
                    return;
                }
            }
            else
            {
                // 即使没有变化，也要确保绘制所有内容
                RenderContent(context, bounds, bounds);
                return;
            }

            RenderContent(context, bounds, renderRegion);
        }

        /// <summary>
        /// 渲染所有内容 - 使用专门的渲染器
        /// </summary>
        private void RenderContent(DrawingContext context, Rect bounds, Rect renderRegion)
        {
            if (ViewModel == null) return;

            // 1. 绘制横向网格（钢琴键背景）
            _horizontalGridRenderer.Render(context, ViewModel, bounds, renderRegion);
            
            // 2. 绘制纵向网格（小节线、节拍线）
            _verticalGridRenderer.Render(context, ViewModel, bounds, renderRegion);
            
            // 3. 绘制洋葱皮效果
            _onionSkinRenderer.Render(context, ViewModel, bounds);
            
            // 4. 绘制音符
            RenderNotes(context, ViewModel, bounds);
            
            // 5. 绘制洋葱皮帧指示器
            _onionSkinRenderer.RenderFrameIndicators(context, ViewModel, bounds);
            
            // 6. 绘制播放指示线（最后绘制，确保在最顶层）
            _playheadRenderer.Render(context, ViewModel, bounds);
        }

        /// <summary>
        /// 渲染音符 - 保持原有逻辑
        /// </summary>
        private void RenderNotes(DrawingContext context, PianoRollViewModel viewModel, Rect bounds)
        {
            var noteRenderer = new NoteRenderer();
            
            foreach (var note in viewModel.Notes)
            {
                // 检查音符是否在可视区域内
                var noteRect = viewModel.GetNoteRect(note);
                if (!noteRect.Intersects(bounds))
                    continue;

                var renderType = NoteRenderType.Normal;
                if (note.IsSelected)
                    renderType = NoteRenderType.Selected;
                else if (viewModel.IsDragging && viewModel.DraggingNotes.Contains(note))
                    renderType = NoteRenderType.Dragging;
                else if (viewModel.IsCreatingNote && viewModel.CreatingNote == note)
                    renderType = NoteRenderType.Preview;

                noteRenderer.DrawNote(context, note, viewModel.Zoom, viewModel.PixelsPerTick, viewModel.KeyHeight, renderType);
            }
        }
    }
}