using System;
using Lumino.Models.Music;
using Lumino.Services.Interfaces;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// MIDIת������ʵ�֣�ר�Ŵ���MIDI�ļ����뵼��ʱ��Tickת��
    /// </summary>
    public class MidiConversionService : IMidiConversionService
    {
        /// <summary>
        /// �ķ������ı�׼tickֵ��MIDI��׼��
        /// </summary>
        public int QuarterNoteTicks { get; private set; } = 96;

        /// <summary>
        /// �����ַ���ת��ΪMIDI tickֵ
        /// ʹ�ô�ͳ���ּ��׷����㣺1/4 = �ķ����� = QuarterNoteTicks
        /// </summary>
        /// <param name="fraction">���ַ���</param>
        /// <returns>MIDI tickֵ</returns>
        public double ConvertToTicks(MusicalFraction fraction)
        {
            // ��ͳ���ּ��׷�ת����
            // 1/1 = ȫ���� = 4 * �ķ����� = 4 * QuarterNoteTicks
            // 1/2 = �������� = 2 * �ķ����� = 2 * QuarterNoteTicks  
            // 1/4 = �ķ����� = 1 * �ķ����� = QuarterNoteTicks
            // 1/8 = �˷����� = 0.5 * �ķ����� = QuarterNoteTicks/2
            // 1/16 = ʮ�������� = 0.25 * �ķ����� = QuarterNoteTicks/4

            // ��ʽ��(4/��ĸ) * (����) * QuarterNoteTicks
            return (double)fraction.Numerator * 4 / fraction.Denominator * QuarterNoteTicks;
        }

        /// <summary>
        /// ��MIDI tickֵ�������ַ���
        /// </summary>
        /// <param name="ticks">MIDI tickֵ</param>
        /// <returns>���ַ���</returns>
        public MusicalFraction ConvertFromTicks(double ticks)
        {
            // ���Ӱ�ȫ���
            if (double.IsNaN(ticks) || double.IsInfinity(ticks) || ticks < 0)
            {
                return new MusicalFraction(1, 16); // Ĭ��ʮ��������
            }

            // �޸������ticksΪ0��ӽ�0��ֱ�ӷ���0
            if (Math.Abs(ticks) < 1e-10)
            {
                return new MusicalFraction(0, 1); // 0ʱ��λ��
            }

            // ����������ķ������ı���
            var quarterNoteMultiple = ticks / QuarterNoteTicks;

            // ֧�ֳ���������ʱֵ��ĸ��1, 2, 4, 8, 16, 32, 64
            var commonDenominators = new[] { 1, 2, 4, 8, 16, 32, 64 };

            foreach (var denominator in commonDenominators)
            {
                // ���մ�ͳ���׷��������
                var numerator = Math.Round(quarterNoteMultiple * denominator / 4.0);

                if (numerator >= 1 && numerator <= int.MaxValue)
                {
                    var intNumerator = (int)numerator;
                    var testFraction = new MusicalFraction(intNumerator, denominator);

                    // ���ת�����tickֵ�Ƿ�ƥ��
                    if (Math.Abs(ConvertToTicks(testFraction) - ticks) < 0.001)
                    {
                        return testFraction;
                    }
                }
            }

            // Ĭ��ʹ��64����������
            var bestNumerator = Math.Max(1, Math.Round(quarterNoteMultiple * 64 / 4.0));
            if (bestNumerator <= int.MaxValue)
            {
                return new MusicalFraction((int)bestNumerator, 64);
            }

            // �����Ȼ��������ذ�ȫ��Ĭ��ֵ
            return new MusicalFraction(1, 16); // Ĭ��ʮ��������
        }

        /// <summary>
        /// ����ÿ�ķ�������tick�������ڲ�ͬ��MIDI�ļ���ʽ��
        /// </summary>
        /// <param name="ticksPerQuarterNote">ÿ�ķ�������tick��</param>
        public void SetTicksPerQuarterNote(int ticksPerQuarterNote)
        {
            if (ticksPerQuarterNote <= 0)
                throw new ArgumentException("ÿ�ķ�������tick���������0", nameof(ticksPerQuarterNote));
            
            QuarterNoteTicks = ticksPerQuarterNote;
        }

        /// <summary>
        /// ����λ�õ����񣨻���MIDI tick��
        /// </summary>
        /// <param name="positionInTicks">Ҫ������λ�ã�tick��</param>
        /// <param name="gridUnit">����λ����MusicalFraction��</param>
        /// <returns>�������λ�ã�tick��</returns>
        public double QuantizeToGridTicks(double positionInTicks, MusicalFraction gridUnit)
        {
            // ���Ӱ�ȫ���
            if (double.IsNaN(positionInTicks) || double.IsInfinity(positionInTicks))
            {
                return 0;
            }

            // �޸������λ���Ѿ��ǳ��ӽ�0��ֱ�ӷ���0����������ƫ��
            if (Math.Abs(positionInTicks) < 1e-10)
            {
                return 0;
            }

            var gridSizeInTicks = ConvertToTicks(gridUnit);
            if (gridSizeInTicks <= 0)
            {
                return positionInTicks; // ��������С��Ч������ԭֵ
            }

            // ʹ�ø���ȷ�������㷨��ȷ��0λ�ò���ƫ��
            var quantizedPosition = Math.Round(positionInTicks / gridSizeInTicks) * gridSizeInTicks;
            
            // ȷ���������λ�ò�Ϊ����������ԭλ�þ��Ǹ�����
            if (positionInTicks >= 0 && quantizedPosition < 0)
            {
                quantizedPosition = 0;
            }

            return quantizedPosition;
        }

        /// <summary>
        /// �������ʼλ�õ�����λ�õ��������ȣ�����MIDI tick��
        /// </summary>
        /// <param name="startTicks">��ʼλ�ã�tick��</param>
        /// <param name="endTicks">����λ�ã�tick��</param>
        /// <param name="gridUnit">����λ</param>
        /// <returns>������ĳ��ȷ���</returns>
        public MusicalFraction CalculateQuantizedDurationFromTicks(double startTicks, double endTicks, MusicalFraction gridUnit)
        {
            var durationTicks = Math.Max(ConvertToTicks(gridUnit), endTicks - startTicks);
            var gridSizeInTicks = ConvertToTicks(gridUnit);

            if (gridSizeInTicks <= 0)
            {
                return gridUnit; // ����Ĭ������λ
            }

            var gridUnits = Math.Max(1, Math.Round(durationTicks / gridSizeInTicks));

            // ���Ӱ�ȫ��飬�������
            var resultNumerator = gridUnits * gridUnit.Numerator;
            if (resultNumerator >= int.MinValue && resultNumerator <= int.MaxValue)
            {
                return new MusicalFraction((int)resultNumerator, gridUnit.Denominator);
            }

            // ������������ԭ����λ
            return gridUnit;
        }
    }
}