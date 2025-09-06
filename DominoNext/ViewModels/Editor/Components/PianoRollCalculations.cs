using System;
using System.Collections.Generic;
using System.Linq;
using DominoNext.Models.Music;

namespace DominoNext.ViewModels.Editor.Components
{
    /// <summary>
    /// 钢琴卷帘计算组件 - 负责所有的尺寸和位置计算
    /// 符合单一职责原则，专注于坐标和尺寸的计算逻辑
    /// </summary>
    public class PianoRollCalculations
    {
        #region 依赖的配置
        private readonly PianoRollConfiguration _configuration;
        #endregion

        #region 构造函数
        public PianoRollCalculations(PianoRollConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }
        #endregion

        #region 基础计算属性
        /// <summary>
        /// 基础时间单位：一个四分音符对应的像素宽度
        /// </summary>
        public double BaseQuarterNoteWidth => 100.0 * _configuration.Zoom;

        /// <summary>
        /// 直接基于分数计算时间到像素的缩放比例
        /// </summary>
        public double TimeToPixelScale => BaseQuarterNoteWidth;

        /// <summary>
        /// 音符高度
        /// </summary>
        public double KeyHeight => 12.0 * _configuration.VerticalZoom;

        /// <summary>
        /// 标准4/4拍的每小节拍数
        /// </summary>
        public int BeatsPerMeasure => 4;
        #endregion

        #region 尺寸计算
        /// <summary>
        /// 一小节的像素宽度
        /// </summary>
        public double MeasureWidth => BeatsPerMeasure * BaseQuarterNoteWidth;

        /// <summary>
        /// 一拍的像素宽度（等于四分音符宽度）
        /// </summary>
        public double BeatWidth => BaseQuarterNoteWidth;

        /// <summary>
        /// 八分音符宽度
        /// </summary>
        public double EighthNoteWidth => BaseQuarterNoteWidth * 0.5;

        /// <summary>
        /// 十六分音符宽度
        /// </summary>
        public double SixteenthNoteWidth => BaseQuarterNoteWidth * 0.25;

        /// <summary>
        /// 钢琴卷帘总高度（128个MIDI音符）
        /// </summary>
        public double TotalHeight => 128 * KeyHeight;
        #endregion

        #region 音符相关计算
        /// <summary>
        /// 计算指定音乐分数的像素宽度
        /// </summary>
        public double GetNoteWidth(MusicalFraction duration)
        {
            return duration.ToDouble() * BaseQuarterNoteWidth;
        }

        /// <summary>
        /// 计算音符在指定时间位置的X坐标
        /// </summary>
        public double GetNoteX(MusicalFraction startPosition)
        {
            return startPosition.ToDouble() * BaseQuarterNoteWidth;
        }

        /// <summary>
        /// 计算音符在指定音高的Y坐标
        /// </summary>
        public double GetNoteY(int pitch)
        {
            // MIDI音符127在顶部，0在底部
            return (127 - pitch) * KeyHeight;
        }

