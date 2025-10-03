using System;
using Lumino.Models.Music;
using Lumino.Services.Interfaces;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// MIDI转换服务实现，专门处理MIDI文件导入导出时的Tick转换
    /// </summary>
    public class MidiConversionService : IMidiConversionService
    {
        /// <summary>
        /// 四分音符的标准tick值（MIDI标准）
        /// </summary>
        public int QuarterNoteTicks { get; private set; } = 96;

        /// <summary>
        /// 将音乐分数转换为MIDI tick值
        /// 使用传统音乐记谱法计算：1/4 = 四分音符 = QuarterNoteTicks
        /// </summary>
        /// <param name="fraction">音乐分数</param>
        /// <returns>MIDI tick值</returns>
        public double ConvertToTicks(MusicalFraction fraction)
        {
            // 传统音乐记谱法转换：
            // 1/1 = 全音符 = 4 * 四分音符 = 4 * QuarterNoteTicks
            // 1/2 = 二分音符 = 2 * 四分音符 = 2 * QuarterNoteTicks  
            // 1/4 = 四分音符 = 1 * 四分音符 = QuarterNoteTicks
            // 1/8 = 八分音符 = 0.5 * 四分音符 = QuarterNoteTicks/2
            // 1/16 = 十六分音符 = 0.25 * 四分音符 = QuarterNoteTicks/4

            // 公式：(4/分母) * (分子) * QuarterNoteTicks
            return (double)fraction.Numerator * 4 / fraction.Denominator * QuarterNoteTicks;
        }

        /// <summary>
        /// 从MIDI tick值创建音乐分数
        /// </summary>
        /// <param name="ticks">MIDI tick值</param>
        /// <returns>音乐分数</returns>
        public MusicalFraction ConvertFromTicks(double ticks)
        {
            // 添加安全检查
            if (double.IsNaN(ticks) || double.IsInfinity(ticks) || ticks < 0)
            {
                return new MusicalFraction(1, 16); // 默认十六分音符
            }

            // 修复：如果ticks为0或接近0，直接返回0
            if (Math.Abs(ticks) < 1e-10)
            {
                return new MusicalFraction(0, 1); // 0时间位置
            }

            // 计算相对于四分音符的倍数
            var quarterNoteMultiple = ticks / QuarterNoteTicks;

            // 支持常见的音符时值分母：1, 2, 4, 8, 16, 32, 64
            var commonDenominators = new[] { 1, 2, 4, 8, 16, 32, 64 };

            foreach (var denominator in commonDenominators)
            {
                // 按照传统记谱法计算分子
                var numerator = Math.Round(quarterNoteMultiple * denominator / 4.0);

                if (numerator >= 1 && numerator <= int.MaxValue)
                {
                    var intNumerator = (int)numerator;
                    var testFraction = new MusicalFraction(intNumerator, denominator);

                    // 检查转换后的tick值是否匹配
                    if (Math.Abs(ConvertToTicks(testFraction) - ticks) < 0.001)
                    {
                        return testFraction;
                    }
                }
            }

            // 默认使用64分音符精度
            var bestNumerator = Math.Max(1, Math.Round(quarterNoteMultiple * 64 / 4.0));
            if (bestNumerator <= int.MaxValue)
            {
                return new MusicalFraction((int)bestNumerator, 64);
            }

            // 如果仍然溢出，返回安全的默认值
            return new MusicalFraction(1, 16); // 默认十六分音符
        }

        /// <summary>
        /// 设置每四分音符的tick数（用于不同的MIDI文件格式）
        /// </summary>
        /// <param name="ticksPerQuarterNote">每四分音符的tick数</param>
        public void SetTicksPerQuarterNote(int ticksPerQuarterNote)
        {
            if (ticksPerQuarterNote <= 0)
                throw new ArgumentException("每四分音符的tick数必须大于0", nameof(ticksPerQuarterNote));
            
            QuarterNoteTicks = ticksPerQuarterNote;
        }

        /// <summary>
        /// 量化位置到网格（基于MIDI tick）
        /// </summary>
        /// <param name="positionInTicks">要量化的位置（tick）</param>
        /// <param name="gridUnit">网格单位（此MusicalFraction）</param>
        /// <returns>量化后的位置（tick）</returns>
        public double QuantizeToGridTicks(double positionInTicks, MusicalFraction gridUnit)
        {
            // 添加安全检查
            if (double.IsNaN(positionInTicks) || double.IsInfinity(positionInTicks))
            {
                return 0;
            }

            // 修复：如果位置已经非常接近0，直接返回0，避免量化偏移
            if (Math.Abs(positionInTicks) < 1e-10)
            {
                return 0;
            }

            var gridSizeInTicks = ConvertToTicks(gridUnit);
            if (gridSizeInTicks <= 0)
            {
                return positionInTicks; // 如果网格大小无效，返回原值
            }

            // 使用更精确的量化算法，确保0位置不会偏移
            var quantizedPosition = Math.Round(positionInTicks / gridSizeInTicks) * gridSizeInTicks;
            
            // 确保量化后的位置不为负数（除非原位置就是负数）
            if (positionInTicks >= 0 && quantizedPosition < 0)
            {
                quantizedPosition = 0;
            }

            return quantizedPosition;
        }

        /// <summary>
        /// 计算从起始位置到结束位置的量化长度（基于MIDI tick）
        /// </summary>
        /// <param name="startTicks">起始位置（tick）</param>
        /// <param name="endTicks">结束位置（tick）</param>
        /// <param name="gridUnit">网格单位</param>
        /// <returns>量化后的长度分数</returns>
        public MusicalFraction CalculateQuantizedDurationFromTicks(double startTicks, double endTicks, MusicalFraction gridUnit)
        {
            var durationTicks = Math.Max(ConvertToTicks(gridUnit), endTicks - startTicks);
            var gridSizeInTicks = ConvertToTicks(gridUnit);

            if (gridSizeInTicks <= 0)
            {
                return gridUnit; // 返回默认网格单位
            }

            var gridUnits = Math.Max(1, Math.Round(durationTicks / gridSizeInTicks));

            // 添加安全检查，避免溢出
            var resultNumerator = gridUnits * gridUnit.Numerator;
            if (resultNumerator >= int.MinValue && resultNumerator <= int.MaxValue)
            {
                return new MusicalFraction((int)resultNumerator, gridUnit.Denominator);
            }

            // 如果溢出，返回原网格单位
            return gridUnit;
        }
    }
}