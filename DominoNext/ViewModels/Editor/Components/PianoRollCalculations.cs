using System;
using System.Collections.Generic;
using System.Linq;
using Lumino.Models.Music;

namespace Lumino.ViewModels.Editor.Components
{
    /// <summary>
    /// ���پ���������� - �������еĳߴ��λ�ü���
    /// ��ѭ��һְ��ԭ��רע����ֵ�ͳߴ�ļ����߼�
    /// ����ʹ���ϸ�ĸ������Ⱥ͹�������Ӧ��ϵ
    /// </summary>
    public class PianoRollCalculations
    {
        #region �������
        private readonly PianoRollZoomManager _zoomManager;
        #endregion

        #region ���캯��
        public PianoRollCalculations(PianoRollZoomManager zoomManager)
        {
            _zoomManager = zoomManager ?? throw new ArgumentNullException(nameof(zoomManager));
        }
        #endregion

        #region �����ߴ絥λ
        /// <summary>
        /// ����ʱ�䵥λ��һ�ķ���������Ӧ�����ؿ���
        /// </summary>
        public double BaseQuarterNoteWidth => 100.0 * _zoomManager.Zoom;

        /// <summary>
        /// ֱ�ӻ��ڷ�����ʱ�䵽���ص����ű���
        /// </summary>
        public double TimeToPixelScale => BaseQuarterNoteWidth;

        /// <summary>
        /// ���߶�
        /// </summary>
        public double KeyHeight => 12.0 * _zoomManager.VerticalZoom;

        /// <summary>
        /// ��׼4/4�ĵ�ÿС������
        /// </summary>
        public int BeatsPerMeasure => 4;
        #endregion

        #region �ߴ����
        /// <summary>
        /// һС�ڵ����ؿ���
        /// </summary>
        public double MeasureWidth => BeatsPerMeasure * BaseQuarterNoteWidth;

        /// <summary>
        /// һ�ĵ����ؿ��ȣ��ķ��������ȣ�
        /// </summary>
        public double BeatWidth => BaseQuarterNoteWidth;

        /// <summary>
        /// �˷���������
        /// </summary>
        public double EighthNoteWidth => BaseQuarterNoteWidth * 0.5;

        /// <summary>
        /// ʮ������������
        /// </summary>
        public double SixteenthNoteWidth => BaseQuarterNoteWidth * 0.25;

        /// <summary>
        /// ���پ����ܸ߶ȣ�128��MIDI������
        /// </summary>
        public double TotalHeight => 128 * KeyHeight;

        /// <summary>
        /// ��С��ʾMIDI����
        /// </summary>
        public int MinVisiblePitch => 0;

        /// <summary>
        /// 最大显示MIDI音高
        /// </summary>
        public int MaxVisiblePitch => 127;

        /// <summary>
        /// 当前BPM
        /// </summary>
        public double CurrentBPM => 120;
        #endregion

        #region ��������
        /// <summary>
        /// ����ָ������ʱ�������ؿ���
        /// </summary>
        public double GetNoteWidth(MusicalFraction duration)
        {
            return duration.ToDouble() * BaseQuarterNoteWidth;
        }

        /// <summary>
        /// ��������ָ��ʱ��λ�õ�X����
        /// </summary>
        public double GetNoteX(MusicalFraction startPosition)
        {
            return startPosition.ToDouble() * BaseQuarterNoteWidth;
        }

        /// <summary>
        /// ��������ָ�����ߵ�Y����
        /// </summary>
        public double GetNoteY(int pitch)
        {
            // MIDI����127�ڶ�����0�ڵײ�
            return (127 - pitch) * KeyHeight;
        }
        #endregion

