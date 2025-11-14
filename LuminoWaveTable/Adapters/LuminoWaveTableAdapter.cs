using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnderWaveTableAccessingParty.Services;
using LuminoWaveTable.Interfaces;
using LuminoWaveTable.Services;
using EnderDebugger;

namespace LuminoWaveTable.Adapters
{
    /// <summary>
    /// Lumino播表服务适配器 - 兼容现有的IMidiPlaybackService接口
    /// </summary>
    public class LuminoWaveTableAdapter : EnderWaveTableAccessingParty.Services.IMidiPlaybackService, IDisposable
    {
        private readonly EnderLogger _logger;
        private readonly LuminoWaveTableService _luminoService;
        private bool _isInitialized;
        private bool _isDisposed;

        public LuminoWaveTableAdapter(EnderLogger logger)
        {
            _logger = logger;
            _luminoService = new LuminoWaveTableService();
            _isInitialized = false;
            
            // 订阅事件
            _luminoService.WaveTableChanged += OnWaveTableChanged;
            _luminoService.DeviceChanged += OnDeviceChanged;
            _luminoService.PerformanceUpdated += OnPerformanceUpdated;
            
            _logger.Info("LuminoWaveTableAdapter", "Lumino播表适配器初始化完成");
        }

        public bool IsInitialized => _isInitialized;
        public int CurrentDeviceId { get; set; } = 0;
        public string CurrentWaveTableId { get; set; } = "lumino_gm_complete";
        public List<WaveTableInfo> WaveTables { get; private set; } = new();
        public WaveTableInfo CurrentWaveTable { get; private set; } = new();

        /// <summary>
        /// 初始化播表数据
        /// </summary>
        private async Task InitializeWaveTablesAsync()
        {
            _logger.Debug("LuminoWaveTableAdapter", "开始初始化播表数据");
            WaveTables.Clear();

            try
            {
                // 获取Lumino播表列表
                var luminoWaveTables = await _luminoService.GetWaveTablesAsync();
                
                foreach (var luminoWaveTable in luminoWaveTables)
                {
                    var waveTableInfo = ConvertToWaveTableInfo(luminoWaveTable);
                    WaveTables.Add(waveTableInfo);
                }

                // 设置默认播表
                if (WaveTables.Count > 0)
                {
                    var defaultWaveTable = WaveTables.FirstOrDefault(wt => wt.Id == CurrentWaveTableId) ?? WaveTables.First();
                    CurrentWaveTable = defaultWaveTable;
                    CurrentWaveTableId = defaultWaveTable.Id;
                }
                
                _logger.Info("LuminoWaveTableAdapter", $"播表数据初始化完成，共 {WaveTables.Count} 个播表，默认播表: {CurrentWaveTable?.Name}");
            }
            catch (Exception ex)
            {
                _logger.Error("LuminoWaveTableAdapter", $"初始化播表数据失败: {ex.Message}");
                // 添加默认播表作为后备
                AddDefaultWaveTables();
            }
        }

        /// <summary>
        /// 添加默认播表
        /// </summary>
        private void AddDefaultWaveTables()
        {
            // OmniMIDI播表 - 支持完整的GM音色集
            WaveTables.Add(new WaveTableInfo
            {
                Id = "omnimidi",
                Name = "OmniMIDI",
                Description = "OmniMIDI完整GM音色集",
                IsSystem = true,
                InstrumentMappings = CreateGmInstrumentMappings()
            });

            // 电子音乐播表
            WaveTables.Add(new WaveTableInfo
            {
                Id = "electronic",
                Name = "电子音乐",
                Description = "现代电子音乐音色",
                IsSystem = true,
                InstrumentMappings = CreateElectronicInstrumentMappings()
            });

            // 设置默认播表
            CurrentWaveTable = WaveTables.First(wt => wt.Id == "omnimidi");
            CurrentWaveTableId = CurrentWaveTable.Id;
        }

