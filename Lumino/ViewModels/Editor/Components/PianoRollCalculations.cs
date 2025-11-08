using System;
using EnderDebugger;
using System.Collections.Generic;
using System.Linq;
using Lumino.Models.Music;

namespace Lumino.ViewModels.Editor.Components
{
    /// <summary>
    /// 钢琴卷帘计算组件 - 负责所有的尺寸和位置计算
    /// 遵循单一职责原则，专注于数值和尺寸的计算逻辑
    /// 现在使用严格的歌曲长度和滚动条对应关系
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

        #region 基础尺寸单位
        /// <summary>
        /// 基础时间单位（一四分音符）对应的像素宽度
        /// </summary>
        public double BaseQuarterNoteWidth => 100.0 * _zoomManager.Zoom;

        /// <summary>
        /// 直接基于分数的时间到像素的缩放比例
        /// </summary>
        public double TimeToPixelScale => BaseQuarterNoteWidth;

        /// <summary>
        /// 键高度
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

        #region 音符计算
        /// <summary>
        /// 计算指定音符时长的像素宽度
        /// </summary>
        public double GetNoteWidth(MusicalFraction duration)
        {
            return duration.ToDouble() * BaseQuarterNoteWidth;
        }

        /// <summary>
        /// 根据音符指定时间位置的X坐标
        /// </summary>
        public double GetNoteX(MusicalFraction startPosition)
        {
            return startPosition.ToDouble() * BaseQuarterNoteWidth;
        }

        /// <summary>
        /// 根据音符指定音高的Y坐标
        /// </summary>
        public double GetNoteY(int pitch)
        {
            // MIDI音符127在顶部，0在底部
            return (127 - pitch) * KeyHeight;
        }
        #endregion

        #region 歌曲长度计算 - 新的严格标准
        /// <summary>
        /// 计算歌曲的有效长度（四分音符单位）
        /// 取音符最远结束位置和MIDI文件时长的最大值
        /// </summary>
        /// <param name="noteEndPositions">音符结束位置的集合</param>
        /// <param name="midiFileDuration">MIDI文件时长（可选，四分音符单位）</param>
        /// <returns>歌曲有效长度（四分音符单位）</returns>
        public double CalculateEffectiveSongLength(IEnumerable<MusicalFraction> noteEndPositions, double? midiFileDuration = null)
        {
            double maxContentPosition = 0;

            // 检查MIDI文件的时长
            if (midiFileDuration.HasValue && midiFileDuration.Value > 0)
            {
                maxContentPosition = Math.Max(maxContentPosition, midiFileDuration.Value);
            }

            // 检查音符的结束位置
            if (noteEndPositions.Any())
            {
                var maxNoteEndPosition = noteEndPositions.Max();
                maxContentPosition = Math.Max(maxContentPosition, maxNoteEndPosition.ToDouble());
            }

            // 如果没有任何有效内容位置，返回默认的8小节
            if (maxContentPosition <= 0)
            {
                return BeatsPerMeasure * 8; // 8小节 = 32四分音符
            }

            // 返回实际的歌曲有效长度
            return maxContentPosition;
        }

        /// <summary>
        /// 计算滚动条总长度（四分音符单位）
        /// 严格按照：歌曲有效长度 + 8小节
        /// </summary>
        /// <param name="effectiveSongLength">歌曲有效长度（四分音符单位）</param>
        /// <returns>滚动条总长度（四分音符单位）</returns>
        public double CalculateScrollbarTotalLength(double effectiveSongLength)
        {
            // 固定添加8小节
            var additionalMeasures = 8;
            var additionalLength = additionalMeasures * BeatsPerMeasure; // 8小节 = 32四分音符
            
            return effectiveSongLength + additionalLength;
        }

        /// <summary>
        /// 计算滚动条总长度（像素单位）
        /// </summary>
        /// <param name="effectiveSongLength">歌曲有效长度（四分音符单位）</param>
        /// <returns>滚动条总长度（像素单位）</returns>
        public double CalculateScrollbarTotalLengthInPixels(double effectiveSongLength)
        {
            var totalLengthInQuarterNotes = CalculateScrollbarTotalLength(effectiveSongLength);
            return totalLengthInQuarterNotes * BaseQuarterNoteWidth;
        }

