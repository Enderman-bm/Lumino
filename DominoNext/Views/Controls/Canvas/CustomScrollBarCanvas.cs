using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using DominoNext.ViewModels.Editor.Components;

namespace DominoNext.Views.Controls.Canvas
{
    /// <summary>
    /// 自定义滚动条Canvas基类
    /// </summary>
    public abstract class CustomScrollBarCanvas : Control
    {
        #region 依赖属性
        public static readonly StyledProperty<CustomScrollBarViewModel?> ViewModelProperty =
            AvaloniaProperty.Register<CustomScrollBarCanvas, CustomScrollBarViewModel?>(nameof(ViewModel));

        public static readonly StyledProperty<IBrush?> TrackBrushProperty =
            AvaloniaProperty.Register<CustomScrollBarCanvas, IBrush?>(nameof(TrackBrush));

        public static readonly StyledProperty<IBrush?> ThumbBrushProperty =
            AvaloniaProperty.Register<CustomScrollBarCanvas, IBrush?>(nameof(ThumbBrush));

        public static readonly StyledProperty<IBrush?> ThumbHoverBrushProperty =
            AvaloniaProperty.Register<CustomScrollBarCanvas, IBrush?>(nameof(ThumbHoverBrush));

        public static readonly StyledProperty<IBrush?> ThumbPressedBrushProperty =
            AvaloniaProperty.Register<CustomScrollBarCanvas, IBrush?>(nameof(ThumbPressedBrush));

        public static readonly StyledProperty<double> CornerRadiusProperty =
            AvaloniaProperty.Register<CustomScrollBarCanvas, double>(nameof(CornerRadius), 2.0);

        public CustomScrollBarViewModel? ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public IBrush? TrackBrush
        {
            get => GetValue(TrackBrushProperty);
            set => SetValue(TrackBrushProperty, value);
        }

        public IBrush? ThumbBrush
        {
            get => GetValue(ThumbBrushProperty);
            set => SetValue(ThumbBrushProperty, value);
        }

        public IBrush? ThumbHoverBrush
        {
            get => GetValue(ThumbHoverBrushProperty);
            set => SetValue(ThumbHoverBrushProperty, value);
        }

        public IBrush? ThumbPressedBrush
        {
            get => GetValue(ThumbPressedBrushProperty);
            set => SetValue(ThumbPressedBrushProperty, value);
        }

        public double CornerRadius
        {
            get => GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }
        #endregion

        #region 状态
        private bool _isMouseOver = false;
        private bool _isPressed = false;
        private StandardCursorType _currentCursor = StandardCursorType.Arrow;
        #endregion

        #region 构造函数
        protected CustomScrollBarCanvas()
        {
            ClipToBounds = true;
            IsHitTestVisible = true;
            Focusable = true;
        }
        #endregion

