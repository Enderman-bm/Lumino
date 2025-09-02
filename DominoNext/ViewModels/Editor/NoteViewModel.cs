using CommunityToolkit.Mvvm.ComponentModel;
using DominoNext.Models.Music;
using DominoNext.Services.Interfaces;
using DominoNext.Services.Implementation;
using System;

namespace DominoNext.ViewModels.Editor
{
    public partial class NoteViewModel : ViewModelBase
    {
        // 包装的数据模型
        private readonly Note _note;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isPreview;

        // 缓存计算结果以提升性能 - 优化版本
        private double _cachedX = double.NaN;
        private double _cachedY = double.NaN;
        private double _cachedWidth = double.NaN;
        private double _cachedHeight = double.NaN;
        private double _lastTimeToPixelScale = double.NaN;
        private double _lastVerticalZoom = double.NaN;

        // MIDI转换服务，仅用于兼容性接口
        private static readonly IMidiConversionService _midiConverter = new MidiConversionService();

        public NoteViewModel(Note note)
        {
            _note = note ?? throw new ArgumentNullException(nameof(note));
        }

        public NoteViewModel() : this(new Note()) { }

        // 公开属性，直接访问模型数据
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

        // 兼容性属性 - 使用MIDI转换服务
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

        // 获取底层数据模型
        public Note GetModel() => _note;

        // 基于分数的UI计算方法 - 新实现
        public double GetX(double baseQuarterNoteWidth)
        {
            if (double.IsNaN(_cachedX) || Math.Abs(_lastTimeToPixelScale - baseQuarterNoteWidth) > 0.001)
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
                    if (Math.Abs(startValue) < 1e-10)
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

        public double GetY(double keyHeight)
        {
            if (double.IsNaN(_cachedY) || Math.Abs(_lastVerticalZoom - keyHeight) > 0.001)
            {
                _cachedY = (127 - Pitch) * keyHeight;
                _lastVerticalZoom = keyHeight;
            }
            return _cachedY;
        }

        public double GetWidth(double baseQuarterNoteWidth)
        {
            if (double.IsNaN(_cachedWidth) || Math.Abs(_lastTimeToPixelScale - baseQuarterNoteWidth) > 0.001)
            {
                var durationValue = Duration.ToDouble();
                // 添加安全检查
                if (double.IsNaN(durationValue) || double.IsInfinity(durationValue) || durationValue <= 0)
                {
                    _cachedWidth = 4; // 最小宽度
                }
                else
                {
                    _cachedWidth = durationValue * baseQuarterNoteWidth;
                }
                _lastTimeToPixelScale = baseQuarterNoteWidth;
            }
            return _cachedWidth;
        }

        public double GetHeight(double keyHeight)
        {
            if (double.IsNaN(_cachedHeight) || Math.Abs(_lastVerticalZoom - keyHeight) > 0.001)
            {
                _cachedHeight = keyHeight;
                _lastVerticalZoom = keyHeight;
            }
            return _cachedHeight;
        }

        // 兼容性方法 - 使用旧的TimeToPixelScale参数
        [Obsolete("建议使用GetX(double baseQuarterNoteWidth)方法")]
        public double GetX(double timeToPixelScale, bool isLegacyCall)
        {
            if (isLegacyCall)
            {
                // 为了兼容性，将timeToPixelScale转换为baseQuarterNoteWidth
                // 假设原来的TimeToPixelScale是基于96 ticks/quarter的
                var estimatedBaseWidth = timeToPixelScale * 96 / 4; // 简化转换
                return GetX(estimatedBaseWidth);
            }
            return GetX(timeToPixelScale);
        }

        [Obsolete("建议使用GetWidth(double baseQuarterNoteWidth)方法")]
        public double GetWidth(double timeToPixelScale, bool isLegacyCall)
        {
            if (isLegacyCall)
            {
                // 为了兼容性，将timeToPixelScale转换为baseQuarterNoteWidth
                var estimatedBaseWidth = timeToPixelScale * 96 / 4; // 简化转换
                return GetWidth(estimatedBaseWidth);
            }
            return GetWidth(timeToPixelScale);
        }

        public void InvalidateCache()
        {
            _cachedX = double.NaN;
            _cachedY = double.NaN;
            _cachedWidth = double.NaN;
            _cachedHeight = double.NaN;
        }
    }
}