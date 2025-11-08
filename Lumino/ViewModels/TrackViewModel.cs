using System.ComponentModel;
using System.Linq;
// Lumino - 音轨视图模型，管理单个音轨的状态与属性。
// 全局注释：本文件为音轨 MVVM 逻辑，禁止随意更改关键属性，不然末影君锤爆你！
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Lumino.ViewModels
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
    private int _channelGroupIndex = -1; // 组索引：0->A,1->B...
    private int _channelNumberInGroup = -1; // 组内编号（从1开始），-1表示未指定
        private bool _isConductorTrack; // 是否为Conductor轨
        private bool _isOnionSkinEnabled; // 是否启用洋葱皮显示
        
        /// <summary>
        /// TrackSelector的引用，用于访问Toolbar
        /// </summary>
        public TrackSelectorViewModel? TrackSelector { get; set; }

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
        /// 是否启用洋葱皮显示
        /// </summary>
        public bool IsOnionSkinEnabled
        {
            get => _isOnionSkinEnabled;
            set
            {
                if (SetProperty(ref _isOnionSkinEnabled, value))
                {
                    // 当音轨洋葱皮状态改变时，通知TrackSelector触发事件
                    TrackSelector?.NotifyOnionSkinTrackStateChanged();
                }
            }
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

        /// <summary>
        /// 用户可选的通道组索引（0->A,1->B...），用于手动设置通道组
        /// </summary>
        public int ChannelGroupIndex
        {
            get => _channelGroupIndex;
            set
            {
                if (SetProperty(ref _channelGroupIndex, value))
                {
                    // 如果用户手动设置组索引且组内编号已设置，则更新 ChannelIndex
                    if (_channelNumberInGroup > 0)
                    {
                        ChannelIndex = _channelNumberInGroup - 1;
                    }
                    UpdateChannelName();
                }
            }
        }

        /// <summary>
        /// 用户可选的组内编号（1-16），用于手动设置通道号
        /// </summary>
        public int ChannelNumberInGroup
        {
            get => _channelNumberInGroup;
            set
            {
                if (SetProperty(ref _channelNumberInGroup, value))
                {
                    // 更新 ChannelIndex（内部使用从0开始）
                    if (value > 0)
                        ChannelIndex = value - 1;
                    UpdateChannelName();
                }
            }
        }

        public TrackViewModel(int trackNumber, string channelName, string trackName = "", bool isConductorTrack = false)
        {
            TrackNumber = trackNumber;
            ChannelName = channelName;
            _isConductorTrack = isConductorTrack;
                EnderDebugger.EnderLogger.Instance.Info("TrackViewModel", $"[EnderDebugger][2025-10-02 18:41:03.114][EnderLogger][TrackViewModel]音轨ViewModel已创建，编号:{trackNumber}, 通道:{channelName}, 名称:{trackName}, 是否Conductor:{isConductorTrack}");
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
        public RelayCommand ToggleOnionSkinCommand => new RelayCommand(ToggleOnionSkin);

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

        private void ToggleOnionSkin()
        {
            // Conductor轨不支持洋葱皮
            if (_isConductorTrack) return;

            // 只切换当前音轨的洋葱皮状态，不影响全局开关
            IsOnionSkinEnabled = !IsOnionSkinEnabled;
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

            // 如果用户手动指定了组与组内编号，优先使用用户设置
            if (_channelGroupIndex >= 0 && _channelNumberInGroup > 0)
            {
                var letter = (char)('A' + _channelGroupIndex);
                ChannelName = $"{letter}{_channelNumberInGroup}";
                return;
            }

            // 优先使用 TrackNumber 的分组规则生成字母部分（与 TrackSelector.GenerateChannelName 保持一致）
            // 若 ChannelIndex 可用，则作为组内序号显示；否则退回到基于 TrackNumber 的默认编号
            if (TrackNumber > 0)
            {
                var groupIndex = (TrackNumber - 1) / 16;
                var letter = (char)('A' + groupIndex);

                int number;
                if (_channelIndex >= 0)
                {
                    // 如果 ChannelIndex 已分配，显示为组内索引（从1开始）
                    number = _channelIndex + 1;
                }
                else
                {
                    // 否则根据 TrackNumber 计算组内序号（与 GenerateChannelName 一致）
                    number = ((TrackNumber - 1) % 16) + 1;
                }

                ChannelName = $"{letter}{number}";
                return;
            }

            // 如果只有 MIDI 通道号可用，但没有 TrackNumber 信息，则以 CH.# 格式回退显示
            if (_midiChannel >= 0 && _midiChannel <= 15)
            {
                ChannelName = $"CH.{_midiChannel + 1}";
                return;
            }
            // 否则保持原来的通道名称不变
        }
    }
}