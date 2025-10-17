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
    /// ������������
    /// �����������������Ԥ�������ߣ�����ָ��ߣ������ߣ�С���ߣ�
    /// </summary>
    public class TrackOverviewCanvas : Control
    {
        private readonly EnderLogger _logger = EnderLogger.Instance;

        #region ��������

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

        #region ��ˢ�ͻ��ʻ���

        private IBrush _backgroundBrush;
        private IPen _trackSeparatorPen;
        private IPen _measureLinePen;
        private IPen _beatLinePen;
        private IBrush _noteBrush;
        private IBrush _selectedTrackBackgroundBrush;

        #endregion

        public TrackOverviewCanvas()
        {
            // ��ʼ����ˢ�ͻ���
            _backgroundBrush = RenderingUtils.GetResourceBrush("MainCanvasBackgroundBrush", "#FFFFFFFF");
            _trackSeparatorPen = RenderingUtils.GetResourcePen("GridLineBrush", "#FFCCCCCC", 1.0);
            _measureLinePen = RenderingUtils.GetResourcePen("MeasureLineBrush", "#FF000080", 1.5);
            _beatLinePen = RenderingUtils.GetResourcePen("GridLineBrush", "#FFD0D0D0", 0.8);
            _noteBrush = new SolidColorBrush(Color.FromRgb(100, 149, 237)); // ǳ��ɫ
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

            // ���Ʊ���
            context.DrawRectangle(_backgroundBrush, null, bounds);

            // ����С���ߣ����ߣ�
            DrawMeasureLines(context, bounds);

            // �������ߣ����ߣ���ѡ��
            DrawBeatLines(context, bounds);

            // �������������
            DrawTracksAndNotes(context, bounds);
        }

        /// <summary>
        /// ����С���ߣ����ߣ�
        /// </summary>
        private void DrawMeasureLines(DrawingContext context, Rect bounds)
        {
            if (ViewModel == null) return;

            var scrollOffset = ViewModel.CurrentScrollOffset;
            var scale = ViewModel.TimeToPixelScale;
            var measureInterval = (double)ViewModel.BeatsPerMeasure;

            // 计算可见范围（基于ScrollViewer的滚动位置）
            var visibleStartTime = scrollOffset / scale;
            var visibleEndTime = (scrollOffset + bounds.Width) / scale;

            var startMeasure = (int)(visibleStartTime / measureInterval);
            var endMeasure = (int)(visibleEndTime / measureInterval) + 1;

            for (int i = startMeasure; i <= endMeasure; i++)
            {
                var timeValue = i * measureInterval;
                // 直接计算Canvas坐标，ScrollViewer会自动处理偏移
                var x = timeValue * scale;

                if (x >= scrollOffset && x <= scrollOffset + bounds.Width)
                {
                    context.DrawLine(_measureLinePen, new Point(x, 0), new Point(x, bounds.Height));
                }
            }
        }

        /// <summary>
        /// �������ߣ����ߣ�
        /// </summary>
        private void DrawBeatLines(DrawingContext context, Rect bounds)
        {
            if (ViewModel == null) return;

            // ֻ�ڷŴ�ʱ��ʾ����
            if (ViewModel.Zoom < 0.8) return;

            var scrollOffset = ViewModel.CurrentScrollOffset;
            var scale = ViewModel.TimeToPixelScale;
            var beatInterval = 1.0; // 每拍一个四分音符

            // 计算可见范围
            var visibleStartTime = scrollOffset / scale;
            var visibleEndTime = (scrollOffset + bounds.Width) / scale;

            var startBeat = (int)(visibleStartTime / beatInterval);
            var endBeat = (int)(visibleEndTime / beatInterval) + 1;

            for (int i = startBeat; i <= endBeat; i++)
            {
                // 跳过与小节线重合的位置
                if (i % ViewModel.BeatsPerMeasure == 0) continue;

                var timeValue = i * beatInterval;
                var x = timeValue * scale;

                if (x >= scrollOffset && x <= scrollOffset + bounds.Width)
                {
                    context.DrawLine(_beatLinePen, new Point(x, 0), new Point(x, bounds.Height));
                }
            }
        }

        /// <summary>
        /// �������������
        /// </summary>
        private void DrawTracksAndNotes(DrawingContext context, Rect bounds)
        {
            if (ViewModel == null || TrackSelector == null || PianoRoll == null)
            {
                _logger.Debug("TrackOverviewCanvas", "����ʧ�ܣ�ViewModel��TrackSelector �� PianoRoll Ϊ��");
                return;
            }

            var tracks = TrackSelector.Tracks.ToList();
            var trackHeight = ViewModel.TrackHeight;
            // ע�⣺����ScrollViewer�Ѿ����������⣬����Ҫʹ��scrollOffset

            _logger.Debug("TrackOverviewCanvas", $"��ʼ���� {tracks.Count} �����죬�����߶�: {bounds.Height}, �ܸ߶�: {ViewModel.TotalHeight}");

            for (int i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i];
                var y = i * trackHeight;

                // ����ѡ������ı���
                if (track.IsSelected)
                {
                    var trackRect = new Rect(0, y, bounds.Width, trackHeight);
                    context.DrawRectangle(_selectedTrackBackgroundBrush, null, trackRect);
                }

                // ��������ָ��ߣ����ߣ�
                var separatorY = y + trackHeight;
                context.DrawLine(_trackSeparatorPen, 
                    new Point(0, separatorY), 
                    new Point(bounds.Width, separatorY));

                // ���Ƹ����������
                DrawNotesForTrack(context, bounds, track, y, trackHeight);
            }

            _logger.Debug("TrackOverviewCanvas", $"��ɻ��� {tracks.Count} ������");
        }

        /// <summary>
        /// ����ָ�����������Ԥ��
        /// </summary>
        private void DrawNotesForTrack(DrawingContext context, Rect bounds, TrackViewModel track, 
            double trackY, double trackHeight)
        {
            if (PianoRoll == null || ViewModel == null) return;

            var scrollOffset = ViewModel.CurrentScrollOffset;
            var scale = ViewModel.TimeToPixelScale;

            // ��ȡ���������������
            var trackIndex = track.TrackNumber - 1;
            var notes = PianoRoll.GetAllNotes()
                .Where(n => n.TrackIndex == trackIndex)
                .ToList();

            _logger.Debug("TrackOverviewCanvas", $"���� {track.TrackNumber} ({track.TrackName}): �ҵ� {notes.Count} ������");

            if (!notes.Any()) return;

            // ����ɼ���Χ
            var visibleStartTime = scrollOffset / scale;
            var visibleEndTime = (scrollOffset + bounds.Width) / scale;

            // ��ȡ���߷�Χ���ڹ�һ��
            var minPitch = notes.Min(n => n.Pitch);
            var maxPitch = notes.Max(n => n.Pitch);
            var pitchRange = maxPitch - minPitch;
            if (pitchRange == 0) pitchRange = 1; // ���������

            // Ϊ����Ԥ�����±߾�
            var notePadding = 4.0;
            var availableHeight = trackHeight - 2 * notePadding;

            int visibleNotesCount = 0;
            foreach (var note in notes)
            {
                var noteStartTime = note.StartPosition.ToDouble();
                var noteEndTime = noteStartTime + note.Duration.ToDouble();

                // �������ڿɼ���Χ�ڵ�����
                if (noteEndTime < visibleStartTime || noteStartTime > visibleEndTime)
                    continue;

                visibleNotesCount++;

                // 计算音符的X位置和宽度
                var x = noteStartTime * scale;
                var width = note.Duration.ToDouble() * ViewModel.TimeToPixelScale;
                width = Math.Max(2, width); // 最小宽度2像素

                // 计算音高的Y位置（音高越高，Y越小，即越靠上方）
                var normalizedPitch = (note.Pitch - minPitch) / (double)pitchRange;
                var noteHeight = Math.Max(2, availableHeight / 8); // �����߶�
                var noteY = trackY + notePadding + (availableHeight - noteHeight) * (1 - normalizedPitch);

                // ������������
                var noteRect = new Rect(x, noteY, width, noteHeight);
                
                // �������ȵ���͸����
                var alpha = (byte)(127 + note.Velocity);
                var noteBrush = new SolidColorBrush(Color.FromArgb(alpha, 100, 149, 237));
                
                context.DrawRectangle(noteBrush, null, noteRect);
            }

            if (visibleNotesCount > 0)
            {
                _logger.Debug("TrackOverviewCanvas", $"���� {track.TrackNumber}: ������ {visibleNotesCount} ���ɼ�����");
            }
        }
    }
}
