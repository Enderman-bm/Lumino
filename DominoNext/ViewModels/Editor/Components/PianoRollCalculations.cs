using System;
using System.Collections.Generic;
using System.Linq;
using DominoNext.Models.Music;

namespace DominoNext.ViewModels.Editor.Components
{
    /// <summary>
    /// 钢琴卷帘计算组件 - 负责所有的尺寸和位置计算
    /// 遵循单一职责原则，专注于数值和尺寸的计算逻辑
    /// 现在使用独立的缩放管理器获取缩放值
    /// </summary>
    public class PianoRollCalculations
    {
        #region 依赖组件
        private readonly PianoRollZoomManager _zoomManager;
        #endregion

        #region 构造函数
        public PianoRollCalculations(PianoRollZoomManager zoomManager)
        {
            _zoomManager = zoomManager ?? throw new ArgumentNullException(nameof(zoomManager));
        }
        #endregion

        #region 基础单位属性
        /// <summary>
        /// 基础时间单位：一个四分音符对应的像素宽度
        /// </summary>
        public double BaseQuarterNoteWidth => 100.0 * _zoomManager.Zoom;

        /// <summary>
        /// 直接基于分数的时间到像素的缩放比例
        /// </summary>
        public double TimeToPixelScale => BaseQuarterNoteWidth;

        /// <summary>
        /// 音符高度
        /// </summary>
        public double KeyHeight => 12.0 * _zoomManager.VerticalZoom;

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
        /// 一拍的像素宽度（四分音符宽度）
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
        /// 计算指定音符时长的像素宽度
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
        /// 计算内容的总宽度（基于音符数据）
        /// 支持自动延长小节功能和MIDI文件对应
        /// </summary>
        /// <param name="noteEndPositions">音符结束位置的集合</param>
        /// <param name="midiFileDuration">MIDI文件时长（可选，如果提供时会基于这个计算）</param>
        public double CalculateContentWidth(IEnumerable<MusicalFraction> noteEndPositions, double? midiFileDuration = null)
        {
            // 默认显示8个小节
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

            // 如果没有有效内容位置，返回默认宽度
            if (maxContentPosition <= 0)
            {
                return defaultWidth;
            }

            // 计算容纳最后一个音符或MIDI内容所需的小节数
            var lastContentMeasure = Math.Ceiling(maxContentPosition / BeatsPerMeasure);
            
            // 在最后的音符或MIDI内容后添加4-6个小节，确保有足够的编辑空间
            // 额外的小节数随内容长度动态调整
            var additionalMeasures = Math.Max(4, Math.Min(8, (int)(lastContentMeasure * 0.1)));
            var totalMeasures = Math.Max(defaultMeasures, lastContentMeasure + additionalMeasures);
            
            var calculatedWidth = totalMeasures * MeasureWidth;
            
            // 确保计算宽度至少包含最后一个音符或MIDI内容位置加上缓冲区域
            // 缓冲区大小根据当前缩放动态调整
            var bufferWidth = Math.Max(3 * MeasureWidth, MeasureWidth * _zoomManager.Zoom);
            var minRequiredWidth = maxContentPosition * BaseQuarterNoteWidth + bufferWidth;
            
            var finalWidth = Math.Max(calculatedWidth, minRequiredWidth);
            
            // 对于很长的MIDI文件，确保不会过度占用内存空间
            // 但仍然要保证基本的功能需求
            var maxReasonableWidth = 1000000; // 100万像素的合理上限
            if (finalWidth > maxReasonableWidth)
            {
                // 如果超过上限，使用基于实际数据的最小必需宽度
                finalWidth = Math.Max(minRequiredWidth, maxReasonableWidth);
            }
            
            return finalWidth;
        }

        /// <summary>
        /// 计算内容的总宽度（基于音符数据）
        /// 此方法已过时，请使用带midiFileDuration参数的CalculateContentWidth重载
        /// </summary>
        /// <param name="noteEndPositions">音符结束位置的集合</param>
        [Obsolete("使用CalculateContentWidth(noteEndPositions, midiFileDuration)重载")]
        public double CalculateContentWidth(IEnumerable<MusicalFraction> noteEndPositions)
        {
            return CalculateContentWidth(noteEndPositions, null);
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 判断指定的MIDI音符是否为黑键
        /// </summary>
        public bool IsBlackKey(int midiNote)
        {
            var noteInOctave = midiNote % 12;
            return noteInOctave == 1 || noteInOctave == 3 || noteInOctave == 6 || noteInOctave == 8 || noteInOctave == 10;
        }

        /// <summary>
        /// 获取MIDI音符名称
        /// </summary>
        public string GetNoteName(int midiNote)
        {
            var noteNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            var octave = midiNote / 12 - 1;
            var noteIndex = midiNote % 12;
            return $"{noteNames[noteIndex]}{octave}";
        }

        /// <summary>
        /// 获取网格线的位置
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
        /// 获取小节线的位置
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