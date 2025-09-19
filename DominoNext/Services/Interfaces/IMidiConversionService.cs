using Lumino.Models.Music;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// MIDIת������ӿڣ�ר�Ŵ���MIDI�ļ����뵼��ʱ��Tickת��
    /// </summary>
    public interface IMidiConversionService
    {
        /// <summary>
        /// �ķ������ı�׼tickֵ��MIDI��׼��
        /// </summary>
        int QuarterNoteTicks { get; }

        /// <summary>
        /// �����ַ���ת��ΪMIDI tickֵ
        /// </summary>
        /// <param name="fraction">���ַ���</param>
        /// <returns>MIDI tickֵ</returns>
        double ConvertToTicks(MusicalFraction fraction);

        /// <summary>
        /// ��MIDI tickֵ�������ַ���
        /// </summary>
        /// <param name="ticks">MIDI tickֵ</param>
        /// <returns>���ַ���</returns>
        MusicalFraction ConvertFromTicks(double ticks);

        /// <summary>
        /// ����ÿ�ķ�������tick�������ڲ�ͬ��MIDI�ļ���ʽ��
        /// </summary>
        /// <param name="ticksPerQuarterNote">ÿ�ķ�������tick��</param>
        void SetTicksPerQuarterNote(int ticksPerQuarterNote);
    }
}