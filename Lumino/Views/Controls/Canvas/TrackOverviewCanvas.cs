using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lumino.ViewModels;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Utils;
using EnderDebugger;

namespace Lumino.Views.Controls.Canvas
{
    /// <summary>
    /// 音轨总览画布
    /// 绘制所有音轨的音符预览、横线（音轨分隔线）、纵线（小节线）
    /// </summary>
    public class TrackOverviewCanvas : Control
    {
        private readonly EnderLogger _logger = EnderLogger.Instance;

        #region 依赖属性

        public static readonly StyledProperty<TrackOverviewViewModel?> ViewModelProperty =
            AvaloniaProperty.Register<TrackOverviewCanvas, TrackOverviewViewModel?>(nameof(ViewModel));

        public static readonly StyledProperty<TrackSelectorViewModel?> TrackSelectorProperty =
            AvaloniaProperty.Register<TrackOverviewCanvas, TrackSelectorViewModel?>(nameof(TrackSelector));

        public static readonly StyledProperty<PianoRollViewModel?> PianoRollProperty =
            AvaloniaProperty.Register<TrackOverviewCanvas, PianoRollViewModel?>(nameof(PianoRoll));

        public TrackOverviewViewModel? ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public TrackSelectorViewModel? TrackSelector
        {
            get => GetValue(TrackSelectorProperty);
            set => SetValue(TrackSelectorProperty, value);
        }

        public PianoRollViewModel? PianoRoll
        {
            get => GetValue(PianoRollProperty);
            set => SetValue(PianoRollProperty, value);
        }

        #endregion

        #region 画刷和画笔缓存

        private IBrush _backgroundBrush;
        private IPen _trackSeparatorPen;
        private IPen _measureLinePen;
        private IPen _beatLinePen;
        private IBrush _noteBrush;
        private IBrush _selectedTrackBackgroundBrush;

        #endregion

        public TrackOverviewCanvas()
        {
            // 初始化画刷和画笔
            _backgroundBrush = RenderingUtils.GetResourceBrush("MainCanvasBackgroundBrush", "#FFFFFFFF");
            _trackSeparatorPen = RenderingUtils.GetResourcePen("GridLineBrush", "#FFCCCCCC", 1.0);
            _measureLinePen = RenderingUtils.GetResourcePen("MeasureLineBrush", "#FF000080", 1.5);
            _beatLinePen = RenderingUtils.GetResourcePen("GridLineBrush", "#FFD0D0D0", 0.8);
            _noteBrush = new SolidColorBrush(Color.FromRgb(100, 149, 237)); // 浅蓝色
            _selectedTrackBackgroundBrush = new SolidColorBrush(Color.FromArgb(30, 100, 149, 237));
        }

