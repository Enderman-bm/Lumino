using CommunityToolkit.Mvvm.ComponentModel;
using DominoNext.Models.Music;
using DominoNext.Services.Interfaces;
using System;
using Avalonia;

namespace DominoNext.ViewModels.Editor
{
    /// <summary>
    /// 音符视图模型 - 符合MVVM最佳实践
    /// 包装Note数据模型，提供UI绑定属性和缓存优化
    /// </summary>
    public partial class NoteViewModel : ViewModelBase
    {
        #region 常量定义
        private const double MinNoteWidth = 4.0; // 最小音符宽度
        private const double CacheInvalidValue = double.NaN; // 缓存失效标记值
        private const double ToleranceValue = 1e-10; // 浮点数比较容差
        private const int StandardTicksPerQuarter = 96; // 标准四分音符Tick数，用于兼容性转换
        #endregion

        #region 服务依赖
        private readonly IMidiConversionService _midiConverter;
        #endregion

        #region 私有字段
        // 包装的数据模型
        private readonly Note _note;

        // 缓存计算结果以提升性能
        private double _cachedX = CacheInvalidValue;
        private double _cachedY = CacheInvalidValue;
        private double _cachedWidth = CacheInvalidValue;
        private double _cachedHeight = CacheInvalidValue;
        private double _lastTimeToPixelScale = CacheInvalidValue;
        private double _lastVerticalZoom = CacheInvalidValue;

        // 屏幕矩形缓存 - 考虑滚动偏移
        private Rect? _cachedScreenRect;
        private double _cachedForScrollX = CacheInvalidValue;
        private double _cachedForScrollY = CacheInvalidValue;
        private double _cachedForTimeScale = CacheInvalidValue;
        private double _cachedForKeyHeight = CacheInvalidValue;
        #endregion

        #region 可观察属性
        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isPreview;
        #endregion

        #region 构造函数
        /// <summary>
        /// 主构造函数 - 通过依赖注入获取所需服务
        /// </summary>
        /// <param name="note">音符数据模型</param>
        /// <param name="midiConverter">MIDI转换服务</param>
        public NoteViewModel(Note note, IMidiConversionService midiConverter)
        {
            _note = note ?? throw new ArgumentNullException(nameof(note));
            _midiConverter = midiConverter ?? throw new ArgumentNullException(nameof(midiConverter));
        }

        /// <summary>
        /// 简化构造函数 - 使用默认音符和注入的服务
        /// </summary>
        /// <param name="midiConverter">MIDI转换服务</param>
        public NoteViewModel(IMidiConversionService midiConverter) : this(new Note(), midiConverter)
        {
        }

        /// <summary>
        /// 设计时构造函数 - 仅用于XAML设计器
        /// 生产环境应该通过依赖注入容器获取服务实例
        /// </summary>
        public NoteViewModel() : this(new Note(), CreateDesignTimeMidiConverter())
        {
        }

        /// <summary>
        /// 创建设计时使用的MIDI转换服务
        /// </summary>
        private static IMidiConversionService CreateDesignTimeMidiConverter()
        {
            // 仅用于设计时，避免在生产环境中调用
            return new DominoNext.Services.Implementation.MidiConversionService();
        }
        #endregion

        #region 核心属性 - 直接访问模型数据
        public Guid Id => _note.Id;

        public int Pitch
        {
            get => _note.Pitch;
            set
            {
                if (_note.Pitch != value)
                {
                    _note.Pitch = value;
                    OnPropertyChanged();
                    InvalidateCache();
                }
            }
        }

        public MusicalFraction StartPosition
        {
            get => _note.StartPosition;
            set
            {
                if (_note.StartPosition != value)
                {
                    _note.StartPosition = value;
                    OnPropertyChanged();
#pragma warning disable CS0618 // 兼容性通知需要保留
                    OnPropertyChanged(nameof(StartTime)); // 兼容性通知
#pragma warning restore CS0618
                    InvalidateCache();
                }
            }
        }

        public MusicalFraction Duration
        {
            get => _note.Duration;
            set
            {
                if (_note.Duration != value)
                {
                    _note.Duration = value;
                    OnPropertyChanged();
#pragma warning disable CS0618 // 兼容性通知需要保留
                    OnPropertyChanged(nameof(DurationInTicks)); // 兼容性通知
#pragma warning restore CS0618
                    InvalidateCache();
                }
            }
        }

        public int Velocity
        {
            get => _note.Velocity;
            set
            {
                if (_note.Velocity != value)
                {
                    _note.Velocity = value;
                    OnPropertyChanged();
                }
            }
        }

