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
            set => SetProperty(ref _trackName, value);
        }

        public bool IsMuted
        {
            get => _isMuted;
            set => SetProperty(ref _isMuted, value);
        }

        public bool IsSolo
        {
            get => _isSolo;
            set => SetProperty(ref _isSolo, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public TrackViewModel(int trackNumber, string channelName, string trackName = "")
        {
            TrackNumber = trackNumber;
            ChannelName = channelName;
            TrackName = string.IsNullOrEmpty(trackName) ? $"Track {trackNumber}" : trackName;
        }

        public RelayCommand ToggleMuteCommand => new RelayCommand(ToggleMute);
        public RelayCommand ToggleSoloCommand => new RelayCommand(ToggleSolo);
        public RelayCommand SelectTrackCommand => new RelayCommand(SelectTrack);

        private void ToggleMute()
        {
            IsMuted = !IsMuted;
            // TODO: 实现静音逻辑
        }

        private void ToggleSolo()
        {
            IsSolo = !IsSolo;
            // TODO: 实现独奏逻辑
        }

        private void SelectTrack()
        {
            IsSelected = true;
            // TODO: 通知其他音轨取消选择
        }
    }
}