        /// <summary>
        /// 计算内容的最大宽度（基于音符数据）
        /// 支持自动延长小节功能和缩放响应
        /// </summary>
        /// <param name="noteEndPositions">音符结束位置的集合</param>
        /// <param name="midiFileDuration">MIDI文件时长（可选），当提供时将覆盖基于音符的计算</param>
        public double CalculateContentWidth(IEnumerable<MusicalFraction> noteEndPositions, double? midiFileDuration = null)
        {
            // 默认至少8个小节
            var defaultMeasures = 8;
            var defaultWidth = defaultMeasures * MeasureWidth;

            if (!noteEndPositions.Any() && !midiFileDuration.HasValue)
            {
                // 没有音符和MIDI时长时，返回默认宽度
                return defaultWidth;
            }

            double maxContentPosition = 0;

            // 考虑MIDI文件的时长
            if (midiFileDuration.HasValue && midiFileDuration.Value > 0)
            {
                maxContentPosition = Math.Max(maxContentPosition, midiFileDuration.Value);
            }

            // 考虑音符的结束位置
            if (noteEndPositions.Any())
            {
                var maxNoteEndPosition = noteEndPositions.Max();
                maxContentPosition = Math.Max(maxContentPosition, maxNoteEndPosition.ToDouble());
            }

            // 如果没有有效的内容位置，返回默认宽度
            if (maxContentPosition <= 0)
            {
                return defaultWidth;
            }

            // 计算最后一个音符或MIDI结束所在的小节
            var lastContentMeasure = Math.Ceiling(maxContentPosition / BeatsPerMeasure);
            
            // 在最后音符或MIDI结束的小节后再增加4-6个小节，确保有足够的编辑空间
            // 增加的小节数根据内容长度动态调整
            var additionalMeasures = Math.Max(4, Math.Min(8, (int)(lastContentMeasure * 0.1)));
            var totalMeasures = Math.Max(defaultMeasures, lastContentMeasure + additionalMeasures);
            
            var calculatedWidth = totalMeasures * MeasureWidth;
            
            // 确保计算的宽度至少包含最后一个音符或MIDI结束位置加上充足的缓冲区
            // 缓冲区大小基于当前缩放级别动态调整
            var bufferWidth = Math.Max(3 * MeasureWidth, MeasureWidth * _configuration.Zoom);
            var minRequiredWidth = maxContentPosition * BaseQuarterNoteWidth + bufferWidth;
            
            var finalWidth = Math.Max(calculatedWidth, minRequiredWidth);
            
            // 对于很长的MIDI文件，确保不会产生过大的内存占用
            // 但仍然要保证完整的滚动访问
            var maxReasonableWidth = 1000000; // 100万像素的合理上限
            if (finalWidth > maxReasonableWidth)
            {
                // 如果超过合理上限，使用基于实际内容的最小必需宽度
                finalWidth = Math.Max(minRequiredWidth, maxReasonableWidth);
            }
            
            return finalWidth;
        }

        /// <summary>
        /// 计算内容的最大宽度（基于音符数据）
        /// 该方法已过时，建议使用带有midiFileDuration参数的CalculateContentWidth方法
        /// </summary>
        /// <param name="noteEndPositions">音符结束位置的集合</param>
        [Obsolete("使用CalculateContentWidth(noteEndPositions, midiFileDuration)代替")]
        public double CalculateContentWidth(IEnumerable<MusicalFraction> noteEndPositions)
        {
            return CalculateContentWidth(noteEndPositions, null);
        }
        #endregion

        #region 工具方法
        /// <summary>
        /// 检查指定的MIDI音符是否为黑键
        /// </summary>
        public bool IsBlackKey(int midiNote)
        {
            var noteInOctave = midiNote % 12;
            return noteInOctave == 1 || noteInOctave == 3 || noteInOctave == 6 || noteInOctave == 8 || noteInOctave == 10;
        }

        /// <summary>
        /// 获取MIDI音符的名称
        /// </summary>
        public string GetNoteName(int midiNote)
        {
            var noteNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            var octave = midiNote / 12 - 1;
            var noteIndex = midiNote % 12;
            return $"{noteNames[noteIndex]}{octave}";
        }

        /// <summary>
        /// 计算网格线的位置
        /// </summary>
        public IEnumerable<double> GetGridLinePositions(MusicalFraction gridUnit, double visibleStartTime, double visibleEndTime)
        {
            var gridUnitInQuarterNotes = gridUnit.ToDouble();
            var startPosition = Math.Floor(visibleStartTime / gridUnitInQuarterNotes) * gridUnitInQuarterNotes;
            
            var positions = new List<double>();
            for (var position = startPosition; position <= visibleEndTime; position += gridUnitInQuarterNotes)
            {
                positions.Add(position * BaseQuarterNoteWidth);
            }
            
            return positions;
        }

        /// <summary>
        /// 计算小节线的位置
        /// </summary>
        public IEnumerable<double> GetMeasureLinePositions(double visibleStartTime, double visibleEndTime)
        {
            var measureUnitInQuarterNotes = BeatsPerMeasure; // 4个四分音符为一小节
            var startMeasure = Math.Floor(visibleStartTime / measureUnitInQuarterNotes) * measureUnitInQuarterNotes;
            
            var positions = new List<double>();
            for (var position = startMeasure; position <= visibleEndTime; position += measureUnitInQuarterNotes)
            {
                positions.Add(position * BaseQuarterNoteWidth);
            }
            
            return positions;
        }
        #endregion
    }
}