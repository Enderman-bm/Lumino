using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using EnderDebugger;
using EnderWaveTableAccessingParty.Models;

namespace EnderWaveTableAccessingParty.Services
{
    public partial class MidiPlaybackService : IMidiPlaybackService, IDisposable
    {
        // KDMAPI函数声明
        [DllImport("OmniMIDI.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int InitializeKDMAPIStream();
        
        [DllImport("OmniMIDI.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int TerminateKDMAPIStream();
        
        [DllImport("OmniMIDI.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SendDirectData(uint dwData);
        
        [DllImport("OmniMIDI.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SendDirectDataNoBuf(uint dwData);
        
        [DllImport("OmniMIDI.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int IsKDMAPIAvailable();
        
        [DllImport("OmniMIDI.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int LoadCustomSoundFontsList(string Directory);
        
        [DllImport("OmniMIDI.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void ResetKDMAPIStream();

        private readonly EnderLogger _logger;
        private readonly List<WaveTableInfo> _waveTables;
        private WaveTableInfo _currentWaveTable;
        private string _currentWaveTableId;
        private bool _isPlaying;
        private bool _disposed = false;
        private bool _isKDMAPIAvailable = false;
        private bool _isInitialized = false;

        public MidiPlaybackService(EnderLogger logger)
        {
            _logger = logger;
            _waveTables = new List<WaveTableInfo>();
            _currentWaveTable = new WaveTableInfo();
            _currentWaveTableId = "";
            _isPlaying = false;
            
            // 检查KDMAPI是否可用
            _isKDMAPIAvailable = IsKDMAPIAvailable() == 1;
            if (_isKDMAPIAvailable)
            {
                _logger.Info("MidiPlaybackService", "KDMAPI is available");
                // 初始化KDMAPI流
                InitializeKDMAPIStream();
            }
            else
            {
                _logger.Warn("MidiPlaybackService", "KDMAPI is not available, falling back to standard MIDI output");
            }
        }

        public bool IsInitialized => _isInitialized;

        public int CurrentDeviceId { get; set; } = 0;

        public string CurrentWaveTableId 
        { 
            get => _currentWaveTableId;
            set => _currentWaveTableId = value;
        }

        public List<WaveTableInfo> WaveTables => _waveTables;
        public WaveTableInfo CurrentWaveTable => _currentWaveTable;
        public bool IsPlaying => _isPlaying;

        /// <summary>
        /// 初始化播表数据
        /// </summary>
        private async Task InitializeWaveTablesAsync()
        {
            _logger.Debug("MidiPlaybackService", "开始初始化播表数据");
            _waveTables.Clear();

            // 添加OmniMIDI播表 - 支持完整的GM音色集
            _logger.Debug("MidiPlaybackService", "添加OmniMIDI播表");
            _waveTables.Add(new WaveTableInfo
            {
                Id = "omnimidi",
                Name = "OmniMIDI",
                Description = "OmniMIDI完整GM音色集",
                IsSystem = true,
                InstrumentMappings = new Dictionary<int, string>
                {
                    // 钢琴类 (0-7)
                    { 0, "Acoustic Grand Piano" },
                    { 1, "Bright Acoustic Piano" },
                    { 2, "Electric Grand Piano" },
                    { 3, "Honky-tonk Piano" },
                    { 4, "Electric Piano 1" },
                    { 5, "Electric Piano 2" },
                    { 6, "Harpsichord" },
                    { 7, "Clavinet" },
                    
                    // 打击乐器 (8-15)
                    { 8, "Celesta" },
                    { 9, "Glockenspiel" },
                    { 10, "Music Box" },
                    { 11, "Vibraphone" },
                    { 12, "Marimba" },
                    { 13, "Xylophone" },
                    { 14, "Tubular Bells" },
                    { 15, "Dulcimer" },
                    
                    // 风琴类 (16-23)
                    { 16, "Drawbar Organ" },
                    { 17, "Percussive Organ" },
                    { 18, "Rock Organ" },
                    { 19, "Church Organ" },
                    { 20, "Reed Organ" },
                    { 21, "Accordion" },
                    { 22, "Harmonica" },
                    { 23, "Tango Accordion" },
                    
                    // 吉他类 (24-31)
                    { 24, "Acoustic Guitar (nylon)" },
                    { 25, "Acoustic Guitar (steel)" },
                    { 26, "Electric Guitar (jazz)" },
                    { 27, "Electric Guitar (clean)" },
                    { 28, "Electric Guitar (muted)" },
                    { 29, "Overdriven Guitar" },
                    { 30, "Distortion Guitar" },
                    { 31, "Guitar Harmonics" },
                    
                    // 贝斯类 (32-39)
                    { 32, "Acoustic Bass" },
                    { 33, "Electric Bass (finger)" },
                    { 34, "Electric Bass (pick)" },
                    { 35, "Fretless Bass" },
                    { 36, "Slap Bass 1" },
                    { 37, "Slap Bass 2" },
                    { 38, "Synth Bass 1" },
                    { 39, "Synth Bass 2" },
                    
                    // 弦乐类 (40-47)
                    { 40, "Violin" },
                    { 41, "Viola" },
                    { 42, "Cello" },
                    { 43, "Contrabass" },
                    { 44, "Tremolo Strings" },
                    { 45, "Pizzicato Strings" },
                    { 46, "Orchestral Harp" },
                    { 47, "Timpani" },
                    
                    // 合奏/人声类 (48-55)
                    { 48, "String Ensemble 1" },
                    { 49, "String Ensemble 2" },
                    { 50, "Synth Strings 1" },
                    { 51, "Synth Strings 2" },
                    { 52, "Choir Aahs" },
                    { 53, "Voice Oohs" },
                    { 54, "Synth Voice" },
                    { 55, "Orchestra Hit" },
                    
                    // 铜管类 (56-63)
                    { 56, "Trumpet" },
                    { 57, "Trombone" },
                    { 58, "Tuba" },
                    { 59, "Muted Trumpet" },
                    { 60, "French Horn" },
                    { 61, "Brass Section" },
                    { 62, "Synth Brass 1" },
                    { 63, "Synth Brass 2" },
                    
                    // 吹管类 (64-71)
                    { 64, "Soprano Sax" },
                    { 65, "Alto Sax" },
                    { 66, "Tenor Sax" },
                    { 67, "Baritone Sax" },
                    { 68, "Oboe" },
                    { 69, "English Horn" },
                    { 70, "Bassoon" },
                    { 71, "Clarinet" },
                    
                    // 吹管类 (72-79)
                    { 72, "Piccolo" },
                    { 73, "Flute" },
                    { 74, "Recorder" },
                    { 75, "Pan Flute" },
                    { 76, "Blown Bottle" },
                    { 77, "Shakuhachi" },
                    { 78, "Whistle" },
                    { 79, "Ocarina" },
                    
                    // 合成主音类 (80-87)
                    { 80, "Synth Lead 1 (square)" },
                    { 81, "Synth Lead 2 (sawtooth)" },
                    { 82, "Synth Lead 3 (calliope)" },
                    { 83, "Synth Lead 4 (chiff)" },
                    { 84, "Synth Lead 5 (charang)" },
                    { 85, "Synth Lead 6 (voice)" },
                    { 86, "Synth Lead 7 (fifths)" },
                    { 87, "Synth Lead 8 (bass+lead)" },
                    
                    // 合成柔音类 (88-95)
                    { 88, "Synth Pad 1 (new age)" },
                    { 89, "Synth Pad 2 (warm)" },
                    { 90, "Synth Pad 3 (polysynth)" },
                    { 91, "Synth Pad 4 (choir)" },
                    { 92, "Synth Pad 5 (bowed)" },
                    { 93, "Synth Pad 6 (metallic)" },
                    { 94, "Synth Pad 7 (halo)" },
                    { 95, "Synth Pad 8 (sweep)" },
                    
                    // 合成效果类 (96-103)
                    { 96, "FX 1 (rain)" },
                    { 97, "FX 2 (soundtrack)" },
                    { 98, "FX 3 (crystal)" },
                    { 99, "FX 4 (atmosphere)" },
                    { 100, "FX 5 (brightness)" },
                    { 101, "FX 6 (goblins)" },
                    { 102, "FX 7 (echoes)" },
                    { 103, "FX 8 (sci-fi)" },
                    
                    // 民族乐器类 (104-111)
                    { 104, "Sitar" },
                    { 105, "Banjo" },
                    { 106, "Shamisen" },
                    { 107, "Koto" },
                    { 108, "Kalimba" },
                    { 109, "Bagpipe" },
                    { 110, "Fiddle" },
                    { 111, "Shanai" },
                    
                    // 打击乐/音效类 (112-127)
                    { 112, "Tinkle Bell" },
                    { 113, "Agogo" },
                    { 114, "Steel Drums" },
                    { 115, "Woodblock" },
                    { 116, "Taiko Drum" },
                    { 117, "Melodic Tom" },
                    { 118, "Synth Drum" },
                    { 119, "Reverse Cymbal" },
                    { 120, "Guitar Fret Noise" },
                    { 121, "Breath Noise" },
                    { 122, "Seashore" },
                    { 123, "Bird Tweet" },
                    { 124, "Telephone Ring" },
                    { 125, "Helicopter" },
                    { 126, "Applause" },
                    { 127, "Gunshot" }
                }
            });

            // 添加电子音乐播表
            _logger.Debug("MidiPlaybackService", "添加电子音乐播表");
            _waveTables.Add(new WaveTableInfo
            {
                Id = "electronic",
                Name = "电子音乐",
                Description = "现代电子音乐音色",
                IsSystem = true,
                InstrumentMappings = new Dictionary<int, string>
                {
                    { 25, "Acoustic Guitar (steel)" }, // 重复但用于电子音乐场景
                    { 26, "Electric Guitar (jazz)" },  // 重复但用于电子音乐场景
                    { 27, "Electric Guitar (clean)" }, // 重复但用于电子音乐场景
                    { 29, "Overdriven Guitar" },       // 重复但用于电子音乐场景
                    { 30, "Distortion Guitar" },       // 重复但用于电子音乐场景
                    { 80, "Synth Lead 1 (square)" },
                    { 81, "Synth Lead 2 (sawtooth)" },
                    { 82, "Synth Lead 3 (calliope)" },
                    { 83, "Synth Lead 4 (chiff)" },
                    { 84, "Synth Lead 5 (charang)" },
                    { 85, "Synth Lead 6 (voice)" },
                    { 86, "Synth Lead 7 (fifths)" },
                    { 87, "Synth Lead 8 (bass+lead)" },
                    { 88, "Synth Pad 1 (new age)" },
                    { 89, "Synth Pad 2 (warm)" },
                    { 90, "Synth Pad 3 (polysynth)" },
                    { 91, "Synth Pad 4 (choir)" },
                    { 92, "Synth Pad 5 (bowed)" },
                    { 93, "Synth Pad 6 (metallic)" },
                    { 94, "Synth Pad 7 (halo)" },
                    { 95, "Synth Pad 8 (sweep)" },
                    { 96, "FX 1 (rain)" },
                    { 97, "FX 2 (soundtrack)" },
                    { 98, "FX 3 (crystal)" },
                    { 99, "FX 4 (atmosphere)" },
                    { 100, "FX 5 (brightness)" },
                    { 101, "FX 6 (goblins)" },
                    { 102, "FX 7 (echoes)" },
                    { 103, "FX 8 (sci-fi)" },
                    { 120, "Guitar Fret Noise" },
                    { 121, "Breath Noise" },
                    { 122, "Seashore" },
                    { 123, "Bird Tweet" },
                    { 124, "Telephone Ring" },
                    { 125, "Helicopter" },
                    { 126, "Applause" },
                    { 127, "Gunshot" }
                }
            });

            // 设置默认播表
            if (_waveTables.Any(wt => wt.Id == "omnimidi"))
            {
                _currentWaveTable = _waveTables.First(wt => wt.Id == "omnimidi");
            }
            else
            {
                _currentWaveTable = _waveTables.First();
            }
            
            _currentWaveTableId = _currentWaveTable.Id;
            _logger.Info("MidiPlaybackService", $"播表数据初始化完成，共 {_waveTables.Count} 个播表，默认播表: {_currentWaveTable.Name}");

            await Task.CompletedTask;
        }

        /// <summary>
        /// 发送MIDI消息
        /// </summary>
        /// <param name="message"></param>
        public void SendMidiMessage(uint message)
        {
            if (_isKDMAPIAvailable)
            {
                // 解析MIDI消息（仅用于日志）
                byte status = (byte)(message & 0xFF);
                byte data1 = (byte)((message >> 8) & 0xFF);
                byte data2 = (byte)((message >> 16) & 0xFF);
                
                _logger.Debug("MidiPlaybackService", $"发送 MIDI 消息: 0x{message:X8} [Status=0x{status:X2} Data1={data1} Data2={data2}]");
                
                // 使用KDMAPI发送消息
                SendDirectData(message);
            }
            else
            {
                // 如果KDMAPI不可用，则不发送消息
                _logger.Warn("MidiPlaybackService", $"KDMAPI not available, message not sent: 0x{message:X8}");
            }
        }

        /// <summary>
        /// 发送MIDI消息（无缓冲）
        /// </summary>
        /// <param name="message"></param>
        public void SendMidiMessageNoBuf(uint message)
        {
            if (_isKDMAPIAvailable)
            {
                // 使用KDMAPI发送消息（无缓冲）
                SendDirectDataNoBuf(message);
            }
            else
            {
                // 如果KDMAPI不可用，则不发送消息
                _logger.Warn("MidiPlaybackService", "KDMAPI not available, message not sent");
            }
        }

        /// <summary>
        /// 重置MIDI流
        /// </summary>
        public void ResetMidiStream()
        {
            if (_isKDMAPIAvailable)
            {
                ResetKDMAPIStream();
            }
            else
            {
                _logger.Warn("MidiPlaybackService", "KDMAPI not available, stream not reset");
            }
        }

        /// <summary>
        /// 加载自定义SoundFonts列表
        /// </summary>
        /// <param name="directory"></param>
        /// <returns></returns>
        public bool LoadCustomSoundFonts(string directory)
        {
            if (_isKDMAPIAvailable)
            {
                return LoadCustomSoundFontsList(directory) == 1;
            }
            else
            {
                _logger.Warn("MidiPlaybackService", "KDMAPI not available, SoundFonts not loaded");
                return false;
            }
        }

        public async Task InitializeAsync()
        {
            await InitializeWaveTablesAsync();
            _isInitialized = true;
        }

        public void Play()
        {
            _isPlaying = true;
            _logger.Info("MidiPlaybackService", "开始播放");
        }

        public void Stop()
        {
            _isPlaying = false;
            _logger.Info("MidiPlaybackService", "停止播放");
            
            // 发送所有音符关闭消息
            for (int i = 0; i < 16; i++)
            {
                SendMidiMessageNoBuf((uint)(0xB0 | i | (123 << 8))); // All notes off
                SendMidiMessageNoBuf((uint)(0xB0 | i | (120 << 8))); // All sound off
            }
        }

        public bool SwitchWaveTable(string waveTableId)
        {
            var waveTable = _waveTables.FirstOrDefault(wt => wt.Id == waveTableId);
            if (waveTable != null)
            {
                _currentWaveTable = waveTable;
                _currentWaveTableId = waveTableId;
                _logger.Info("MidiPlaybackService", $"切换播表到: {waveTable.Name}");
                return true;
            }
            
            _logger.Warn("MidiPlaybackService", $"未找到播表: {waveTableId}");
            return false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 清理托管资源
                }
                
                // 终止KDMAPI流
                if (_isKDMAPIAvailable)
                {
                    TerminateKDMAPIStream();
                }
                
                _disposed = true;
            }
        }

        ~MidiPlaybackService()
        {
            Dispose(false);
        }

        // 实现IMidiPlaybackService接口
        public Task<List<MidiDeviceInfo>> GetMidiDevicesAsync()
        {
            // 对于KDMAPI，我们只有一个设备 - OmniMIDI
            var devices = new List<MidiDeviceInfo>
            {
                new MidiDeviceInfo
                {
                    DeviceId = 0,
                    Name = "OmniMIDI",
                    IsDefault = true,
                    Technology = 0,
                    Voices = 128,
                    Notes = 128,
                    ChannelMask = 0xFFFF,
                    Support = 0
                }
            };
            
            return Task.FromResult(devices);
        }

        public Task<List<WaveTableInfo>> GetWaveTablesAsync()
        {
            return Task.FromResult(_waveTables.ToList());
        }

        public async Task PlayNoteAsync(int midiNote, int velocity = 100, int durationMs = 200, int channel = 0)
        {
            // 发送Note On消息
            uint noteOn = (uint)(0x90 | channel | (midiNote << 8) | (velocity << 16));
            SendMidiMessage(noteOn);
            
            // 等待指定时间后发送Note Off消息
            await Task.Delay(durationMs);
            
            uint noteOff = (uint)(0x80 | channel | (midiNote << 8) | (0 << 16));
            SendMidiMessage(noteOff);
        }

        public Task StopNoteAsync(int midiNote, int channel = 0)
        {
            uint noteOff = (uint)(0x80 | channel | (midiNote << 8) | (0 << 16));
            SendMidiMessage(noteOff);
            return Task.CompletedTask;
        }

        public Task ChangeInstrumentAsync(int instrumentId, int channel = 0)
        {
            uint programChange = (uint)(0xC0 | channel | (instrumentId << 8));
            SendMidiMessage(programChange);
            return Task.CompletedTask;
        }

        public Task SetWaveTableAsync(string waveTableId)
        {
            SwitchWaveTable(waveTableId);
            return Task.CompletedTask;
        }

        public Task<WaveTableInfo?> GetCurrentWaveTableAsync()
        {
            return Task.FromResult<WaveTableInfo?>(_currentWaveTable);
        }

        public Task CleanupAsync()
        {
            // 停止播放并重置流
            Stop();
            ResetMidiStream();
            return Task.CompletedTask;
        }
    }
}