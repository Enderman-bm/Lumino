using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumino.ViewModels.Base;
using Lumino.ViewModels.Editor;

namespace Lumino.ViewModels
{
    /// <summary>
    /// 重构后的音轨视图模型 - 使用属性通知增强功能
    /// 提供更好的属性依赖管理和通知机制
    /// </summary>
    public partial class TrackViewModel : PropertyNotificationViewModelBase
    {
        #region 私有字段
        private int _trackNumber;
        private string _channelName = string.Empty;
        private string _trackName = string.Empty;
        private bool _isMuted;
        private bool _isSolo;
        private bool _isSelected;
        private int _midiChannel = -1; // MIDI通道号，-1表示未指定
        private int _channelIndex = -1; // 在同一MIDI通道中的索引，-1表示未指定
        private bool _isConductorTrack; // 是否为Conductor轨
        private Color _color = Colors.Blue; // 音轨颜色
        private int _index; // 音轨索引
        #endregion

        #region 属性 - 使用增强的属性通知
        public int TrackNumber
        {
            get => _trackNumber;
            set => SetPropertyWithAutoDependents(ref _trackNumber, value);
        }

        public string ChannelName
        {
            get => _channelName;
            set => SetPropertyWithAutoDependents(ref _channelName, value);
        }

        public string TrackName
        {
            get => _trackName;
            set
            {
                // Conductor轨不允许改名
                if (!_isConductorTrack)
                {
                    SetPropertyWithAutoDependents(ref _trackName, value);
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
                    SetPropertyWithAutoDependents(ref _isMuted, value);
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
                    SetPropertyWithAutoDependents(ref _isSolo, value);
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetPropertyWithAutoDependents(ref _isSelected, value);
        }

        /// <summary>
        /// 是否为Conductor轨（速度轨）
        /// </summary>
        public bool IsConductorTrack
        {
            get => _isConductorTrack;
            set => SetPropertyWithAutoDependents(ref _isConductorTrack, value);
        }

        /// <summary>
        /// MIDI通道号（0-15），-1表示未指定
        /// </summary>
        public int MidiChannel
        {
            get => _midiChannel;
            set
            {
                if (SetPropertyWithAutoDependents(ref _midiChannel, value))
                {
                    // 更新显示的通道名称
                    UpdateChannelName();
                }
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
                if (SetPropertyWithAutoDependents(ref _channelIndex, value))
                {
                    // 更新显示的通道名称
                    UpdateChannelName();
                }
            }
        }

        /// <summary>
        /// 音轨颜色
        /// </summary>
        public Color Color
        {
            get => _color;
            set => SetPropertyWithAutoDependents(ref _color, value);
        }

        /// <summary>
        /// 音符集合
        /// </summary>
        public ObservableCollection<NoteViewModel> Notes { get; } = new ObservableCollection<NoteViewModel>();

        /// <summary>
        /// 音轨索引
        /// </summary>
        public int Index
        {
            get => _index;
            set => SetPropertyWithAutoDependents(ref _index, value);
        }

        /// <summary>
        /// 音轨名称（兼容属性）
        /// </summary>
        public string Name
        {
            get => TrackName;
            set => TrackName = value;
        }
        #endregion

        #region 构造函数
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
        #endregion

        #region 命令
        public RelayCommand ToggleMuteCommand => new RelayCommand(ToggleMute);
        public RelayCommand ToggleSoloCommand => new RelayCommand(ToggleSolo);
        public RelayCommand SelectTrackCommand => new RelayCommand(SelectTrack);
        #endregion

        #region 属性依赖关系注册
        /// <summary>
        /// 注册属性依赖关系
        /// </summary>
        protected override void RegisterPropertyDependencies()
        {
            // 注册MIDI通道相关的依赖关系
            RegisterDependency(nameof(MidiChannel), nameof(ChannelName));
            RegisterDependency(nameof(ChannelIndex), nameof(ChannelName));
            RegisterDependency(nameof(IsConductorTrack), nameof(ChannelName), nameof(TrackName));
        }
        #endregion

        #region 私有方法
        private void ToggleMute()
        {
            // Conductor轨不支持静音
            if (_isConductorTrack) return;
            
            IsMuted = !IsMuted;
            // TODO: 实现静音逻辑
            System.Diagnostics.Debug.WriteLine($"[Track {TrackNumber}] 静音状态: {IsMuted}");
        }

        private void ToggleSolo()
        {
            // Conductor轨不支持独奏
            if (_isConductorTrack) return;
            
            IsSolo = !IsSolo;
            // TODO: 实现独奏逻辑
            System.Diagnostics.Debug.WriteLine($"[Track {TrackNumber}] 独奏状态: {IsSolo}");
        }

        private void SelectTrack()
        {
            IsSelected = true;
            // TODO: 通知其他组件轨道选择
            System.Diagnostics.Debug.WriteLine($"[Track {TrackNumber}] 已选择轨道: {TrackName}");
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
        #endregion

        #region 公共方法
        /// <summary>
        /// 重置轨道到默认状态
        /// </summary>
        public void ResetToDefault()
        {
            if (!_isConductorTrack)
            {
                IsMuted = false;
                IsSolo = false;
                TrackName = $"Track {TrackNumber}";
            }
            
            System.Diagnostics.Debug.WriteLine($"[Track {TrackNumber}] 已重置到默认状态");
        }

        /// <summary>
        /// 获取轨道状态摘要
        /// </summary>
        public string GetStatusSummary()
        {
            if (_isConductorTrack)
            {
                return $"Conductor轨 - {ChannelName}";
            }

            var status = new List<string>();
            if (_isMuted) status.Add("静音");
            if (_isSolo) status.Add("独奏");
            if (_isSelected) status.Add("已选");

            var statusText = status.Any() ? $" ({string.Join(", ", status)})" : "";
            return $"{TrackName} - {ChannelName}{statusText}";
        }
        #endregion

        #region 资源清理
        /// <summary>
        /// 释放特定资源
        /// </summary>
        protected override void DisposeCore()
        {
            // TrackViewModel 不需要特殊的资源清理
            System.Diagnostics.Debug.WriteLine($"[Track {TrackNumber}] ViewModel已释放");
        }
        #endregion
    }
}