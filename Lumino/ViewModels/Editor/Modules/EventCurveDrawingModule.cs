using Avalonia;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lumino.ViewModels.Editor.Modules
{
    /// <summary>
    /// 事件曲线绘制模块 - 简化版本
    /// 负责处理铅笔工具的曲线绘制功能
    /// </summary>
    public class EventCurveDrawingModule
    {
        #region 私有字段
        private readonly IEventCurveCalculationService _calculationService;
        private PianoRollViewModel? _pianoRollViewModel;

        private bool _isDrawing = false;
        private List<CurvePoint> _currentCurvePoints = new();
        private EventType _currentEventType = EventType.Velocity;
        private int _currentCCNumber = 1;
        private double _canvasHeight = 100;
        #endregion

        #region 属性
        /// <summary>
        /// 是否正在绘制曲线
        /// </summary>
        public bool IsDrawing => _isDrawing;

        /// <summary>
        /// 当前绘制的曲线点集合
        /// </summary>
        public List<CurvePoint> CurrentCurvePoints => _currentCurvePoints;

        /// <summary>
        /// 当前绘制的事件类型
        /// </summary>
        public EventType CurrentEventType => _currentEventType;

        /// <summary>
        /// 当前CC控制器号
        /// </summary>
        public int CurrentCCNumber => _currentCCNumber;

        /// <summary>
        /// 画布高度（用于坐标计算）
        /// </summary>
        public double CanvasHeight => _canvasHeight;
        #endregion

        #region 事件
        /// <summary>
        /// 曲线绘制更新事件
        /// </summary>
        public event Action? OnCurveUpdated;

        /// <summary>
        /// 曲线绘制完成事件
        /// </summary>
        public event Action<List<CurvePoint>>? OnCurveCompleted;

        /// <summary>
        /// 曲线绘制取消事件
        /// </summary>
        public event Action? OnCurveCancelled;
        #endregion

        #region 构造函数
        public EventCurveDrawingModule(IEventCurveCalculationService calculationService)
        {
            _calculationService = calculationService ?? throw new ArgumentNullException(nameof(calculationService));
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 设置PianoRollViewModel引用
        /// </summary>
        public void SetPianoRollViewModel(PianoRollViewModel pianoRollViewModel)
        {
            _pianoRollViewModel = pianoRollViewModel;
        }

        /// <summary>
        /// 开始绘制曲线
        /// </summary>
        public void StartDrawing(Point startPoint, EventType eventType, int ccNumber = 1, double canvasHeight = 100)
        {
            if (_isDrawing) return;

            // 验证CC号范围
            if (eventType == EventType.ControlChange && (ccNumber < 0 || ccNumber > 127))
            {
                throw new ArgumentException($"CC号必须在0-127范围内，当前值: {ccNumber}", nameof(ccNumber));
            }

            // 验证画布高度
            if (canvasHeight <= 0)
            {
                throw new ArgumentException($"画布高度必须大于0，当前值: {canvasHeight}", nameof(canvasHeight));
            }

            _currentEventType = eventType;
            _currentCCNumber = ccNumber;
            _canvasHeight = canvasHeight;
            
            // 将起始点转换为曲线点
            var value = _calculationService.YToValue(startPoint.Y, canvasHeight, eventType, ccNumber);
            
            // 限制数值在有效范围内
            value = _calculationService.ClampValue(value, eventType, ccNumber);
            
            var curvePoint = new CurvePoint
            {
                Time = startPoint.X,
                Value = value,
                ScreenPosition = startPoint
            };

            _currentCurvePoints = new List<CurvePoint> { curvePoint };
            _isDrawing = true;

            OnCurveUpdated?.Invoke();
        }

        /// <summary>
        /// 更新曲线绘制
        /// </summary>
        public void UpdateDrawing(Point currentPoint)
        {
            if (!_isDrawing) return;

            // 将当前点转换为曲线点
            var value = _calculationService.YToValue(currentPoint.Y, _canvasHeight, _currentEventType, _currentCCNumber);
            
            // 限制数值在有效范围内
            value = _calculationService.ClampValue(value, _currentEventType, _currentCCNumber);
            
            var curvePoint = new CurvePoint
            {
                Time = currentPoint.X,
                Value = value,
                ScreenPosition = currentPoint
            };

            // 检查是否需要插入中间点
            var lastPoint = _currentCurvePoints.LastOrDefault();
            if (lastPoint != null)
            {
                var distance = Math.Abs(currentPoint.X - lastPoint.ScreenPosition.X);
                if (distance > 5.0) // 如果距离超过5像素，插入中间点
                {
                    InsertIntermediatePoints(lastPoint, curvePoint);
                }
            }

            _currentCurvePoints.Add(curvePoint);
            OnCurveUpdated?.Invoke();
        }

        /// <summary>
        /// 完成曲线绘制
        /// </summary>
        public void FinishDrawing()
        {
            if (!_isDrawing) return;

            _isDrawing = false;
            
            // 优化曲线点
            var optimizedPoints = OptimizeCurvePoints(_currentCurvePoints);
            
            OnCurveCompleted?.Invoke(optimizedPoints);
            
            // 清空当前曲线点
            _currentCurvePoints.Clear();
        }

        /// <summary>
        /// 取消曲线绘制
        /// </summary>
        public void CancelDrawing()
        {
            if (!_isDrawing) return;

            _isDrawing = false;
            _currentCurvePoints.Clear();
            
            OnCurveCancelled?.Invoke();
        }

        /// <summary>
        /// 获取当前数值范围描述
        /// </summary>
        public string GetCurrentValueRangeDescription()
        {
            return _calculationService.GetValueRangeDescription(_currentEventType, _currentCCNumber);
        }

        /// <summary>
        /// 获取指定位置的数值
        /// </summary>
        public int GetValueAtPosition(Point position)
        {
            return _calculationService.YToValue(position.Y, _canvasHeight, _currentEventType, _currentCCNumber);
        }

        /// <summary>
        /// 获取指定数值的Y坐标
        /// </summary>
        public double GetYPositionForValue(int value)
        {
            return _calculationService.ValueToY(value, _canvasHeight, _currentEventType, _currentCCNumber);
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 在两个点之间插入中间点
        /// </summary>
        private void InsertIntermediatePoints(CurvePoint startPoint, CurvePoint endPoint)
        {
            var timeDistance = Math.Abs(endPoint.Time - startPoint.Time);
            var steps = Math.Max(1, (int)(timeDistance / 3.0));

            for (int i = 1; i < steps; i++)
            {
                var ratio = (double)i / steps;
                var intermediateTime = startPoint.Time + (endPoint.Time - startPoint.Time) * ratio;
                var intermediateY = startPoint.ScreenPosition.Y + (endPoint.ScreenPosition.Y - startPoint.ScreenPosition.Y) * ratio;
                
                var intermediateValue = _calculationService.YToValue(intermediateY, _canvasHeight, _currentEventType, _currentCCNumber);
                
                // 限制中间点的数值范围
                intermediateValue = _calculationService.ClampValue(intermediateValue, _currentEventType, _currentCCNumber);
                
                var intermediatePoint = new CurvePoint
                {
                    Time = intermediateTime,
                    Value = intermediateValue,
                    ScreenPosition = new Point(intermediateTime, intermediateY)
                };

                _currentCurvePoints.Add(intermediatePoint);
            }
        }

        /// <summary>
        /// 优化曲线点
        /// </summary>
        private List<CurvePoint> OptimizeCurvePoints(List<CurvePoint> points)
        {
            if (points.Count <= 2) return points;

            var optimized = new List<CurvePoint> { points[0] };

            for (int i = 1; i < points.Count - 1; i++)
            {
                var prev = points[i - 1];
                var current = points[i];
                var next = points[i + 1];

                var slope1 = (current.Value - prev.Value) / Math.Max(1, current.Time - prev.Time);
                var slope2 = (next.Value - current.Value) / Math.Max(1, next.Time - current.Time);

                if (Math.Abs(slope1 - slope2) > 0.1)
                {
                    optimized.Add(current);
                }
            }

            optimized.Add(points[^1]);
            return optimized;
        }
        #endregion
    }

    /// <summary>
    /// 曲线点数据结构
    /// </summary>
    public class CurvePoint
    {
        /// <summary>
        /// 时间位置（X坐标）
        /// </summary>
        public double Time { get; set; }

        /// <summary>
        /// 事件数值
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// 屏幕坐标位置
        /// </summary>
        public Point ScreenPosition { get; set; }

        /// <summary>
        /// 是否为关键点
        /// </summary>
        public bool IsKeyPoint { get; set; } = false;
    }
}