        public int TrackIndex
        {
            get => _note.TrackIndex;
            set
            {
                if (_note.TrackIndex != value)
                {
                    _note.TrackIndex = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? Lyric
        {
            get => _note.Lyric;
            set
            {
                if (_note.Lyric != value)
                {
                    _note.Lyric = value;
                    OnPropertyChanged();
                }
            }
        }
        #endregion

        #region 兼容性属性 - 使用MIDI转换服务
        /// <summary>
        /// 开始时间（Tick单位） - 兼容性属性
        /// 建议使用StartPosition属性。此属性仅为兼容性保留，在MIDI导入导出时使用。
        /// </summary>
        [Obsolete("建议使用StartPosition属性。此属性仅为兼容性保留，在MIDI导入导出时使用。")]
        public double StartTime
        {
            get => _midiConverter.ConvertToTicks(StartPosition);
            set
            {
                try
                {
                    // 添加安全检查，避免无效值
                    if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                    {
                        return; // 忽略无效值
                    }

                    var newPosition = _midiConverter.ConvertFromTicks(value);
                    StartPosition = newPosition;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"设置StartTime错误: {ex.Message}, 值: {value}");
                    // 发生错误时不更新位置
                }
            }
        }

        /// <summary>
        /// 持续时间（Tick单位） - 兼容性属性
        /// 建议使用Duration属性。此属性仅为兼容性保留，在MIDI导入导出时使用。
        /// </summary>
        [Obsolete("建议使用Duration属性。此属性仅为兼容性保留，在MIDI导入导出时使用。")]
        public double DurationInTicks
        {
            get => _midiConverter.ConvertToTicks(Duration);
            set
            {
                try
                {
                    if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                    {
                        return; // 忽略无效值
                    }

                    var newDuration = _midiConverter.ConvertFromTicks(value);
                    Duration = newDuration;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"设置DurationInTicks错误: {ex.Message}, 值: {value}");
                }
            }
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 获取底层数据模型
        /// </summary>
        public Note GetModel() => _note;
        
        /// <summary>
        /// 创建底层数据模型的副本
        /// </summary>
        /// <returns>新的Note对象</returns>
        public Note ToNoteModel()
        {
            return new Note
            {
                Id = this.Id,
                Pitch = this.Pitch,
                StartPosition = this.StartPosition,
                Duration = this.Duration,
                Velocity = this.Velocity,
                Lyric = this.Lyric,
                TrackIndex = this.TrackIndex
            };
        }
        #endregion

        #region UI计算方法 - 基于分数的新实现
        /// <summary>
        /// 获取音符在界面上的X坐标
        /// </summary>
        /// <param name="baseQuarterNoteWidth">四分音符的像素宽度</param>
        /// <returns>X坐标</returns>
        public double GetX(double baseQuarterNoteWidth)
        {
            if (double.IsNaN(_cachedX) || Math.Abs(_lastTimeToPixelScale - baseQuarterNoteWidth) > ToleranceValue)
            {
                var startValue = StartPosition.ToDouble();
                // 添加安全检查
                if (double.IsNaN(startValue) || double.IsInfinity(startValue))
                {
                    _cachedX = 0;
                }
                else
                {
                    // 修复：对于startValue为0的情况，确保x位置也是0
                    if (Math.Abs(startValue) < ToleranceValue)
                    {
                        _cachedX = 0;
                    }
                    else
                    {
                        _cachedX = startValue * baseQuarterNoteWidth;
                    }
                }
                _lastTimeToPixelScale = baseQuarterNoteWidth;
            }
            return _cachedX;
        }

        /// <summary>
        /// 获取音符在界面上的Y坐标
        /// </summary>
        /// <param name="keyHeight">每个键的高度</param>
        /// <returns>Y坐标</returns>
        public double GetY(double keyHeight)
        {
            if (double.IsNaN(_cachedY) || Math.Abs(_lastVerticalZoom - keyHeight) > ToleranceValue)
            {
                _cachedY = (127 - Pitch) * keyHeight;
                _lastVerticalZoom = keyHeight;
            }
            return _cachedY;
        }

        /// <summary>
        /// 获取音符在界面上的宽度
        /// </summary>
        /// <param name="baseQuarterNoteWidth">四分音符的像素宽度</param>
        /// <returns>宽度</returns>
        public double GetWidth(double baseQuarterNoteWidth)
        {
            if (double.IsNaN(_cachedWidth) || Math.Abs(_lastTimeToPixelScale - baseQuarterNoteWidth) > ToleranceValue)
            {
                var durationValue = Duration.ToDouble();
                // 添加安全检查
                if (double.IsNaN(durationValue) || double.IsInfinity(durationValue) || durationValue <= 0)
                {
                    _cachedWidth = MinNoteWidth; // 最小宽度
                }
                else
                {
                    _cachedWidth = durationValue * baseQuarterNoteWidth;
                }
                _lastTimeToPixelScale = baseQuarterNoteWidth;
            }
            return _cachedWidth;
        }

        /// <summary>
        /// 获取音符在界面上的高度
        /// </summary>
        /// <param name="keyHeight">每个键的高度</param>
        /// <returns>高度</returns>
        public double GetHeight(double keyHeight)
        {
            if (double.IsNaN(_cachedHeight) || Math.Abs(_lastVerticalZoom - keyHeight) > ToleranceValue)
            {
                _cachedHeight = keyHeight;
                _lastVerticalZoom = keyHeight;
            }
            return _cachedHeight;
        }
        #endregion

        #region 兼容性方法 - 使用旧的TimeToPixelScale参数
        /// <summary>
        /// 获取X坐标 - 兼容性方法
        /// 建议使用GetX(double baseQuarterNoteWidth)方法
        /// </summary>
        [Obsolete("建议使用GetX(double baseQuarterNoteWidth)方法")]
        public double GetX(double timeToPixelScale, bool isLegacyCall)
        {
            if (isLegacyCall)
            {
                // 为了兼容性，将timeToPixelScale转换为baseQuarterNoteWidth
                // 假设原来的TimeToPixelScale是基于96 ticks/quarter的
                var estimatedBaseWidth = timeToPixelScale * StandardTicksPerQuarter / 4; // 简化转换
                return GetX(estimatedBaseWidth);
            }
            return GetX(timeToPixelScale);
        }

        /// <summary>
        /// 获取宽度 - 兼容性方法
        /// 建议使用GetWidth(double baseQuarterNoteWidth)方法
        /// </summary>
        [Obsolete("建议使用GetWidth(double baseQuarterNoteWidth)方法")]
        public double GetWidth(double timeToPixelScale, bool isLegacyCall)
        {
            if (isLegacyCall)
            {
                // 为了兼容性，将timeToPixelScale转换为baseQuarterNoteWidth
                var estimatedBaseWidth = timeToPixelScale * StandardTicksPerQuarter / 4; // 简化转换
                return GetWidth(estimatedBaseWidth);
            }
            return GetWidth(timeToPixelScale);
        }
        #endregion

        #region 缓存管理
        /// <summary>
        /// 使缓存失效，强制重新计算位置和尺寸
        /// </summary>
        public void InvalidateCache()
        {
            _cachedX = CacheInvalidValue;
            _cachedY = CacheInvalidValue;
            _cachedWidth = CacheInvalidValue;
            _cachedHeight = CacheInvalidValue;
            _cachedScreenRect = null;
        }

        /// <summary>
        /// 获取缓存的屏幕矩形（考虑滚动偏移）
        /// </summary>
        /// <param name="timeToPixelScale">时间到像素的缩放比例</param>
        /// <param name="keyHeight">键高</param>
        /// <param name="scrollX">水平滚动偏移</param>
        /// <param name="scrollY">垂直滚动偏移</param>
        /// <returns>屏幕矩形，如果缓存无效则返回null</returns>
        public Rect? GetCachedScreenRect(double timeToPixelScale, double keyHeight, double scrollX, double scrollY)
        {
            if (_cachedScreenRect.HasValue &&
                Math.Abs(_cachedForTimeScale - timeToPixelScale) < ToleranceValue &&
                Math.Abs(_cachedForKeyHeight - keyHeight) < ToleranceValue &&
                Math.Abs(_cachedForScrollX - scrollX) < ToleranceValue &&
                Math.Abs(_cachedForScrollY - scrollY) < ToleranceValue)
            {
                return _cachedScreenRect.Value;
            }
            return null;
        }

        /// <summary>
        /// 设置缓存的屏幕矩形
        /// </summary>
        /// <param name="rect">屏幕矩形</param>
        /// <param name="timeToPixelScale">时间到像素的缩放比例</param>
        /// <param name="keyHeight">键高</param>
        /// <param name="scrollX">水平滚动偏移</param>
        /// <param name="scrollY">垂直滚动偏移</param>
        public void SetCachedScreenRect(Rect rect, double timeToPixelScale, double keyHeight, double scrollX, double scrollY)
        {
            _cachedScreenRect = rect;
            _cachedForTimeScale = timeToPixelScale;
            _cachedForKeyHeight = keyHeight;
            _cachedForScrollX = scrollX;
            _cachedForScrollY = scrollY;
        }

        /// <summary>
        /// 检查矩形缓存是否仍然有效
        /// </summary>
        /// <param name="timeToPixelScale">时间到像素的缩放比例</param>
        /// <param name="keyHeight">键高</param>
        /// <param name="scrollX">水平滚动偏移</param>
        /// <param name="scrollY">垂直滚动偏移</param>
        /// <returns>缓存是否有效</returns>
        public bool IsScreenRectCacheValid(double timeToPixelScale, double keyHeight, double scrollX, double scrollY)
        {
            return _cachedScreenRect.HasValue &&
                   Math.Abs(_cachedForTimeScale - timeToPixelScale) < ToleranceValue &&
                   Math.Abs(_cachedForKeyHeight - keyHeight) < ToleranceValue &&
                   Math.Abs(_cachedForScrollX - scrollX) < ToleranceValue &&
                   Math.Abs(_cachedForScrollY - scrollY) < ToleranceValue;
        }
        #endregion
    }
}