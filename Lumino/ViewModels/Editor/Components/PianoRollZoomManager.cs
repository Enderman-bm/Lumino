using System;
using System.ComponentModel;

namespace Lumino.ViewModels.Editor.Components
{
    /// <summary>
    /// 钢琴卷帘缩放管理器 - 独立管理所有缩放相关功能
    /// 遵循单一职责原则，专注于缩放逻辑和状态管理
    /// </summary>
    public class PianoRollZoomManager : INotifyPropertyChanged
    {
        #region 事件
        public event PropertyChangedEventHandler? PropertyChanged;
        #endregion

        #region 私有字段
        private double _zoom = 1.0;
        private double _verticalZoom = 1.0;
        private double _zoomSliderValue = 50.0;
        private double _verticalZoomSliderValue = 50.0;
        #endregion

        #region 缩放属性
        /// <summary>
        /// 水平缩放系数 (0.1-5.0)
        /// </summary>
        public double Zoom
        {
            get => _zoom;
            set
            {
                if (Math.Abs(_zoom - value) > 0.001)
                {
                    _zoom = value;
                    OnPropertyChanged(nameof(Zoom));
                }
            }
        }

        /// <summary>
        /// 垂直缩放系数 (0.5-3.0)
        /// </summary>
        public double VerticalZoom
        {
            get => _verticalZoom;
            set
            {
                if (Math.Abs(_verticalZoom - value) > 0.001)
                {
                    _verticalZoom = value;
                    OnPropertyChanged(nameof(VerticalZoom));
                }
            }
        }

        /// <summary>
        /// 水平缩放滑块值 (0-100)
        /// </summary>
        public double ZoomSliderValue
        {
            get => _zoomSliderValue;
            set
            {
                if (Math.Abs(_zoomSliderValue - value) > 0.1)
                {
                    _zoomSliderValue = value;
                    OnPropertyChanged(nameof(ZoomSliderValue));
                }
            }
        }

        /// <summary>
        /// 垂直缩放滑块值 (0-100)
        /// </summary>
        public double VerticalZoomSliderValue
        {
            get => _verticalZoomSliderValue;
            set
            {
                if (Math.Abs(_verticalZoomSliderValue - value) > 0.1)
                {
                    _verticalZoomSliderValue = value;
                    OnPropertyChanged(nameof(VerticalZoomSliderValue));
                }
            }
        }
        #endregion

        #region 缩放范围常量
        // 水平缩放范围
        private const double MinZoom = 0.1;
        private const double MaxZoom = 5.0;
        private const double DefaultZoom = 1.0;

        // 垂直缩放范围
        private const double MinVerticalZoom = 0.5;
        private const double MaxVerticalZoom = 3.0;
        private const double DefaultVerticalZoom = 1.0;

        // 滑块范围
        private const double MinSliderValue = 0.0;
        private const double MaxSliderValue = 100.0;
        private const double DefaultSliderValue = 50.0;
        #endregion

        #region 构造函数
        public PianoRollZoomManager()
        {
            // 初始化默认值
            ResetToDefaults();
            
            // 监听属性变化
            PropertyChanged += OnInternalPropertyChanged;
        }
        #endregion

        #region 属性变化通知
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region 滑块值转换为缩放系数
        /// <summary>
        /// 将水平滑块值转换为缩放系数
        /// 0-50 映射到 0.1-1.0 (缩小)
        /// 50-100 映射到 1.0-5.0 (放大)
        /// </summary>
        /// <param name="sliderValue">滑块值 (0-100)</param>
        /// <returns>缩放系数</returns>
        private double ConvertSliderValueToZoom(double sliderValue)
        {
            // 确保滑块值在有效范围内
            sliderValue = Math.Max(MinSliderValue, Math.Min(MaxSliderValue, sliderValue));
            
            if (sliderValue <= DefaultSliderValue)
            {
                // 0-50对应0.1-1.0
                return MinZoom + (sliderValue / DefaultSliderValue) * (DefaultZoom - MinZoom);
            }
            else
            {
                // 50-100对应1.0-5.0
                return DefaultZoom + ((sliderValue - DefaultSliderValue) / DefaultSliderValue) * (MaxZoom - DefaultZoom);
            }
        }

