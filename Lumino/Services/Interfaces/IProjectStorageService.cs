using Lumino.Models.Music;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lumino.Services.Interfaces
{
    public interface IProjectStorageService
    {
        Task<bool> SaveProjectAsync(string filePath, IEnumerable<Note> notes, ProjectMetadata metadata);
        Task<(IEnumerable<Note> notes, ProjectMetadata metadata)> LoadProjectAsync(string filePath);
        Task<bool> ExportMidiAsync(string filePath, IEnumerable<Note> notes, Lumino.Models.ProjectSettings? projectSettings = null);
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
        public int BeatsPerMeasure { get; set; } = 4;
        public int TicksPerBeat { get; set; } = 96;
        public double Tempo { get; set; } = 120.0;
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;
    }
}