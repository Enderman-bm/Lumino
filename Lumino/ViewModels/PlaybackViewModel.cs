using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia.Threading;
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

        /// <summary>
        /// 是否自动跟随播放头滚动
        /// </summary>
        [ObservableProperty]
        private bool isAutoScrollEnabled = true;

        /// <summary>
        /// 当前播放时间（秒）
        /// </summary>
        [ObservableProperty]
        private double currentPlaybackTime = 0.0;

        /// <summary>
        /// 工程 BPM 速度（从 MainWindowViewModel 获取）
        /// </summary>
        [ObservableProperty]
        private double projectTempo = 120.0;

        /// <summary>
        /// 总时长（秒） - 公开访问，用于Seek操作
        /// </summary>
        public double TotalDuration => _playbackService.TotalDuration;
        #endregion

        #region 事件
        /// <summary>
        /// 请求滚动到播放头位置的事件
        /// </summary>
        public event EventHandler<ScrollToPlayheadEventArgs>? ScrollToPlayheadRequested;

        /// <summary>
        /// 播放时间变化事件（用于更新TimelinePosition）
        /// </summary>
        public event EventHandler<double>? TimelinePositionChanged;

        /// <summary>
        /// 播放前请求同步音符的事件
        /// 当用户点击播放时，如果没有加载音符但钢琴卷帘上有音符，此事件将被触发
        /// MainWindowViewModel 可以订阅此事件并同步音符到播放系统
        /// </summary>
        public event EventHandler? SyncNotesRequested;
        #endregion

        #region 命令
        [RelayCommand]
        public void Play()
        {
            _logger.Info("PlaybackViewModel", $"播放命令开始执行 - TotalDuration={_playbackService.TotalDuration:F2}s, CurrentTime={_playbackService.CurrentTime:F2}s, TotalNoteCount={TotalNoteCount}");
            
            // 如果没有加载音符或时长为0，尝试请求同步
            if (TotalNoteCount <= 0 || _playbackService.TotalDuration <= 0)
            {
                _logger.Info("PlaybackViewModel", "播放前请求同步音符...");
                SyncNotesRequested?.Invoke(this, EventArgs.Empty);
                
                // 重新检查是否有音符
                if (TotalNoteCount <= 0)
                {
                    _logger.Warn("PlaybackViewModel", "无法播放：没有音符");
                    return;
                }
                
                if (_playbackService.TotalDuration <= 0)
                {
                    _logger.Warn("PlaybackViewModel", "无法播放：总时长为0");
                    return;
                }
            }
            
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
            
            // 重置演奏指示线位置到起点
            PlayheadX = 0;
            CurrentPlaybackTime = 0;
            PlayProgress = 0;
            
            // 触发时间轴位置变化事件，将指示线重置到起点
            TimelinePositionChanged?.Invoke(this, 0);
            
            _logger.Info("PlaybackViewModel", "停止命令已执行，演奏指示线已重置");
        }

        [RelayCommand]
        public void ToggleAutoScroll()
        {
            IsAutoScrollEnabled = !IsAutoScrollEnabled;
            _logger.Info("PlaybackViewModel", $"自动跟随播放头: {(IsAutoScrollEnabled ? "开启" : "关闭")}");
        }

        [RelayCommand]
        public void ScrollToPlayhead()
        {
            // 手动触发滚动到播放头位置
            RequestScrollToPlayhead(true);
            _logger.Info("PlaybackViewModel", "手动滚动到播放头位置");
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

        /// <summary>
        /// 打开工程设置的委托命令（由 MainWindowViewModel 设置）
        /// </summary>
        public System.Windows.Input.ICommand? OpenProjectSettingsCommand { get; set; }

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

        #region 时间转换参数
        private int _ticksPerQuarter = 480;
        private double _tempoInMicrosecondsPerQuarter = 500000.0; // 默认120 BPM
        private double _lastScrollRequestPosition = 0; // 上次滚动请求的位置，用于节流
        private List<Note>? _cachedNotes; // 缓存的音符列表，用于 BPM 改变后重新计算时长
        
        /// <summary>
        /// 设置时间转换参数
        /// </summary>
        public void SetTimeConversionParams(int ticksPerQuarter, double tempoInMicrosecondsPerQuarter)
        {
            _ticksPerQuarter = ticksPerQuarter;
            _tempoInMicrosecondsPerQuarter = tempoInMicrosecondsPerQuarter;
        }
        
        /// <summary>
        /// 将播放时间（秒）转换为四分音符位置
        /// </summary>
        private double ConvertSecondsToQuarterNotes(double timeInSeconds)
        {
            // 每四分音符的秒数
            double secondsPerQuarter = _tempoInMicrosecondsPerQuarter / 1_000_000.0;
            return timeInSeconds / secondsPerQuarter;
        }
        #endregion

        /// <summary>
        /// 加载音符列表用于播放
        /// </summary>
        /// <param name="notes">音符列表</param>
        /// <param name="ticksPerQuarter">每四分音符的 ticks 数</param>
        /// <param name="tempoInMicrosecondsPerQuarter">每四分音符的微秒数（默认使用工程 BPM）</param>
        public void LoadNotes(List<Note> notes, int ticksPerQuarter = 480, double tempoInMicrosecondsPerQuarter = -1)
        {
            // 缓存音符列表
            _cachedNotes = notes;
            
            // 如果没有指定 tempo，使用工程 BPM 计算
            if (tempoInMicrosecondsPerQuarter < 0)
            {
                // BPM 转换为微秒/四分音符: 60,000,000 / BPM
                tempoInMicrosecondsPerQuarter = 60_000_000.0 / ProjectTempo;
                _logger.Debug("PlaybackViewModel", $"使用工程 BPM={ProjectTempo}，tempo={tempoInMicrosecondsPerQuarter} µs/quarter");
            }

            // 保存时间转换参数
            SetTimeConversionParams(ticksPerQuarter, tempoInMicrosecondsPerQuarter);
            
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
        /// 当工程 BPM 改变时重新计算播放时长
        /// </summary>
        public void RecalculateDurationFromTempo()
        {
            if (_cachedNotes == null || _cachedNotes.Count == 0)
            {
                _logger.Debug("PlaybackViewModel", "无缓存音符，跳过时长重计算");
                return;
            }

            // 使用新的 BPM 重新计算
            double tempoInMicrosecondsPerQuarter = 60_000_000.0 / ProjectTempo;
            _logger.Info("PlaybackViewModel", $"BPM 已更改为 {ProjectTempo}，重新计算播放时长");

            // 更新时间转换参数
            SetTimeConversionParams(_ticksPerQuarter, tempoInMicrosecondsPerQuarter);
            
            // 重新加载音符到播放引擎
            _notePlaybackEngine.LoadNotes(_cachedNotes, _ticksPerQuarter, tempoInMicrosecondsPerQuarter);

            // 重新计算总时长
            double maxEndTime = 0;
            foreach (var note in _cachedNotes)
            {
                double startTime = ConvertFractionToSeconds(note.StartPosition, _ticksPerQuarter, tempoInMicrosecondsPerQuarter);
                double duration = ConvertFractionToSeconds(note.Duration, _ticksPerQuarter, tempoInMicrosecondsPerQuarter);
                maxEndTime = Math.Max(maxEndTime, startTime + duration);
            }

            _playbackService.TotalDuration = maxEndTime;
            UpdateTotalDurationDisplay();

            _logger.Info("PlaybackViewModel", $"BPM={ProjectTempo}，重新计算后总时长{maxEndTime:F2}秒");
        }

        /// <summary>
        /// 播放时间变更回调
        /// </summary>
        private void OnPlaybackTimeChanged(object? sender, PlaybackTimeChangedEventArgs e)
        {
            // 由于此回调可能从后台线程调用，需要调度到 UI 线程执行
            Dispatcher.UIThread.Post(() =>
            {
                // 更新显示信息
                UpdateTimeDisplay(e.CurrentTime);
                PlayProgress = e.Progress;
                CurrentPlaybackTime = e.CurrentTime;

                // 计算当前时间对应的四分音符位置
                var quarterNotePosition = ConvertSecondsToQuarterNotes(e.CurrentTime);
                
                // 更新指示线位置（基于像素缩放）
                PlayheadX = e.CurrentTime * TimeToPixelScale;

                // 更新活跃音符数
                ActiveNoteCount = _notePlaybackEngine.GetActiveNoteCount();
                
                // 触发时间轴位置变化事件（用于更新PianoRollViewModel的TimelinePosition）
                TimelinePositionChanged?.Invoke(this, quarterNotePosition);

                // 如果启用自动滚动且正在播放，请求滚动到播放头位置
                // 优化：只在播放头位置变化超过阈值时才请求滚动，减少不必要的滚动操作
                if (IsAutoScrollEnabled && IsPlaying)
                {
                    // 节流：仅当位置变化足够大时才触发滚动
                    if (Math.Abs(quarterNotePosition - _lastScrollRequestPosition) > 0.5) // 每0.5个四分音符检查一次
                    {
                        _lastScrollRequestPosition = quarterNotePosition;
                        RequestScrollToPlayhead(false);
                    }
                }
            }, DispatcherPriority.Render);
        }

        /// <summary>
        /// 请求滚动到播放头位置
        /// </summary>
        /// <param name="forceCenter">是否强制将播放头居中显示</param>
        private void RequestScrollToPlayhead(bool forceCenter)
        {
            ScrollToPlayheadRequested?.Invoke(this, new ScrollToPlayheadEventArgs
            {
                PlayheadX = PlayheadX,
                CurrentTime = CurrentPlaybackTime,
                QuarterNotePosition = ConvertSecondsToQuarterNotes(CurrentPlaybackTime),
                ForceCenter = forceCenter
            });
        }

        /// <summary>
        /// 播放状态变更回调
        /// </summary>
        private void OnPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
        {
            // 由于此回调可能从后台线程调用，需要调度到 UI 线程执行
            Dispatcher.UIThread.Post(() =>
            {
                IsPlaying = (e.State == PlaybackState.Playing);
                IsPaused = (e.State == PlaybackState.Paused);

                _logger.Debug("PlaybackViewModel", $"播放状态已变更为: {e.State}");
            }, DispatcherPriority.Normal);
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

    /// <summary>
    /// 滚动到播放头事件参数
    /// </summary>
    public class ScrollToPlayheadEventArgs : EventArgs
    {
        /// <summary>
        /// 播放头X坐标（像素）- 基于TimeToPixelScale
        /// </summary>
        public double PlayheadX { get; set; }

        /// <summary>
        /// 当前播放时间（秒）
        /// </summary>
        public double CurrentTime { get; set; }

        /// <summary>
        /// 当前播放位置（四分音符）
        /// </summary>
        public double QuarterNotePosition { get; set; }

        /// <summary>
        /// 是否强制将播放头居中显示
        /// </summary>
        public bool ForceCenter { get; set; }
    }
}