        /// <summary>
        /// 将垂直滑块值转换为缩放系数
        /// 0-50 映射到 0.5-1.0 (缩小)
        /// 50-100 映射到 1.0-3.0 (放大)
        /// </summary>
        /// <param name="sliderValue">滑块值 (0-100)</param>
        /// <returns>缩放系数</returns>
        private double ConvertSliderValueToVerticalZoom(double sliderValue)
        {
            // 确保滑块值在有效范围内
            sliderValue = Math.Max(MinSliderValue, Math.Min(MaxSliderValue, sliderValue));
            
            if (sliderValue <= DefaultSliderValue)
            {
                // 0-50对应0.5-1.0
                return MinVerticalZoom + (sliderValue / DefaultSliderValue) * (DefaultVerticalZoom - MinVerticalZoom);
            }
            else
            {
                // 50-100对应1.0-3.0
                return DefaultVerticalZoom + ((sliderValue - DefaultSliderValue) / DefaultSliderValue) * (MaxVerticalZoom - DefaultVerticalZoom);
            }
        }
        #endregion

        #region 缩放系数转换为滑块值
        /// <summary>
        /// 将水平缩放系数转换为滑块值
        /// </summary>
        /// <param name="zoom">缩放系数</param>
        /// <returns>滑块值 (0-100)</returns>
        private double ConvertZoomToSliderValue(double zoom)
        {
            zoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
            
            if (zoom <= DefaultZoom)
            {
                // 0.1-1.0对应0-50
                return (zoom - MinZoom) / (DefaultZoom - MinZoom) * DefaultSliderValue;
            }
            else
            {
                // 1.0-5.0对应50-100
                return DefaultSliderValue + (zoom - DefaultZoom) / (MaxZoom - DefaultZoom) * DefaultSliderValue;
            }
        }

        /// <summary>
        /// 将垂直缩放系数转换为滑块值
        /// </summary>
        /// <param name="verticalZoom">垂直缩放系数</param>
        /// <returns>滑块值 (0-100)</returns>
        private double ConvertVerticalZoomToSliderValue(double verticalZoom)
        {
            verticalZoom = Math.Max(MinVerticalZoom, Math.Min(MaxVerticalZoom, verticalZoom));
            
            if (verticalZoom <= DefaultVerticalZoom)
            {
                // 0.5-1.0对应0-50
                return (verticalZoom - MinVerticalZoom) / (DefaultVerticalZoom - MinVerticalZoom) * DefaultSliderValue;
            }
            else
            {
                // 1.0-3.0对应50-100
                return DefaultSliderValue + (verticalZoom - DefaultVerticalZoom) / (MaxVerticalZoom - DefaultVerticalZoom) * DefaultSliderValue;
            }
        }
        #endregion

        #region 属性变化处理
        private void OnInternalPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ZoomSliderValue):
                    var newZoom = ConvertSliderValueToZoom(_zoomSliderValue);
                    if (Math.Abs(newZoom - _zoom) > 0.001) // 避免无限循环
                    {
                        _zoom = newZoom; // 直接设置字段，避免触发属性变化
                        OnPropertyChanged(nameof(Zoom));
                    }
                    break;
                    
                case nameof(VerticalZoomSliderValue):
                    var newVerticalZoom = ConvertSliderValueToVerticalZoom(_verticalZoomSliderValue);
                    if (Math.Abs(newVerticalZoom - _verticalZoom) > 0.001) // 避免无限循环
                    {
                        _verticalZoom = newVerticalZoom; // 直接设置字段，避免触发属性变化
                        OnPropertyChanged(nameof(VerticalZoom));
                    }
                    break;
                    
                case nameof(Zoom):
                    var newZoomSliderValue = ConvertZoomToSliderValue(_zoom);
                    if (Math.Abs(newZoomSliderValue - _zoomSliderValue) > 0.1) // 避免无限循环
                    {
                        _zoomSliderValue = newZoomSliderValue; // 直接设置字段，避免触发属性变化
                        OnPropertyChanged(nameof(ZoomSliderValue));
                    }
                    break;
                    
