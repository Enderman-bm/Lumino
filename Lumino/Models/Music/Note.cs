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

        public Note()
        {
            EnderDebugger.EnderLogger.Instance.Info("Note", $"[EnderDebugger][2025-10-02 18:41:03.114][EnderLogger][Note]音符对象已创建，Id:{Id}, 音高:{Pitch}, 轨道:{TrackIndex}");
        }
    }
}