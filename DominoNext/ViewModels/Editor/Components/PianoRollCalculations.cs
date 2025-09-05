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
        /// 支持自动延长小节功能
        /// </summary>
        public double CalculateContentWidth(IEnumerable<MusicalFraction> noteEndPositions)
        {
            if (!noteEndPositions.Any())
            {
                // 没有音符时，至少显示8个小节
                return 8 * MeasureWidth;
            }

            // 找到最后一个音符的结束位置
            var maxEndPosition = noteEndPositions.Max();
            var maxEndPixels = maxEndPosition.ToDouble() * BaseQuarterNoteWidth;

            // 计算最后音符所在的小节
            var lastNoteMeasure = Math.Ceiling(maxEndPosition.ToDouble() / BeatsPerMeasure);
            
            // 在最后音符的小节后再增加2-3个小节，确保有足够的编辑空间
            var totalMeasures = Math.Max(8, lastNoteMeasure + 3); // 至少8个小节，最后音符后至少3个小节
            
            var calculatedWidth = totalMeasures * MeasureWidth;
            
            // 确保计算的宽度至少包含最后一个音符加上缓冲区
            var minRequiredWidth = maxEndPixels + 2 * MeasureWidth;
            
            return Math.Max(calculatedWidth, minRequiredWidth);
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