        #region �������ȼ��� - �µ��ϸ��׼
        /// <summary>
        /// �����������Ч���ȣ��ķ�������λ��
        /// ȡ������Զ����λ�ú�MIDI�ļ�ʱ�������ֵ
        /// </summary>
        /// <param name="noteEndPositions">��������λ�õļ���</param>
        /// <param name="midiFileDuration">MIDI�ļ�ʱ������ѡ���ķ�������λ��</param>
        /// <returns>������Ч���ȣ��ķ�������λ��</returns>
        public double CalculateEffectiveSongLength(IEnumerable<MusicalFraction> noteEndPositions, double? midiFileDuration = null)
        {
            double maxContentPosition = 0;

            // ���MIDI�ļ���ʱ��
            if (midiFileDuration.HasValue && midiFileDuration.Value > 0)
            {
                maxContentPosition = Math.Max(maxContentPosition, midiFileDuration.Value);
            }

            // ��������Ľ���λ��
            if (noteEndPositions.Any())
            {
                var maxNoteEndPosition = noteEndPositions.Max();
                maxContentPosition = Math.Max(maxContentPosition, maxNoteEndPosition.ToDouble());
            }

            // ���û���κ���Ч����λ�ã�����Ĭ�ϵ�8С��
            if (maxContentPosition <= 0)
            {
                return BeatsPerMeasure * 8; // 8С�� = 32�ķ�����
            }

            // ����ʵ�ʵĸ�����Ч����
            return maxContentPosition;
        }

        /// <summary>
        /// ����������ܳ��ȣ��ķ�������λ��
        /// �ϸ��գ�������Ч���� + 8С��
        /// </summary>
        /// <param name="effectiveSongLength">������Ч���ȣ��ķ�������λ��</param>
        /// <returns>�������ܳ��ȣ��ķ�������λ��</returns>
        public double CalculateScrollbarTotalLength(double effectiveSongLength)
        {
            // �̶�����8С��
            var additionalMeasures = 8;
            var additionalLength = additionalMeasures * BeatsPerMeasure; // 8С�� = 32�ķ�����
            
            return effectiveSongLength + additionalLength;
        }

        /// <summary>
        /// ����������ܳ��ȣ����ص�λ��
        /// </summary>
        /// <param name="effectiveSongLength">������Ч���ȣ��ķ�������λ��</param>
        /// <returns>�������ܳ��ȣ����ص�λ��</returns>
        public double CalculateScrollbarTotalLengthInPixels(double effectiveSongLength)
        {
            var totalLengthInQuarterNotes = CalculateScrollbarTotalLength(effectiveSongLength);
            return totalLengthInQuarterNotes * BaseQuarterNoteWidth;
        }

        /// <summary>
        /// �������ݵ��ܿ��ȣ����ص�λ���������µ��ϸ��׼
        /// ������������ϸ���"������Ч����+8С��"����
        /// </summary>
        /// <param name="noteEndPositions">��������λ�õļ���</param>
        /// <param name="midiFileDuration">MIDI�ļ�ʱ������ѡ���ķ�������λ��</param>
        public double CalculateContentWidth(IEnumerable<MusicalFraction> noteEndPositions, double? midiFileDuration = null)
        {
            // ���������Ч����
            var effectiveSongLength = CalculateEffectiveSongLength(noteEndPositions, midiFileDuration);
            
            // ����������ܳ��ȣ����أ�
            var totalLengthInPixels = CalculateScrollbarTotalLengthInPixels(effectiveSongLength);
            
            System.Diagnostics.Debug.WriteLine($"[PianoRollCalculations] ������Ч����: {effectiveSongLength:F2} �ķ�����");
            System.Diagnostics.Debug.WriteLine($"[PianoRollCalculations] �������ܳ���: {totalLengthInPixels:F1} ����");
            System.Diagnostics.Debug.WriteLine($"[PianoRollCalculations] �����ķ���������: {BaseQuarterNoteWidth:F1} ����");
            
            return totalLengthInPixels;
        }

        /// <summary>
        /// ��ȡ������Ч����
        /// </summary>
        public double GetEffectiveSongLength(IEnumerable<MusicalFraction> noteEndPositions, double? midiFileDuration = null)
        {
            return CalculateEffectiveSongLength(noteEndPositions, midiFileDuration);
        }

