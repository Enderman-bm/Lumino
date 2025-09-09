using System;
using CommunityToolkit.Mvvm.ComponentModel;
using DominoNext.ViewModels.Editor;

namespace DominoNext.ViewModels.Editor.Components
{
    /// <summary>
    /// 钢琴卷帘的滚动条管理器，负责管理自定义滚动条与PianoRoll系统的集成
    /// </summary>
    public partial class PianoRollScrollBarManager : ObservableObject
    {
        #region 滚动条实例
        /// <summary>
        /// 横向滚动条ViewModel
        /// </summary>
        public CustomScrollBarViewModel HorizontalScrollBar { get; }

        /// <summary>
        /// 纵向滚动条ViewModel
        /// </summary>
        public CustomScrollBarViewModel VerticalScrollBar { get; }
        #endregion

        #region 私有字段
        private PianoRollViewModel? _pianoRollViewModel;
        private bool _isUpdatingFromPianoRoll = false;
        private bool _isUpdatingFromScrollBar = false;
        #endregion

        #region 构造函数
        public PianoRollScrollBarManager()
        {
            // 创建横向和纵向滚动条
            HorizontalScrollBar = new CustomScrollBarViewModel(ScrollBarOrientation.Horizontal);
            VerticalScrollBar = new CustomScrollBarViewModel(ScrollBarOrientation.Vertical);

            // 订阅滚动条事件
            SubscribeToScrollBarEvents();
        }
        #endregion

        #region 初始化
        /// <summary>
        /// 设置PianoRoll的引用并建立双向绑定
        /// </summary>
        public void SetPianoRollViewModel(PianoRollViewModel pianoRollViewModel)
        {
            if (_pianoRollViewModel != null)
            {
                UnsubscribeFromPianoRoll();
            }

            _pianoRollViewModel = pianoRollViewModel;
            
            if (_pianoRollViewModel != null)
            {
                SubscribeToPianoRoll();
                InitializeScrollBars();
            }
        }
        #endregion

        #region 事件订阅
        private void SubscribeToScrollBarEvents()
        {
            // 横向滚动条事件
            HorizontalScrollBar.ValueChanged += OnHorizontalScrollValueChanged;
            HorizontalScrollBar.ViewportSizeChanged += OnHorizontalViewportSizeChanged;

            // 纵向滚动条事件
            VerticalScrollBar.ValueChanged += OnVerticalScrollValueChanged;
            VerticalScrollBar.ViewportSizeChanged += OnVerticalViewportSizeChanged;
        }

        private void SubscribeToPianoRoll()
        {
            if (_pianoRollViewModel != null)
            {
                _pianoRollViewModel.PropertyChanged += OnPianoRollPropertyChanged;
            }
        }

        private void UnsubscribeFromPianoRoll()
        {
            if (_pianoRollViewModel != null)
            {
                _pianoRollViewModel.PropertyChanged -= OnPianoRollPropertyChanged;
            }
        }
        #endregion

        #region 滚动条初始化
        private void InitializeScrollBars()
        {
            if (_pianoRollViewModel == null) return;

            _isUpdatingFromPianoRoll = true;

            try
            {
                // 初始化横向滚动条
                HorizontalScrollBar.SetParameters(
                    minimum: 0,
                    maximum: _pianoRollViewModel.MaxScrollExtent + _pianoRollViewModel.ViewportWidth,
                    value: _pianoRollViewModel.CurrentScrollOffset,
                    viewportSize: _pianoRollViewModel.ViewportWidth
                );

                // 初始化纵向滚动条
                var verticalMax = Math.Max(_pianoRollViewModel.TotalHeight, _pianoRollViewModel.ViewportHeight);
                VerticalScrollBar.SetParameters(
                    minimum: 0,
                    maximum: verticalMax,
                    value: _pianoRollViewModel.VerticalScrollOffset,
                    viewportSize: _pianoRollViewModel.ViewportHeight
                );
            }
            finally
            {
                _isUpdatingFromPianoRoll = false;
            }
        }
        #endregion

        #region PianoRoll属性变化处理
        private void OnPianoRollPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_isUpdatingFromScrollBar) return;

            _isUpdatingFromPianoRoll = true;