        /// <summary>
        /// 转换Lumino播表信息到标准格式
        /// </summary>
        private WaveTableInfo ConvertToWaveTableInfo(LuminoWaveTable.Models.LuminoWaveTableInfo luminoWaveTable)
        {
            return new WaveTableInfo
            {
                Id = luminoWaveTable.Id,
                Name = luminoWaveTable.Name,
                Description = luminoWaveTable.Description,
                IsSystem = luminoWaveTable.IsSystem,
                CreatedTime = luminoWaveTable.CreatedTime,
                ModifiedTime = luminoWaveTable.ModifiedTime,
                InstrumentMappings = new Dictionary<int, string>(luminoWaveTable.InstrumentMappings)
            };
        }

        /// <summary>
        /// 创建GM完整音色映射
        /// </summary>
        private Dictionary<int, string> CreateGmInstrumentMappings()
        {
            return new Dictionary<int, string>
            {
                // 钢琴类 (0-7)
                { 0, "Acoustic Grand Piano" }, { 1, "Bright Acoustic Piano" }, { 2, "Electric Grand Piano" }, { 3, "Honky-tonk Piano" },
                { 4, "Electric Piano 1" }, { 5, "Electric Piano 2" }, { 6, "Harpsichord" }, { 7, "Clavinet" },
                
                // 打击乐器 (8-15)
                { 8, "Celesta" }, { 9, "Glockenspiel" }, { 10, "Music Box" }, { 11, "Vibraphone" },
                { 12, "Marimba" }, { 13, "Xylophone" }, { 14, "Tubular Bells" }, { 15, "Dulcimer" },
                
                // 风琴类 (16-23)
                { 16, "Drawbar Organ" }, { 17, "Percussive Organ" }, { 18, "Rock Organ" }, { 19, "Church Organ" },
                { 20, "Reed Organ" }, { 21, "Accordion" }, { 22, "Harmonica" }, { 23, "Tango Accordion" },
                
                // 吉他类 (24-31)
                { 24, "Acoustic Guitar (nylon)" }, { 25, "Acoustic Guitar (steel)" }, { 26, "Electric Guitar (jazz)" }, { 27, "Electric Guitar (clean)" },
                { 28, "Electric Guitar (muted)" }, { 29, "Overdriven Guitar" }, { 30, "Distortion Guitar" }, { 31, "Guitar Harmonics" },
                
                // 贝斯类 (32-39)
                { 32, "Acoustic Bass" }, { 33, "Electric Bass (finger)" }, { 34, "Electric Bass (pick)" }, { 35, "Fretless Bass" },
                { 36, "Slap Bass 1" }, { 37, "Slap Bass 2" }, { 38, "Synth Bass 1" }, { 39, "Synth Bass 2" },
                
                // 弦乐类 (40-47)
                { 40, "Violin" }, { 41, "Viola" }, { 42, "Cello" }, { 43, "Contrabass" },
                { 44, "Tremolo Strings" }, { 45, "Pizzicato Strings" }, { 46, "Orchestral Harp" }, { 47, "Timpani" },
                
                // 合奏/人声类 (48-55)
                { 48, "String Ensemble 1" }, { 49, "String Ensemble 2" }, { 50, "Synth Strings 1" }, { 51, "Synth Strings 2" },
                { 52, "Choir Aahs" }, { 53, "Voice Oohs" }, { 54, "Synth Voice" }, { 55, "Orchestra Hit" },
                
                // 铜管类 (56-63)
                { 56, "Trumpet" }, { 57, "Trombone" }, { 58, "Tuba" }, { 59, "Muted Trumpet" },
                { 60, "French Horn" }, { 61, "Brass Section" }, { 62, "Synth Brass 1" }, { 63, "Synth Brass 2" },
                
                // 木管类 (64-71)
                { 64, "Soprano Sax" }, { 65, "Alto Sax" }, { 66, "Tenor Sax" }, { 67, "Baritone Sax" },
                { 68, "Oboe" }, { 69, "English Horn" }, { 70, "Bassoon" }, { 71, "Clarinet" },
                
                // 吹管类 (72-79)
                { 72, "Piccolo" }, { 73, "Flute" }, { 74, "Recorder" }, { 75, "Pan Flute" },
                { 76, "Blown Bottle" }, { 77, "Shakuhachi" }, { 78, "Whistle" }, { 79, "Ocarina" },
                
                // 合成主音类 (80-87)
                { 80, "Synth Lead 1 (square)" }, { 81, "Synth Lead 2 (sawtooth)" }, { 82, "Synth Lead 3 (calliope)" }, { 83, "Synth Lead 4 (chiff)" },
                { 84, "Synth Lead 5 (charang)" }, { 85, "Synth Lead 6 (voice)" }, { 86, "Synth Lead 7 (fifths)" }, { 87, "Synth Lead 8 (bass+lead)" },
                
                // 合成柔音类 (88-95)
                { 88, "Synth Pad 1 (new age)" }, { 89, "Synth Pad 2 (warm)" }, { 90, "Synth Pad 3 (polysynth)" }, { 91, "Synth Pad 4 (choir)" },
                { 92, "Synth Pad 5 (bowed)" }, { 93, "Synth Pad 6 (metallic)" }, { 94, "Synth Pad 7 (halo)" }, { 95, "Synth Pad 8 (sweep)" },
                
                // 合成效果类 (96-103)
                { 96, "FX 1 (rain)" }, { 97, "FX 2 (soundtrack)" }, { 98, "FX 3 (crystal)" }, { 99, "FX 4 (atmosphere)" },
                { 100, "FX 5 (brightness)" }, { 101, "FX 6 (goblins)" }, { 102, "FX 7 (echoes)" }, { 103, "FX 8 (sci-fi)" },
                
                // 民族乐器类 (104-111)
                { 104, "Sitar" }, { 105, "Banjo" }, { 106, "Shamisen" }, { 107, "Koto" },
                { 108, "Kalimba" }, { 109, "Bagpipe" }, { 110, "Fiddle" }, { 111, "Shanai" },
                
                // 打击乐/音效类 (112-127)
                { 112, "Tinkle Bell" }, { 113, "Agogo" }, { 114, "Steel Drums" }, { 115, "Woodblock" },
                { 116, "Taiko Drum" }, { 117, "Melodic Tom" }, { 118, "Synth Drum" }, { 119, "Reverse Cymbal" },
                { 120, "Guitar Fret Noise" }, { 121, "Breath Noise" }, { 122, "Seashore" }, { 123, "Bird Tweet" },
                { 124, "Telephone Ring" }, { 125, "Helicopter" }, { 126, "Applause" }, { 127, "Gunshot" }
            };
        }

