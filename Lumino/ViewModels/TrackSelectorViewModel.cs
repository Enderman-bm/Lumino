using System.Collections.ObjectModel;
// Lumino - 音轨选择器视图模型，负责音轨列表与选中逻辑。
// 全局注释：本文件为音轨选择器 MVVM 逻辑，禁止随意更改集合操作。
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MidiReader;
using System.Collections.Generic;
using System.Linq;
using EnderDebugger;

namespace Lumino.ViewModels
{
    public partial class TrackSelectorViewModel : ViewModelBase
    {
        private ObservableCollection<TrackViewModel> _tracks;
        private TrackViewModel? _selectedTrack;

        /// <summary>
        /// 当前视图类型，用于控制显示内容
        /// </summary>
        [ObservableProperty]
        private ViewType _currentView = ViewType.PianoRoll;

        public ObservableCollection<TrackViewModel> Tracks
        {
            get => _tracks;
            set => SetProperty(ref _tracks, value);
        }

        public TrackViewModel? SelectedTrack
        {
            get => _selectedTrack;
            set => SetProperty(ref _selectedTrack, value);
        }

        public RelayCommand<TrackViewModel> SelectTrackCommand => new RelayCommand<TrackViewModel>(SelectTrack);
        public RelayCommand AddTrackCommand => new RelayCommand(AddTrack);
        public RelayCommand<TrackViewModel> RemoveTrackCommand => new RelayCommand<TrackViewModel>(RemoveTrack);

        public TrackSelectorViewModel()
        {
            _tracks = new ObservableCollection<TrackViewModel>();
            
            // 初始化默认音轨
            InitializeDefaultTracks();
                // 日志：初始化音轨选择器
                var logger = EnderLogger.Instance;
                logger.Info("TrackSelectorViewModel", "[EnderDebugger][2025-10-02 18:41:03.114][EnderLogger][TrackSelectorViewModel]音轨选择器ViewModel已初始化");
        }

        private void InitializeDefaultTracks()
        {
            // 首先添加Conductor轨，编号为00
            var conductorTrack = new TrackViewModel(0, "COND", "Conductor", isConductorTrack: true);
            conductorTrack.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(TrackViewModel.IsSelected) && sender is TrackViewModel selectedTrack)
                {
                    if (selectedTrack.IsSelected)
                    {
                        SelectTrack(selectedTrack);
                    }
                }
            };
            Tracks.Add(conductorTrack);

