using Lumino.Models.Music;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// MIDI转换服务接口，专门处理MIDI文件导入导出时的Tick转换
    /// </summary>
    public interface IMidiConversionService
    {
        /// <summary>
        /// 四分音符的标准tick值（MIDI标准）
        /// </summary>
        int QuarterNoteTicks { get; }

        /// <summary>
        /// 将音乐分数转换为MIDI tick值
        /// </summary>
        /// <param name="fraction">音乐分数</param>
        /// <returns>MIDI tick值</returns>
        double ConvertToTicks(MusicalFraction fraction);

        /// <summary>
        /// 从MIDI tick值创建音乐分数
        /// </summary>
        /// <param name="ticks">MIDI tick值</param>
        /// <returns>音乐分数</returns>
        MusicalFraction ConvertFromTicks(double ticks);

        /// <summary>
        /// 设置每四分音符的tick数（用于不同的MIDI文件格式）
        /// </summary>
        /// <param name="ticksPerQuarterNote">每四分音符的tick数</param>
        void SetTicksPerQuarterNote(int ticksPerQuarterNote);
    }
}