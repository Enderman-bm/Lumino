using CommunityToolkit.Mvvm.ComponentModel;
using DominoNext.Models.Music;
using System;

namespace DominoNext.ViewModels.Editor
{
    public partial class NoteViewModel : ViewModelBase
    {
        // 包装的数据模型
        private readonly Note _note;

        [ObservableProperty] private MusicalFraction _duration;
        [ObservableProperty] private int _velocity = 100;
        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private bool _isPreview;

        // 缓存计算结果以提升性能
        private double _cachedX = double.NaN;
        private double _cachedY = double.NaN;
        private double _cachedWidth = double.NaN;
        private double _cachedHeight = double.NaN;
        private double _lastZoom = double.NaN;
        private double _lastVerticalZoom = double.NaN;
        private double _lastPixelsPerTick = double.NaN;

        public NoteViewModel(Note note)
        {
            _note = note ?? throw new ArgumentNullException(nameof(note));
            // 初始化属性值
            _duration = _note.Duration;
            _velocity = _note.Velocity;
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
                    InvalidateCache();
                }
            }
        }

        // 移除手动定义的Duration和Velocity属性，使用源生成器生成的属性
        // 注意：需要同步模型数据
        
        partial void OnDurationChanged(MusicalFraction value)
        {
            _note.Duration = value;
            InvalidateCache();
        }
        
        partial void OnVelocityChanged(int value)
        {
            _note.Velocity = value;
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

        // 获取底层数据模型
        public Note GetModel() => _note;

        // UI相关方法，使用分数进行计算
        public double GetX(double zoom, double pixelsPerTick)
        {
            var currentKey = zoom * pixelsPerTick;
            if (double.IsNaN(_cachedX) || Math.Abs(_lastZoom - currentKey) > 0.001)
            {
                var startTicks = StartPosition.ToTicks(MusicalFraction.QUARTER_NOTE_TICKS);
                if (double.IsNaN(startTicks) || double.IsInfinity(startTicks))
                {
                    _cachedX = 0;
                }
                else
                {
                    if (Math.Abs(startTicks) < 1e-10)
                    {
                        _cachedX = 0;
                    }
                    else
                    {
                        _cachedX = startTicks * pixelsPerTick * zoom;
                    }
                }
                _lastZoom = currentKey;
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

        public double GetWidth(double zoom, double pixelsPerTick)
        {
            var currentKey = zoom * pixelsPerTick;
            if (double.IsNaN(_cachedWidth) || Math.Abs(_lastPixelsPerTick - currentKey) > 0.001)
            {
                var durationTicks = Duration.ToTicks(MusicalFraction.QUARTER_NOTE_TICKS);
                if (double.IsNaN(durationTicks) || double.IsInfinity(durationTicks) || durationTicks <= 0)
                {
                    _cachedWidth = 4; // 最小宽度
                }
                else
                {
                    _cachedWidth = durationTicks * pixelsPerTick * zoom;
                }
                _lastPixelsPerTick = currentKey;
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

        public void InvalidateCache()
        {
            _cachedX = double.NaN;
            _cachedY = double.NaN;
            _cachedWidth = double.NaN;
            _cachedHeight = double.NaN;
        }


        /// <summary>
        /// 工厂方法：创建四分音符
        /// </summary>
        public static NoteViewModel CreateQuarterNote(int pitch, MusicalFraction startPosition, int velocity = 100)
        {
            var note = Note.CreateQuarterNote(pitch, startPosition, velocity);
            return new NoteViewModel(note);
        }
    }
}