        /// <summary>
        /// ���㵱ǰ�ӿ�������ܸ������ȵı���
        /// </summary>
        /// <param name="viewportWidth">�ӿڿ��ȣ����أ�</param>
        /// <param name="noteEndPositions">��������λ�õļ���</param>
        /// <param name="midiFileDuration">MIDI�ļ�ʱ������ѡ��</param>
        /// <returns>�ӿڱ�����0-1��</returns>
        public double CalculateViewportRatio(double viewportWidth, IEnumerable<MusicalFraction> noteEndPositions, double? midiFileDuration = null)
        {
            var totalContentWidth = CalculateContentWidth(noteEndPositions, midiFileDuration);
            
            if (totalContentWidth <= 0)
                return 1.0;
            
            var ratio = Math.Min(1.0, viewportWidth / totalContentWidth);
            
            System.Diagnostics.Debug.WriteLine($"[PianoRollCalculations] �ӿڱ���: {ratio:P2} (�ӿڿ���: {viewportWidth:F1}, �ܿ���: {totalContentWidth:F1})");
            
            return ratio;
        }

        /// <summary>
        /// ���㵱ǰ����λ��������ܳ��ȵı���
        /// </summary>
        /// <param name="currentScrollOffset">��ǰ����ƫ�ƣ����أ�</param>
        /// <param name="viewportWidth">�ӿڿ��ȣ����أ�</param>
        /// <param name="noteEndPositions">��������λ�õļ���</param>
        /// <param name="midiFileDuration">MIDI�ļ�ʱ������ѡ��</param>
        /// <returns>����λ�ñ�����0-1��</returns>
        public double CalculateScrollPositionRatio(double currentScrollOffset, double viewportWidth, IEnumerable<MusicalFraction> noteEndPositions, double? midiFileDuration = null)
        {
            var totalContentWidth = CalculateContentWidth(noteEndPositions, midiFileDuration);
            var maxScrollOffset = Math.Max(0, totalContentWidth - viewportWidth);
            
            if (maxScrollOffset <= 0)
                return 0.0;
            
            var ratio = Math.Min(1.0, currentScrollOffset / maxScrollOffset);
            
            System.Diagnostics.Debug.WriteLine($"[PianoRollCalculations] ����λ�ñ���: {ratio:P2} (����ƫ��: {currentScrollOffset:F1}, ������: {maxScrollOffset:F1})");
            
            return ratio;
        }
        #endregion

        #region �����Է���
        /// <summary>
        /// �������ݵ��ܿ��ȣ������ݣ�
        /// �ѹ�ʱ��ʹ��CalculateContentWidth(noteEndPositions, midiFileDuration)����
        /// </summary>
        /// <param name="noteEndPositions">��������λ�õļ���</param>
        [Obsolete("ʹ��CalculateContentWidth(noteEndPositions, midiFileDuration)����")]
        public double CalculateContentWidth(IEnumerable<MusicalFraction> noteEndPositions)
        {
            return CalculateContentWidth(noteEndPositions, null);
        }
        #endregion

        #region ��������
        /// <summary>
        /// �ж�ָ����MIDI�����Ƿ�Ϊ�ڼ�
        /// </summary>
        public bool IsBlackKey(int midiNote)
        {
            var noteInOctave = midiNote % 12;
            return noteInOctave == 1 || noteInOctave == 3 || noteInOctave == 6 || noteInOctave == 8 || noteInOctave == 10;
        }

        /// <summary>
        /// ��ȡMIDI������
        /// </summary>
        public string GetNoteName(int midiNote)
        {
            var noteNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            var octave = midiNote / 12 - 1;
            var noteIndex = midiNote % 12;
            return $"{noteNames[noteIndex]}{octave}";
        }

        /// <summary>
        /// ��ȡ�����ߵ�λ��
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
        /// ��ȡС���ߵ�λ��
        /// </summary>
        public IEnumerable<double> GetMeasureLinePositions(double visibleStartTime, double visibleEndTime)
        {
            var measureUnitInQuarterNotes = BeatsPerMeasure; // 4���ķ�����ΪһС��
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