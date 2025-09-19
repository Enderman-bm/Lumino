using Lumino.ViewModels.Editor.Enums;
using System;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// �¼�������ֵ�������ӿ�
    /// ������ݲ�ͬ�¼����ͼ����Ӧ����ֵ��Χ������ת��
    /// </summary>
    public interface IEventCurveCalculationService
    {
        /// <summary>
        /// ��ȡָ���¼����͵���Сֵ
        /// </summary>
        /// <param name="eventType">�¼�����</param>
        /// <param name="ccNumber">CC�������ţ������¼�����ΪControlChangeʱʹ�ã�</param>
        /// <returns>��Сֵ</returns>
        int GetMinValue(EventType eventType, int ccNumber = 0);

        /// <summary>
        /// ��ȡָ���¼����͵����ֵ
        /// </summary>
        /// <param name="eventType">�¼�����</param>
        /// <param name="ccNumber">CC�������ţ������¼�����ΪControlChangeʱʹ�ã�</param>
        /// <returns>���ֵ</returns>
        int GetMaxValue(EventType eventType, int ccNumber = 0);

        /// <summary>
        /// ������Y����ת��Ϊ�¼���ֵ
        /// </summary>
        /// <param name="y">����Y����</param>
        /// <param name="canvasHeight">�����߶�</param>
        /// <param name="eventType">�¼�����</param>
        /// <param name="ccNumber">CC�������ţ������¼�����ΪControlChangeʱʹ�ã�</param>
        /// <returns>�¼���ֵ</returns>
        int YToValue(double y, double canvasHeight, EventType eventType, int ccNumber = 0);

        /// <summary>
        /// ���¼���ֵת��Ϊ����Y����
        /// </summary>
        /// <param name="value">�¼���ֵ</param>
        /// <param name="canvasHeight">�����߶�</param>
        /// <param name="eventType">�¼�����</param>
        /// <param name="ccNumber">CC�������ţ������¼�����ΪControlChangeʱʹ�ã�</param>
        /// <returns>����Y����</returns>
        double ValueToY(int value, double canvasHeight, EventType eventType, int ccNumber = 0);

        /// <summary>
        /// ������ֵ����Ч��Χ��
        /// </summary>
        /// <param name="value">Ҫ���Ƶ���ֵ</param>
        /// <param name="eventType">�¼�����</param>
        /// <param name="ccNumber">CC�������ţ������¼�����ΪControlChangeʱʹ�ã�</param>
        /// <returns>���ƺ����ֵ</returns>
        int ClampValue(int value, EventType eventType, int ccNumber = 0);

        /// <summary>
        /// ��ȡ�¼����͵���ֵ��Χ����
        /// </summary>
        /// <param name="eventType">�¼�����</param>
        /// <param name="ccNumber">CC�������ţ������¼�����ΪControlChangeʱʹ�ã�</param>
        /// <returns>��Χ�����ַ���</returns>
        string GetValueRangeDescription(EventType eventType, int ccNumber = 0);
    }
}