        static TrackOverviewCanvas()
        {
            AffectsRender<TrackOverviewCanvas>(
                ViewModelProperty,
                TrackSelectorProperty,
                PianoRollProperty);

            ViewModelProperty.Changed.AddClassHandler<TrackOverviewCanvas>((canvas, e) =>
            {
                if (e.OldValue is TrackOverviewViewModel oldVm)
                {
                    oldVm.PropertyChanged -= canvas.OnViewModelPropertyChanged;
                }

                if (e.NewValue is TrackOverviewViewModel newVm)
                {
                    newVm.PropertyChanged += canvas.OnViewModelPropertyChanged;
                }
            });
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TrackOverviewViewModel.Zoom) ||
                e.PropertyName == nameof(TrackOverviewViewModel.CurrentScrollOffset) ||
                e.PropertyName == nameof(TrackOverviewViewModel.TrackHeight))
            {
                InvalidateVisual();
            }
        }

        public override void Render(DrawingContext context)
        {
            if (ViewModel == null || TrackSelector == null || PianoRoll == null)
                return;

            var bounds = Bounds;

            // 绘制背景
            context.DrawRectangle(_backgroundBrush, null, bounds);

            // 绘制小节线（纵线）
            DrawMeasureLines(context, bounds);

            // 绘制拍线（纵线，可选）
            DrawBeatLines(context, bounds);

            // 绘制音轨和音符
            DrawTracksAndNotes(context, bounds);
        }

        /// <summary>
        /// 绘制小节线（纵线）
        /// </summary>
        private void DrawMeasureLines(DrawingContext context, Rect bounds)
        {
            if (ViewModel == null) return;

            var scrollOffset = ViewModel.CurrentScrollOffset;
            var measureInterval = (double)ViewModel.BeatsPerMeasure; // 以四分音符为单位

            // 计算可见范围
            var visibleStartTime = scrollOffset / ViewModel.TimeToPixelScale;
            var visibleEndTime = (scrollOffset + bounds.Width) / ViewModel.TimeToPixelScale;

            var startMeasure = (int)(visibleStartTime / measureInterval);
            var endMeasure = (int)(visibleEndTime / measureInterval) + 1;

            for (int i = startMeasure; i <= endMeasure; i++)
            {
                var timeValue = i * measureInterval;
                var x = timeValue * ViewModel.TimeToPixelScale - scrollOffset;

                if (x >= 0 && x <= bounds.Width)
                {
                    context.DrawLine(_measureLinePen, new Point(x, 0), new Point(x, bounds.Height));
                }
            }
        }

        /// <summary>
        /// 绘制拍线（纵线）
        /// </summary>
        private void DrawBeatLines(DrawingContext context, Rect bounds)
        {
            if (ViewModel == null) return;

            // 只在放大时显示拍线
            if (ViewModel.Zoom < 0.8) return;

            var scrollOffset = ViewModel.CurrentScrollOffset;
            var beatInterval = 1.0; // 每拍一个四分音符

            // 计算可见范围
            var visibleStartTime = scrollOffset / ViewModel.TimeToPixelScale;
            var visibleEndTime = (scrollOffset + bounds.Width) / ViewModel.TimeToPixelScale;

            var startBeat = (int)(visibleStartTime / beatInterval);
            var endBeat = (int)(visibleEndTime / beatInterval) + 1;

            for (int i = startBeat; i <= endBeat; i++)
            {
                // 跳过与小节线重合的位置
                if (i % ViewModel.BeatsPerMeasure == 0) continue;

                var timeValue = i * beatInterval;
                var x = timeValue * ViewModel.TimeToPixelScale - scrollOffset;

                if (x >= 0 && x <= bounds.Width)
                {
                    context.DrawLine(_beatLinePen, new Point(x, 0), new Point(x, bounds.Height));
                }
            }
        }

        /// <summary>
        /// 绘制音轨和音符
        /// </summary>
        private void DrawTracksAndNotes(DrawingContext context, Rect bounds)
        {
            if (ViewModel == null || TrackSelector == null || PianoRoll == null)
            {
                _logger.Debug("TrackOverviewCanvas", "绘制失败：ViewModel、TrackSelector 或 PianoRoll 为空");
                return;
            }

            var tracks = TrackSelector.Tracks.ToList();
            var trackHeight = ViewModel.TrackHeight;
            var scrollOffset = ViewModel.CurrentScrollOffset;

            _logger.Debug("TrackOverviewCanvas", $"开始绘制 {tracks.Count} 个音轨，画布高度: {bounds.Height}, 总高度: {ViewModel.TotalHeight}");

            for (int i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i];
                var y = i * trackHeight;

                // 绘制选中音轨的背景
                if (track.IsSelected)
                {
                    var trackRect = new Rect(0, y, bounds.Width, trackHeight);
                    context.DrawRectangle(_selectedTrackBackgroundBrush, null, trackRect);
                }

                // 绘制音轨分隔线（横线）
                var separatorY = y + trackHeight;
                context.DrawLine(_trackSeparatorPen, 
                    new Point(0, separatorY), 
                    new Point(bounds.Width, separatorY));

                // 绘制该音轨的音符
                DrawNotesForTrack(context, bounds, track, y, trackHeight, scrollOffset);
            }

            _logger.Debug("TrackOverviewCanvas", $"完成绘制 {tracks.Count} 个音轨");
        }

        /// <summary>
        /// 绘制指定音轨的音符预览
        /// </summary>
        private void DrawNotesForTrack(DrawingContext context, Rect bounds, TrackViewModel track, 
            double trackY, double trackHeight, double scrollOffset)
        {
            if (PianoRoll == null || ViewModel == null) return;

            // 获取该音轨的所有音符
            var trackIndex = track.TrackNumber - 1;
            var notes = PianoRoll.GetAllNotes()
                .Where(n => n.TrackIndex == trackIndex)
                .ToList();

            _logger.Debug("TrackOverviewCanvas", $"音轨 {track.TrackNumber} ({track.TrackName}): 找到 {notes.Count} 个音符");

            if (!notes.Any()) return;

            // 计算可见范围
            var visibleStartTime = scrollOffset / ViewModel.TimeToPixelScale;
            var visibleEndTime = (scrollOffset + bounds.Width) / ViewModel.TimeToPixelScale;

            // 获取音高范围用于归一化
            var minPitch = notes.Min(n => n.Pitch);
            var maxPitch = notes.Max(n => n.Pitch);
            var pitchRange = maxPitch - minPitch;
            if (pitchRange == 0) pitchRange = 1; // 避免除以零

            // 为音符预留上下边距
            var notePadding = 4.0;
            var availableHeight = trackHeight - 2 * notePadding;

            int visibleNotesCount = 0;
            foreach (var note in notes)
            {
                var noteStartTime = note.StartPosition.ToDouble();
                var noteEndTime = noteStartTime + note.Duration.ToDouble();

                // 跳过不在可见范围内的音符
                if (noteEndTime < visibleStartTime || noteStartTime > visibleEndTime)
                    continue;

                visibleNotesCount++;

                // 计算音符的X位置和宽度
                var x = noteStartTime * ViewModel.TimeToPixelScale - scrollOffset;
                var width = note.Duration.ToDouble() * ViewModel.TimeToPixelScale;
                width = Math.Max(2, width); // 最小宽度2像素

                // 根据音高计算Y位置（音高越高，Y越小，即在上方）
                var normalizedPitch = (note.Pitch - minPitch) / (double)pitchRange;
                var noteHeight = Math.Max(2, availableHeight / 8); // 音符高度
                var noteY = trackY + notePadding + (availableHeight - noteHeight) * (1 - normalizedPitch);

                // 绘制音符矩形
                var noteRect = new Rect(x, noteY, width, noteHeight);
                
                // 根据力度调整透明度
                var alpha = (byte)(127 + note.Velocity);
                var noteBrush = new SolidColorBrush(Color.FromArgb(alpha, 100, 149, 237));
                
                context.DrawRectangle(noteBrush, null, noteRect);
            }

            if (visibleNotesCount > 0)
            {
                _logger.Debug("TrackOverviewCanvas", $"音轨 {track.TrackNumber}: 绘制了 {visibleNotesCount} 个可见音符");
            }
        }
    }
}
