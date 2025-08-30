using System;
using System.Threading.Tasks;
using DominoNext.ViewModels.Editor;
using DominoNext.Services.Interfaces;

namespace DominoNext.Services.Implementation
{
    public class PlaybackService : IPlaybackService
    {
        private PlaybackState _state = PlaybackState.Stopped;
        private long _currentPosition = 0;
        private double _tempo = 120.0; // 默认120 BPM

        public PlaybackState State 
        { 
            get => _state;
            private set
            {
                if (_state != value)
                {
                    _state = value;
                    StateChanged?.Invoke(this, _state);
                }
            }
        }

        public long CurrentPosition 
        { 
            get => _currentPosition;
            private set
            {
                if (_currentPosition != value)
                {
                    _currentPosition = value;
                    PositionChanged?.Invoke(this, _currentPosition);
                }
            }
        }

        public double Tempo 
        { 
            get => _tempo;
            set => _tempo = Math.Max(1, Math.Min(300, value)); // 限制在合理范围内
        }

        public event EventHandler<long>? PositionChanged;
        public event EventHandler<PlaybackState>? StateChanged;

        public async Task PlayAsync(PianoRollViewModel pianoRoll)
        {
            // 简化的播放实现 - 这里可以后续扩展为真正的MIDI播放
            State = PlaybackState.Playing;
            await Task.CompletedTask;
        }

        public async Task PauseAsync()
        {
            State = PlaybackState.Paused;
            await Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            State = PlaybackState.Stopped;
            CurrentPosition = 0;
            await Task.CompletedTask;
        }

        public async Task SeekAsync(long position)
        {
            CurrentPosition = Math.Max(0, position);
            await Task.CompletedTask;
        }
    }
}