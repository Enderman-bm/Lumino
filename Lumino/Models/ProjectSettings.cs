using System;

namespace Lumino.Models
{
    /// <summary>
    /// 项目设置数据模型
    /// </summary>
    public class ProjectSettings
    {
        /// <summary>
        /// BPM (Beats Per Minute) - 每分钟节拍数
        /// </summary>
        public double BPM { get; set; } = 120.0;

        /// <summary>
        /// PPQ (Pulses Per Quarter Note) - 每四分音符的脉冲数/Ticks
        /// 用于MIDI导出的时间分辨率
        /// </summary>
        public int PPQ { get; set; } = 1920;

        /// <summary>
        /// 项目名称
        /// </summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>
        /// MIDI作者信息
        /// </summary>
        public string MidiAuthor { get; set; } = string.Empty;

        /// <summary>
        /// MIDI简介/版权信息
        /// </summary>
        public string MidiDescription { get; set; } = string.Empty;

        /// <summary>
        /// 项目创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 项目最后修改时间
        /// </summary>
        public DateTime LastModifiedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 克隆设置对象
        /// </summary>
        public ProjectSettings Clone()
        {
            return new ProjectSettings
            {
                BPM = this.BPM,
                PPQ = this.PPQ,
                ProjectName = this.ProjectName,
                MidiAuthor = this.MidiAuthor,
                MidiDescription = this.MidiDescription,
                CreatedTime = this.CreatedTime,
                LastModifiedTime = this.LastModifiedTime
            };
        }
    }
}
