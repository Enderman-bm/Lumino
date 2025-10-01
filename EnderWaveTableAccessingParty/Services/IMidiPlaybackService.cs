using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnderDebugger;

namespace EnderWaveTableAccessingParty.Services
{
    /// <summary>
    /// MIDI设备信息
    /// </summary>
    public class MidiDeviceInfo
    {
        public int DeviceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public int Technology { get; set; }
        public int Voices { get; set; }
        public int Notes { get; set; }
        public int ChannelMask { get; set; }
        public int Support { get; set; }
    }

    /// <summary>
    /// 播表信息
    /// </summary>
    public class WaveTableInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSystem { get; set; }
        public DateTime CreatedTime { get; set; } = DateTime.Now;
        public DateTime ModifiedTime { get; set; } = DateTime.Now;
        public Dictionary<int, string> InstrumentMappings { get; set; } = new();
    }

    /// <summary>
    /// MIDI播放服务接口
    /// </summary>
    public interface IMidiPlaybackService
    {
        /// <summary>
        /// 获取可用的MIDI设备列表
        /// </summary>
        Task<List<MidiDeviceInfo>> GetMidiDevicesAsync();

        /// <summary>
        /// 获取可用的播表列表
        /// </summary>
        Task<List<WaveTableInfo>> GetWaveTablesAsync();

        /// <summary>
        /// 播放音符
        /// </summary>
        Task PlayNoteAsync(int midiNote, int velocity = 100, int durationMs = 200, int channel = 0);

        /// <summary>
        /// 停止音符
        /// </summary>
        Task StopNoteAsync(int midiNote, int channel = 0);

        /// <summary>
        /// 更改乐器
        /// </summary>
        Task ChangeInstrumentAsync(int instrumentId, int channel = 0);

        /// <summary>
        /// 设置当前播表
        /// </summary>
        Task SetWaveTableAsync(string waveTableId);

        /// <summary>
        /// 获取当前播表
        /// </summary>
        Task<WaveTableInfo?> GetCurrentWaveTableAsync();

        /// <summary>
        /// 初始化服务
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// 清理资源
        /// </summary>
        Task CleanupAsync();

        /// <summary>
        /// 是否已初始化
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 当前选中的设备ID
        /// </summary>
        int CurrentDeviceId { get; set; }

        /// <summary>
        /// 当前选中的播表ID
        /// </summary>
        string CurrentWaveTableId { get; set; }
    }
}