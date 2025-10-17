using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumino.ViewModels.Editor;
using EnderDebugger;

namespace Lumino.ViewModels
{
    /// <summary>
    /// ��������/�����ߴ�ViewModel
    /// ��ʾ�������켰������Ԥ��
    /// </summary>
    public partial class TrackOverviewViewModel : ViewModelBase
    {
        private readonly EnderLogger _logger;

        #region ����

        /// <summary>
        /// ����߶ȣ�ÿ�У�
        /// </summary>
        [ObservableProperty]
        private double _trackHeight = 60.0;

        /// <summary>
        /// �������������ڼ����ܸ߶ȣ�
        /// </summary>
        [ObservableProperty]
        private int _trackCount = 0;

        /// <summary>
        /// ��ǰ�������ƫ����
        /// </summary>
        [ObservableProperty]
        private double _currentScrollOffset = 0.0;

        /// <summary>
        /// ���ż���Ӱ��ʱ������ʾ��
        /// </summary>
        [ObservableProperty]
        private double _zoom = 1.0;

        /// <summary>
        /// ÿ���ķ������Ļ������ȣ����أ�
        /// </summary>
        [ObservableProperty]
        private double _baseQuarterNoteWidth = 60.0;

        /// <summary>
        /// ÿС�ڵ�������Ĭ��4/4�ģ�
        /// </summary>
        [ObservableProperty]
        private int _beatsPerMeasure = 4;

        /// <summary>
        /// ��ʱ�������ķ�����Ϊ��λ��
        /// </summary>
        [ObservableProperty]
        private double _totalDuration = 64.0; // Ĭ��16С��

        /// <summary>
        /// С�ڿ��ȣ��������ԣ�
        /// </summary>
        public double MeasureWidth => BaseQuarterNoteWidth * BeatsPerMeasure * Zoom;

        /// <summary>
        /// ʱ�䵽���ص�ת������
        /// </summary>
        public double TimeToPixelScale => BaseQuarterNoteWidth * Zoom;

        /// <summary>
        /// �ܿ��ȣ����أ�
        /// </summary>
        public double TotalWidth => TotalDuration * TimeToPixelScale;

        /// <summary>
        /// �ܸ߶ȣ����أ�- ��������ĸ߶��ܺ�
        /// </summary>
        public double TotalHeight => TrackCount * TrackHeight;

        /// <summary>
        /// �Ƿ���ԷŴ�
        /// </summary>
        public bool CanZoomIn => Zoom < 4.0;

        /// <summary>
        /// �Ƿ������С
        /// </summary>
        public bool CanZoomOut => Zoom > 0.25;

        #endregion

        #region ���캯��

        public TrackOverviewViewModel()
        {
            _logger = EnderLogger.Instance;
            _logger.Debug("TrackOverviewViewModel", "TrackOverviewViewModel �Ѵ���");
        }

        #endregion

        #region ����

        /// <summary>
        /// Zoom in
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanZoomIn))]
        private void ZoomIn()
        {
            Zoom = Math.Min(4.0, Zoom * 1.2);
            _logger.Debug("TrackOverviewViewModel", $"Zoomed in to {Zoom:F2}x");
        }

        /// <summary>
        /// Zoom out
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanZoomOut))]
        private void ZoomOut()
        {
            Zoom = Math.Max(0.25, Zoom / 1.2);
            _logger.Debug("TrackOverviewViewModel", $"Zoomed out to {Zoom:F2}x");
        }

        /// <summary>
        /// ������������
        /// </summary>
        [RelayCommand]
        private void ResetZoom()
        {
            Zoom = 1.0;
            _logger.Debug("TrackOverviewViewModel", "����������");
        }

        #endregion

        #region ��������

        /// <summary>
        /// ���ù���ƫ����
        /// </summary>
        public void SetScrollOffset(double offset)
        {
            CurrentScrollOffset = Math.Max(0, Math.Min(offset, TotalWidth));
        }

        /// <summary>
        /// ������ʱ��
        /// </summary>
        public void SetTotalDuration(double duration)
        {
            TotalDuration = Math.Max(64.0, duration);
            _logger.Debug("TrackOverviewViewModel", $"������ʱ��: {TotalDuration:F1} �ķ�����");
        }

        /// <summary>
        /// ������������
        /// </summary>
        public void SetTrackCount(int count)
        {
            TrackCount = Math.Max(0, count);
            _logger.Debug("TrackOverviewViewModel", $"������������: {TrackCount}, �ܸ߶�: {TotalHeight}");
        }

        #endregion

        #region ���Ա仯����

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            
            // ��ĳЩ���Ա仯ʱ��֪ͨ��������
            if (e.PropertyName == nameof(Zoom) ||
                e.PropertyName == nameof(BaseQuarterNoteWidth) ||
                e.PropertyName == nameof(BeatsPerMeasure))
            {
                OnPropertyChanged(nameof(MeasureWidth));
                OnPropertyChanged(nameof(TimeToPixelScale));
                OnPropertyChanged(nameof(TotalWidth));
            }

            if (e.PropertyName == nameof(Zoom))
            {
                OnPropertyChanged(nameof(CanZoomIn));
                OnPropertyChanged(nameof(CanZoomOut));
            }

            if (e.PropertyName == nameof(TotalDuration))
            {
                OnPropertyChanged(nameof(TotalWidth));
            }

            if (e.PropertyName == nameof(TrackCount) || e.PropertyName == nameof(TrackHeight))
            {
                OnPropertyChanged(nameof(TotalHeight));
            }
        }

        #endregion
    }
}
