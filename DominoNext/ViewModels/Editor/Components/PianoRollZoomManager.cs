using System;
using System.ComponentModel;

namespace Lumino.ViewModels.Editor.Components
{
    /// <summary>
    /// ���پ������Ź����� - ������������������ع���
    /// ��ѭ��һְ��ԭ��רע�������߼���״̬����
    /// </summary>
    public class PianoRollZoomManager : INotifyPropertyChanged
    {
        #region �¼�
        public event PropertyChangedEventHandler? PropertyChanged;
        #endregion

        #region ˽���ֶ�
        private double _zoom = 1.0;
        private double _verticalZoom = 1.0;
        private double _zoomSliderValue = 50.0;
        private double _verticalZoomSliderValue = 50.0;
        #endregion

        #region ��������
        /// <summary>
        /// ˮƽ����ϵ�� (0.1-5.0)
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
                    OnPropertyChanged(nameof(TimeToPixelScaleRounded));
                    OnPropertyChanged(nameof(PixelToTimeScale));
                }
            }
        }

        /// <summary>
        /// ʱ��到���أ��������
        /// </summary>
        public double TimeToPixelScaleRounded => Math.Round(Zoom * 100) / 100;

        /// <summary>
        /// ����到时��������
        /// </summary>
        public double PixelToTimeScale => 1.0 / Zoom;

        /// <summary>
        /// 时间到像素缩放比例
        /// </summary>
        public double TimeToPixelScale => Zoom;

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
                    OnPropertyChanged(nameof(KeyHeight));
                    OnPropertyChanged(nameof(VerticalZoomLevel));
                }
            }
        }

        /// <summary>
        /// 音符高度
        /// </summary>
        public double KeyHeight => VerticalZoom * 20;

        /// <summary>
        /// 垂直缩放级别
        /// </summary>
        public double VerticalZoomLevel => VerticalZoom;

        /// <summary>
        /// ˮƽ���Ż���ֵ (0-100)
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
        /// ��ֱ���Ż���ֵ (0-100)
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

        #region ���ŷ�Χ����
        // ˮƽ���ŷ�Χ
        private const double MinZoom = 0.1;
        private const double MaxZoom = 5.0;
        private const double DefaultZoom = 1.0;

        // ��ֱ���ŷ�Χ
        private const double MinVerticalZoom = 0.5;
        private const double MaxVerticalZoom = 3.0;
        private const double DefaultVerticalZoom = 1.0;

        // ���鷶Χ
        private const double MinSliderValue = 0.0;
        private const double MaxSliderValue = 100.0;
        private const double DefaultSliderValue = 50.0;
        #endregion

        #region ���캯��
        public PianoRollZoomManager()
        {
            // ��ʼ��Ĭ��ֵ
            ResetToDefaults();
            
            // �������Ա仯
            PropertyChanged += OnInternalPropertyChanged;
        }
        #endregion

        #region ���Ա仯֪ͨ
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region ����ֵת��Ϊ����ϵ��
        /// <summary>
        /// ��ˮƽ����ֵת��Ϊ����ϵ��
        /// 0-50 ӳ�䵽 0.1-1.0 (��С)
        /// 50-100 ӳ�䵽 1.0-5.0 (�Ŵ�)
        /// </summary>
        /// <param name="sliderValue">����ֵ (0-100)</param>
        /// <returns>����ϵ��</returns>
        private double ConvertSliderValueToZoom(double sliderValue)
        {
            // ȷ������ֵ����Ч��Χ��
            sliderValue = Math.Max(MinSliderValue, Math.Min(MaxSliderValue, sliderValue));
            
            if (sliderValue <= DefaultSliderValue)
            {
                // 0-50��Ӧ0.1-1.0
                return MinZoom + (sliderValue / DefaultSliderValue) * (DefaultZoom - MinZoom);
            }
            else
            {
                // 50-100��Ӧ1.0-5.0
                return DefaultZoom + ((sliderValue - DefaultSliderValue) / DefaultSliderValue) * (MaxZoom - DefaultZoom);
            }
        }

        /// <summary>
        /// ����ֱ����ֵת��Ϊ����ϵ��
        /// 0-50 ӳ�䵽 0.5-1.0 (��С)
        /// 50-100 ӳ�䵽 1.0-3.0 (�Ŵ�)
        /// </summary>
        /// <param name="sliderValue">����ֵ (0-100)</param>
        /// <returns>����ϵ��</returns>
        private double ConvertSliderValueToVerticalZoom(double sliderValue)
        {
            // ȷ������ֵ����Ч��Χ��
            sliderValue = Math.Max(MinSliderValue, Math.Min(MaxSliderValue, sliderValue));
            
            if (sliderValue <= DefaultSliderValue)
            {
                // 0-50��Ӧ0.5-1.0
                return MinVerticalZoom + (sliderValue / DefaultSliderValue) * (DefaultVerticalZoom - MinVerticalZoom);
            }
            else
            {
                // 50-100��Ӧ1.0-3.0
                return DefaultVerticalZoom + ((sliderValue - DefaultSliderValue) / DefaultSliderValue) * (MaxVerticalZoom - DefaultVerticalZoom);
            }
        }
        #endregion

        #region ����ϵ��ת��Ϊ����ֵ
        /// <summary>
        /// ��ˮƽ����ϵ��ת��Ϊ����ֵ
        /// </summary>
        /// <param name="zoom">����ϵ��</param>
        /// <returns>����ֵ (0-100)</returns>
        private double ConvertZoomToSliderValue(double zoom)
        {
            zoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
            
            if (zoom <= DefaultZoom)
            {
                // 0.1-1.0��Ӧ0-50
                return (zoom - MinZoom) / (DefaultZoom - MinZoom) * DefaultSliderValue;
            }
            else
            {
                // 1.0-5.0��Ӧ50-100
                return DefaultSliderValue + (zoom - DefaultZoom) / (MaxZoom - DefaultZoom) * DefaultSliderValue;
            }
        }

        /// <summary>
        /// ����ֱ����ϵ��ת��Ϊ����ֵ
        /// </summary>
        /// <param name="verticalZoom">��ֱ����ϵ��</param>
        /// <returns>����ֵ (0-100)</returns>
        private double ConvertVerticalZoomToSliderValue(double verticalZoom)
        {
            verticalZoom = Math.Max(MinVerticalZoom, Math.Min(MaxVerticalZoom, verticalZoom));
            
            if (verticalZoom <= DefaultVerticalZoom)
            {
                // 0.5-1.0��Ӧ0-50
                return (verticalZoom - MinVerticalZoom) / (DefaultVerticalZoom - MinVerticalZoom) * DefaultSliderValue;
            }
            else
            {
                // 1.0-3.0��Ӧ50-100
                return DefaultSliderValue + (verticalZoom - DefaultVerticalZoom) / (MaxVerticalZoom - DefaultVerticalZoom) * DefaultSliderValue;
            }
        }
        #endregion

        #region ���Ա仯����
        private void OnInternalPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ZoomSliderValue):
                    var newZoom = ConvertSliderValueToZoom(_zoomSliderValue);
                    if (Math.Abs(newZoom - _zoom) > 0.001) // ��������ѭ��
                    {
                        _zoom = newZoom; // ֱ�������ֶΣ����ⴥ�����Ա仯
                        OnPropertyChanged(nameof(Zoom));
                    }
                    break;
                    
                case nameof(VerticalZoomSliderValue):
                    var newVerticalZoom = ConvertSliderValueToVerticalZoom(_verticalZoomSliderValue);
                    if (Math.Abs(newVerticalZoom - _verticalZoom) > 0.001) // ��������ѭ��
                    {
                        _verticalZoom = newVerticalZoom; // ֱ�������ֶΣ����ⴥ�����Ա仯
                        OnPropertyChanged(nameof(VerticalZoom));
                    }
                    break;
                    
                case nameof(Zoom):
                    var newZoomSliderValue = ConvertZoomToSliderValue(_zoom);
                    if (Math.Abs(newZoomSliderValue - _zoomSliderValue) > 0.1) // ��������ѭ��
                    {
                        _zoomSliderValue = newZoomSliderValue; // ֱ�������ֶΣ����ⴥ�����Ա仯
                        OnPropertyChanged(nameof(ZoomSliderValue));
                    }
                    break;
                    
                case nameof(VerticalZoom):
                    var newVerticalZoomSliderValue = ConvertVerticalZoomToSliderValue(_verticalZoom);
                    if (Math.Abs(newVerticalZoomSliderValue - _verticalZoomSliderValue) > 0.1) // ��������ѭ��
                    {
                        _verticalZoomSliderValue = newVerticalZoomSliderValue; // ֱ�������ֶΣ����ⴥ�����Ա仯
                        OnPropertyChanged(nameof(VerticalZoomSliderValue));
                    }
                    break;
            }
        }
        #endregion

        #region ��������
        /// <summary>
        /// ����ˮƽ����ϵ��
        /// </summary>
        /// <param name="zoom">����ϵ��</param>
        public void SetZoom(double zoom)
        {
            Zoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
        }

        /// <summary>
        /// ���ô�ֱ����ϵ��
        /// </summary>
        /// <param name="verticalZoom">��ֱ����ϵ��</param>
        public void SetVerticalZoom(double verticalZoom)
        {
            VerticalZoom = Math.Max(MinVerticalZoom, Math.Min(MaxVerticalZoom, verticalZoom));
        }

        /// <summary>
        /// ����ˮƽ����ֵ
        /// </summary>
        /// <param name="sliderValue">����ֵ</param>
        public void SetZoomSliderValue(double sliderValue)
        {
            ZoomSliderValue = Math.Max(MinSliderValue, Math.Min(MaxSliderValue, sliderValue));
        }

        /// <summary>
        /// ���ô�ֱ����ֵ
        /// </summary>
        /// <param name="sliderValue">����ֵ</param>
        public void SetVerticalZoomSliderValue(double sliderValue)
        {
            VerticalZoomSliderValue = Math.Max(MinSliderValue, Math.Min(MaxSliderValue, sliderValue));
        }

        /// <summary>
        /// ���õ�Ĭ��ֵ
        /// </summary>
        public void ResetToDefaults()
        {
            _zoom = DefaultZoom;
            _verticalZoom = DefaultVerticalZoom;
            _zoomSliderValue = DefaultSliderValue;
            _verticalZoomSliderValue = DefaultSliderValue;
            
            // ֪ͨ�������Ա仯
            OnPropertyChanged(nameof(Zoom));
            OnPropertyChanged(nameof(VerticalZoom));
            OnPropertyChanged(nameof(ZoomSliderValue));
            OnPropertyChanged(nameof(VerticalZoomSliderValue));
        }

        /// <summary>
        /// ����ˮƽ���� (�Ŵ�)
        /// </summary>
        /// <param name="step">���ӵĲ�����Ĭ��Ϊ10</param>
        public void ZoomIn(double step = 10.0)
        {
            SetZoomSliderValue(_zoomSliderValue + step);
        }

        /// <summary>
        /// ����ˮƽ���� (��С)
        /// </summary>
        /// <param name="step">���ٵĲ�����Ĭ��Ϊ10</param>
        public void ZoomOut(double step = 10.0)
        {
            SetZoomSliderValue(_zoomSliderValue - step);
        }

        /// <summary>
        /// ���Ӵ�ֱ���� (�Ŵ�)
        /// </summary>
        /// <param name="step">���ӵĲ�����Ĭ��Ϊ10</param>
        public void VerticalZoomIn(double step = 10.0)
        {
            SetVerticalZoomSliderValue(_verticalZoomSliderValue + step);
        }

        /// <summary>
        /// ���ٴ�ֱ���� (��С)
        /// </summary>
        /// <param name="step">���ٵĲ�����Ĭ��Ϊ10</param>
        public void VerticalZoomOut(double step = 10.0)
        {
            SetVerticalZoomSliderValue(_verticalZoomSliderValue - step);
        }

        /// <summary>
        /// �ʺϴ��� - �Զ�������������Ӧ����
        /// </summary>
        /// <param name="contentWidth">���ݿ���</param>
        /// <param name="viewportWidth">�ӿڿ���</param>
        public void FitToWindow(double contentWidth, double viewportWidth)
        {
            if (contentWidth > 0 && viewportWidth > 0)
            {
                var optimalZoom = Math.Min(MaxZoom, viewportWidth / contentWidth);
                SetZoom(optimalZoom);
            }
        }

        /// <summary>
        /// �ʺϸ߶� - �Զ�������ֱ��������Ӧ����
        /// </summary>
        /// <param name="contentHeight">���ݸ߶�</param>
        /// <param name="viewportHeight">�ӿڸ߶�</param>
        public void FitToHeight(double contentHeight, double viewportHeight)
        {
            if (contentHeight > 0 && viewportHeight > 0)
            {
                var optimalVerticalZoom = Math.Min(MaxVerticalZoom, viewportHeight / contentHeight);
                SetVerticalZoom(optimalVerticalZoom);
            }
        }
        #endregion

        #region ֻ������
        /// <summary>
        /// �Ƿ���Ĭ������״̬
        /// </summary>
        public bool IsAtDefaultZoom => Math.Abs(_zoom - DefaultZoom) < 0.001 && Math.Abs(_verticalZoom - DefaultVerticalZoom) < 0.001;

        /// <summary>
        /// �Ƿ�����С����״̬
        /// </summary>
        public bool IsAtMinimumZoom => Math.Abs(_zoom - MinZoom) < 0.001 && Math.Abs(_verticalZoom - MinVerticalZoom) < 0.001;

        /// <summary>
        /// �Ƿ����������״̬
        /// </summary>
        public bool IsAtMaximumZoom => Math.Abs(_zoom - MaxZoom) < 0.001 && Math.Abs(_verticalZoom - MaxVerticalZoom) < 0.001;

        /// <summary>
        /// ���ŷ�Χ��Ϣ
        /// </summary>
        public string ZoomRangeInfo => $"ˮƽ: {MinZoom:F1}x - {MaxZoom:F1}x, ��ֱ: {MinVerticalZoom:F1}x - {MaxVerticalZoom:F1}x";

        /// <summary>
        /// ��ǰ����״̬��Ϣ
        /// </summary>
        public string CurrentZoomInfo => $"ˮƽ: {_zoom:F2}x, ��ֱ: {_verticalZoom:F2}x";
        #endregion
    }
}