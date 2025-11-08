using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Enums;
using Lumino.ViewModels.Editor.Modules;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// 控制器事件管理与曲线应用逻辑。
    /// </summary>
    public partial class PianoRollViewModel
    {
        private void OnControllerEventsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateCurrentTrackControllerEvents();
        }

        private void UpdateCurrentTrackControllerEvents()
        {
            CurrentTrackControllerEvents.Clear();

            var filtered = ControllerEvents
                .Where(evt => evt.TrackIndex == CurrentTrackIndex)
                .OrderBy(evt => evt.ControllerNumber)
                .ThenBy(evt => evt.Time.ToDouble())
                .ToList();

            foreach (var evt in filtered)
            {
                CurrentTrackControllerEvents.Add(evt);
            }
        }

        public IEnumerable<ControllerEventViewModel> GetAllControllerEvents()
        {
            return ControllerEvents;
        }

        public void SetControllerEvents(IEnumerable<ControllerEvent>? events)
        {
            ControllerEvents.CollectionChanged -= OnControllerEventsCollectionChanged;
            ControllerEvents.Clear();

            if (events != null)
            {
                foreach (var model in events)
                {
                    ControllerEvents.Add(ControllerEventViewModel.FromModel(model));
                }
            }

            ControllerEvents.CollectionChanged += OnControllerEventsCollectionChanged;

            UpdateCurrentTrackControllerEvents();
            InvalidateVisual();
        }

        private void ApplyCurveDrawingResult(List<CurvePoint> curvePoints)
        {
            if (curvePoints == null || curvePoints.Count == 0)
            {
                return;
            }

            switch (CurrentEventType)
            {
                case EventType.Velocity:
                    ApplyVelocityCurve(curvePoints);
                    break;
                case EventType.ControlChange:
                    ApplyControlChangeCurve(curvePoints, CurrentCCNumber);
                    break;
                case EventType.PitchBend:
                    ApplyPitchBendCurve(curvePoints);
                    break;
                case EventType.Tempo:
                    ApplyTempoCurve(curvePoints);
                    break;
            }
        }

        private void ApplyVelocityCurve(List<CurvePoint> curvePoints)
        {
            if (IsCurrentTrackConductor || CurrentTrackNotes.Count == 0)
            {
                return;
            }

            var scale = TimeToPixelScale;
            if (scale <= 0)
            {
                return;
            }

            foreach (var point in curvePoints)
            {
                var quarter = point.Time / scale;
                var snapped = Toolbar.SnapToGridTime(quarter);
                var target = CurrentTrackNotes.FirstOrDefault(n =>
                    n.StartPosition.ToDouble() <= snapped &&
                    (n.StartPosition.ToDouble() + n.Duration.ToDouble()) > snapped);

                if (target != null)
                {
                    target.Velocity = Math.Clamp(point.Value, 0, 127);
                }
            }
        }

        private void ApplyControlChangeCurve(List<CurvePoint> curvePoints, int ccNumber)
        {
            if (CurrentTrackIndex < 0)
            {
                return;
            }

            var scale = TimeToPixelScale;
            if (scale <= 0)
            {
                return;
            }

            var aggregated = new SortedDictionary<double, int>();
            foreach (var point in curvePoints)
            {
                var quarter = point.Time / scale;
                var snapped = Toolbar.SnapToGridTime(quarter);
                aggregated[snapped] = Math.Clamp(point.Value, 0, 127);
            }

            if (aggregated.Count == 0)
            {
                return;
            }

            var minQuarter = aggregated.Keys.First();
            var maxQuarter = aggregated.Keys.Last();
            const double Epsilon = 1e-6;

            var toRemove = ControllerEvents
                .Where(evt => evt.TrackIndex == CurrentTrackIndex && evt.ControllerNumber == ccNumber)
                .Where(evt =>
                {
                    var time = evt.Time.ToDouble();
                    return time >= minQuarter - Epsilon && time <= maxQuarter + Epsilon;
                })
                .ToList();

            foreach (var evt in toRemove)
            {
                ControllerEvents.Remove(evt);
            }

            foreach (var kv in aggregated)
            {
                var model = new ControllerEventViewModel
                {
                    TrackIndex = CurrentTrackIndex,
                    ControllerNumber = ccNumber,
                    Time = MusicalFraction.FromDouble(kv.Key),
                    Value = kv.Value
                };

                ControllerEvents.Add(model);

                // 同步最近的音符默认CC值，便于实时预览。
                var target = CurrentTrackNotes.FirstOrDefault(n =>
                    n.StartPosition.ToDouble() <= kv.Key &&
                    (n.StartPosition.ToDouble() + n.Duration.ToDouble()) > kv.Key);

                if (target != null)
                {
                    target.GetModel().ControlChangeValue = kv.Value;
                }
            }

            UpdateCurrentTrackControllerEvents();
        }

        private void ApplyPitchBendCurve(List<CurvePoint> curvePoints)
        {
            if (IsCurrentTrackConductor || CurrentTrackNotes.Count == 0)
            {
                return;
            }

            var scale = TimeToPixelScale;
            if (scale <= 0)
            {
                return;
            }

            foreach (var point in curvePoints)
            {
                var quarter = point.Time / scale;
                var snapped = Toolbar.SnapToGridTime(quarter);
                var target = CurrentTrackNotes.FirstOrDefault(n =>
                    n.StartPosition.ToDouble() <= snapped &&
                    (n.StartPosition.ToDouble() + n.Duration.ToDouble()) > snapped);

                if (target != null)
                {
                    target.GetModel().PitchBendValue = point.Value;
                }
            }
        }

        private void ApplyTempoCurve(List<CurvePoint> curvePoints)
        {
            if (curvePoints.Count == 0)
            {
                return;
            }

            var lastValue = Math.Clamp(curvePoints.Last().Value, 20, 300);
            CurrentTempo = lastValue;
        }
    }
}