            // 添加16个MIDI通道音轨，编号从01开始
            for (int i = 1; i <= 16; i++)
            {
                var channelName = GenerateChannelName(i);
                var track = new TrackViewModel(i, channelName); // TrackNumber从1开始
                
                // 订阅选中状态改变事件
                track.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(TrackViewModel.IsSelected) && sender is TrackViewModel selectedTrack)
                    {
                        if (selectedTrack.IsSelected)
                        {
                            SelectTrack(selectedTrack);
                        }
                    }
                };

                Tracks.Add(track);
            }

            // 默认选择第一个非Conductor音轨
            var firstNonConductorTrack = Tracks.FirstOrDefault(t => !t.IsConductorTrack);
            if (firstNonConductorTrack != null)
            {
                SelectTrack(firstNonConductorTrack);
            }
            else if (Tracks.Count > 0)
            {
                // 如果只有Conductor轨，则选择它（尽管通常不应在Conductor轨上创建音符）
                SelectTrack(Tracks[0]);
            }
        }

        private string GenerateChannelName(int channelNumber)
        {
            // 生成通道名称：A1-A16, B1-B16, 依此类推
            var letterIndex = (channelNumber - 1) / 16;
            var numberIndex = ((channelNumber - 1) % 16) + 1;
            var letter = (char)('A' + letterIndex);
            
            return $"{letter}{numberIndex}";
        }

        private void SelectTrack(TrackViewModel track)
        {
            if (track == null) return;
            
            // 取消其他音轨的选中状态
            foreach (var t in Tracks)
            {
                if (t != track)
                {
                    t.IsSelected = false;
                }
            }

            // 选中当前音轨
            track.IsSelected = true;
            SelectedTrack = track;
        }

        public void AddTrack()
        {
            // 获取当前最大的普通轨道编号
            var maxTrackNumber = Tracks.Where(t => !t.IsConductorTrack).Max(t => t.TrackNumber);
            var newTrackNumber = maxTrackNumber + 1;
            var channelName = GenerateChannelName(newTrackNumber);
            var newTrack = new TrackViewModel(newTrackNumber, channelName);
            
            newTrack.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(TrackViewModel.IsSelected) && sender is TrackViewModel selectedTrack)
                {
                    if (selectedTrack.IsSelected)
                    {
                        SelectTrack(selectedTrack);
                    }
                }
            };

            Tracks.Add(newTrack);
        }

        /// <summary>
        /// 清除所有音轨
        /// </summary>
        public void ClearTracks()
        {
            _selectedTrack = null;
            Tracks.Clear();
        }

        /// <summary>
        /// 根据MIDI文件更新音轨列表
        /// </summary>
        /// <param name="midiFile">MIDI文件</param>
        public void LoadTracksFromMidi(MidiFile midiFile)
        {
            ClearTracks();
            
            // 首先创建Conductor轨，编号为00
            var conductorTrack = new TrackViewModel(0, "COND", "Conductor", isConductorTrack: true);
            conductorTrack.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(TrackViewModel.IsSelected) && sender is TrackViewModel selectedTrack)
                {
                    if (selectedTrack.IsSelected)
                    {
                        SelectTrack(selectedTrack);
                    }
                }
            };
            Tracks.Add(conductorTrack);
            
            // 收集每个音轨使用的通道信息
            var trackChannels = new List<Dictionary<int, int>>();
            
            // 收集非Conductor轨道信息
            var regularMidiTracks = new List<(MidiTrack track, int originalIndex)>();
            
            // 分析每个音轨中使用的MIDI通道，并识别非Conductor轨道
            for (int i = 0; i < midiFile.Tracks.Count; i++)
            {
                var channels = new Dictionary<int, int>();
                var track = midiFile.Tracks[i];
                
                // 检查是否包含Conductor相关的Meta事件
                var hasConductorEvents = HasConductorEvents(track);
                
                // 遍历音轨中的所有事件，收集使用的通道
                foreach (var midiEvent in track.Events)
                {
                    if (midiEvent.IsChannelEvent && 
                        ((midiEvent.EventType == MidiEventType.NoteOn && midiEvent.Data2 > 0) ||
                        midiEvent.EventType == MidiEventType.NoteOff))
                    {
                        if (channels.ContainsKey(midiEvent.Channel))
                        {
                            channels[midiEvent.Channel]++;
                        }
                        else
                        {
                            channels[midiEvent.Channel] = 1;
                        }
                    }
                }
                
                trackChannels.Add(channels);
                
                // 如果不包含Conductor事件，添加到普通轨道列表
                if (!hasConductorEvents)
                {
                    regularMidiTracks.Add((track, i));
                }
                else
                {
                    // TODO: 将Conductor相关事件添加到conductorTrack中
                    // 这里可以扩展TrackViewModel来支持存储MIDI事件
                }
            }
            
            // 根据收集的非Conductor轨道创建音轨视图模型，编号从01开始
            for (int trackIndex = 0; trackIndex < regularMidiTracks.Count; trackIndex++)
            {
                var (midiTrack, originalIndex) = regularMidiTracks[trackIndex];
                var trackNumber = trackIndex + 1; // 从1开始编号
                
                // 获取音轨名称，如果没有则使用默认名称
                var trackName = !string.IsNullOrEmpty(midiTrack.Name) ? midiTrack.Name : $"Track {trackNumber}";
                
                // 生成通道名称
                var channelName = GenerateChannelName(trackNumber);
                
                var track = new TrackViewModel(trackNumber, channelName, trackName);
                
                // 使用原始索引获取对应的通道信息
                if (trackChannels[originalIndex].Any())
                {
                    // 使用使用最频繁的通道作为主要通道
                    var mainChannel = trackChannels[originalIndex].OrderByDescending(kvp => kvp.Value).First().Key;
                    track.MidiChannel = mainChannel;
                }
                
                // 订阅选中状态改变事件
                track.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(TrackViewModel.IsSelected) && sender is TrackViewModel selectedTrack)
                    {
                        if (selectedTrack.IsSelected)
                        {
                            SelectTrack(selectedTrack);
                        }
                    }
                };

                Tracks.Add(track);
            }
            
            // 为每个MIDI通道分配索引，确保同一通道内的音轨有正确的唯一索引
            var regularTracks = Tracks.Where(t => !t.IsConductorTrack).ToList();
            
            var channelGroups = regularTracks
                .Where(t => t.MidiChannel >= 0)
                .GroupBy(t => t.MidiChannel)
                .ToList();

            // 处理没有MIDI通道的音轨
            var noChannelTracks = regularTracks
                .Where(t => t.MidiChannel < 0)
                .ToList();

            // 重置所有非Conductor音轨的ChannelIndex
            foreach (var track in regularTracks)
            {
                track.ChannelIndex = -1;
            }

            // 为每个通道组分配连续的索引
            for (int i = 0; i < channelGroups.Count; i++)
            {
                var group = channelGroups[i];
                for (int j = 0; j < group.Count(); j++)
                {
                    group.ElementAt(j).ChannelIndex = j;
                }
            }

            // 为没有通道的音轨分配索引
            for (int i = 0; i < noChannelTracks.Count; i++)
            {
                noChannelTracks[i].ChannelIndex = i;
            }

            // 如果除了Conductor轨没有其他音轨，添加一个默认音轨
            if (Tracks.Count == 1)
            {
                var track = new TrackViewModel(1, GenerateChannelName(1));
                track.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(TrackViewModel.IsSelected) && sender is TrackViewModel selectedTrack)
                    {
                        if (selectedTrack.IsSelected)
                        {
                            SelectTrack(selectedTrack);
                        }
                    }
                };
                Tracks.Add(track);
            }

            // 默认选择A1轨（索引1），而不是Conductor轨（索引0）
            if (Tracks.Count > 1)
            {
                SelectTrack(Tracks[1]);
            }
            else if (Tracks.Count > 0)
            {
                SelectTrack(Tracks[0]);
            }
        }

        /// <summary>
        /// 检查MIDI轨道是否包含Conductor相关的事件
        /// </summary>
        /// <param name="track">MIDI轨道</param>
        /// <returns>是否包含Conductor事件</returns>
        private bool HasConductorEvents(MidiTrack track)
        {
            foreach (var midiEvent in track.Events)
            {
                if (midiEvent.IsMetaEvent)
                {
                    switch (midiEvent.Data1)
                    {
                        case 0x51: // Tempo事件
                        case 0x58: // Time Signature事件
                        case 0x59: // Key Signature事件
                        case 0x54: // SMPTE Offset事件
                        case 0x7F: // Sequencer-Specific事件
                            return true;
                    }
                }
            }
            return false;
        }

        private void RemoveTrack(TrackViewModel track)
        {
            // 不允许删除Conductor轨
            if (track == null || track.IsConductorTrack || Tracks.Count <= 1 || !Tracks.Contains(track)) 
                return;
            
            var wasSelected = track.IsSelected;
            Tracks.Remove(track);

            // 如果删除的是选中的音轨，选择前一个
            if (wasSelected && Tracks.Count > 0)
            {
                SelectTrack(Tracks[^1]);
            }
        }
    }
}