using System;
using System.Threading.Tasks;
using EnderWaveTableAccessingParty.Services;
using EnderDebugger;

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

        /// <summary>
        /// 构造函数
        /// </summary>
        public WaveTableManager()
        {
            _playbackService = new MidiPlaybackService();
            _logger = new EnderLogger("WaveTableManager");
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
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    if (_playbackService is IDisposable disposableService)
                    {
                        disposableService.Dispose();
                    }
                }

                _disposed = true;
            }
        }
    }
}