using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
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
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private ControllerEventViewModel? _selectedControllerEvent;

        /// <summary>
        /// CC线工具的起点
        /// </summary>
        private ControllerEventViewModel? _ccLineStartPoint;

        /// <summary>
        /// CC线工具是否已设置起点
        /// </summary>
        private bool _ccLineStartSet = false;

        /// <summary>
        /// 用于绘制CCLine预览线的起点
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private Avalonia.Point _ccLinePreviewStartPoint = new Avalonia.Point(double.NaN, double.NaN);

        /// <summary>
        /// 用于绘制CCLine预览线的终点
        /// </summary>
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private Avalonia.Point _ccLinePreviewEndPoint = new Avalonia.Point(double.NaN, double.NaN);

        /// <summary>
        /// 将在给定的屏幕坐标处添加或选择一个控制器事件点（用于 CC 点工具）
        /// </summary>
        public void AddOrSelectControllerPoint(Avalonia.Point worldPosition, double canvasHeight)
        {
            if (CurrentTrackIndex < 0) return;
            if (CurrentEventType != Enums.EventType.ControlChange) return;

            var scale = TimeToPixelScale;
            if (scale <= 0) return;

            // 计算以四分音符为单位的时间并吸附到网格
            var quarter = worldPosition.X / scale;
            var snapped = Toolbar.SnapToGridTime(quarter);

            // 计算数值
            var value = _eventCurveCalculationService.YToValue(worldPosition.Y, canvasHeight, CurrentEventType, CurrentCCNumber);
            value = _eventCurveCalculationService.ClampValue(value, CurrentEventType, CurrentCCNumber);

            // 查找是否已有接近时间点的事件（在同一轨道和相同CC号）
            const double Epsilon = 1e-6;
            var existing = ControllerEvents.FirstOrDefault(evt =>
                evt.TrackIndex == CurrentTrackIndex &&
                evt.ControllerNumber == CurrentCCNumber &&
                Math.Abs(evt.Time.ToDouble() - snapped) <= Epsilon);

            if (existing != null)
            {
                // 选择已存在的事件
                SelectedControllerEvent = existing;
            }
            else
            {
                var model = new ControllerEventViewModel
                {
                    TrackIndex = CurrentTrackIndex,
                    ControllerNumber = CurrentCCNumber,
                    Time = MusicalFraction.FromDouble(snapped),
                    Value = value
                };

                ControllerEvents.Add(model);
                SelectedControllerEvent = model;
            }

            UpdateCurrentTrackControllerEvents();
            InvalidateVisual();
        }

        /// <summary>
        /// 对当前选中的控制器事件进行微调（上下移动数值）
        /// </summary>
        public void NudgeSelectedControllerEvent(int delta)
        {
            if (SelectedControllerEvent == null) return;

            var newValue = Math.Clamp(SelectedControllerEvent.Value + delta, 0, 127);
            SelectedControllerEvent.Value = newValue;

            // 同步最近音符的默认CC值以便实时预览
            var kv = SelectedControllerEvent;
            var target = CurrentTrackNotes.FirstOrDefault(n =>
                n.StartPosition.ToDouble() <= kv.Time.ToDouble() &&
                (n.StartPosition.ToDouble() + n.Duration.ToDouble()) > kv.Time.ToDouble());

            if (target != null)
            {
                target.GetModel().ControlChangeValue = kv.Value;
            }

            InvalidateVisual();
        }
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

        /// <summary>
        /// 处理 CC 线工具的点击事件（两次点击定义直线的起点和终点）
        /// </summary>
        public void HandleCCLineClick(Avalonia.Point worldPosition, double canvasHeight)
        {
            if (CurrentTrackIndex < 0) return;
            if (CurrentEventType != Enums.EventType.ControlChange) return;

            var scale = TimeToPixelScale;
            if (scale <= 0) return;

            // 计算以四分音符为单位的时间并吸附到网格
            var quarter = worldPosition.X / scale;
            var snapped = Toolbar.SnapToGridTime(quarter);

            // 计算数值
            var value = _eventCurveCalculationService.YToValue(worldPosition.Y, canvasHeight, CurrentEventType, CurrentCCNumber);
            value = _eventCurveCalculationService.ClampValue(value, CurrentEventType, CurrentCCNumber);

            if (!_ccLineStartSet)
            {
                // 第一次点击：设置起点
                _ccLineStartPoint = new ControllerEventViewModel
                {
                    TrackIndex = CurrentTrackIndex,
                    ControllerNumber = CurrentCCNumber,
                    Time = MusicalFraction.FromDouble(snapped),
                    Value = value
                };
                _ccLineStartSet = true;
                SelectedControllerEvent = _ccLineStartPoint;
                
                // 设置预览线的起点
                var scrollOffset = CurrentScrollOffset;
                var pixelX = snapped * scale - scrollOffset;
                var heightRatio = Math.Clamp(value / 127.0, 0, 1);
                var pixelY = canvasHeight - (heightRatio * canvasHeight);
                CcLinePreviewStartPoint = new Avalonia.Point(pixelX, pixelY);
                
                // 清除预览线的终点
                CcLinePreviewEndPoint = new Avalonia.Point(double.NaN, double.NaN);
            }
            else
            {
                // 第二次点击：生成直线并添加到事件中
                if (_ccLineStartPoint != null)
                {
                    // 设置预览线的终点
                    var scrollOffset = CurrentScrollOffset;
                    var pixelX = snapped * scale - scrollOffset;
                    var heightRatio = Math.Clamp(value / 127.0, 0, 1);
                    var pixelY = canvasHeight - (heightRatio * canvasHeight);
                    CcLinePreviewEndPoint = new Avalonia.Point(pixelX, pixelY);
                    
                    // 延迟生成直线，让UI有机会显示预览线
                    var startPointCopy = _ccLineStartPoint;
                    var snappedCopy = snapped;
                    var valueCopy = value;
                    
                    _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await Task.Delay(100); // 显示预览线100ms
                        GenerateCCLine(startPointCopy, new ControllerEventViewModel
                        {
                            TrackIndex = CurrentTrackIndex,
                            ControllerNumber = CurrentCCNumber,
                            Time = MusicalFraction.FromDouble(snappedCopy),
                            Value = valueCopy
                        });
                        
                        // 重置预览线
                        CcLinePreviewStartPoint = new Avalonia.Point(double.NaN, double.NaN);
                        CcLinePreviewEndPoint = new Avalonia.Point(double.NaN, double.NaN);
                    });
                }
                // 重置状态
                _ccLineStartSet = false;
                _ccLineStartPoint = null;
            }

            UpdateCurrentTrackControllerEvents();
            InvalidateVisual();
        }

        /// <summary>
        /// 生成从起点到终点的线性插值的 CC 事件
        /// </summary>
        private void GenerateCCLine(ControllerEventViewModel startPoint, ControllerEventViewModel endPoint)
        {
            if (startPoint == null || endPoint == null) return;

            var startTime = startPoint.Time.ToDouble();
            var endTime = endPoint.Time.ToDouble();

            // 确保起点在终点之前
            if (startTime > endTime)
            {
                var temp = startPoint;
                startPoint = endPoint;
                endPoint = temp;
                startTime = startPoint.Time.ToDouble();
                endTime = endPoint.Time.ToDouble();
            }

            if (Math.Abs(startTime - endTime) < 0.001)
            {
                // 时间太接近，只添加一个点
                if (!ControllerEvents.Any(e => e.TrackIndex == startPoint.TrackIndex && 
                                                 e.ControllerNumber == startPoint.ControllerNumber &&
                                                 Math.Abs(e.Time.ToDouble() - startTime) < 0.001))
                {
                    ControllerEvents.Add(startPoint);
                }
                return;
            }

            // 移除在这个范围内已存在的事件
            var toRemove = ControllerEvents
                .Where(e => e.TrackIndex == startPoint.TrackIndex &&
                            e.ControllerNumber == startPoint.ControllerNumber &&
                            e.Time.ToDouble() >= startTime && e.Time.ToDouble() <= endTime)
                .ToList();

            foreach (var evt in toRemove)
            {
                ControllerEvents.Remove(evt);
            }

            // 生成线性插值的点
            var duration = endTime - startTime;
            var valueDiff = endPoint.Value - startPoint.Value;
            
            // 确定采样间隔（基于工具栏的网格量化）
            var gridTime = Toolbar.SnapToGridTime(startTime + 0.25) - Toolbar.SnapToGridTime(startTime);
            if (gridTime <= 0) gridTime = 0.25; // 默认四分音符的四分之一

            var currentTime = startTime;
            while (currentTime <= endTime + 0.001)
            {
                var ratio = (currentTime - startTime) / duration;
                ratio = Math.Clamp(ratio, 0, 1);

                var interpolatedValue = (int)Math.Round(startPoint.Value + valueDiff * ratio);
                interpolatedValue = Math.Clamp(interpolatedValue, 0, 127);

                var newEvent = new ControllerEventViewModel
                {
                    TrackIndex = startPoint.TrackIndex,
                    ControllerNumber = startPoint.ControllerNumber,
                    Time = MusicalFraction.FromDouble(currentTime),
                    Value = interpolatedValue
                };

                ControllerEvents.Add(newEvent);

                // 同步最近的音符默认CC值
                var target = CurrentTrackNotes.FirstOrDefault(n =>
                    n.StartPosition.ToDouble() <= currentTime &&
                    (n.StartPosition.ToDouble() + n.Duration.ToDouble()) > currentTime);

                if (target != null)
                {
                    target.GetModel().ControlChangeValue = interpolatedValue;
                }

                currentTime += gridTime;
            }
        }
    }
}