        /// <summary>
        /// 创建电子音乐音色映射
        /// </summary>
        private Dictionary<int, string> CreateElectronicInstrumentMappings()
        {
            return new Dictionary<int, string>
            {
                { 25, "Acoustic Guitar (steel)" }, { 26, "Electric Guitar (jazz)" }, { 27, "Electric Guitar (clean)" }, { 28, "Electric Guitar (muted)" },
                { 29, "Overdriven Guitar" }, { 30, "Distortion Guitar" }, { 80, "Synth Lead 1 (square)" }, { 81, "Synth Lead 2 (sawtooth)" },
                { 82, "Synth Lead 3 (calliope)" }, { 83, "Synth Lead 4 (chiff)" }, { 84, "Synth Lead 5 (charang)" }, { 85, "Synth Lead 6 (voice)" },
                { 86, "Synth Lead 7 (fifths)" }, { 87, "Synth Lead 8 (bass+lead)" }, { 88, "Synth Pad 1 (new age)" }, { 89, "Synth Pad 2 (warm)" },
                { 90, "Synth Pad 3 (polysynth)" }, { 91, "Synth Pad 4 (choir)" }, { 92, "Synth Pad 5 (bowed)" }, { 93, "Synth Pad 6 (metallic)" },
                { 94, "Synth Pad 7 (halo)" }, { 95, "Synth Pad 8 (sweep)" }, { 96, "FX 1 (rain)" }, { 97, "FX 2 (soundtrack)" },
                { 98, "FX 3 (crystal)" }, { 99, "FX 4 (atmosphere)" }, { 100, "FX 5 (brightness)" }, { 101, "FX 6 (goblins)" },
                { 102, "FX 7 (echoes)" }, { 103, "FX 8 (sci-fi)" }, { 120, "Guitar Fret Noise" }, { 121, "Breath Noise" },
                { 122, "Seashore" }, { 123, "Bird Tweet" }, { 124, "Telephone Ring" }, { 125, "Helicopter" }, { 126, "Applause" }, { 127, "Gunshot" }
            };
        }

