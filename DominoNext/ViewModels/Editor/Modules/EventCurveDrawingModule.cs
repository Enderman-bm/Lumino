using Avalonia;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lumino.ViewModels.Editor.Modules
{
    /// <summary>
    /// �¼����߻���ģ�� - �򻯰汾
    /// ������Ǧ�ʹ��ߵ����߻��ƹ���
    /// </summary>
    public class EventCurveDrawingModule
    {
        #region ˽���ֶ�
        private readonly IEventCurveCalculationService _calculationService;
        private PianoRollViewModel? _pianoRollViewModel;

        private bool _isDrawing = false;
        private List<CurvePoint> _currentCurvePoints = new();
        private EventType _currentEventType = EventType.Velocity;
        private int _currentCCNumber = 1;
        private double _canvasHeight = 100;
        #endregion

        #region ����
        /// <summary>
        /// �Ƿ����ڻ�������
        /// </summary>
        public bool IsDrawing => _isDrawing;

        /// <summary>
        /// ��ǰ���Ƶ����ߵ㼯��
        /// </summary>
        public List<CurvePoint> CurrentCurvePoints => _currentCurvePoints;

        /// <summary>
        /// ��ǰ���Ƶ��¼�����
        /// </summary>
        public EventType CurrentEventType => _currentEventType;

        /// <summary>
        /// ��ǰCC��������
        /// </summary>
        public int CurrentCCNumber => _currentCCNumber;

        /// <summary>
        /// �����߶ȣ�����������㣩
        /// </summary>
        public double CanvasHeight => _canvasHeight;
        #endregion

        #region �¼�
        /// <summary>
        /// ���߻��Ƹ����¼�
        /// </summary>
        public event Action? OnCurveUpdated;

        /// <summary>
        /// ���߻�������¼�
        /// </summary>
        public event Action<List<CurvePoint>>? OnCurveCompleted;

        /// <summary>
        /// ���߻���ȡ���¼�
        /// </summary>
        public event Action? OnCurveCancelled;
        #endregion

        #region ���캯��
        public EventCurveDrawingModule(IEventCurveCalculationService calculationService)
        {
            _calculationService = calculationService ?? throw new ArgumentNullException(nameof(calculationService));
        }
        #endregion

        #region ��������
        /// <summary>
        /// ����PianoRollViewModel����
        /// </summary>
        public void SetPianoRollViewModel(PianoRollViewModel pianoRollViewModel)
        {
            _pianoRollViewModel = pianoRollViewModel;
        }

        /// <summary>
        /// ��ʼ��������
        /// </summary>
        public void StartDrawing(Point startPoint, EventType eventType, int ccNumber = 1, double canvasHeight = 100)
        {
            if (_isDrawing) return;

            _currentEventType = eventType;
            _currentCCNumber = ccNumber;
            _canvasHeight = canvasHeight;
            
            // ����ʼ��ת��Ϊ���ߵ�
            var value = _calculationService.YToValue(startPoint.Y, canvasHeight, eventType, ccNumber);
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
        /// �������߻���
        /// </summary>
        public void UpdateDrawing(Point currentPoint)
        {
            if (!_isDrawing) return;

            // ����ǰ��ת��Ϊ���ߵ�
            var value = _calculationService.YToValue(currentPoint.Y, _canvasHeight, _currentEventType, _currentCCNumber);
            var curvePoint = new CurvePoint
            {
                Time = currentPoint.X,
                Value = value,
                ScreenPosition = currentPoint
            };

            // ����Ƿ���Ҫ�����м��
            var lastPoint = _currentCurvePoints.LastOrDefault();
            if (lastPoint != null)
            {
                var distance = Math.Abs(currentPoint.X - lastPoint.ScreenPosition.X);
                if (distance > 5.0) // ������볬��5���أ������м��
                {
                    InsertIntermediatePoints(lastPoint, curvePoint);
                }
            }

            _currentCurvePoints.Add(curvePoint);
            OnCurveUpdated?.Invoke();
        }

        /// <summary>
        /// ������߻���
        /// </summary>
        public void FinishDrawing()
        {
            if (!_isDrawing) return;

            _isDrawing = false;
            
            // �Ż����ߵ�
            var optimizedPoints = OptimizeCurvePoints(_currentCurvePoints);
            
            OnCurveCompleted?.Invoke(optimizedPoints);
            
            // ��յ�ǰ���ߵ�
            _currentCurvePoints.Clear();
        }

        /// <summary>
        /// ȡ�����߻���
        /// </summary>
        public void CancelDrawing()
        {
            if (!_isDrawing) return;

            _isDrawing = false;
            _currentCurvePoints.Clear();
            
            OnCurveCancelled?.Invoke();
        }

        /// <summary>
        /// ��ȡ��ǰ��ֵ��Χ����
        /// </summary>
        public string GetCurrentValueRangeDescription()
        {
            return _calculationService.GetValueRangeDescription(_currentEventType, _currentCCNumber);
        }

        /// <summary>
        /// ��ȡָ��λ�õ���ֵ
        /// </summary>
        public int GetValueAtPosition(Point position)
        {
            return _calculationService.YToValue(position.Y, _canvasHeight, _currentEventType, _currentCCNumber);
        }

        /// <summary>
        /// ��ȡָ����ֵ��Y����
        /// </summary>
        public double GetYPositionForValue(int value)
        {
            return _calculationService.ValueToY(value, _canvasHeight, _currentEventType, _currentCCNumber);
        }
        #endregion

        #region ˽�з���
        /// <summary>
        /// ��������֮������м��
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
        /// �Ż����ߵ�
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
    /// ���ߵ����ݽṹ
    /// </summary>
    public class CurvePoint
    {
        /// <summary>
        /// ʱ��λ�ã�X���꣩
        /// </summary>
        public double Time { get; set; }

        /// <summary>
        /// �¼���ֵ
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// ��Ļ����λ��
        /// </summary>
        public Point ScreenPosition { get; set; }

        /// <summary>
        /// �Ƿ�Ϊ�ؼ���
        /// </summary>
        public bool IsKeyPoint { get; set; } = false;
    }
}