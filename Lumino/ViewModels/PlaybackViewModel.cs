using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Windows.Input;
using EnderDebugger;
using Lumino.Models.Music;
using EnderWaveTableAccessingParty.Services;
using Lumino.Services.Implementation;

namespace Lumino.ViewModels
{
    /// <summary>
    /// 播放视图模型 - 管理播放UI的状态和命令
    /// 遵循MVVM模式，与PlaybackService和NotePlaybackEngine交互
    /// </summary>
    public partial class PlaybackViewModel : ViewModelBase
    {
        private readonly EnderLogger _logger = EnderLogger.Instance;
        private readonly PlaybackService _playbackService;
        private readonly NotePlaybackEngine _notePlaybackEngine;
        private readonly MidiPlaybackService _midiPlaybackService;

        #region 可观察属性
        /// <summary>
        /// 当前播放时间（格式：MM:SS.MS）
        /// </summary>
        [ObservableProperty]
        private string currentTimeDisplay = "00:00.000";

        /// <summary>
        /// 总时长（格式：MM:SS.MS）
        /// </summary>
        [ObservableProperty]
        private string totalDurationDisplay = "00:00.000";

        /// <summary>
        /// 播放进度（0-1）
        /// </summary>
        [ObservableProperty]
        private double playProgress = 0.0;

        /// <summary>
        /// 是否正在播放
        /// </summary>
        [ObservableProperty]
        private bool isPlaying = false;

        /// <summary>
        /// 是否已暂停
        /// </summary>
        [ObservableProperty]
        private bool isPaused = false;

        /// <summary>
        /// 播放速度（0.5-2.0）
        /// </summary>
        [ObservableProperty]
        private double playbackSpeed = 1.0;

        /// <summary>
        /// 当前活跃音符数
        /// </summary>
        [ObservableProperty]
        private int activeNoteCount = 0;

        /// <summary>
        /// 当前加载的音符总数
        /// </summary>
        [ObservableProperty]
        private int totalNoteCount = 0;

        /// <summary>
        /// 演奏指示线位置（X坐标，像素）
        /// </summary>
        [ObservableProperty]
        private double playheadX = 0.0;

        /// <summary>
        /// 播放时间到像素的缩放系数（像素/秒）
        /// </summary>
        [ObservableProperty]
        private double timeToPixelScale = 100.0; // 默认每秒100像素
        #endregion

        #region 命令
        [RelayCommand]
        public void Play()
        {
            _playbackService.Play();
            _logger.Info("PlaybackViewModel", "播放命令已执行");
        }

        [RelayCommand]
        public void Pause()
        {
            _playbackService.Pause();
            _logger.Info("PlaybackViewModel", "暂停命令已执行");
        }

        [RelayCommand]
        public void Stop()
        {
            _playbackService.Stop();
            _logger.Info("PlaybackViewModel", "停止命令已执行");
        }

        [RelayCommand]
        public void IncreaseSpeed()
        {
            double newSpeed = Math.Min(_playbackService.PlaybackSpeed + 0.1, 2.0);
            _playbackService.PlaybackSpeed = newSpeed;
            PlaybackSpeed = newSpeed;
            _logger.Info("PlaybackViewModel", $"播放速度调整为 {newSpeed:F1}x");
        }

        [RelayCommand]
        public void DecreaseSpeed()
        {
            double newSpeed = Math.Max(_playbackService.PlaybackSpeed - 0.1, 0.5);
            _playbackService.PlaybackSpeed = newSpeed;
            PlaybackSpeed = newSpeed;
            _logger.Info("PlaybackViewModel", $"播放速度调整为 {newSpeed:F1}x");
        }

        [RelayCommand]
        public void ResetSpeed()
        {
            _playbackService.PlaybackSpeed = 1.0;
            PlaybackSpeed = 1.0;
            _logger.Info("PlaybackViewModel", "播放速度已重置为 1.0x");
        }

        [RelayCommand]
        public void EnablePlayback()
        {
            _notePlaybackEngine.IsEnabled = true;
            _logger.Info("PlaybackViewModel", "音符播放已启用");
        }

        [RelayCommand]
        public void DisablePlayback()
        {
            _notePlaybackEngine.IsEnabled = false;
            _logger.Info("PlaybackViewModel", "音符播放已禁用");
        }
        #endregion

