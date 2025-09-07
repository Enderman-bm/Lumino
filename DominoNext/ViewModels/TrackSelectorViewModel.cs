using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MidiReader;
using System.Collections.Generic;
using System.Linq;

namespace DominoNext.ViewModels
{
    public partial class TrackSelectorViewModel : ViewModelBase
    {
        private ObservableCollection<TrackViewModel> _tracks;
        private TrackViewModel? _selectedTrack;

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
        }

        private void InitializeDefaultTracks()
        {
            // 添加16个MIDI通道音轨
            for (int i = 1; i <= 16; i++)
            {
                var channelName = GenerateChannelName(i);
                var track = new TrackViewModel(i, channelName);
                
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

            // 默认选择第一个音轨
            if (Tracks.Count > 0)
            {
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
            var trackNumber = Tracks.Count + 1;
            var channelName = GenerateChannelName(trackNumber);
            var newTrack = new TrackViewModel(trackNumber, channelName);
            
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
            
            // 收集每个音轨使用的通道信息
            var trackChannels = new List<Dictionary<int, int>>();
            
            // 分析每个音轨中使用的MIDI通道
            for (int i = 0; i < midiFile.Tracks.Count; i++)
            {
                var channels = new Dictionary<int, int>();
                var track = midiFile.Tracks[i];
                
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
            }
            
            // 根据MIDI文件中的音轨创建音轨视图模型
            for (int i = 0; i < midiFile.Tracks.Count; i++)
            {
                var midiTrack = midiFile.Tracks[i];
                var trackNumber = i + 1;
                
                // 获取音轨名称，如果没有则使用默认名称
                var trackName = !string.IsNullOrEmpty(midiTrack.Name) ? midiTrack.Name : $"Track {trackNumber}";
                
                // 生成通道名称
                var channelName = GenerateChannelName(trackNumber);
                
                var track = new TrackViewModel(trackNumber, channelName, trackName);
                
                // 如果该音轨使用了通道，设置主要通道号
                if (trackChannels[i].Any())
                {
                    // 使用使用最频繁的通道作为主要通道
                    var mainChannel = trackChannels[i].OrderByDescending(kvp => kvp.Value).First().Key;
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
            var channelGroups = Tracks
                .Where(t => t.MidiChannel >= 0)
                .GroupBy(t => t.MidiChannel)
                .ToList();

            // 处理没有MIDI通道的音轨
            var noChannelTracks = Tracks
                .Where(t => t.MidiChannel < 0)
                .ToList();

            // 重置所有音轨的ChannelIndex
            foreach (var track in Tracks)
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

            // 如果没有音轨，添加一个默认音轨
            if (Tracks.Count == 0)
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

            // 默认选择第一个音轨
            if (Tracks.Count > 0)
            {
                SelectTrack(Tracks[0]);
            }
        }

        private void RemoveTrack(TrackViewModel track)
        {
            if (track == null || Tracks.Count <= 1 || !Tracks.Contains(track)) return;
            
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