                case nameof(VerticalZoom):
                    var newVerticalZoomSliderValue = ConvertVerticalZoomToSliderValue(_verticalZoom);
                    if (Math.Abs(newVerticalZoomSliderValue - _verticalZoomSliderValue) > 0.1) // 避免无限循环
                    {
                        _verticalZoomSliderValue = newVerticalZoomSliderValue; // 直接设置字段，避免触发属性变化
                        OnPropertyChanged(nameof(VerticalZoomSliderValue));
                    }
                    break;
            }
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 设置水平缩放系数
        /// </summary>
        /// <param name="zoom">缩放系数</param>
        public void SetZoom(double zoom)
        {
            Zoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
        }

        /// <summary>
        /// 设置垂直缩放系数
        /// </summary>
        /// <param name="verticalZoom">垂直缩放系数</param>
        public void SetVerticalZoom(double verticalZoom)
        {
            VerticalZoom = Math.Max(MinVerticalZoom, Math.Min(MaxVerticalZoom, verticalZoom));
        }

        /// <summary>
        /// 设置水平滑块值
        /// </summary>
        /// <param name="sliderValue">滑块值</param>
        public void SetZoomSliderValue(double sliderValue)
        {
            ZoomSliderValue = Math.Max(MinSliderValue, Math.Min(MaxSliderValue, sliderValue));
        }

        /// <summary>
        /// 设置垂直滑块值
        /// </summary>
        /// <param name="sliderValue">滑块值</param>
        public void SetVerticalZoomSliderValue(double sliderValue)
        {
            VerticalZoomSliderValue = Math.Max(MinSliderValue, Math.Min(MaxSliderValue, sliderValue));
        }

        /// <summary>
        /// 重置到默认值
        /// </summary>
        public void ResetToDefaults()
        {
            _zoom = DefaultZoom;
            _verticalZoom = DefaultVerticalZoom;
            _zoomSliderValue = DefaultSliderValue;
            _verticalZoomSliderValue = DefaultSliderValue;
            
            // 通知所有属性变化
            OnPropertyChanged(nameof(Zoom));
            OnPropertyChanged(nameof(VerticalZoom));
            OnPropertyChanged(nameof(ZoomSliderValue));
            OnPropertyChanged(nameof(VerticalZoomSliderValue));
        }

        /// <summary>
        /// 增加水平缩放 (放大)
        /// </summary>
        /// <param name="step">增加的步长，默认为10</param>
        public void ZoomIn(double step = 10.0)
        {
            SetZoomSliderValue(_zoomSliderValue + step);
        }

        /// <summary>
        /// 减少水平缩放 (缩小)
        /// </summary>
        /// <param name="step">减少的步长，默认为10</param>
        public void ZoomOut(double step = 10.0)
        {
            SetZoomSliderValue(_zoomSliderValue - step);
        }

        /// <summary>
        /// 增加垂直缩放 (放大)
        /// </summary>
        /// <param name="step">增加的步长，默认为10</param>
        public void VerticalZoomIn(double step = 10.0)
        {
            SetVerticalZoomSliderValue(_verticalZoomSliderValue + step);
        }

        /// <summary>
        /// 减少垂直缩放 (缩小)
        /// </summary>
        /// <param name="step">减少的步长，默认为10</param>
        public void VerticalZoomOut(double step = 10.0)
        {
            SetVerticalZoomSliderValue(_verticalZoomSliderValue - step);
        }

        /// <summary>
        /// 适合窗口 - 自动调整缩放以适应内容
        /// </summary>
        /// <param name="contentWidth">内容宽度</param>
        /// <param name="viewportWidth">视口宽度</param>
        public void FitToWindow(double contentWidth, double viewportWidth)
        {
            if (contentWidth > 0 && viewportWidth > 0)
            {
                var optimalZoom = Math.Min(MaxZoom, viewportWidth / contentWidth);
                SetZoom(optimalZoom);
            }
        }

        /// <summary>
        /// 适合高度 - 自动调整垂直缩放以适应内容
        /// </summary>
        /// <param name="contentHeight">内容高度</param>
        /// <param name="viewportHeight">视口高度</param>
        public void FitToHeight(double contentHeight, double viewportHeight)
        {
            if (contentHeight > 0 && viewportHeight > 0)
            {
                var optimalVerticalZoom = Math.Min(MaxVerticalZoom, viewportHeight / contentHeight);
                SetVerticalZoom(optimalVerticalZoom);
            }
        }
        #endregion

        #region 只读属性
        /// <summary>
        /// 是否处于默认缩放状态
        /// </summary>
        public bool IsAtDefaultZoom => Math.Abs(_zoom - DefaultZoom) < 0.001 && Math.Abs(_verticalZoom - DefaultVerticalZoom) < 0.001;

        /// <summary>
        /// 是否处于最小缩放状态
        /// </summary>
        public bool IsAtMinimumZoom => Math.Abs(_zoom - MinZoom) < 0.001 && Math.Abs(_verticalZoom - MinVerticalZoom) < 0.001;

        /// <summary>
        /// 是否处于最大缩放状态
        /// </summary>
        public bool IsAtMaximumZoom => Math.Abs(_zoom - MaxZoom) < 0.001 && Math.Abs(_verticalZoom - MaxVerticalZoom) < 0.001;

        /// <summary>
        /// 缩放范围信息
        /// </summary>
        public string ZoomRangeInfo => $"水平: {MinZoom:F1}x - {MaxZoom:F1}x, 垂直: {MinVerticalZoom:F1}x - {MaxVerticalZoom:F1}x";

        /// <summary>
        /// 当前缩放状态信息
        /// </summary>
        public string CurrentZoomInfo => $"水平: {_zoom:F2}x, 垂直: {_verticalZoom:F2}x";
        #endregion
    }
}