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
    /// 音轨总览/工程走带ViewModel
    /// 显示所有音轨及其音符预览
    /// </summary>
    public partial class TrackOverviewViewModel : ViewModelBase
    {
        private readonly EnderLogger _logger;

        #region 属性

        /// <summary>
        /// 音轨高度（每行）
        /// </summary>
        [ObservableProperty]
        private double _trackHeight = 60.0;

        /// <summary>
        /// 音轨数量（用于计算总高度）
        /// </summary>
        [ObservableProperty]
        private int _trackCount = 0;

        /// <summary>
        /// 当前横向滚动偏移量
        /// </summary>
        [ObservableProperty]
        private double _currentScrollOffset = 0.0;

        /// <summary>
        /// 缩放级别（影响时间轴显示）
        /// </summary>
        [ObservableProperty]
        private double _zoom = 1.0;

        /// <summary>
        /// 每个四分音符的基础宽度（像素）
        /// </summary>
        [ObservableProperty]
        private double _baseQuarterNoteWidth = 60.0;

        /// <summary>
        /// 每小节的拍数（默认4/4拍）
        /// </summary>
        [ObservableProperty]
        private int _beatsPerMeasure = 4;

        /// <summary>
        /// 总时长（以四分音符为单位）
        /// </summary>
        [ObservableProperty]
        private double _totalDuration = 64.0; // 默认16小节

        /// <summary>
        /// 小节宽度（计算属性）
        /// </summary>
        public double MeasureWidth => _baseQuarterNoteWidth * _beatsPerMeasure * _zoom;

        /// <summary>
        /// 时间到像素的转换比例
        /// </summary>
        public double TimeToPixelScale => _baseQuarterNoteWidth * _zoom;

        /// <summary>
        /// 总宽度（像素）
        /// </summary>
        public double TotalWidth => _totalDuration * TimeToPixelScale;

        /// <summary>
        /// 总高度（像素）- 所有音轨的高度总和
        /// </summary>
        public double TotalHeight => _trackCount * _trackHeight;

        /// <summary>
        /// 是否可以放大
        /// </summary>
        public bool CanZoomIn => _zoom < 4.0;

        /// <summary>
        /// 是否可以缩小
        /// </summary>
        public bool CanZoomOut => _zoom > 0.25;

        #endregion

        #region 构造函数

        public TrackOverviewViewModel()
        {
            _logger = EnderLogger.Instance;
            _logger.Debug("TrackOverviewViewModel", "TrackOverviewViewModel 已创建");
        }

        #endregion

        #region 命令

        /// <summary>
        /// 放大命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanZoomIn))]
        private void ZoomIn()
        {
            Zoom = Math.Min(4.0, Zoom * 1.2);
            _logger.Debug("TrackOverviewViewModel", $"放大至 {Zoom:F2}x");
        }

        /// <summary>
        /// 缩小命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanZoomOut))]
        private void ZoomOut()
        {
            Zoom = Math.Max(0.25, Zoom / 1.2);
            _logger.Debug("TrackOverviewViewModel", $"缩小至 {Zoom:F2}x");
        }

        /// <summary>
        /// 重置缩放命令
        /// </summary>
        [RelayCommand]
        private void ResetZoom()
        {
            Zoom = 1.0;
            _logger.Debug("TrackOverviewViewModel", "缩放已重置");
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置滚动偏移量
        /// </summary>
        public void SetScrollOffset(double offset)
        {
            CurrentScrollOffset = Math.Max(0, Math.Min(offset, TotalWidth));
        }

        /// <summary>
        /// 设置总时长
        /// </summary>
        public void SetTotalDuration(double duration)
        {
            TotalDuration = Math.Max(64.0, duration);
            _logger.Debug("TrackOverviewViewModel", $"设置总时长: {TotalDuration:F1} 四分音符");
        }

        /// <summary>
        /// 设置音轨数量
        /// </summary>
        public void SetTrackCount(int count)
        {
            TrackCount = Math.Max(0, count);
            _logger.Debug("TrackOverviewViewModel", $"设置音轨数量: {TrackCount}, 总高度: {TotalHeight}");
        }

        #endregion

        #region 属性变化处理

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            
            // 当某些属性变化时，通知依赖属性
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