        /// <summary>
        /// 播表变更事件处理
        /// </summary>
        private void OnWaveTableChanged(object? sender, WaveTableChangedEventArgs e)
        {
            try
            {
                // 更新当前播表
                var newWaveTable = _luminoService.GetCurrentWaveTableAsync().Result;
                if (newWaveTable != null)
                {
                    CurrentWaveTable = ConvertToWaveTableInfo(newWaveTable);
                    CurrentWaveTableId = newWaveTable.Id;
                    _logger.Info("LuminoWaveTableAdapter", $"播表已切换: {newWaveTable.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("LuminoWaveTableAdapter", $"处理播表变更事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设备变更事件处理
        /// </summary>
        private void OnDeviceChanged(object? sender, DeviceChangedEventArgs e)
        {
            _logger.Info("LuminoWaveTableAdapter", $"MIDI设备已切换 - 从 {e.OldDeviceId} 到 {e.NewDeviceId}");
        }

        /// <summary>
        /// 性能更新事件处理
        /// </summary>
        private void OnPerformanceUpdated(object? sender, PerformanceUpdatedEventArgs e)
        {
            // 可以在这里处理性能更新，如更新UI等
            _logger.Debug("LuminoWaveTableAdapter", $"性能更新 - CPU: {e.PerformanceInfo.CpuUsage:F1}%, 延迟: {e.PerformanceInfo.LatencyMs:F2}ms");
        }

        /// <summary>
        /// 初始化服务
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                _logger.Warn("LuminoWaveTableAdapter", "服务已经初始化");
                return;
            }

            try
            {
                _logger.Info("LuminoWaveTableAdapter", "开始初始化播表适配器...");
                
                // 初始化Lumino服务
                await _luminoService.InitializeAsync();
                
                // 初始化播表数据
                await InitializeWaveTablesAsync();
                
                // 同步设备ID
                CurrentDeviceId = _luminoService.CurrentDeviceId;
                CurrentWaveTableId = _luminoService.CurrentWaveTableId;
                
                _isInitialized = true;
                _logger.Info("LuminoWaveTableAdapter", "播表适配器初始化完成");
            }
            catch (Exception ex)
            {
                _logger.Error("LuminoWaveTableAdapter", $"初始化失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取MIDI设备列表
        /// </summary>
        public Task<List<MidiDeviceInfo>> GetMidiDevicesAsync()
        {
            try
            {
                var luminoDevices = _luminoService.GetMidiDevicesAsync().Result;
                var devices = luminoDevices.Select(d => new MidiDeviceInfo
                {
                    DeviceId = d.DeviceId,
                    Name = d.Name,
                    IsDefault = d.IsDefault,
                    Technology = (int)d.Technology,
                    Voices = (int)d.Voices,
                    Notes = (int)d.Notes,
                    ChannelMask = (int)d.ChannelMask,
                    Support = (int)d.Support
                }).ToList();
                
                return Task.FromResult(devices);
            }
            catch (Exception ex)
            {
                _logger.Error("LuminoWaveTableAdapter", $"获取MIDI设备列表失败: {ex.Message}");
                return Task.FromResult(new List<MidiDeviceInfo>());
            }
        }

        /// <summary>
        /// 获取播表列表
        /// </summary>
        public Task<List<WaveTableInfo>> GetWaveTablesAsync()
        {
            return Task.FromResult(new List<WaveTableInfo>(WaveTables));
        }

        /// <summary>
        /// 播放音符
        /// </summary>
        public async Task PlayNoteAsync(int midiNote, int velocity = 100, int durationMs = 200, int channel = 0)
        {
            if (!_isInitialized)
            {
                _logger.Warn("LuminoWaveTableAdapter", "服务未初始化");
                return;
            }

            try
            {
                await _luminoService.PlayNoteAsync(midiNote, velocity, durationMs, channel);
            }
            catch (Exception ex)
            {
                _logger.Error("LuminoWaveTableAdapter", $"播放音符失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止音符
        /// </summary>
        public async Task StopNoteAsync(int midiNote, int channel = 0)
        {
            if (!_isInitialized)
            {
                return;
            }

            try
            {
                await _luminoService.StopNoteAsync(midiNote, channel);
            }
            catch (Exception ex)
            {
                _logger.Error("LuminoWaveTableAdapter", $"停止音符失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更改乐器
        /// </summary>
        public async Task ChangeInstrumentAsync(int instrumentId, int channel = 0)
        {
            if (!_isInitialized)
            {
                return;
            }

            try
            {
                await _luminoService.ChangeInstrumentAsync(instrumentId, channel);
            }
            catch (Exception ex)
            {
                _logger.Error("LuminoWaveTableAdapter", $"更改乐器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置播表
        /// </summary>
        public async Task SetWaveTableAsync(string waveTableId)
        {
            if (!_isInitialized)
            {
                return;
            }

            try
            {
                await _luminoService.SetWaveTableAsync(waveTableId);
                CurrentWaveTableId = waveTableId;
            }
            catch (Exception ex)
            {
                _logger.Error("LuminoWaveTableAdapter", $"设置播表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前播表
        /// </summary>
        public async Task<WaveTableInfo?> GetCurrentWaveTableAsync()
        {
            if (!_isInitialized)
            {
                return null;
            }

            try
            {
                var luminoWaveTable = await _luminoService.GetCurrentWaveTableAsync();
                return luminoWaveTable != null ? ConvertToWaveTableInfo(luminoWaveTable) : null;
            }
            catch (Exception ex)
            {
                _logger.Error("LuminoWaveTableAdapter", $"获取当前播表失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public async Task CleanupAsync()
        {
            try
            {
                _logger.Info("LuminoWaveTableAdapter", "开始清理资源...");
                
                await _luminoService.CleanupAsync();
                
                _logger.Info("LuminoWaveTableAdapter", "资源清理完成");
            }
            catch (Exception ex)
            {
                _logger.Error("LuminoWaveTableAdapter", $"清理资源失败: {ex.Message}");
            }
        }


        /// <summary>
        /// 切换播表
        /// </summary>
        public bool SwitchWaveTable(string waveTableId)
        {
            try
            {
                _ = Task.Run(async () => await SetWaveTableAsync(waveTableId));
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("LuminoWaveTableAdapter", $"切换播表失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 加载自定义SoundFonts列表
        /// </summary>
        public bool LoadCustomSoundFonts(string directory)
        {
            // Lumino播表服务不支持SoundFonts，返回false
            _logger.Warn("LuminoWaveTableAdapter", "Lumino播表服务不支持SoundFonts加载");
            return false;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                _logger.Info("LuminoWaveTableAdapter", "释放资源...");
                
                // 取消事件订阅
                _luminoService.WaveTableChanged -= OnWaveTableChanged;
                _luminoService.DeviceChanged -= OnDeviceChanged;
                _luminoService.PerformanceUpdated -= OnPerformanceUpdated;
                
                // 释放Lumino服务
                _luminoService.Dispose();
                
                _isDisposed = true;
                _logger.Info("LuminoWaveTableAdapter", "资源释放完成");
            }
            catch (Exception ex)
            {
                _logger.Error("LuminoWaveTableAdapter", $"释放资源失败: {ex.Message}");
            }
        }
    }
}