            try
            {
                switch (e.PropertyName)
                {
                    case nameof(PianoRollViewModel.CurrentScrollOffset):
                        HorizontalScrollBar.SetValueDirect(_pianoRollViewModel?.CurrentScrollOffset ?? 0);
                        break;

                    case nameof(PianoRollViewModel.VerticalScrollOffset):
                        VerticalScrollBar.SetValueDirect(_pianoRollViewModel?.VerticalScrollOffset ?? 0);
                        break;

                    case nameof(PianoRollViewModel.ViewportWidth):
                        UpdateHorizontalScrollBarParameters();
                        break;

                    case nameof(PianoRollViewModel.ViewportHeight):
                        UpdateVerticalScrollBarParameters();
                        break;

                    case nameof(PianoRollViewModel.MaxScrollExtent):
                        UpdateHorizontalScrollBarParameters();
                        break;

                    case nameof(PianoRollViewModel.TotalHeight):
                        UpdateVerticalScrollBarParameters();
                        break;

                    case nameof(PianoRollViewModel.Zoom):
                        UpdateHorizontalZoomFromPianoRoll();
                        break;

                    case nameof(PianoRollViewModel.VerticalZoom):
                        UpdateVerticalZoomFromPianoRoll();
                        break;
                }
            }
            finally
            {
                _isUpdatingFromPianoRoll = false;
            }
        }

        private void UpdateHorizontalScrollBarParameters()
        {
            if (_pianoRollViewModel == null) return;

            HorizontalScrollBar.SetParameters(
                minimum: 0,
                maximum: _pianoRollViewModel.MaxScrollExtent + _pianoRollViewModel.ViewportWidth,
                value: _pianoRollViewModel.CurrentScrollOffset,
                viewportSize: _pianoRollViewModel.ViewportWidth
            );
        }

        private void UpdateVerticalScrollBarParameters()
        {
            if (_pianoRollViewModel == null) return;

            var verticalMax = Math.Max(_pianoRollViewModel.TotalHeight, _pianoRollViewModel.ViewportHeight);
            VerticalScrollBar.SetParameters(
                minimum: 0,
                maximum: verticalMax,
                value: _pianoRollViewModel.VerticalScrollOffset,
                viewportSize: _pianoRollViewModel.ViewportHeight
            );
        }

        private void UpdateHorizontalZoomFromPianoRoll()
        {
            if (_pianoRollViewModel == null) return;

            // 将PianoRoll的缩放值转换为滚动条的视口大小
            // 缩放越大，视口相对于内容的比例越小
            var baseViewportSize = _pianoRollViewModel.ViewportWidth;
            var zoomFactor = _pianoRollViewModel.Zoom;
            
            // 计算基于缩放的"逻辑"视口大小
            var logicalViewportSize = baseViewportSize / Math.Max(0.1, zoomFactor);
            
            HorizontalScrollBar.SetViewportSizeDirect(logicalViewportSize);
        }

        private void UpdateVerticalZoomFromPianoRoll()
        {
            if (_pianoRollViewModel == null) return;

            // 将PianoRoll的垂直缩放值转换为滚动条的视口大小
            var baseViewportSize = _pianoRollViewModel.ViewportHeight;
            var verticalZoomFactor = _pianoRollViewModel.VerticalZoom;
            
            // 计算基于缩放的"逻辑"视口大小
            var logicalViewportSize = baseViewportSize / Math.Max(0.1, verticalZoomFactor);
            
            VerticalScrollBar.SetViewportSizeDirect(logicalViewportSize);
        }
        #endregion

        #region 滚动条事件处理
        private void OnHorizontalScrollValueChanged(double value)
        {
            if (_isUpdatingFromPianoRoll || _pianoRollViewModel == null) return;

            _isUpdatingFromScrollBar = true;

            try
            {
                _pianoRollViewModel.SetCurrentScrollOffset(value);
            }
            finally
            {
                _isUpdatingFromScrollBar = false;
            }
        }

        private void OnVerticalScrollValueChanged(double value)
        {
            if (_isUpdatingFromPianoRoll || _pianoRollViewModel == null) return;

            _isUpdatingFromScrollBar = true;

            try
            {
                _pianoRollViewModel.SetVerticalScrollOffset(value);
            }
            finally
            {
                _isUpdatingFromScrollBar = false;
            }
        }

        private void OnHorizontalViewportSizeChanged(double viewportSize)
        {
            if (_isUpdatingFromPianoRoll || _pianoRollViewModel == null) return;

            _isUpdatingFromScrollBar = true;

            try
            {
                // 将滚动条的视口大小变化转换为PianoRoll的缩放变化
                var baseViewportSize = _pianoRollViewModel.ViewportWidth;
                var newZoomFactor = baseViewportSize / Math.Max(1, viewportSize);
                
                // 限制缩放范围
                newZoomFactor = Math.Max(0.1, Math.Min(10.0, newZoomFactor));
                
                // 计算对应的滑块值（0-100）
                var sliderValue = ZoomToSliderValue(newZoomFactor);
                _pianoRollViewModel.SetZoomSliderValue(sliderValue);
            }
            finally
            {
                _isUpdatingFromScrollBar = false;
            }
        }

        private void OnVerticalViewportSizeChanged(double viewportSize)
        {
            if (_isUpdatingFromPianoRoll || _pianoRollViewModel == null) return;

            _isUpdatingFromScrollBar = true;

            try
            {
                // 将滚动条的视口大小变化转换为PianoRoll的垂直缩放变化
                var baseViewportSize = _pianoRollViewModel.ViewportHeight;
                var newVerticalZoomFactor = baseViewportSize / Math.Max(1, viewportSize);
                
                // 限制缩放范围
                newVerticalZoomFactor = Math.Max(0.1, Math.Min(10.0, newVerticalZoomFactor));
                
                // 计算对应的滑块值（0-100）
                var sliderValue = ZoomToSliderValue(newVerticalZoomFactor);
                _pianoRollViewModel.SetVerticalZoomSliderValue(sliderValue);
            }
            finally
            {
                _isUpdatingFromScrollBar = false;
            }
        }
        #endregion

        #region 缩放转换辅助方法
        /// <summary>
        /// 将缩放因子转换为滑块值（0-100）
        /// </summary>
        private static double ZoomToSliderValue(double zoomFactor)
        {
            // 假设缩放范围是 0.1x 到 10x
            // 滑块值 0 对应 0.1x，滑块值 100 对应 10x
            // 使用对数缩放以获得更好的用户体验
            
            var minZoom = 0.1;
            var maxZoom = 10.0;
            
            var normalizedZoom = Math.Max(minZoom, Math.Min(maxZoom, zoomFactor));
            var logMin = Math.Log(minZoom);
            var logMax = Math.Log(maxZoom);
            var logZoom = Math.Log(normalizedZoom);
            
            var sliderValue = ((logZoom - logMin) / (logMax - logMin)) * 100;
            return Math.Max(0, Math.Min(100, sliderValue));
        }

        /// <summary>
        /// 将滑块值（0-100）转换为缩放因子
        /// </summary>
        private static double SliderValueToZoom(double sliderValue)
        {
            var minZoom = 0.1;
            var maxZoom = 10.0;
            
            var normalizedSlider = Math.Max(0, Math.Min(100, sliderValue)) / 100.0;
            var logMin = Math.Log(minZoom);
            var logMax = Math.Log(maxZoom);
            var logZoom = logMin + (normalizedSlider * (logMax - logMin));
            
            return Math.Exp(logZoom);
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 设置滚动条轨道长度
        /// </summary>
        public void SetScrollBarTrackLengths(double horizontalLength, double verticalLength)
        {
            HorizontalScrollBar.SetTrackLength(horizontalLength);
            VerticalScrollBar.SetTrackLength(verticalLength);
        }

        /// <summary>
        /// 强制更新所有滚动条参数
        /// </summary>
        public void ForceUpdateScrollBars()
        {
            if (_pianoRollViewModel == null) return;

            _isUpdatingFromPianoRoll = true;

            try
            {
                UpdateHorizontalScrollBarParameters();
                UpdateVerticalScrollBarParameters();
                UpdateHorizontalZoomFromPianoRoll();
                UpdateVerticalZoomFromPianoRoll();
            }
            finally
            {
                _isUpdatingFromPianoRoll = false;
            }
        }
        #endregion

        #region 清理
        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            UnsubscribeFromPianoRoll();
            _pianoRollViewModel = null;
        }
        #endregion
    }
}