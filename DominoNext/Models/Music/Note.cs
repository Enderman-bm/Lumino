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
    }
}