        #region 生命周期
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            SubscribeToViewModel();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            UnsubscribeFromViewModel();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ViewModelProperty)
            {
                UnsubscribeFromViewModel();
                SubscribeToViewModel();
                InvalidateVisual();
            }
            else if (change.Property == TrackBrushProperty ||
                     change.Property == ThumbBrushProperty ||
                     change.Property == ThumbHoverBrushProperty ||
                     change.Property == ThumbPressedBrushProperty ||
                     change.Property == CornerRadiusProperty)
            {
                InvalidateVisual();
            }
        }
        #endregion

        #region ViewModel事件订阅
        private void SubscribeToViewModel()
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += OnViewModelPropertyChanged;
                ViewModel.CursorChanged += OnCursorChanged;
                UpdateTrackLength();
            }
        }

        private void UnsubscribeFromViewModel()
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                ViewModel.CursorChanged -= OnCursorChanged;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.ThumbPosition) ||
                e.PropertyName == nameof(ViewModel.ThumbLength) ||
                e.PropertyName == nameof(ViewModel.IsDraggingThumb) ||
                e.PropertyName == nameof(ViewModel.IsDraggingStartEdge) ||
                e.PropertyName == nameof(ViewModel.IsDraggingEndEdge))
            {
                Dispatcher.UIThread.Post(InvalidateVisual);
            }
        }

        private void OnCursorChanged(ScrollBarCursorType cursorType)
        {
            var newCursor = cursorType switch
            {
                ScrollBarCursorType.Hand => StandardCursorType.Hand,
                ScrollBarCursorType.ResizeHorizontal => StandardCursorType.SizeWestEast,
                ScrollBarCursorType.ResizeVertical => StandardCursorType.SizeNorthSouth,
                _ => StandardCursorType.Arrow
            };

            if (_currentCursor != newCursor)
            {
                _currentCursor = newCursor;
                Cursor = new Cursor(newCursor);
            }
        }
        #endregion

        #region 鼠标事件
        protected override void OnPointerEntered(PointerEventArgs e)
        {
            base.OnPointerEntered(e);
            _isMouseOver = true;
            InvalidateVisual();
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            _isMouseOver = false;
            Cursor = new Cursor(StandardCursorType.Arrow);
            InvalidateVisual();
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            
            if (_isPressed && ViewModel != null)
            {
                var position = GetPositionFromPoint(e.GetPosition(this));
                ViewModel.UpdateDrag(position);
                e.Handled = true;
            }
            else if (_isMouseOver && ViewModel != null)
            {
                var position = GetPositionFromPoint(e.GetPosition(this));
                ViewModel.HandleMouseMove(position);
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && ViewModel != null)
            {
                _isPressed = true;
                var position = GetPositionFromPoint(e.GetPosition(this));
                ViewModel.StartDrag(position);
                e.Pointer.Capture(this);
                InvalidateVisual();
                e.Handled = true;
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            
            if (_isPressed && ViewModel != null)
            {
                _isPressed = false;
                ViewModel.EndDrag();
                e.Pointer.Capture(null);
                InvalidateVisual();
                e.Handled = true;
            }
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            
            if (ViewModel != null)
            {
                var isCtrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);
                ViewModel.HandleWheel(e.Delta.Y * 50, isCtrlPressed); // 调整滚轮灵敏度
                e.Handled = true;
            }
        }
        #endregion

        #region 布局
        protected override Size MeasureOverride(Size availableSize)
        {
            return GetDesiredSize(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            UpdateTrackLength();
            return finalSize;
        }

        private void UpdateTrackLength()
        {
            if (ViewModel != null)
            {
                var trackLength = GetTrackLength(Bounds.Size);
                ViewModel.SetTrackLength(trackLength);
            }
        }
        #endregion

        #region 渲染
        public override void Render(DrawingContext context)
        {
            if (ViewModel == null) return;

            var bounds = new Rect(Bounds.Size);
            
            // 绘制轨道
            DrawTrack(context, bounds);
            
            // 绘制滑块
            DrawThumb(context, bounds);
        }

        protected virtual void DrawTrack(DrawingContext context, Rect bounds)
        {
            if (TrackBrush == null) return;

            var trackRect = GetTrackRect(bounds);
            
            context.FillRectangle(TrackBrush, trackRect, (float)CornerRadius);
        }

        protected virtual void DrawThumb(DrawingContext context, Rect bounds)
        {
            if (ViewModel == null) return;

            var thumbBrush = GetThumbBrush();
            if (thumbBrush == null) return;

            var thumbRect = GetThumbRect(bounds);
            
            context.FillRectangle(thumbBrush, thumbRect, (float)CornerRadius);
        }

        private IBrush? GetThumbBrush()
        {
            if (ViewModel?.IsDraggingThumb == true || ViewModel?.IsDraggingStartEdge == true || ViewModel?.IsDraggingEndEdge == true)
                return ThumbPressedBrush ?? ThumbBrush;
            
            if (_isMouseOver)
                return ThumbHoverBrush ?? ThumbBrush;
            
            return ThumbBrush;
        }
        #endregion

        #region 抽象方法 - 由子类实现
        /// <summary>
        /// 从鼠标位置获取滚动条位置值
        /// </summary>
        protected abstract double GetPositionFromPoint(Point point);

        /// <summary>
        /// 获取期望大小
        /// </summary>
        protected abstract Size GetDesiredSize(Size availableSize);

        /// <summary>
        /// 获取轨道长度
        /// </summary>
        protected abstract double GetTrackLength(Size size);

        /// <summary>
        /// 获取轨道矩形
        /// </summary>
        protected abstract Rect GetTrackRect(Rect bounds);

        /// <summary>
        /// 获取滑块矩形
        /// </summary>
        protected abstract Rect GetThumbRect(Rect bounds);
        #endregion
    }
}