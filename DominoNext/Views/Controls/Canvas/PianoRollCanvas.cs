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

        // 添加洋葱皮渲染器
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

        private IPen GetResourcePen(string brushKey, string fallbackHex, double thickness = 1, DashStyle? dashStyle = null)
        {
            var brush = GetResourceBrush(brushKey, fallbackHex);
            var pen = new Pen(brush, thickness);
            if (dashStyle != null)
                pen.DashStyle = dashStyle;
            return pen;
        }

        // 添加播放指示线画笔
        private IPen PlaybackIndicatorPen => GetResourcePen("PlaybackIndicatorBrush", "#FFFF0000", 2);

        // 使用动态资源的画刷和画笔
        private IBrush TimelineBrush => GetResourceBrush("VelocityIndicatorBrush", "#FFFF0000");
        private IBrush WhiteKeyRowBrush => GetResourceBrush("KeyWhiteBrush", "#FFFFFFFF");
        private IBrush BlackKeyRowBrush => GetResourceBrush("AppBackgroundBrush", "#FFedf3fe");
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
                        // 立即重绘，不使用延迟
                        InvalidateVisual();
                    }
                    break;
                    
                case nameof(PianoRollViewModel.VerticalZoom):
                    if (ViewModel != null && Math.Abs(_lastVerticalZoom - ViewModel.VerticalZoom) > 0.001)
                    {
                        _lastVerticalZoom = ViewModel.VerticalZoom;
                        _needsFullRedraw = true;
                        // 立即重绘，不使用延迟
                        InvalidateVisual();
                    }
                    break;
                    
                case nameof(PianoRollViewModel.TimelinePosition):
                    if (ViewModel != null && Math.Abs(_lastTimelinePosition - ViewModel.TimelinePosition) > 1.0)
                    {
                        // 时间线移动：只重绘受影响的区域
                        InvalidateTimelineRegion(_lastTimelinePosition, ViewModel.TimelinePosition);
                        _lastTimelinePosition = ViewModel.TimelinePosition;
                        // 立即重绘，不使用延迟
                        InvalidateVisual();
                    }
                    break;
                    
                // 添加对小节相关属性的监听，确保小节线显示正确更新
                case nameof(PianoRollViewModel.IsOnionSkinEnabled):
                case nameof(PianoRollViewModel.OnionSkinOpacity):
                case nameof(PianoRollViewModel.OnionSkinPreviousFrames):
                case nameof(PianoRollViewModel.OnionSkinNextFrames):
                case nameof(PianoRollViewModel.TicksPerBeat):
                case nameof(PianoRollViewModel.BeatsPerMeasure):
                case nameof(PianoRollViewModel.TotalMeasures):
                case nameof(PianoRollViewModel.GridQuantization):
                case nameof(PianoRollViewModel.MeasureWidth): // 添加对MeasureWidth的监听
                    // 这些属性变化需要完全重绘
                    _needsFullRedraw = true;
                    // 立即重绘，不使用延迟
                    InvalidateVisual();
                    break;
                    
                case nameof(PianoRollViewModel.ContentWidth):
                case nameof(PianoRollViewModel.TotalHeight):
                    // 尺寸变化时需要重新测量和排列
                    _needsFullRedraw = true;
                    InvalidateMeasure();
                    // 立即重绘，不使用延迟
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

            // 移除帧率限制 - 让Avalonia自己管理渲染频率
            // Avalonia的渲染管道已经做了很好的优化，强制限制帧率可能导致卡顿
            
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
                // 修复：即使没有变化，也要确保绘制背景网格线
                // 这样可以确保初始加载时背景图案可见
                RenderContent(context, bounds, bounds);
                return;
            }

            RenderContent(context, bounds, renderRegion);
        }

        private void RenderContent(DrawingContext context, Rect bounds, Rect renderRegion)
        {
            DrawHorizontalGridLinesOptimized(context, bounds, renderRegion);
            DrawVerticalGridLinesOptimized(context, bounds, renderRegion);
            
            // 绘制洋葱皮效果
            _onionSkinRenderer.Render(context, ViewModel, bounds);
            
            DrawTimeline(context, bounds, renderRegion);
            
            // 绘制音符
            if (ViewModel != null)
            {
                RenderNotes(context, ViewModel, bounds);
            }
            
            // 绘制洋葱皮帧指示器
            _onionSkinRenderer.RenderFrameIndicators(context, ViewModel, bounds);
        }

        /// <summary>
        /// 绘制时间线
        /// </summary>
        private void DrawTimeline(DrawingContext context, Rect bounds, Rect renderRegion)
        {
            if (ViewModel?.PlaybackPosition >= 0)
            {
                var timelinePen = GetResourcePen("PlayheadBrush", "#FFFF0000", 2);
                var timelineX = ViewModel.PlaybackPosition * ViewModel.PixelsPerTick;
                context.DrawLine(timelinePen, new Point(timelineX, bounds.Top), new Point(timelineX, bounds.Bottom));
            }
        }

        /// <summary>
        /// 优化的水平网格线绘制 - 只绘制可见区域
        /// </summary>
        private void DrawHorizontalGridLinesOptimized(DrawingContext context, Rect bounds, Rect viewport)
        {
            var keyHeight = ViewModel!.KeyHeight;
            var startKey = Math.Max(0, (int)(viewport.Y / keyHeight));
            var endKey = Math.Min(127, (int)((viewport.Y + viewport.Height) / keyHeight) + 1);

            for (int i = startKey; i <= endKey; i++)
            {
                var midiNote = 127 - i;
                var y = i * keyHeight;
                var isBlackKey = ViewModel.IsBlackKey(midiNote);

                var rowRect = new Rect(0, y, bounds.Width, keyHeight);
                var rowBrush = isBlackKey ? GetBlackKeyRowBrush() : WhiteKeyRowBrush;
                context.DrawRectangle(rowBrush, null, rowRect);

                var isOctaveBoundary = midiNote % 12 == 0;
                var pen = isOctaveBoundary 
                    ? GetResourcePen("BorderLineBlackBrush", "#FF000000", 1.5)
                    : GetResourcePen("GridLineBrush", "#FFbad2f2", 0.5);
                
                context.DrawLine(pen, new Point(0, y + keyHeight), new Point(bounds.Width, y + keyHeight));
            }
        }

        private void DrawVerticalGridLinesOptimized(DrawingContext context, Rect bounds, Rect viewport)
        {
            var measureWidth = ViewModel!.MeasureWidth;
            var beatWidth = ViewModel.BeatWidth;
            var startX = viewport.X;
            var endX = viewport.X + viewport.Width;
            var startY = viewport.Y;
            var endY = Math.Min(viewport.Y + viewport.Height, 128 * ViewModel.KeyHeight);

            // 只绘制可见区域的拍线
            var startBeat = Math.Max(0, (int)(startX / beatWidth));
            var endBeat = (int)(endX / beatWidth) + 1;
            var beatLinePen = GetResourcePen("GridLineBrush", "#FFafafaf", 0.8);

            for (int i = startBeat; i <= endBeat; i++)
            {
                if (i % ViewModel.BeatsPerMeasure == 0) continue;
                var x = i * beatWidth;
                if (x >= startX && x <= endX)
                    context.DrawLine(beatLinePen, new Point(x, startY), new Point(x, endY));
            }

            // 只绘制可见区域的小节线
            var startMeasure = Math.Max(0, (int)(startX / measureWidth));
            var endMeasure = (int)(endX / measureWidth) + 1;
            var measureLinePen = GetResourcePen("MeasureLineBrush", "#FF000080", 1.2);

            for (int i = startMeasure; i <= endMeasure; i++)
            {
                var x = i * measureWidth;
                if (x >= startX && x <= endX)
                    context.DrawLine(measureLinePen, new Point(x, startY), new Point(x, endY));
            }
        }

        private IBrush GetBlackKeyRowBrush()
        {
            var mainBg = GetResourceBrush("MainCanvasBackgroundBrush", "#FFFFFFFF");
            
            if (mainBg is SolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                var brightness = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) / 255.0;
                
                if (brightness < 0.5)
                {
                    return new SolidColorBrush(Color.FromArgb(255,
                        (byte)Math.Min(255, color.R + 15),
                        (byte)Math.Min(255, color.G + 15),
                        (byte)Math.Min(255, color.B + 15)));
                }
                else
                {
                    return new SolidColorBrush(Color.FromArgb(255,
                        (byte)Math.Max(0, color.R - 25),
                        (byte)Math.Max(0, color.G - 25),
                        (byte)Math.Max(0, color.B - 25)));
                }
            }
            
            return GetResourceBrush("AppBackgroundBrush", "#FFedf3fe");
        }

        private void RenderMeasureLines(DrawingContext context, PianoRollViewModel viewModel, Rect bounds)
        {
            var measurePen = GetResourcePen("MeasureLineBrush", "#FFCCCCCC", 1);
            var beatPen = GetResourcePen("BeatLineBrush", "#FFDDDDDD", 1, new DashStyle(new double[] { 2, 2 }, 0));

            // 修复：正确计算可视区域的起始和结束小节
            var measureWidth = viewModel.MeasureWidth;
            var startMeasure = Math.Max(0, (int)(bounds.X / measureWidth));
            var endMeasure = (int)((bounds.X + bounds.Width) / measureWidth) + 1;

            // 绘制小节线和拍线
            for (int measure = startMeasure; measure <= endMeasure; measure++)
            {
                // 小节线
                var measureX = measure * measureWidth;
                // 修复：检查小节线是否在可视区域内
                if (measureX >= bounds.X && measureX <= bounds.X + bounds.Width)
                {
                    context.DrawLine(measurePen, new Point(measureX, bounds.Top), new Point(measureX, bounds.Bottom));
                }

                // 拍线
                if (measure < endMeasure)
                {
                    for (int beat = 1; beat < viewModel.BeatsPerMeasure; beat++)
                    {
                        var beatX = measureX + beat * viewModel.BeatWidth;
                        // 修复：检查拍线是否在可视区域内
                        if (beatX >= bounds.X && beatX <= bounds.X + bounds.Width)
                        {
                            context.DrawLine(beatPen, new Point(beatX, bounds.Top), new Point(beatX, bounds.Bottom));
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 绘制播放指示线
        /// </summary>
        private void RenderPlayhead(DrawingContext context, PianoRollViewModel viewModel, Rect bounds)
        {
            if (viewModel.IsPlaying)
            {
                var playheadPen = GetResourcePen("PlayheadBrush", "#FFFF0000", 2);
                var playheadX = viewModel.PlaybackPosition * viewModel.PixelsPerTick;
                context.DrawLine(playheadPen, new Point(playheadX, bounds.Top), new Point(playheadX, bounds.Bottom));
            }
        }

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