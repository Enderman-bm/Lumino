using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LuminoWaveTable.Models;

namespace LuminoWaveTable.Interfaces
{
    /// <summary>
    /// 播表引擎信息
    /// </summary>
    public class WaveTableEngineInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsAvailable { get; set; } = true;
        public string? ErrorMessage { get; set; }
        public string Provider { get; set; } = string.Empty;
    }

    /// <summary>
    /// 播表性能信息
    /// </summary>
    public class WaveTablePerformanceInfo
    {
        public double CpuUsage { get; set; }
        public long MemoryUsage { get; set; }
        public int ActiveVoices { get; set; }
        public int MaxVoices { get; set; }
        public double LatencyMs { get; set; }
        public bool IsOptimized { get; set; }
    }

    /// <summary>
    /// Lumino播表服务接口
    /// </summary>
    public interface ILuminoWaveTableService : IDisposable
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        string ServiceName { get; }

        /// <summary>
        /// 服务版本
        /// </summary>
        string ServiceVersion { get; }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 是否正在播放
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// 当前选中的设备ID
        /// </summary>
        int CurrentDeviceId { get; set; }

        /// <summary>
        /// 当前选中的播表ID
        /// </summary>
        string CurrentWaveTableId { get; set; }

        /// <summary>
        /// 性能信息
        /// </summary>
        WaveTablePerformanceInfo PerformanceInfo { get; }

        /// <summary>
        /// 获取可用的MIDI设备列表
        /// </summary>
        Task<List<LuminoMidiDeviceInfo>> GetMidiDevicesAsync();

        /// <summary>
        /// 获取可用的播表列表
        /// </summary>
        Task<List<LuminoWaveTableInfo>> GetWaveTablesAsync();

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
        /// 发送原始MIDI消息
        /// </summary>
        Task SendMidiMessageAsync(uint message);

        /// <summary>
        /// 设置当前播表
        /// </summary>
        Task SetWaveTableAsync(string waveTableId);

        /// <summary>
        /// 获取当前播表
        /// </summary>
        Task<LuminoWaveTableInfo?> GetCurrentWaveTableAsync();

        /// <summary>
        /// 初始化服务
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// 清理资源
        /// </summary>
        Task CleanupAsync();

        /// <summary>
        /// 重置MIDI流
        /// </summary>
        Task ResetMidiStreamAsync();

        /// <summary>
        /// 获取性能统计信息
        /// </summary>
        Task<WaveTablePerformanceInfo> GetPerformanceInfoAsync();

        /// <summary>
        /// 优化性能设置
        /// </summary>
        Task OptimizePerformanceAsync();

        /// <summary>
        /// 获取可用的播表引擎列表
        /// </summary>
        List<WaveTableEngineInfo> GetAvailableEngines();

        /// <summary>
        /// 播表变更事件
        /// </summary>
        event EventHandler<WaveTableChangedEventArgs>? WaveTableChanged;

        /// <summary>
        /// 设备变更事件
        /// </summary>
        event EventHandler<DeviceChangedEventArgs>? DeviceChanged;

        /// <summary>
        /// 性能更新事件
        /// </summary>
        event EventHandler<PerformanceUpdatedEventArgs>? PerformanceUpdated;
    }

    /// <summary>
    /// 播表变更事件参数
    /// </summary>
    public class WaveTableChangedEventArgs : EventArgs
    {
        public string OldWaveTableId { get; set; } = string.Empty;
        public string NewWaveTableId { get; set; } = string.Empty;
        public LuminoWaveTableInfo? NewWaveTable { get; set; }
    }

    /// <summary>
    /// 设备变更事件参数
    /// </summary>
    public class DeviceChangedEventArgs : EventArgs
    {
        public int OldDeviceId { get; set; }
        public int NewDeviceId { get; set; }
        public LuminoMidiDeviceInfo? NewDevice { get; set; }
    }

    /// <summary>
    /// 性能更新事件参数
    /// </summary>
    public class PerformanceUpdatedEventArgs : EventArgs
    {
        public WaveTablePerformanceInfo PerformanceInfo { get; set; } = new();
    }
}