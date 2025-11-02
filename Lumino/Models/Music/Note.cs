using System;

namespace Lumino.Models.Music
{
    /// <summary>
    /// 音符数据模型 - 纯数据，不包含UI逻辑
    /// </summary>
    public class Note
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int Pitch { get; set; }
        public MusicalFraction StartPosition { get; set; }
        public MusicalFraction Duration { get; set; }
        public int Velocity { get; set; } = 100;
        public string? Lyric { get; set; }
        public int TrackIndex { get; set; } = 0; // 音轨索引，默认为第一个音轨
        
        // 事件曲线值 - 每个音符可以有默认的事件值
        public int PitchBendValue { get; set; } = 0; // 弯音初始值，范围 -8192～8191，默认为 0 (无弯音)
        public int ControlChangeValue { get; set; } = 0; // CC控制器初始值，范围 0-127，默认为 0

        public Note()
        {
            // 移除日志输出以提升性能
        }
    }
}