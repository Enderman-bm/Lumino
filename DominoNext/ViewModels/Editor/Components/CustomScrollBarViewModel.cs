using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Lumino.ViewModels.Editor.Components
{
    /// <summary>
    /// �Զ����������ViewModel��֧�ֹ����ͱ�Ե��ק���Ź���
    /// </summary>
    public partial class CustomScrollBarViewModel : ObservableObject
    {
        #region ��������
        /// <summary>
        /// ����������
        /// </summary>
        public ScrollBarOrientation Orientation { get; }

        /// <summary>
        /// ��Сֵ
        /// </summary>
        [ObservableProperty]
        private double _minimum = 0;

        /// <summary>
        /// ���ֵ
        /// </summary>
        [ObservableProperty]
        private double _maximum = 100;

        /// <summary>
        /// ��ǰֵ
        /// </summary>
        [ObservableProperty]
        private double _value = 0;

        /// <summary>
        /// �ӿڴ�С���ɼ������С��
        /// </summary>
        [ObservableProperty]
        private double _viewportSize = 10;

        /// <summary>
        /// ����������ܳ��ȣ����أ�
        /// </summary>
        [ObservableProperty]
        private double _trackLength = 200;

        /// <summary>
        /// ������������С���ȣ����أ�
        /// </summary>
        [ObservableProperty]
        private double _thumbMinLength = 20;
        #endregion

        #region ��������
        /// <summary>
        /// ������Χ
        /// </summary>
        public double ScrollRange => Math.Max(0, Maximum - Minimum);

        /// <summary>
        /// �ɹ�����Χ
        /// </summary>
        public double ScrollableRange => Math.Max(0, ScrollRange - ViewportSize);

        /// <summary>
        /// ���鳤�ȣ������ӿڴ�С�������㣩
        /// </summary>
        public double ThumbLength
        {
            get
            {
                if (ScrollRange <= 0) return TrackLength;
                
                var ratio = ViewportSize / ScrollRange;
                var length = TrackLength * ratio;
                return Math.Max(ThumbMinLength, Math.Min(length, TrackLength));
            }
        }

        /// <summary>
        /// ����λ�ã����ڵ�ǰֵ���㣩
        /// </summary>
        public double ThumbPosition
        {
            get
            {
                if (ScrollableRange <= 0) return 0;
                
                var availableSpace = TrackLength - ThumbLength;
                var ratio = (Value - Minimum) / ScrollableRange;
                return availableSpace * ratio;
            }
        }

        /// <summary>
        /// ����������0-1��
        /// </summary>
        public double ScrollRatio
        {
            get
            {
                if (ScrollableRange <= 0) return 0;
                return (Value - Minimum) / ScrollableRange;
            }
        }
        #endregion

        #region ��ק״̬
        /// <summary>
        /// �Ƿ�������ק����
        /// </summary>
        [ObservableProperty]
        private bool _isDraggingThumb = false;

        /// <summary>
        /// �Ƿ�������ק��/�ϱ�Ե��������
        /// </summary>
        [ObservableProperty]
        private bool _isDraggingStartEdge = false;

        /// <summary>
        /// �Ƿ�������ק��/�±�Ե��������
        /// </summary>
        [ObservableProperty]
        private bool _isDraggingEndEdge = false;

        /// <summary>
        /// ��ק��ʼʱ�����λ��
        /// </summary>
        private double _dragStartPosition;

        /// <summary>
        /// ��ק��ʼʱ��ֵ
        /// </summary>
        private double _dragStartValue;

        /// <summary>
        /// ��ק��ʼʱ���ӿڴ�С
        /// </summary>
        private double _dragStartViewportSize;

        /// <summary>
        /// ��Ե��������С�����أ�
        /// </summary>
        private const double EdgeDetectionSize = 8;
        #endregion

        #region �¼�
        /// <summary>
        /// ֵ�仯�¼�
        /// </summary>
        public event Action<double>? ValueChanged;

        /// <summary>
        /// �ӿڴ�С�仯�¼������ţ�
        /// </summary>
        public event Action<double>? ViewportSizeChanged;

        /// <summary>
        /// ���ָ�����ͱ仯�¼�
        /// </summary>
        public event Action<ScrollBarCursorType>? CursorChanged;
        #endregion

        #region ���캯��
        public CustomScrollBarViewModel(ScrollBarOrientation orientation)
        {
            Orientation = orientation;
        }
        #endregion

        #region ����¼�����
        /// <summary>
        /// ��������ƶ�������Ƿ��ڱ�Ե���򲢸������ָ��
        /// </summary>
        public void HandleMouseMove(double position)
        {
            if (IsDraggingThumb || IsDraggingStartEdge || IsDraggingEndEdge)
                return;

            var thumbStart = ThumbPosition;
            var thumbEnd = thumbStart + ThumbLength;

            ScrollBarCursorType cursorType;

            if (position >= thumbStart - EdgeDetectionSize && position <= thumbStart + EdgeDetectionSize)
            {
                // ����/�ϱ�Ե
                cursorType = Orientation == ScrollBarOrientation.Horizontal 
                    ? ScrollBarCursorType.ResizeHorizontal 
                    : ScrollBarCursorType.ResizeVertical;
            }
            else if (position >= thumbEnd - EdgeDetectionSize && position <= thumbEnd + EdgeDetectionSize)
            {
                // ����/�±�Ե
                cursorType = Orientation == ScrollBarOrientation.Horizontal 
                    ? ScrollBarCursorType.ResizeHorizontal 
                    : ScrollBarCursorType.ResizeVertical;
            }
            else if (position >= thumbStart && position <= thumbEnd)
            {
                // �ڻ�������
                cursorType = ScrollBarCursorType.Hand;
            }
            else
            {
                // �ڹ����
                cursorType = ScrollBarCursorType.Default;
            }

            CursorChanged?.Invoke(cursorType);
        }

        /// <summary>
        /// ��ʼ��ק����
        /// </summary>
        public void StartDrag(double position)
        {
            var thumbStart = ThumbPosition;
            var thumbEnd = thumbStart + ThumbLength;

            _dragStartPosition = position;
            _dragStartValue = Value;
            _dragStartViewportSize = ViewportSize;

            if (position >= thumbStart - EdgeDetectionSize && position <= thumbStart + EdgeDetectionSize)
            {
                // ��ק��/�ϱ�Ե
                IsDraggingStartEdge = true;
            }
            else if (position >= thumbEnd - EdgeDetectionSize && position <= thumbEnd + EdgeDetectionSize)
            {
                // ��ק��/�±�Ե
                IsDraggingEndEdge = true;
            }
            else if (position >= thumbStart && position <= thumbEnd)
            {
                // ��ק����
                IsDraggingThumb = true;
            }
            else
            {
                // ����������ת����λ��
                JumpToPosition(position);
            }
        }

        /// <summary>
        /// ������ק
        /// </summary>
        public void UpdateDrag(double currentPosition)
        {
            var delta = currentPosition - _dragStartPosition;

            if (IsDraggingThumb)
            {
                UpdateThumbDrag(delta);
            }
            else if (IsDraggingStartEdge)
            {
                UpdateStartEdgeDrag(delta);
            }
            else if (IsDraggingEndEdge)
            {
                UpdateEndEdgeDrag(delta);
            }
        }

        /// <summary>
        /// ������ק
        /// </summary>
        public void EndDrag()
        {
            IsDraggingThumb = false;
            IsDraggingStartEdge = false;
            IsDraggingEndEdge = false;
            CursorChanged?.Invoke(ScrollBarCursorType.Default);
        }
        #endregion

        #region ��ק�߼�
        /// <summary>
        /// ���»�����ק
        /// </summary>
        private void UpdateThumbDrag(double delta)
        {
            var availableSpace = TrackLength - ThumbLength;
            if (availableSpace <= 0) return;

            var deltaRatio = delta / availableSpace;
            var newValue = _dragStartValue + (deltaRatio * ScrollableRange);
            
            SetValue(newValue);
        }

        /// <summary>
        /// ���¿�ʼ/�ϱ�Ե��ק�����ţ�
        /// </summary>
        private void UpdateStartEdgeDrag(double delta)
        {
            // ����/������ק��С�ӿڣ��Ŵ󣩣�����/������ק�Ŵ��ӿڣ���С��
            var deltaRatio = -delta / TrackLength; // ��ת����
            var viewportSizeDelta = deltaRatio * ScrollRange * 0.5; // �����ٶȱ���
            
            var newViewportSize = Math.Max(ScrollRange * 0.01, // ��С1%
                Math.Min(ScrollRange, _dragStartViewportSize + viewportSizeDelta)); // ���100%
            
            SetViewportSize(newViewportSize);
        }

        /// <summary>
        /// ���½���/�±�Ե��ק�����ţ�
        /// </summary>
        private void UpdateEndEdgeDrag(double delta)
        {
            // ����/����ק�Ŵ��ӿڣ���С��������/����ק��С�ӿڣ��Ŵ�
            var deltaRatio = delta / TrackLength;
            var viewportSizeDelta = deltaRatio * ScrollRange * 0.5; // �����ٶȱ���
            
            var newViewportSize = Math.Max(ScrollRange * 0.01, // ��С1%
                Math.Min(ScrollRange, _dragStartViewportSize + viewportSizeDelta)); // ���100%
            
            SetViewportSize(newViewportSize);
        }

        /// <summary>
        /// ��ת��ָ��λ��
        /// </summary>
        private void JumpToPosition(double position)
        {
            var availableSpace = TrackLength - ThumbLength;
            if (availableSpace <= 0) return;

            var targetThumbCenter = position;
            var targetThumbStart = targetThumbCenter - ThumbLength / 2;
            var ratio = targetThumbStart / availableSpace;
            var newValue = Minimum + (ratio * ScrollableRange);
            
            SetValue(newValue);
        }
        #endregion

        #region ���ִ���
        /// <summary>
        /// ����������
        /// </summary>
        public void HandleWheel(double delta, bool isCtrlPressed)
        {
            if (isCtrlPressed)
            {
                // Ctrl+���֣�����
                // ���Ϲ��֣���ֵ��Ӧ�÷Ŵ���С�ӿڣ������¹��֣���ֵ��Ӧ����С���Ŵ��ӿڣ�
                var zoomDelta = -delta * ViewportSize * 0.1; // ��ת����
                var newViewportSize = Math.Max(ScrollRange * 0.01,
                    Math.Min(ScrollRange, ViewportSize + zoomDelta));
                SetViewportSize(newViewportSize);
            }
            else
            {
                // ��ͨ���֣�����
                var scrollDelta = delta * ViewportSize * 0.1;
                SetValue(Value + scrollDelta);
            }
        }
        #endregion

        #region ֵ���÷���
        /// <summary>
        /// ����ֵ�������¼�
        /// </summary>
        private void SetValue(double newValue)
        {
            var clampedValue = Math.Max(Minimum, Math.Min(Maximum - ViewportSize, newValue));
            if (Math.Abs(Value - clampedValue) > 1e-10)
            {
                Value = clampedValue;
                System.Diagnostics.Debug.WriteLine($"[CustomScrollBar] ֵ�仯: {Value:F1}");
                ValueChanged?.Invoke(Value);
                OnPropertyChanged(nameof(ThumbPosition));
                OnPropertyChanged(nameof(ScrollRatio));
            }
        }

        /// <summary>
        /// �����ӿڴ�С�������¼�
        /// </summary>
        private void SetViewportSize(double newViewportSize)
        {
            var clampedSize = Math.Max(ScrollRange * 0.01, Math.Min(ScrollRange, newViewportSize));
            if (Math.Abs(ViewportSize - clampedSize) > 1e-10)
            {
                ViewportSize = clampedSize;
                
                // ���ֵ�ǰֵռ�ȣ��������λ�ö�ʧ
                var currentRatio = ScrollRatio;
                var newScrollableRange = Math.Max(0, ScrollRange - ViewportSize);
                var newValue = Minimum + (currentRatio * newScrollableRange);
                Value = Math.Max(Minimum, Math.Min(Maximum - ViewportSize, newValue));
                
                System.Diagnostics.Debug.WriteLine($"[CustomScrollBar] �ӿڴ�С�仯: {ViewportSize:F1}");
                ViewportSizeChanged?.Invoke(ViewportSize);
                OnPropertyChanged(nameof(ThumbLength));
                OnPropertyChanged(nameof(ThumbPosition));
                OnPropertyChanged(nameof(ScrollableRange));
            }
        }
        #endregion

        #region �������÷���
        /// <summary>
        /// ���ù���������
        /// </summary>
        public void SetParameters(double minimum, double maximum, double value, double viewportSize)
        {
            Minimum = minimum;
            Maximum = maximum;
            ViewportSize = Math.Max(0.01, Math.Min(maximum - minimum, viewportSize));
            Value = Math.Max(minimum, Math.Min(maximum - ViewportSize, value));
            
            OnPropertyChanged(nameof(ScrollRange));
            OnPropertyChanged(nameof(ScrollableRange));
            OnPropertyChanged(nameof(ThumbLength));
            OnPropertyChanged(nameof(ThumbPosition));
            OnPropertyChanged(nameof(ScrollRatio));
        }

        /// <summary>
        /// ���ù������
        /// </summary>
        public void SetTrackLength(double length)
        {
            TrackLength = Math.Max(50, length);
            OnPropertyChanged(nameof(ThumbLength));
            OnPropertyChanged(nameof(ThumbPosition));
        }

        /// <summary>
        /// �ⲿ����ֵ���������¼���
        /// </summary>
        public void SetValueDirect(double value)
        {
            var clampedValue = Math.Max(Minimum, Math.Min(Maximum - ViewportSize, value));
            if (Math.Abs(Value - clampedValue) > 1e-10)
            {
                Value = clampedValue;
                OnPropertyChanged(nameof(ThumbPosition));
                OnPropertyChanged(nameof(ScrollRatio));
            }
        }

        /// <summary>
        /// �ⲿ�����ӿڴ�С���������¼���
        /// </summary>
        public void SetViewportSizeDirect(double viewportSize)
        {
            var clampedSize = Math.Max(ScrollRange * 0.01, Math.Min(ScrollRange, viewportSize));
            if (Math.Abs(ViewportSize - clampedSize) > 1e-10)
            {
                ViewportSize = clampedSize;
                OnPropertyChanged(nameof(ThumbLength));
                OnPropertyChanged(nameof(ThumbPosition));
                OnPropertyChanged(nameof(ScrollableRange));
            }
        }
        #endregion
    }

    /// <summary>
    /// ����������ö��
    /// </summary>
    public enum ScrollBarOrientation
    {
        Horizontal,
        Vertical
    }

    /// <summary>
    /// ���������ָ������
    /// </summary>
    public enum ScrollBarCursorType
    {
        Default,
        Hand,
        ResizeHorizontal,
        ResizeVertical
    }
}