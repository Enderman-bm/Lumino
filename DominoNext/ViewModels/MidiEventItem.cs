using System.ComponentModel;

namespace DominoNext.ViewModels
{
    public class MidiEventItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private int _measure;
        private int _tick;
        private string _event = "";
        private string _gate = "";
        private int _velocity;

        public int Measure
        {
            get => _measure;
            set
            {
                _measure = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Measure)));
            }
        }

        public int Tick
        {
            get => _tick;
            set
            {
                _tick = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tick)));
            }
        }

        public string Event
        {
            get => _event;
            set
            {
                _event = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Event)));
            }
        }

        public string Gate
        {
            get => _gate;
            set
            {
                _gate = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Gate)));
            }
        }

        public int Velocity
        {
            get => _velocity;
            set
            {
                _velocity = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Velocity)));
            }
        }
    }
}