using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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