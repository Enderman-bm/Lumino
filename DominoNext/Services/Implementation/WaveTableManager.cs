using System;
using System.Threading.Tasks;
using EnderWaveTableAccessingParty.Services;
using EnderDebugger;
using DominoNext.Services.Interfaces;
using System.ComponentModel;
using DominoNext.Models.Settings;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// 播表管理器 - 提供简化的播表播放接口
    /// </summary>
    public class WaveTableManager : IDisposable
    {
        private readonly IMidiPlaybackService _playbackService;
        private readonly EnderLogger _logger;
        private bool _disposed = false;
        private bool _isAudioFeedbackEnabled = true;

        /// <summary>
        /// 音频反馈是否启用
        /// </summary>
        public bool IsAudioFeedbackEnabled 
        { 
            get => _isAudioFeedbackEnabled; 
            set => _isAudioFeedbackEnabled = value; 
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public WaveTableManager()
        {
            _logger = new EnderLogger("WaveTableManager");
            _playbackService = new MidiPlaybackService(_logger);
        }

        /// <summary>
        /// 设置服务引用
        /// </summary>
        public void SetSettingsService(ISettingsService settingsService)
        {
            if (settingsService != null)
            {
                IsAudioFeedbackEnabled = settingsService.Settings.EnableAudioFeedback;
                settingsService.Settings.PropertyChanged += OnSettingsPropertyChanged;
            }
        }

        /// <summary>
        /// 处理设置属性变化
        /// </summary>
        private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is SettingsModel settings && e.PropertyName == nameof(settings.EnableAudioFeedback))
            {
                IsAudioFeedbackEnabled = settings.EnableAudioFeedback;
                _logger.Debug("WaveTableManager", $"音频反馈设置已更新: {IsAudioFeedbackEnabled}");
            }
        }

        /// <summary>
        /// 初始化播表管理器
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_playbackService is MidiPlaybackService midiService)
            {
                await midiService.InitializeAsync();
            }
        }

        /// <summary>
        /// 播放音符
        /// </summary>
        /// <param name="pitch">音高 (MIDI音符编号)</param>
        /// <param name="velocity">力度 (1-127)</param>
        /// <param name="durationMs">持续时间(毫秒)</param>
        public void PlayNote(int pitch, int velocity = 100, int durationMs = 200)
        {
            if (_disposed) return;

            try
            {
                // 异步播放音符，不等待完成
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // 如果服务未初始化，则先初始化
                        if (!_playbackService.IsInitialized)
                        {
                            _logger.Debug("WaveTableManager", "检测到MIDI播放服务未初始化，正在初始化");
                            await _playbackService.InitializeAsync();
                            _logger.Debug("WaveTableManager", "MIDI播放服务初始化完成");
                        }
                        
                        await _playbackService.PlayNoteAsync(pitch, velocity, durationMs);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("WaveTableManager", $"播放音符异常: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"播放音符异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止音符
        /// </summary>
        /// <param name="pitch">音高</param>
        public void StopNote(int pitch)
        {
            if (_disposed) return;

            try
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // 如果服务未初始化，则先初始化
                        if (!_playbackService.IsInitialized)
                        {
                            _logger.Debug("WaveTableManager", "检测到MIDI播放服务未初始化，正在初始化");
                            await _playbackService.InitializeAsync();
                            _logger.Debug("WaveTableManager", "MIDI播放服务初始化完成");
                        }
                        
                        await _playbackService.StopNoteAsync(pitch);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("WaveTableManager", $"停止音符异常: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableManager", $"停止音符异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 更改乐器
        /// </summary>
        /// <param name="instrumentId">乐器ID</param>
        public void ChangeInstrument(int instrumentId)
        {
            if (_disposed) return;

            try
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // 如果服务未初始化，则先初始化
                        if (!_playbackService.IsInitialized)
                        {
                            _logger.Debug("WaveTableManager", "检测到MIDI播放服务未初始化，正在初始化");
                            await _playbackService.InitializeAsync();
                            _logger.Debug("WaveTableManager", "MIDI播放服务初始化完成");
                        }
                        
                        await _playbackService.ChangeInstrumentAsync(instrumentId);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("WaveTableManager", $"更改乐器异常: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableManager", $"更改乐器异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置播表
        /// </summary>
        /// <param name="waveTableId">播表ID</param>
        public void SetWaveTable(string waveTableId)
        {
            if (_disposed) return;

            try
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // 如果服务未初始化，则先初始化
                        if (!_playbackService.IsInitialized)
                        {
                            _logger.Debug("WaveTableManager", "检测到MIDI播放服务未初始化，正在初始化");
                            await _playbackService.InitializeAsync();
                            _logger.Debug("WaveTableManager", "MIDI播放服务初始化完成");
                        }
                        
                        await _playbackService.SetWaveTableAsync(waveTableId);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("WaveTableManager", $"设置播表异常: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableManager", $"设置播表异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            if (_playbackService is MidiPlaybackService midiService)
                            {
                                midiService.Stop();
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("WaveTableManager", $"清理MIDI播放服务时发生异常: {ex.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.Error("WaveTableManager", $"启动清理MIDI播放服务任务时发生异常: {ex.Message}");
                }

                _disposed = true;
            }
        }
    }
}