using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using EnderDebugger;
using EnderWaveTableAccessingParty.Services;
namespace EnderWaveTableAccessingParty.Services
{
    /// <summary>
    /// MIDI播放服务实现
    /// </summary>
    public class MidiPlaybackService : IMidiPlaybackService, IDisposable
    {
        private int _midiOutHandle = -1;
        private bool _isInitialized = false;
        private int _currentDeviceId = -1;
        private string _currentWaveTableId = string.Empty;
        private List<MidiDeviceInfo> _midiDevices = new();
        private List<WaveTableInfo> _waveTables = new();
        private WaveTableInfo? _currentWaveTable;
        private readonly EnderLogger _logger;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 当前选中的设备ID
        /// </summary>
        public int CurrentDeviceId 
        { 
            get => _currentDeviceId;
            set
            {
                if (_currentDeviceId != value)
                {
                    _currentDeviceId = value;
                    _ = ReinitializeDeviceAsync();
                }
            }
        }

        /// <summary>
        /// 当前选中的播表ID
        /// </summary>
        public string CurrentWaveTableId 
        { 
            get => _currentWaveTableId;
            set
            {
                if (_currentWaveTableId != value)
                {
                    _currentWaveTableId = value;
                    _ = SetWaveTableAsync(value);
                }
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public MidiPlaybackService()
        {
            _logger = EnderLogger.Instance;
            _logger.Debug("MidiPlaybackService", "MIDI播放服务实例已创建");
        }

        /// <summary>
        /// 初始化服务
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                _logger.Debug("MidiPlaybackService", "服务已初始化，跳过重复初始化");
                return;
            }

            _logger.Info("MidiPlaybackService", "开始初始化MIDI播放服务");

            try
            {
                // 获取MIDI设备列表
                _logger.Debug("MidiPlaybackService", "正在获取MIDI设备列表");
                await RefreshMidiDevicesAsync();
                _logger.Info("MidiPlaybackService", $"MIDI设备列表获取完成，共找到 {_midiDevices.Count} 个设备");

                // 初始化播表数据
                _logger.Debug("MidiPlaybackService", "正在初始化播表数据");
                await InitializeWaveTablesAsync();
                _logger.Info("MidiPlaybackService", $"播表数据初始化完成，共找到 {_waveTables.Count} 个播表");

                // 打开默认MIDI设备
                _logger.Debug("MidiPlaybackService", "正在打开默认MIDI设备");
                await OpenDefaultDeviceAsync();

                _isInitialized = true;
                _logger.Info("MidiPlaybackService", "MIDI播放服务初始化成功");
            }
            catch (Exception ex)
            {
                _logger.Error("MidiPlaybackService", $"MIDI播放服务初始化失败: {ex.Message}");
                _logger.LogException(ex, "InitializeAsync");
                throw;
            }
        }

        /// <summary>
        /// 获取可用的MIDI设备列表
        /// </summary>
        public async Task<List<MidiDeviceInfo>> GetMidiDevicesAsync()
        {
            if (!_isInitialized)
                await InitializeAsync();

            return _midiDevices.ToList();
        }

        /// <summary>
        /// 获取可用的播表列表
        /// </summary>
        public async Task<List<WaveTableInfo>> GetWaveTablesAsync()
        {
            if (!_isInitialized)
                await InitializeAsync();

            return _waveTables.ToList();
        }

