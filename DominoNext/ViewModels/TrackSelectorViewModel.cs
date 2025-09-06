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
            
            // ��ʼ��Ĭ������
            InitializeDefaultTracks();
        }

        private void InitializeDefaultTracks()
        {
            // ����16��MIDIͨ��������
            for (int i = 1; i <= 16; i++)
            {
                var channelName = GenerateChannelName(i);
                var track = new TrackViewModel(i, channelName);
                
                // ��������ѡ���¼�
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

            // Ĭ��ѡ���һ������
            if (Tracks.Count > 0)
            {
                SelectTrack(Tracks[0]);
            }
        }

        private string GenerateChannelName(int channelNumber)
        {
            // ����ͨ�����ƣ�A1-A16, B1-B16, �ȵ�
            var letterIndex = (channelNumber - 1) / 16;
            var numberIndex = ((channelNumber - 1) % 16) + 1;
            var letter = (char)('A' + letterIndex);
            
            return $"{letter}{numberIndex}";
        }

        private void SelectTrack(TrackViewModel track)
        {
            if (track == null) return;
            
            // ȡ�����������ѡ��
            foreach (var t in Tracks)
            {
                if (t != track)
                {
                    t.IsSelected = false;
                }
            }

            // ѡ��ǰ����
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

        private void RemoveTrack(TrackViewModel track)
        {
            if (track == null || Tracks.Count <= 1 || !Tracks.Contains(track)) return;
            
            var wasSelected = track.IsSelected;
            Tracks.Remove(track);

            // ���ɾ������ѡ�е����죬ѡ��ǰһ��
            if (wasSelected && Tracks.Count > 0)
            {
                SelectTrack(Tracks[^1]);
            }
        }
    }
}