namespace Lumino.Models.Music
{
    /// <summary>
    /// ����ʱֵѡ��ģ�͡�
    /// �����ڱ༭���б�ʾ��ѡ������ʱֵ�����ķ��������˷������ȣ����������ơ�ʱֵ��ͼ�ꡣ
    /// ���ܽ��������Note.csְ��һ�����������ÿ�������洢��ʽ����������ѡ����ʹ�õġ�
    /// </summary>
    public class NoteDurationOption
    {
        /// <summary>
        /// ѡ�����ƣ��硰�ķ������������˷��������ȣ���
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// ����ʱֵ��ʹ�� MusicalFraction ��ʾ���� 1/4 ��ʾ�ķ���������
        /// </summary>
        public MusicalFraction Duration { get; set; }
        /// <summary>
        /// ͼ����Դ·�����ʶ�������ڽ�������ʾ��ʱֵ��ͼ�ꡣ
        /// </summary>
        public string Icon { get; set; }
        /// <summary>
        /// ���캯������ʼ������ʱֵѡ�
        /// </summary>
        /// <param name="name">ѡ������</param>
        /// <param name="duration">����ʱֵ��MusicalFraction ���ͣ�</param>
        /// <param name="icon">��ʾʲôͼ�꣨���ķ�������</param>
        public NoteDurationOption(string name, MusicalFraction duration, string icon)
        {
            Name = name;
            Duration = duration;
            Icon = icon;
        }
    }
}