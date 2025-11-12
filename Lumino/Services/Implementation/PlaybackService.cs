using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnderDebugger;
using MidiReader;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// 播放服务 - 管理MIDI播放状态、时间进度、速度控制
    /// 支持Play/Pause/Stop/Seek等操作，提供实时播放时间和进度回调
    /// </summary>
    public class PlaybackService : IDisposable
    {
        private readonly EnderLogger _logger = EnderLogger.Instance;
        private readonly Stopwatch _playbackTimer;
        private CancellationTokenSource? _playCancellationToken;
        private Task? _playbackTask;

        // 播放状态
        private PlaybackState _state = PlaybackState.Stopped;
        private double _currentTime = 0.0; // 当前播放时间（秒）
        private double _totalDuration = 0.0; // 总时长（秒）
        private double _playbackSpeed = 1.0; // 播放速度倍数
        private bool _disposed = false;

        // 事件回调
        public event EventHandler<PlaybackTimeChangedEventArgs>? PlaybackTimeChanged;
        public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;

        public PlaybackService()
        {
            _playbackTimer = new Stopwatch();
        }

        /// <summary>
        /// 当前播放状态
        /// </summary>
        public PlaybackState State => _state;

        /// <summary>
        /// 当前播放时间（秒）
        /// </summary>
        public double CurrentTime
        {
            get => _currentTime;
            set
            {
                if (Math.Abs(_currentTime - value) > 0.001) // 精度0.1ms
                {
                    _currentTime = Math.Max(0, Math.Min(value, _totalDuration));
                    OnPlaybackTimeChanged();
                }
            }
        }

        /// <summary>
        /// 总时长（秒）
        /// </summary>
        public double TotalDuration
        {
            get => _totalDuration;
            set => _totalDuration = Math.Max(0, value);
        }

        /// <summary>
        /// 播放进度（0-1）
        /// </summary>
        public double Progress => _totalDuration > 0 ? _currentTime / _totalDuration : 0;

        /// <summary>
        /// 播放速度（倍数）
        /// </summary>
        public double PlaybackSpeed
        {
            get => _playbackSpeed;
            set => _playbackSpeed = Math.Max(0.1, Math.Min(value, 2.0)); // 限制0.1x~2.0x
        }

        /// <summary>
        /// 是否正在播放
        /// </summary>
        public bool IsPlaying => _state == PlaybackState.Playing;

        /// <summary>
        /// 是否已暂停
        /// </summary>
        public bool IsPaused => _state == PlaybackState.Paused;

        /// <summary>
        /// 开始播放
        /// </summary>
        public void Play()
        {
            if (_state == PlaybackState.Playing)
                return;

            _state = PlaybackState.Playing;
            _playbackTimer.Restart();
            _playCancellationToken = new CancellationTokenSource();

            // 启动播放更新线程
            _playbackTask = Task.Run(() => PlaybackUpdateLoop(_playCancellationToken.Token), _playCancellationToken.Token);

            _logger.Info("PlaybackService", $"播放开始 at {_currentTime:F3}s");
            OnPlaybackStateChanged();
        }

        /// <summary>
        /// 暂停播放
        /// </summary>
        public void Pause()
        {
            if (_state != PlaybackState.Playing)
                return;

            _playbackTimer.Stop();
            _state = PlaybackState.Paused;

            _logger.Info("PlaybackService", $"播放暂停 at {_currentTime:F3}s");
            OnPlaybackStateChanged();
        }

        /// <summary>
        /// 停止播放并重置到开头
        /// </summary>
        public void Stop()
        {
            if (_state == PlaybackState.Stopped)
                return;

            _playbackTimer.Stop();
            _playCancellationToken?.Cancel();
            _state = PlaybackState.Stopped;
            _currentTime = 0.0;

            _logger.Info("PlaybackService", "播放停止");
            OnPlaybackTimeChanged();
            OnPlaybackStateChanged();
        }

        /// <summary>
        /// 跳转到指定时间
        /// </summary>
        public void Seek(double timeInSeconds)
        {
            if (_state == PlaybackState.Playing)
            {
                _playbackTimer.Restart();
            }

            CurrentTime = timeInSeconds;
            _logger.Debug("PlaybackService", $"Seek to {_currentTime:F3}s");
        }

        /// <summary>
        /// 播放更新循环 - 在后台线程运行
        /// 定期更新播放时间，每帧约16ms（60FPS）
        /// </summary>
        private void PlaybackUpdateLoop(CancellationToken cancellationToken)
        {
            const int UpdateIntervalMs = 16; // 60 FPS
            var lastUpdateTime = DateTime.Now;

            try
            {
                while (!cancellationToken.IsCancellationRequested && _state == PlaybackState.Playing)
                {
                    var now = DateTime.Now;
                    var elapsedSinceLastUpdate = (now - lastUpdateTime).TotalMilliseconds;

                    if (elapsedSinceLastUpdate >= UpdateIntervalMs)
                    {
                        // 计算经过的实际时间并应用速度倍数
                        double realElapsed = _playbackTimer.Elapsed.TotalSeconds;
                        double adjustedElapsed = realElapsed * _playbackSpeed;

                        _currentTime += adjustedElapsed;

                        // 检查是否超过总时长
                        if (_currentTime >= _totalDuration)
                        {
                            _currentTime = _totalDuration;
                            _state = PlaybackState.Stopped;
                            _playbackTimer.Stop();
                            _logger.Info("PlaybackService", "播放完成");
                            OnPlaybackStateChanged();
                            break;
                        }

                        _playbackTimer.Restart();
                        OnPlaybackTimeChanged();

                        lastUpdateTime = now;
                    }

                    // 睡眠以减少CPU占用
                    Thread.Sleep(1);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("PlaybackService", "播放更新循环已取消");
            }
            catch (Exception ex)
            {
                _logger.Error("PlaybackService", $"播放更新循环错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 触发播放时间变更事件
        /// </summary>
        private void OnPlaybackTimeChanged()
        {
            PlaybackTimeChanged?.Invoke(this, new PlaybackTimeChangedEventArgs
            {
                CurrentTime = _currentTime,
                TotalDuration = _totalDuration,
                Progress = Progress
            });
        }

        /// <summary>
        /// 触发播放状态变更事件
        /// </summary>
        private void OnPlaybackStateChanged()
        {
            PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs
            {
                State = _state,
                Timestamp = DateTime.Now
            });
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Stop();
            _playCancellationToken?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// 播放状态枚举
    /// </summary>
    public enum PlaybackState
    {
        Stopped,  // 停止
        Playing,  // 播放
        Paused    // 暂停
    }

    /// <summary>
    /// 播放时间变更事件参数
    /// </summary>
    public class PlaybackTimeChangedEventArgs : EventArgs
    {
        public double CurrentTime { get; set; }
        public double TotalDuration { get; set; }
        public double Progress { get; set; }
    }

    /// <summary>
    /// 播放状态变更事件参数
    /// </summary>
    public class PlaybackStateChangedEventArgs : EventArgs
    {
        public PlaybackState State { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