        /// <summary>
        /// 播放音符
        /// </summary>
        public async Task PlayNoteAsync(int midiNote, int velocity = 100, int durationMs = 200, int channel = 0)
        {
            if (!_isInitialized || _midiOutHandle == -1)
            {
                _logger.Warn("MidiPlaybackService", "播放音符失败：服务未初始化或MIDI设备未打开");
                return;
            }

            _logger.Debug("MidiPlaybackService", $"开始播放音符 - Note: {midiNote}, Velocity: {velocity}, Duration: {durationMs}ms, Channel: {channel}");

            try
            {
                // 发送音符开启消息
                var noteOnMsg = WinmmNative.CreateMidiMessage(WinmmNative.MIDI_NOTE_ON, midiNote, velocity, channel);
                int result = WinmmNative.midiOutShortMsg(_midiOutHandle, noteOnMsg);

                if (result != WinmmNative.MMSYSERR_NOERROR)
                {
                    var errorMsg = WinmmNative.GetMidiErrorText(result);
                    _logger.Error("MidiPlaybackService", $"无法播放音符: {errorMsg}");
                    throw new InvalidOperationException($"无法播放音符: {errorMsg}");
                }

                _logger.Debug("MidiPlaybackService", $"音符开启消息发送成功 - Note: {midiNote}");

                // 延迟后发送音符关闭消息
                if (durationMs > 0)
                {
                    await Task.Delay(durationMs);
                    await StopNoteAsync(midiNote, channel);
                }

                _logger.Debug("MidiPlaybackService", $"音符播放完成 - Note: {midiNote}");
            }
            catch (Exception ex)
            {
                _logger.Error("MidiPlaybackService", $"播放音符失败: {ex.Message}");
                _logger.LogException(ex, "PlayNoteAsync");
            }
        }