        public PlaybackViewModel(
            PlaybackService playbackService,
            NotePlaybackEngine notePlaybackEngine,
            MidiPlaybackService midiPlaybackService)
        {
            _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
            _notePlaybackEngine = notePlaybackEngine ?? throw new ArgumentNullException(nameof(notePlaybackEngine));
            _midiPlaybackService = midiPlaybackService ?? throw new ArgumentNullException(nameof(midiPlaybackService));

            // 订阅服务事件
            _playbackService.PlaybackTimeChanged += OnPlaybackTimeChanged;
            _playbackService.PlaybackStateChanged += OnPlaybackStateChanged;

            _logger.Info("PlaybackViewModel", "播放ViewModel已初始化");
        }

        /// <summary>
        /// 加载音符列表用于播放
        /// </summary>
        public void LoadNotes(List<Note> notes, int ticksPerQuarter = 480, double tempoInMicrosecondsPerQuarter = 500000.0)
        {
            _notePlaybackEngine.LoadNotes(notes, ticksPerQuarter, tempoInMicrosecondsPerQuarter);
            TotalNoteCount = notes.Count;

            // 计算总时长
            double maxEndTime = 0;
            foreach (var note in notes)
            {
                double startTime = ConvertFractionToSeconds(note.StartPosition, ticksPerQuarter, tempoInMicrosecondsPerQuarter);
                double duration = ConvertFractionToSeconds(note.Duration, ticksPerQuarter, tempoInMicrosecondsPerQuarter);
                maxEndTime = Math.Max(maxEndTime, startTime + duration);
            }

            _playbackService.TotalDuration = maxEndTime;
            UpdateTotalDurationDisplay();

            _logger.Info("PlaybackViewModel", $"已加载{notes.Count}个音符，总时长{maxEndTime:F2}秒");
        }

        /// <summary>
        /// 播放时间变更回调
        /// </summary>
        private void OnPlaybackTimeChanged(object? sender, PlaybackTimeChangedEventArgs e)
        {
            // 更新显示信息
            UpdateTimeDisplay(e.CurrentTime);
            PlayProgress = e.Progress;

            // 更新指示线位置
            PlayheadX = e.CurrentTime * TimeToPixelScale;

            // 更新活跃音符数
            ActiveNoteCount = _notePlaybackEngine.GetActiveNoteCount();
        }

        /// <summary>
        /// 播放状态变更回调
        /// </summary>
        private void OnPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
        {
            IsPlaying = (e.State == PlaybackState.Playing);
            IsPaused = (e.State == PlaybackState.Paused);

            _logger.Debug("PlaybackViewModel", $"播放状态已变更为: {e.State}");
        }

        /// <summary>
        /// 更新当前播放时间显示
        /// </summary>
        private void UpdateTimeDisplay(double timeInSeconds)
        {
            int minutes = (int)(timeInSeconds / 60);
            int seconds = (int)(timeInSeconds % 60);
            int milliseconds = (int)((timeInSeconds % 1) * 1000);

            CurrentTimeDisplay = $"{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
        }

        /// <summary>
        /// 更新总时长显示
        /// </summary>
        private void UpdateTotalDurationDisplay()
        {
            double duration = _playbackService.TotalDuration;
            int minutes = (int)(duration / 60);
            int seconds = (int)(duration % 60);
            int milliseconds = (int)((duration % 1) * 1000);

            TotalDurationDisplay = $"{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
        }

        /// <summary>
        /// MusicalFraction转换为秒（帮助方法）
        /// </summary>
        private double ConvertFractionToSeconds(MusicalFraction fraction, int ticksPerQuarter, double tempoInMicrosecondsPerQuarter)
        {
            double totalQuarters = fraction.ToDouble();
            double seconds = (totalQuarters * tempoInMicrosecondsPerQuarter) / 1_000_000.0;
            return seconds;
        }

        /// <summary>
        /// 处理进度条拖拽事件
        /// </summary>
        public void OnProgressBarDragged(double progress)
        {
            double targetTime = _playbackService.TotalDuration * progress;
            _playbackService.Seek(targetTime);
            _logger.Debug("PlaybackViewModel", $"进度条拖拽到 {progress:P1}");
        }

        /// <summary>
        /// 设置时间缩放（用于演奏指示线）
        /// </summary>
        public void SetTimeToPixelScale(double pixelsPerSecond)
        {
            TimeToPixelScale = Math.Max(10, pixelsPerSecond); // 最小10像素/秒
            PlayheadX = _playbackService.CurrentTime * TimeToPixelScale;
        }
    }
}
