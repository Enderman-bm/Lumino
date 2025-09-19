using CommunityToolkit.Mvvm.ComponentModel;
using Lumino.Models.Music;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Base;
using System;
using Avalonia;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// 重构后的音符视图模型 - 使用新的缓存管理器和增强基类
    /// 减少重复的缓存管理代码，提高性能和可维护性
    /// </summary>
    public partial class NoteViewModel : ViewModelBase
    {
        #region 常量定义
        private const double MinNoteWidth = 4.0; // 最小音符宽度
        private const int StandardTicksPerQuarter = 96; // 标准四分音符Tick数，用于兼容性转换
        private const double ToleranceValue = 1e-10; // 浮点数比较容差
        #endregion

        #region 服务依赖
        private readonly IMidiConversionService _midiConverter;
        #endregion

        #region 私有字段
        // 包装的数据模型
        private readonly Note _note;

        // 使用新的缓存管理器替代重复的缓存代码
        private readonly UiCalculationCacheManager _cache = new();
        
        // 屏幕矩形缓存 - 单独处理，因为返回类型不同
        private Rect? _cachedScreenRect;
        private double _cachedForScrollX = double.NaN;
        private double _cachedForScrollY = double.NaN;
        private double _cachedForTimeScale = double.NaN;
        private double _cachedForKeyHeight = double.NaN;
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
        /// 设计时构造函数 - 使用统一的设计时服务提供者
        /// </summary>
        public NoteViewModel() : this(new Note(), GetDesignTimeService<IMidiConversionService>())
        {
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

        #region UI计算方法 - 使用新的缓存管理器
        /// <summary>
        /// 获取音符在界面上的X坐标 - 使用缓存管理器
        /// </summary>
        /// <param name="baseQuarterNoteWidth">四分音符的像素宽度</param>
        /// <returns>X坐标</returns>
        public double GetX(double baseQuarterNoteWidth)
        {
            return _cache.GetOrCalculateX(
                parameters => CalculateX(parameters[0]), 
                baseQuarterNoteWidth);
        }

        /// <summary>
        /// 获取音符在界面上的Y坐标 - 使用缓存管理器
        /// </summary>
        /// <param name="keyHeight">每个键的高度</param>
        /// <returns>Y坐标</returns>
        public double GetY(double keyHeight)
        {
            return _cache.GetOrCalculateY(
                parameters => CalculateY(parameters[0]), 
                keyHeight);
        }

        /// <summary>
        /// 获取音符在界面上的宽度 - 使用缓存管理器
        /// </summary>
        /// <param name="baseQuarterNoteWidth">四分音符的像素宽度</param>
        /// <returns>宽度</returns>
        public double GetWidth(double baseQuarterNoteWidth)
        {
            return _cache.GetOrCalculateWidth(
                parameters => CalculateWidth(parameters[0]), 
                baseQuarterNoteWidth);
        }

        /// <summary>
        /// 获取音符在界面上的高度 - 使用缓存管理器
        /// </summary>
        /// <param name="keyHeight">每个键的高度</param>
        /// <returns>高度</returns>
        public double GetHeight(double keyHeight)
        {
            return _cache.GetOrCalculateHeight(
                parameters => keyHeight, 
                keyHeight);
        }
        #endregion

        #region 私有计算方法
        /// <summary>
        /// 计算X坐标的实际逻辑
        /// </summary>
        private double CalculateX(double baseQuarterNoteWidth)
        {
            var startValue = StartPosition.ToDouble();
            
            // 添加安全检查
            if (double.IsNaN(startValue) || double.IsInfinity(startValue))
            {
                return 0;
            }

            // 修复：对于startValue为0的情况，确保x位置也是0
            if (Math.Abs(startValue) < ToleranceValue)
            {
                return 0;
            }

            return startValue * baseQuarterNoteWidth;
        }

        /// <summary>
        /// 计算Y坐标的实际逻辑
        /// </summary>
        private double CalculateY(double keyHeight)
        {
            return (127 - Pitch) * keyHeight;
        }

        /// <summary>
        /// 计算宽度的实际逻辑
        /// </summary>
        private double CalculateWidth(double baseQuarterNoteWidth)
        {
            var durationValue = Duration.ToDouble();
            
            // 添加安全检查
            if (double.IsNaN(durationValue) || double.IsInfinity(durationValue) || durationValue <= 0)
            {
                return MinNoteWidth; // 最小宽度
            }

            return Math.Max(durationValue * baseQuarterNoteWidth, MinNoteWidth);
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

        #region 缓存管理 - 处理屏幕矩形缓存
        /// <summary>
        /// 使缓存失效，强制重新计算位置和尺寸
        /// </summary>
        public void InvalidateCache()
        {
            _cache.InvalidateAllCache();
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

        #region 资源清理
        /// <summary>
        /// 释放特定资源
        /// </summary>
        protected override void DisposeCore()
        {
            // 清理缓存
            _cache.InvalidateAllCache();
            _cachedScreenRect = null;
        }
        #endregion
    }
}