        /// <summary>
        /// 计算内容的总宽度（像素单位），基于新的严格标准
        /// 这个方法现在严格按照"歌曲有效长度+8小节"计算
        /// </summary>
        /// <param name="noteEndPositions">音符结束位置的集合</param>
        /// <param name="midiFileDuration">MIDI文件时长（可选，四分音符单位）</param>
        public double CalculateContentWidth(IEnumerable<MusicalFraction> noteEndPositions, double? midiFileDuration = null)
        {
            // 计算歌曲有效长度
            var effectiveSongLength = CalculateEffectiveSongLength(noteEndPositions, midiFileDuration);
            
            // 计算滚动条总长度（像素）
            var totalLengthInPixels = CalculateScrollbarTotalLengthInPixels(effectiveSongLength);
            
            EnderLogger.Instance.Debug("PianoRollCalculations", $"歌曲有效长度: {effectiveSongLength:F2} 四分音符, 滚动条总长度: {totalLengthInPixels:F1} 像素, 基础四分音符宽度: {BaseQuarterNoteWidth:F1} 像素");
            
            return totalLengthInPixels;
        }

        /// <summary>
        /// 计算当前视口相对于总歌曲长度的比例
        /// </summary>
        /// <param name="viewportWidth">视口宽度（像素）</param>
        /// <param name="noteEndPositions">音符结束位置的集合</param>
        /// <param name="midiFileDuration">MIDI文件时长（可选）</param>
        /// <returns>视口比例（0-1）</returns>
        public double CalculateViewportRatio(double viewportWidth, IEnumerable<MusicalFraction> noteEndPositions, double? midiFileDuration = null)
        {
            var totalContentWidth = CalculateContentWidth(noteEndPositions, midiFileDuration);
            
            if (totalContentWidth <= 0)
                return 1.0;
            
            var ratio = Math.Min(1.0, viewportWidth / totalContentWidth);
            
            EnderLogger.Instance.Debug("PianoRollCalculations", $"视口比例: {ratio:P2} (视口宽度: {viewportWidth:F1}, 总宽度: {totalContentWidth:F1})");
            
            return ratio;
        }

        /// <summary>
        /// 计算当前滚动位置相对于总长度的比例
        /// </summary>
        /// <param name="currentScrollOffset">当前滚动偏移（像素）</param>
        /// <param name="viewportWidth">视口宽度（像素）</param>
        /// <param name="noteEndPositions">音符结束位置的集合</param>
        /// <param name="midiFileDuration">MIDI文件时长（可选）</param>
        /// <returns>滚动位置比例（0-1）</returns>
        public double CalculateScrollPositionRatio(double currentScrollOffset, double viewportWidth, IEnumerable<MusicalFraction> noteEndPositions, double? midiFileDuration = null)
        {
            var totalContentWidth = CalculateContentWidth(noteEndPositions, midiFileDuration);
            var maxScrollOffset = Math.Max(0, totalContentWidth - viewportWidth);
            
            if (maxScrollOffset <= 0)
                return 0.0;
            
            var ratio = Math.Min(1.0, currentScrollOffset / maxScrollOffset);
            
            EnderLogger.Instance.Debug("PianoRollCalculations", $"滚动位置比例: {ratio:P2} (滚动偏移: {currentScrollOffset:F1}, 最大滚动: {maxScrollOffset:F1})");
            
            return ratio;
        }
        #endregion

        #region 兼容性方法
        /// <summary>
        /// 计算内容的总宽度（向后兼容）
        /// 已过时，使用CalculateContentWidth(noteEndPositions, midiFileDuration)代替
        /// </summary>
        /// <param name="noteEndPositions">音符结束位置的集合</param>
        [Obsolete("使用CalculateContentWidth(noteEndPositions, midiFileDuration)代替")]
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
        /// 获取MIDI音符名
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