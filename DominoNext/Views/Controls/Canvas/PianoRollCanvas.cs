using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using DominoNext.ViewModels.Editor;
using DominoNext.Services.Interfaces;
using DominoNext.Services.Implementation;
using System;
using System.Collections.Specialized;

namespace DominoNext.Views.Controls.Canvas
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

        public PianoRollCanvas()
        {
            // 使用全局渲染同步服务
            _renderSyncService = RenderSyncService.Instance;
            _renderSyncService.RegisterTarget(this);
        }

        // 资源画刷获取助手方法
        private IBrush GetResourceBrush(string key, string fallbackHex)
        {
            try
            {
                if (Application.Current?.Resources.TryGetResource(key, null, out var obj) == true && obj is IBrush brush)
                    return brush;
            }
            catch { }

            try
            {
                return new SolidColorBrush(Color.Parse(fallbackHex));
            }
            catch
            {
                return Brushes.Transparent;
            }
        }

        private IPen GetResourcePen(string brushKey, string fallbackHex, double thickness = 1, DashStyle? dashStyle = null)
        {
            var brush = GetResourceBrush(brushKey, fallbackHex);
            var pen = new Pen(brush, thickness);
            if (dashStyle != null)
                pen.DashStyle = dashStyle;
            return pen;
        }

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

                canvas.InvalidateVisual();
            });
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PianoRollViewModel.Zoom) ||
                e.PropertyName == nameof(PianoRollViewModel.VerticalZoom) ||
                e.PropertyName == nameof(PianoRollViewModel.TimelinePosition) ||
                e.PropertyName == nameof(PianoRollViewModel.CurrentScrollOffset) ||
                e.PropertyName == nameof(PianoRollViewModel.VerticalScrollOffset))
            {
                // 优先使用同步渲染服务
                if (_renderSyncService != null)
                {
                    _renderSyncService.SyncRefresh();
                }
                else
                {
                    InvalidateVisual();
                }
            }
        }

        private void OnNotesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 音符变化时同步刷新
            if (_renderSyncService != null)
            {
                _renderSyncService.SyncRefresh();
            }
            else
            {
                InvalidateVisual();
            }
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

            // 绘制背景
            context.DrawRectangle(MainBackgroundBrush, null, bounds);

            // 基于当前滚动偏移量绘制内容
            var scrollOffset = ViewModel.CurrentScrollOffset;
            var verticalScrollOffset = ViewModel.VerticalScrollOffset;
            
            DrawHorizontalGridLines(context, bounds, verticalScrollOffset);
            DrawVerticalGridLines(context, bounds, scrollOffset);
            DrawTimeline(context, bounds, scrollOffset, verticalScrollOffset);
        }

        private void DrawHorizontalGridLines(DrawingContext context, Rect bounds, double verticalScrollOffset)
        {
            var keyHeight = ViewModel!.KeyHeight;

            // 计算可见的键范围
            var visibleStartKey = (int)(verticalScrollOffset / keyHeight);
            var visibleEndKey = (int)((verticalScrollOffset + bounds.Height) / keyHeight) + 1;

            visibleStartKey = Math.Max(0, visibleStartKey);
            visibleEndKey = Math.Min(128, visibleEndKey);

            // 绘制可见范围内的键
            for (int i = visibleStartKey; i < visibleEndKey; i++)
            {
                var midiNote = 127 - i; // MIDI音符号
                var y = i * keyHeight - verticalScrollOffset;
                var isBlackKey = ViewModel.IsBlackKey(midiNote);

                // 绘制行背景
                var rowRect = new Rect(0, y, bounds.Width, keyHeight);
                
                var rowBrush = isBlackKey ? GetBlackKeyRowBrush() : WhiteKeyRowBrush;
                context.DrawRectangle(rowBrush, null, rowRect);

                // 判断是否是八度分界线（B和C之间）
                var isOctaveBoundary = midiNote % 12 == 0;

                // 绘制水平分界线
                var pen = isOctaveBoundary 
                    ? GetResourcePen("BorderLineBlackBrush", "#FF000000", 1.5)
                    : GetResourcePen("GridLineBrush", "#FFbad2f2", 0.5);
                
                context.DrawLine(pen, new Point(0, y + keyHeight), new Point(bounds.Width, y + keyHeight));
            }
        }

        /// <summary>
        /// 绘制垂直网格线（基于滚动偏移量）
        /// </summary>
        private void DrawVerticalGridLines(DrawingContext context, Rect bounds, double scrollOffset)
        {
            var measureWidth = ViewModel!.MeasureWidth;
            var beatWidth = ViewModel.BeatWidth;
            var eighthWidth = ViewModel.EighthNoteWidth;
            var sixteenthWidth = ViewModel.SixteenthNoteWidth;
            var totalKeyHeight = 128 * ViewModel.KeyHeight;

            var startY = 0;
            var endY = Math.Min(bounds.Height, totalKeyHeight);

            // 计算可见范围内的网格线
            var visibleStartTime = scrollOffset / (ViewModel.PixelsPerTick * ViewModel.Zoom);
            var visibleEndTime = (scrollOffset + bounds.Width) / (ViewModel.PixelsPerTick * ViewModel.Zoom);

            // 绘制十六分音符线
            if (sixteenthWidth > 5)
            {
                var sixteenthTicks = ViewModel.TicksPerBeat / 4;
                var startSixteenth = (int)(visibleStartTime / sixteenthTicks);
                var endSixteenth = (int)(visibleEndTime / sixteenthTicks) + 1;

                var sixteenthNotePen = GetResourcePen("GridLineBrush", "#FFafafaf", 0.5, new DashStyle(new double[] { 1, 3 }, 0));

                for (int i = startSixteenth; i <= endSixteenth; i++)
                {
                    if (i % 4 == 0) continue; // 跳过拍线位置

                    var time = i * sixteenthTicks;
                    var x = time * ViewModel.PixelsPerTick * ViewModel.Zoom - scrollOffset;
                    
                    if (x >= 0 && x <= bounds.Width)
                    {
                        context.DrawLine(sixteenthNotePen, new Point(x, startY), new Point(x, endY));
                    }
                }
            }

            // 绘制八分音符线
            if (eighthWidth > 10)
            {
                var eighthTicks = ViewModel.TicksPerBeat / 2;
                var startEighth = (int)(visibleStartTime / eighthTicks);
                var endEighth = (int)(visibleEndTime / eighthTicks) + 1;

                var eighthNotePen = GetResourcePen("GridLineBrush", "#FFafafaf", 0.7, new DashStyle(new double[] { 2, 2 }, 0));

                for (int i = startEighth; i <= endEighth; i++)
                {
                    if (i % 2 == 0) continue; // 跳过拍线位置

                    var time = i * eighthTicks;
                    var x = time * ViewModel.PixelsPerTick * ViewModel.Zoom - scrollOffset;
                    
                    if (x >= 0 && x <= bounds.Width)
                    {
                        context.DrawLine(eighthNotePen, new Point(x, startY), new Point(x, endY));
                    }
                }
            }

            // 绘制拍线
            var beatTicks = ViewModel.TicksPerBeat;
            var startBeat = (int)(visibleStartTime / beatTicks);
            var endBeat = (int)(visibleEndTime / beatTicks) + 1;

            var beatLinePen = GetResourcePen("GridLineBrush", "#FFafafaf", 0.8);

            for (int i = startBeat; i <= endBeat; i++)
            {
                if (i % ViewModel.BeatsPerMeasure == 0) continue; // 跳过小节线位置

                var time = i * beatTicks;
                var x = time * ViewModel.PixelsPerTick * ViewModel.Zoom - scrollOffset;
                
                if (x >= 0 && x <= bounds.Width)
                {
                    context.DrawLine(beatLinePen, new Point(x, startY), new Point(x, endY));
                }
            }

            // 绘制小节线
            var measureTicks = ViewModel.BeatsPerMeasure * ViewModel.TicksPerBeat;
            var startMeasure = (int)(visibleStartTime / measureTicks);
            var endMeasure = (int)(visibleEndTime / measureTicks) + 1;

            var measureLinePen = GetResourcePen("MeasureLineBrush", "#FF000080", 1.2);

            for (int i = startMeasure; i <= endMeasure; i++)
            {
                var time = i * measureTicks;
                var x = time * ViewModel.PixelsPerTick * ViewModel.Zoom - scrollOffset;
                
                if (x >= 0 && x <= bounds.Width)
                {
                    context.DrawLine(measureLinePen, new Point(x, startY), new Point(x, endY));
                }
            }
        }

        /// <summary>
        /// 绘制时间轴（基于滚动偏移量）
        /// </summary>
        private void DrawTimeline(DrawingContext context, Rect bounds, double scrollOffset, double verticalScrollOffset)
        {
            var timelinePixelPosition = ViewModel!.TimelinePosition * ViewModel.PixelsPerTick * ViewModel.Zoom - scrollOffset;

            if (timelinePixelPosition >= 0 && timelinePixelPosition <= bounds.Width)
            {
                var timelinePen = new Pen(TimelineBrush, 2);
                context.DrawLine(timelinePen, 
                    new Point(timelinePixelPosition, 0), 
                    new Point(timelinePixelPosition, bounds.Height));
            }
        }

        /// <summary>
        /// 获取优化的黑键行背景色
        /// </summary>
        private IBrush GetBlackKeyRowBrush()
        {
            // 根据主背景色的亮度动态调整黑键行的颜色
            var mainBg = GetResourceBrush("MainCanvasBackgroundBrush", "#FFFFFFFF");
            
            if (mainBg is SolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                var brightness = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) / 255.0;
                
                if (brightness < 0.5) // 深色主题
                {
                    // 深色主题：使黑键行稍微亮一些
                    return new SolidColorBrush(Color.FromArgb(
                        255,
                        (byte)Math.Min(255, color.R + 15),
                        (byte)Math.Min(255, color.G + 15),
                        (byte)Math.Min(255, color.B + 15)
                    ));
                }
                else // 浅色主题
                {
                    // 浅色主题：使黑键行稍微暗一些
                    return new SolidColorBrush(Color.FromArgb(
                        255,
                        (byte)Math.Max(0, color.R - 25),
                        (byte)Math.Max(0, color.G - 25),
                        (byte)Math.Max(0, color.B - 25)
                    ));
                }
            }
            
            // 回退到预设颜色
            return GetResourceBrush("AppBackgroundBrush", "#FFedf3fe");
        }
    }
}