        /// <summary>
        /// 停止音符
        /// </summary>
        public async Task StopNoteAsync(int midiNote, int channel = 0)
        {
            if (_midiOutHandle == -1)
            {
                _logger.Warn("MidiPlaybackService", "停止音符失败：MIDI设备未打开");
                return;
            }

            _logger.Debug("MidiPlaybackService", $"开始停止音符 - Note: {midiNote}, Channel: {channel}");

            try
            {
                var noteOffMsg = WinmmNative.CreateMidiMessage(WinmmNative.MIDI_NOTE_OFF, midiNote, 0, channel);
                int result = WinmmNative.midiOutShortMsg(_midiOutHandle, noteOffMsg);

                if (result != WinmmNative.MMSYSERR_NOERROR)
                {
                    var errorMsg = WinmmNative.GetMidiErrorText(result);
                    _logger.Error("MidiPlaybackService", $"停止音符失败: {errorMsg}");
                }
                else
                {
                    _logger.Debug("MidiPlaybackService", $"音符停止成功 - Note: {midiNote}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("MidiPlaybackService", $"停止音符失败: {ex.Message}");
                _logger.LogException(ex, "StopNoteAsync");
            }
        }

        /// <summary>
        /// 更改乐器
        /// </summary>
        public async Task ChangeInstrumentAsync(int instrumentId, int channel = 0)
        {
            if (_midiOutHandle == -1)
                return;

            try
            {
                var programChangeMsg = WinmmNative.CreateMidiMessage(WinmmNative.MIDI_PROGRAM_CHANGE, instrumentId, 0, channel);
                int result = WinmmNative.midiOutShortMsg(_midiOutHandle, programChangeMsg);

                if (result != WinmmNative.MMSYSERR_NOERROR)
                {
                    throw new InvalidOperationException($"无法更改乐器: {WinmmNative.GetMidiErrorText(result)}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更改乐器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置当前播表
        /// </summary>
        public async Task SetWaveTableAsync(string waveTableId)
        {
            var waveTable = _waveTables.FirstOrDefault(wt => wt.Id == waveTableId);
            if (waveTable == null)
            {
                throw new ArgumentException($"找不到播表: {waveTableId}");
            }

            _currentWaveTable = waveTable;
            _currentWaveTableId = waveTableId;

            // 应用播表设置（这里可以根据播表配置调整音色等）
            await ApplyWaveTableSettingsAsync(waveTable);
        }

        /// <summary>
        /// 获取当前播表
        /// </summary>
        public async Task<WaveTableInfo?> GetCurrentWaveTableAsync()
        {
            return _currentWaveTable;
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public async Task CleanupAsync()
        {
            _logger.Info("MidiPlaybackService", "开始清理MIDI播放服务资源");

            if (_midiOutHandle != -1)
            {
                try
                {
                    _logger.Debug("MidiPlaybackService", "正在重置MIDI设备");
                    // 重置MIDI设备
                    WinmmNative.midiOutReset(_midiOutHandle);
                    
                    _logger.Debug("MidiPlaybackService", "正在关闭MIDI设备");
                    // 关闭MIDI设备
                    WinmmNative.midiOutClose(_midiOutHandle);
                    _midiOutHandle = -1;
                    
                    _logger.Debug("MidiPlaybackService", "MIDI设备清理完成");
                }
                catch (Exception ex)
                {
                    _logger.Error("MidiPlaybackService", $"清理MIDI设备失败: {ex.Message}");
                    _logger.LogException(ex, "CleanupAsync");
                }
            }

            _isInitialized = false;
            _logger.Info("MidiPlaybackService", "MIDI播放服务资源清理完成");
            await Task.CompletedTask;
        }

        /// <summary>
        /// 刷新MIDI设备列表
        /// </summary>
        private async Task RefreshMidiDevicesAsync()
        {
            _logger.Debug("MidiPlaybackService", "开始刷新MIDI设备列表");
            _midiDevices.Clear();

            try
            {
                int deviceCount = WinmmNative.midiOutGetNumDevs();
                _logger.Debug("MidiPlaybackService", $"系统报告 {deviceCount} 个MIDI输出设备");
                
                for (int i = 0; i < deviceCount; i++)
                {
                    var caps = new WinmmNative.MIDIOUTCAPS();
                    int result = WinmmNative.midiOutGetDevCaps(i, ref caps, Marshal.SizeOf(typeof(WinmmNative.MIDIOUTCAPS)));
                    
                    if (result == WinmmNative.MMSYSERR_NOERROR)
                    {
                        var deviceInfo = new MidiDeviceInfo
                        {
                            DeviceId = i,
                            Name = caps.szPname,
                            IsDefault = (i == 0),
                            Technology = caps.wTechnology,
                            Voices = caps.wVoices,
                            Notes = caps.wNotes,
                            ChannelMask = caps.wChannelMask,
                            Support = caps.dwSupport
                        };
                        
                        _midiDevices.Add(deviceInfo);
                        _logger.Debug("MidiPlaybackService", $"找到MIDI设备: {deviceInfo.Name} (ID: {deviceInfo.DeviceId})");
                    }
                    else
                    {
                        _logger.Warn("MidiPlaybackService", $"获取设备 {i} 信息失败: {WinmmNative.GetMidiErrorText(result)}");
                    }
                }
                
                _logger.Info("MidiPlaybackService", $"MIDI设备列表刷新完成，共找到 {_midiDevices.Count} 个设备");
            }
            catch (Exception ex)
            {
                _logger.Error("MidiPlaybackService", $"刷新MIDI设备列表失败: {ex.Message}");
                _logger.LogException(ex, "RefreshMidiDevicesAsync");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 打开默认MIDI设备
        /// </summary>
        private async Task OpenDefaultDeviceAsync()
        {
            if (_midiDevices.Count == 0)
            {
                _logger.Warn("MidiPlaybackService", "没有找到可用的MIDI设备");
                return;
            }

            // 使用第一个可用设备
            var deviceId = _midiDevices.First().DeviceId;
            var deviceName = _midiDevices.First().Name;
            _logger.Info("MidiPlaybackService", $"正在打开默认MIDI设备: {deviceName} (ID: {deviceId})");
            await OpenDeviceAsync(deviceId);
        }

        /// <summary>
        /// 打开指定MIDI设备
        /// </summary>
        private async Task OpenDeviceAsync(int deviceId)
        {
            if (_midiOutHandle != -1)
            {
                _logger.Debug("MidiPlaybackService", $"关闭当前MIDI设备 (ID: {_currentDeviceId})");
                await CleanupAsync();
            }

            _logger.Debug("MidiPlaybackService", $"正在打开MIDI设备 (ID: {deviceId})");

            try
            {
                int handle = -1;
                int result = WinmmNative.midiOutOpen(ref handle, deviceId, IntPtr.Zero, IntPtr.Zero, 0);

                if (result != WinmmNative.MMSYSERR_NOERROR)
                {
                    var errorMsg = WinmmNative.GetMidiErrorText(result);
                    _logger.Error("MidiPlaybackService", $"无法打开MIDI设备: {errorMsg}");
                    throw new InvalidOperationException($"无法打开MIDI设备: {errorMsg}");
                }

                _midiOutHandle = handle;
                _currentDeviceId = deviceId;
                _logger.Info("MidiPlaybackService", $"MIDI设备打开成功 (ID: {deviceId})");
            }
            catch (Exception ex)
            {
                _logger.Error("MidiPlaybackService", $"打开MIDI设备失败: {ex.Message}");
                _logger.LogException(ex, "OpenDeviceAsync");
                throw;
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 重新初始化设备
        /// </summary>
        private async Task ReinitializeDeviceAsync()
        {
            if (_currentDeviceId >= 0)
            {
                await OpenDeviceAsync(_currentDeviceId);
            }
        }

        /// <summary>
        /// 初始化播表数据
        /// </summary>
        private async Task InitializeWaveTablesAsync()
        {
            _logger.Debug("MidiPlaybackService", "开始初始化播表数据");
            _waveTables.Clear();

            // 添加默认播表
            _logger.Debug("MidiPlaybackService", "添加默认钢琴播表");
            _waveTables.Add(new WaveTableInfo
            {
                Id = "default",
                Name = "默认钢琴",
                Description = "标准钢琴音色",
                IsSystem = true,
                InstrumentMappings = new Dictionary<int, string>
                {
                    { 0, "Acoustic Grand Piano" },
                    { 1, "Bright Acoustic Piano" },
                    { 2, "Electric Grand Piano" },
                    { 3, "Honky-tonk Piano" },
                    { 4, "Electric Piano 1" },
                    { 5, "Electric Piano 2" }
                }
            });

            // 添加电子音色播表
            _logger.Debug("MidiPlaybackService", "添加电子音色播表");
            _waveTables.Add(new WaveTableInfo
            {
                Id = "electronic",
                Name = "电子音色",
                Description = "现代电子音乐音色",
                IsSystem = true,
                InstrumentMappings = new Dictionary<int, string>
                {
                    { 16, "Drawbar Organ" },
                    { 17, "Percussive Organ" },
                    { 18, "Rock Organ" },
                    { 19, "Church Organ" },
                    { 25, "Acoustic Guitar (nylon)" },
                    { 26, "Acoustic Guitar (steel)" },
                    { 27, "Electric Guitar (jazz)" },
                    { 28, "Electric Guitar (clean)" }
                }
            });

            // 添加管弦乐播表
            _logger.Debug("MidiPlaybackService", "添加管弦乐播表");
            _waveTables.Add(new WaveTableInfo
            {
                Id = "orchestral",
                Name = "管弦乐",
                Description = "古典管弦乐音色",
                IsSystem = true,
                InstrumentMappings = new Dictionary<int, string>
                {
                    { 40, "Violin" },
                    { 41, "Viola" },
                    { 42, "Cello" },
                    { 43, "Contrabass" },
                    { 48, "Orchestral Harp" },
                    { 49, "Timpani" },
                    { 56, "Trumpet" },
                    { 57, "Trombone" },
                    { 58, "Tuba" }
                }
            });

            // 设置默认播表
            _currentWaveTable = _waveTables.First();
            _currentWaveTableId = _currentWaveTable.Id;
            _logger.Info("MidiPlaybackService", $"播表数据初始化完成，共 {_waveTables.Count} 个播表，默认播表: {_currentWaveTable.Name}");

            await Task.CompletedTask;
        }

        /// <summary>
        /// 应用播表设置
        /// </summary>
        private async Task ApplyWaveTableSettingsAsync(WaveTableInfo waveTable)
        {
            if (_midiOutHandle == -1)
                return;

            try
            {
                // 这里可以根据播表配置应用不同的音色设置
                // 例如，可以设置默认的乐器程序
                if (waveTable.InstrumentMappings.Any())
                {
                    var firstInstrument = waveTable.InstrumentMappings.First();
                    await ChangeInstrumentAsync(firstInstrument.Key);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用播表设置失败: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            CleanupAsync().Wait();
        }
    }
}