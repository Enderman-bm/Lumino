namespace Lumino.ViewModels.Editor.Enums
{
    /// <summary>
    /// �¼���ͼ�п�ѡ����¼�����
    /// </summary>
    public enum EventType
    {
        /// <summary>
        /// ���ȣ���Χ1-127��
        /// </summary>
        Velocity,
        
        /// <summary>
        /// ��������Χ-8192-8191��
        /// </summary>
        PitchBend,
        
        /// <summary>
        /// �������仯��CC����Χ0-127��
        /// </summary>
        ControlChange,
        
        /// <summary>
        /// �ٶȣ�BPM������Χ20-300
        /// </summary>
        Tempo
    }
}