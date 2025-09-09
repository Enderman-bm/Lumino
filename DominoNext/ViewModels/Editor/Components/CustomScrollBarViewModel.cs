using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DominoNext.ViewModels.Editor.Components
{
    /// <summary>
    /// 自定义滚动条的ViewModel，支持滚动和边缘拖拽缩放功能
    /// </summary>
    public partial class CustomScrollBarViewModel : ObservableObject
    {
        #region 基本属性
        /// <summary>
        /// 滚动条方向
        /// </summary>
        public ScrollBarOrientation Orientation { get; }

        /// <summary>
        /// 最小值
        /// </summary>
        [ObservableProperty]
        private double _minimum = 0;

        /// <summary>
        /// 最大值
        /// </summary>
        [ObservableProperty]
        private double _maximum = 100;

        /// <summary>
        /// 当前值
        /// </summary>
        [ObservableProperty]
        private double _value = 0;

        /// <summary>
        /// 视口大小（可见区域大小）
        /// </summary>
        [ObservableProperty]
        private double _viewportSize = 10;

        /// <summary>
        /// 滚动条轨道总长度（像素）
        /// </summary>
        [ObservableProperty]
        private double _trackLength = 200;

        /// <summary>
        /// 滚动条滑块最小长度（像素）
        /// </summary>
        [ObservableProperty]
        private double _thumbMinLength = 20;
        #endregion

        #region 计算属性
        /// <summary>
        /// 滚动范围
        /// </summary>
        public double ScrollRange => Math.Max(0, Maximum - Minimum);

        /// <summary>
        /// 可滚动范围
        /// </summary>
        public double ScrollableRange => Math.Max(0, ScrollRange - ViewportSize);

        /// <summary>
        /// 滑块长度（基于视口大小比例计算）
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
        /// 滑块位置（基于当前值计算）
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
        /// 滚动比例（0-1）
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

        #region 拖拽状态
        /// <summary>
        /// 是否正在拖拽滑块
        /// </summary>
        [ObservableProperty]
        private bool _isDraggingThumb = false;

        /// <summary>
        /// 是否正在拖拽左/上边缘进行缩放
        /// </summary>
        [ObservableProperty]
        private bool _isDraggingStartEdge = false;

        /// <summary>
        /// 是否正在拖拽右/下边缘进行缩放
        /// </summary>
        [ObservableProperty]
        private bool _isDraggingEndEdge = false;

        /// <summary>
        /// 拖拽开始时的鼠标位置
        /// </summary>
        private double _dragStartPosition;

        /// <summary>
        /// 拖拽开始时的值
        /// </summary>
        private double _dragStartValue;

        /// <summary>
        /// 拖拽开始时的视口大小
        /// </summary>
        private double _dragStartViewportSize;

        /// <summary>
        /// 边缘检测区域大小（像素）
        /// </summary>
        private const double EdgeDetectionSize = 8;
        #endregion

        #region 事件
        /// <summary>
        /// 值变化事件
        /// </summary>
        public event Action<double>? ValueChanged;

        /// <summary>
        /// 视口大小变化事件（缩放）
        /// </summary>
        public event Action<double>? ViewportSizeChanged;

        /// <summary>
        /// 鼠标指针类型变化事件
        /// </summary>
        public event Action<ScrollBarCursorType>? CursorChanged;
        #endregion

        #region 构造函数
        public CustomScrollBarViewModel(ScrollBarOrientation orientation)
        {
            Orientation = orientation;
        }
        #endregion

        #region 鼠标事件处理
        /// <summary>
        /// 处理鼠标移动，检测是否在边缘区域并更新鼠标指针
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
                // 在左/上边缘
                cursorType = Orientation == ScrollBarOrientation.Horizontal 
                    ? ScrollBarCursorType.ResizeHorizontal 
                    : ScrollBarCursorType.ResizeVertical;
            }
            else if (position >= thumbEnd - EdgeDetectionSize && position <= thumbEnd + EdgeDetectionSize)
            {
                // 在右/下边缘
                cursorType = Orientation == ScrollBarOrientation.Horizontal 
                    ? ScrollBarCursorType.ResizeHorizontal 
                    : ScrollBarCursorType.ResizeVertical;
            }
            else if (position >= thumbStart && position <= thumbEnd)
            {
                // 在滑块中央
                cursorType = ScrollBarCursorType.Hand;
            }
            else
            {
                // 在轨道上
                cursorType = ScrollBarCursorType.Default;
            }

            CursorChanged?.Invoke(cursorType);
        }

        /// <summary>
        /// 开始拖拽操作
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
                // 拖拽左/上边缘
                IsDraggingStartEdge = true;
            }
            else if (position >= thumbEnd - EdgeDetectionSize && position <= thumbEnd + EdgeDetectionSize)
            {
                // 拖拽右/下边缘
                IsDraggingEndEdge = true;
            }
            else if (position >= thumbStart && position <= thumbEnd)
            {
                // 拖拽滑块
                IsDraggingThumb = true;
            }
            else
            {
                // 点击轨道，跳转到该位置
                JumpToPosition(position);
            }
        }

        /// <summary>
        /// 更新拖拽
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
        /// 结束拖拽
        /// </summary>
        public void EndDrag()
        {
            IsDraggingThumb = false;
            IsDraggingStartEdge = false;
            IsDraggingEndEdge = false;
            CursorChanged?.Invoke(ScrollBarCursorType.Default);
        }
        #endregion

        #region 拖拽逻辑
        /// <summary>
        /// 更新滑块拖拽
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
        /// 更新左/上边缘拖拽（缩放）
        /// </summary>
        private void UpdateStartEdgeDrag(double delta)
        {
            // 向右/下拖拽增大视口（缩小），向左/上拖拽减小视口（放大）
            var deltaRatio = delta / TrackLength;
            var viewportSizeDelta = deltaRatio * ScrollRange * 0.5; // 缩放速度调节
            
            var newViewportSize = Math.Max(ScrollRange * 0.01, // 最小1%
                Math.Min(ScrollRange, _dragStartViewportSize + viewportSizeDelta)); // 最大100%
            
            SetViewportSize(newViewportSize);
        }

        /// <summary>
        /// 更新右/下边缘拖拽（缩放）
        /// </summary>
        private void UpdateEndEdgeDrag(double delta)
        {
            // 向右/下拖拽减小视口（放大），向左/上拖拽增大视口（缩小）
            var deltaRatio = delta / TrackLength;
            var viewportSizeDelta = -deltaRatio * ScrollRange * 0.5; // 缩放速度调节
            
            var newViewportSize = Math.Max(ScrollRange * 0.01, // 最小1%
                Math.Min(ScrollRange, _dragStartViewportSize + viewportSizeDelta)); // 最大100%
            
            SetViewportSize(newViewportSize);
        }

        /// <summary>
        /// 跳转到指定位置
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

        #region 滚轮处理
        /// <summary>
        /// 处理鼠标滚轮
        /// </summary>
        public void HandleWheel(double delta, bool isCtrlPressed)
        {
            if (isCtrlPressed)
            {
                // Ctrl+滚轮：缩放
                var zoomDelta = delta * ViewportSize * 0.1;
                var newViewportSize = Math.Max(ScrollRange * 0.01,
                    Math.Min(ScrollRange, ViewportSize - zoomDelta));
                SetViewportSize(newViewportSize);
            }
            else
            {
                // 普通滚轮：滚动
                var scrollDelta = delta * ViewportSize * 0.1;
                SetValue(Value + scrollDelta);
            }
        }
        #endregion

        #region 值设置方法
        /// <summary>
        /// 设置值并触发事件
        /// </summary>
        private void SetValue(double newValue)
        {
            var clampedValue = Math.Max(Minimum, Math.Min(Maximum - ViewportSize, newValue));
            if (Math.Abs(Value - clampedValue) > 1e-10)
            {
                Value = clampedValue;
                ValueChanged?.Invoke(Value);
                OnPropertyChanged(nameof(ThumbPosition));
                OnPropertyChanged(nameof(ScrollRatio));
            }
        }

        /// <summary>
        /// 设置视口大小并触发事件
        /// </summary>
        private void SetViewportSize(double newViewportSize)
        {
            var clampedSize = Math.Max(ScrollRange * 0.01, Math.Min(ScrollRange, newViewportSize));
            if (Math.Abs(ViewportSize - clampedSize) > 1e-10)
            {
                ViewportSize = clampedSize;
                
                // 调整当前值以保持相对位置
                var currentRatio = ScrollRatio;
                var newScrollableRange = Math.Max(0, ScrollRange - ViewportSize);
                var newValue = Minimum + (currentRatio * newScrollableRange);
                Value = Math.Max(Minimum, Math.Min(Maximum - ViewportSize, newValue));
                
                ViewportSizeChanged?.Invoke(ViewportSize);
                OnPropertyChanged(nameof(ThumbLength));
                OnPropertyChanged(nameof(ThumbPosition));
                OnPropertyChanged(nameof(ScrollableRange));
            }
        }
        #endregion

        #region 公共设置方法
        /// <summary>
        /// 设置滚动条参数
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
        /// 设置轨道长度
        /// </summary>
        public void SetTrackLength(double length)
        {
            TrackLength = Math.Max(50, length);
            OnPropertyChanged(nameof(ThumbLength));
            OnPropertyChanged(nameof(ThumbPosition));
        }

        /// <summary>
        /// 外部设置值（不触发事件）
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
        /// 外部设置视口大小（不触发事件）
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
    /// 滚动条方向枚举
    /// </summary>
    public enum ScrollBarOrientation
    {
        Horizontal,
        Vertical
    }

    /// <summary>
    /// 滚动条鼠标指针类型
    /// </summary>
    public enum ScrollBarCursorType
    {
        Default,
        Hand,
        ResizeHorizontal,
        ResizeVertical
    }
}