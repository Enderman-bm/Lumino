using CommunityToolkit.Mvvm.ComponentModel;
using Lumino.Models.Music;
using Lumino.Services.Interfaces;
using System;
using EnderDebugger;
using Avalonia;
using System.Runtime.InteropServices;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// 音符视图模型 - 符合MVVM最佳实践
    /// 包装Note数据模型，提供UI绑定属性
    /// 优化版本：移除预缓存结构体，按需计算坐标
    /// </summary>
    public partial class NoteViewModel : ViewModelBase
    {
        #region 常量定义
        private const double MinNoteWidth = 4.0; // 最小音符宽度
        #endregion

        #region 服务依赖
        private readonly IMidiConversionService _midiConverter;
        #endregion

        #region 私有字段
        // 包装的数据模型
        private readonly Note _note;
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
            return new Lumino.Services.Implementation.MidiConversionService();
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

        /// <summary>
        /// MIDI通道 (0-15)
        /// </summary>
        public int MidiChannel
        {
            get => _note.MidiChannel;
            set
            {
                if (_note.MidiChannel != value)
                {
                    _note.MidiChannel = value;
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
                    EnderLogger.Instance.LogException(ex, "NoteViewModel", $"设置StartTime错误, 值: {value}");
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
                    EnderLogger.Instance.LogException(ex, "NoteViewModel", $"设置DurationInTicks错误, 值: {value}");
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

        #region UI计算方法 - 按需计算，无缓存
        /// <summary>
        /// 获取音符在界面上的X坐标
        /// </summary>
        /// <param name="baseQuarterNoteWidth">四分音符的像素宽度</param>
        /// <returns>X坐标</returns>
        public double GetX(double baseQuarterNoteWidth)
        {
            var startValue = StartPosition.ToDouble();
            if (double.IsNaN(startValue) || double.IsInfinity(startValue))
                return 0;
            return startValue * baseQuarterNoteWidth;
        }

        /// <summary>
        /// 获取音符在界面上的Y坐标
        /// </summary>
        /// <param name="keyHeight">每个键的高度</param>
        /// <returns>Y坐标</returns>
        public double GetY(double keyHeight)
        {
            return (127 - Pitch) * keyHeight;
        }

        /// <summary>
        /// 获取音符在界面上的宽度
        /// </summary>
        /// <param name="baseQuarterNoteWidth">四分音符的像素宽度</param>
        /// <returns>宽度</returns>
        public double GetWidth(double baseQuarterNoteWidth)
        {
            var durationValue = Duration.ToDouble();
            if (double.IsNaN(durationValue) || double.IsInfinity(durationValue) || durationValue <= 0)
                return MinNoteWidth;
            return Math.Max(MinNoteWidth, durationValue * baseQuarterNoteWidth);
        }

        /// <summary>
        /// 获取音符在界面上的高度
        /// </summary>
        /// <param name="keyHeight">每个键的高度</param>
        /// <returns>高度</returns>
        public double GetHeight(double keyHeight)
        {
            return keyHeight;
        }

        /// <summary>
        /// 获取音符的屏幕矩形（一次性计算）
        /// </summary>
        public Rect GetScreenRect(double baseQuarterNoteWidth, double keyHeight, double scrollX, double scrollY)
        {
            return new Rect(
                GetX(baseQuarterNoteWidth) - scrollX,
                GetY(keyHeight) - scrollY,
                GetWidth(baseQuarterNoteWidth),
                GetHeight(keyHeight)
            );
        }

        /// <summary>
        /// 检查音符是否在视口内可见
        /// </summary>
        public bool IsVisibleInViewport(Rect viewport, double baseQuarterNoteWidth, double keyHeight, double scrollX, double scrollY)
        {
            var screenRect = GetScreenRect(baseQuarterNoteWidth, keyHeight, scrollX, scrollY);
            return screenRect.Intersects(viewport);
        }
        #endregion

        #region 兼容性方法 - 使用旧的TimeToPixelScale参数
        private const int StandardTicksPerQuarter = 96;

        /// <summary>
        /// 获取X坐标 - 兼容性方法
        /// 建议使用GetX(double baseQuarterNoteWidth)方法
        /// </summary>
        [Obsolete("建议使用GetX(double baseQuarterNoteWidth)方法")]
        public double GetX(double timeToPixelScale, bool isLegacyCall)
        {
            if (isLegacyCall)
            {
                var estimatedBaseWidth = timeToPixelScale * StandardTicksPerQuarter / 4;
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
                var estimatedBaseWidth = timeToPixelScale * StandardTicksPerQuarter / 4;
                return GetWidth(estimatedBaseWidth);
            }
            return GetWidth(timeToPixelScale);
        }
        #endregion

        #region 缓存管理（已废弃，保留空实现以兼容）
        /// <summary>
        /// 使缓存失效 - 已废弃，现在按需计算不再缓存
        /// </summary>
        [Obsolete("现在按需计算坐标，不再缓存")]
        public void InvalidateCache() { }

        /// <summary>
        /// 清除坐标缓存 - 已废弃，现在按需计算不再缓存
        /// </summary>
        [Obsolete("现在按需计算坐标，不再缓存")]
        public void InvalidateCoordinateCache() { }
        #endregion
    }
}