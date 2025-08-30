using System;

namespace DominoNext.Models.Music
{
    /// <summary>
    /// 音符数据模型 - 纯数据，不包含UI逻辑
    /// </summary>
    public class Note
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // 节能酱：这个Pitch指的是键号。不是弯音轮。
        public int Pitch { get; set; }
        public MusicalFraction StartPosition { get; set; }
        public MusicalFraction Duration { get; set; }
        public int Velocity { get; set; } = 100;

        //节能酱：音符数据不需要歌词。
        //public string? Lyric { get; set; }

        /*节能酱：这留着干啥？
        /// <summary>
        /// 创建四分音符
        /// </summary>
        public static Note CreateQuarterNote(int pitch, MusicalFraction startPosition, int velocity = 100)
        {
            return new Note
            {
                Pitch = pitch,
                StartPosition = startPosition,
                Duration = MusicalFraction.QuarterNote,
                Velocity = velocity
            };
        }

        /// <summary>
        /// 克隆音符
        /// </summary>
        public Note Clone()
        {
            return new Note
            {
                Id = Guid.NewGuid(), // 新的ID
                Pitch = Pitch,
                StartPosition = StartPosition,
                Duration = Duration,
                Velocity = Velocity,
                Lyric = Lyric
            };
        }
        */
        public override string ToString()
        {
            return $"Note(Pitch:{Pitch}, Start:{StartPosition}, Duration:{Duration})";
        }
    }
}