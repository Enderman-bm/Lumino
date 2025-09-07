using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DominoNext.ViewModels
{
    public partial class TrackViewModel : ViewModelBase
    {
        private int _trackNumber;
        private string _channelName = string.Empty;
        private string _trackName = string.Empty;
        private bool _isMuted;
        private bool _isSolo;
        private bool _isSelected;
        private int _midiChannel = -1; // MIDI通道号，-1表示未指定
        private int _channelIndex = -1; // 在同一MIDI通道中的索引，-1表示未指定
        private bool _isConductorTrack; // 是否为Conductor轨

        public int TrackNumber
        {
            get => _trackNumber;
            set => SetProperty(ref _trackNumber, value);
        }

        public string ChannelName
        {
            get => _channelName;
            set => SetProperty(ref _channelName, value);
        }

        public string TrackName
        {
            get => _trackName;
            set
            {
                // Conductor轨不允许改名
                if (!_isConductorTrack)
                {
                    SetProperty(ref _trackName, value);
                }
            }
        }

        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                // Conductor轨不支持静音
                if (!_isConductorTrack)
                {
                    SetProperty(ref _isMuted, value);
                }
            }
        }

        public bool IsSolo
        {
            get => _isSolo;
            set
            {
                // Conductor轨不支持独奏
                if (!_isConductorTrack)
                {
                    SetProperty(ref _isSolo, value);
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// 是否为Conductor轨（速度轨）
        /// </summary>
        public bool IsConductorTrack
        {
            get => _isConductorTrack;
            set => SetProperty(ref _isConductorTrack, value);
        }

        /// <summary>
        /// MIDI通道号（0-15），-1表示未指定
        /// </summary>
        public int MidiChannel
        {
            get => _midiChannel;
            set
            {
                SetProperty(ref _midiChannel, value);
                // 更新显示的通道名称
                UpdateChannelName();
            }
        }

        /// <summary>
        /// 在同一MIDI通道中的索引（从0开始），-1表示未指定
        /// </summary>
        public int ChannelIndex
        {
            get => _channelIndex;
            set
            {
                SetProperty(ref _channelIndex, value);
                // 更新显示的通道名称
                UpdateChannelName();
            }
        }

        public TrackViewModel(int trackNumber, string channelName, string trackName = "", bool isConductorTrack = false)
        {
            TrackNumber = trackNumber;
            ChannelName = channelName;
            _isConductorTrack = isConductorTrack;
            
            if (isConductorTrack)
            {
                _trackName = "Conductor";
                ChannelName = "COND";
            }
            else
            {
                _trackName = string.IsNullOrEmpty(trackName) ? $"Track {trackNumber}" : trackName;
            }
        }

        public RelayCommand ToggleMuteCommand => new RelayCommand(ToggleMute);
        public RelayCommand ToggleSoloCommand => new RelayCommand(ToggleSolo);
        public RelayCommand SelectTrackCommand => new RelayCommand(SelectTrack);

        private void ToggleMute()
        {
            // Conductor轨不支持静音
            if (_isConductorTrack) return;
            
            IsMuted = !IsMuted;
            // TODO: 实现静音逻辑
        }

        private void ToggleSolo()
        {
            // Conductor轨不支持独奏
            if (_isConductorTrack) return;
            
            IsSolo = !IsSolo;
            // TODO: 实现独奏逻辑
        }

        private void SelectTrack()
        {
            IsSelected = true;
            // TODO: 通知其他组件获取选择
        }

        /// <summary>
        /// 根据MIDI通道号和通道索引更新显示的通道名称
        /// </summary>
        private void UpdateChannelName()
        {
            // Conductor轨保持固定的通道名称
            if (_isConductorTrack)
            {
                ChannelName = "COND";
                return;
            }

            if (_midiChannel >= 0 && _midiChannel <= 15 && _channelIndex >= 0)
            {
                // 如果有有效的MIDI通道号和通道索引，显示为 A1, A2 等格式
                var letter = (char)('A' + (_midiChannel / 16));
                var number = (_midiChannel % 16) + 1;
                ChannelName = $"{letter}{_channelIndex + 1}";
            }
            else if (_midiChannel >= 0 && _midiChannel <= 15)
            {
                // 如果只有MIDI通道号，显示为 CH.1, CH.2 等格式
                ChannelName = $"CH.{_midiChannel + 1}";
            }
            // 否则保持原来的通道名称不变
        }
    }
}