using Lumino.Models.Music;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lumino.Services.Interfaces
{
    public interface IProjectStorageService
    {
        Task<bool> SaveProjectAsync(string filePath, ProjectSnapshot snapshot, ProjectMetadata metadata, System.Threading.CancellationToken cancellationToken = default);
        Task<(ProjectSnapshot snapshot, ProjectMetadata metadata)> LoadProjectAsync(string filePath, System.Threading.CancellationToken cancellationToken = default);
        Task<bool> ExportMidiAsync(string filePath, ProjectSnapshot snapshot);
        Task<IEnumerable<Note>> ImportMidiAsync(string filePath);
        
        /// <summary>
        /// 导入MIDI文件（带进度回调）
        /// </summary>
        /// <param name="filePath">MIDI文件路径</param>
        /// <param name="progress">进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>导入的音符集合</returns>
        Task<IEnumerable<Note>> ImportMidiWithProgressAsync(string filePath, 
            IProgress<(double Progress, string Status)>? progress = null, 
            CancellationToken cancellationToken = default);
    }

    public class ProjectMetadata
    {
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        /// <summary>
        /// 版权信息
        /// </summary>
        public string Copyright { get; set; } = "";
        public int BeatsPerMeasure { get; set; } = 4;
        public int TicksPerBeat { get; set; } = 96;
        /// <summary>
        /// BPM 速度（默认120）
        /// </summary>
        public double Tempo { get; set; } = 120.0;
        /// <summary>
        /// 创建日期
        /// </summary>
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;
        /// <summary>
        /// 累计创作时间（秒）
        /// </summary>
        public double TotalEditingTimeSeconds { get; set; } = 0.0;
        /// <summary>
        /// 每个音轨的元数据集合（保存 TrackViewModel 需要持久化的字段）
        /// </summary>
        public System.Collections.Generic.List<TrackMetadata> Tracks { get; set; } = new System.Collections.Generic.List<TrackMetadata>();
    }

    public class TrackMetadata
    {
        public int TrackNumber { get; set; }
        public string TrackName { get; set; } = string.Empty;
        public int MidiChannel { get; set; } = -1;
        public int ChannelGroupIndex { get; set; } = -1;
        public int ChannelNumberInGroup { get; set; } = -1;
        public string Instrument { get; set; } = string.Empty;
        public string ColorTag { get; set; } = "#FFFFFF";
        public bool IsConductorTrack { get; set; } = false;
        // 新增可持久化的轨道状态
        public bool IsMuted { get; set; } = false;
        public bool IsSolo { get; set; } = false;
        /// <summary>
        /// Pan 范围 [-1.0, 1.0]
        /// </summary>
        public double Pan { get; set; } = 0.0;
        /// <summary>
        /// 音量（0.0 - 1.0）
        /// </summary>
        public double Volume { get; set; } = 1.0;
    }
}