using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor.Enums;
using System;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// �¼�������ֵ�������ʵ��
    /// ���ݲ�ͬ�¼����ͼ����Ӧ����ֵ��Χ������ת��
    /// </summary>
    public class EventCurveCalculationService : IEventCurveCalculationService
    {
        /// <summary>
        /// ��ȡָ���¼����͵���Сֵ
        /// </summary>
        public int GetMinValue(EventType eventType, int ccNumber = 0)
        {
            return eventType switch
            {
                EventType.Velocity => 1,           // MIDI���ȷ�Χ��1-127
                EventType.PitchBend => -8192,      // MIDI������Χ��-8192��8191
                EventType.ControlChange => 0,       // MIDI CC��Χ��0-127
                EventType.Tempo => 1,             // BPM��Χ��1-300
                _ => 0
            };
        }

        /// <summary>
        /// ��ȡָ���¼����͵����ֵ
        /// </summary>
        public int GetMaxValue(EventType eventType, int ccNumber = 0)
        {
            return eventType switch
            {
                EventType.Velocity => 127,         // MIDI���ȷ�Χ��1-127
                EventType.PitchBend => 8191,       // MIDI������Χ��-8192��8191
                EventType.ControlChange => 127,    // MIDI CC��Χ��0-127
                EventType.Tempo => 300,            // BPM��Χ��1-300
                _ => 127
            };
        }

        /// <summary>
        /// ������Y����ת��Ϊ�¼���ֵ
        /// Y���꣺0Ϊ���������ֵ����canvasHeightΪ�ײ�����Сֵ��
        /// </summary>
        public int YToValue(double y, double canvasHeight, EventType eventType, int ccNumber = 0)
        {
            if (canvasHeight <= 0) return GetMinValue(eventType, ccNumber);

            var minValue = GetMinValue(eventType, ccNumber);
            var maxValue = GetMaxValue(eventType, ccNumber);
            var range = maxValue - minValue;

            // ��Y�����׼����0-1��Χ��ע��Y�ᷭת��0Ϊ����=���ֵ��
            var normalizedY = Math.Max(0, Math.Min(1, y / canvasHeight));
            
            // �����Ӧ����ֵ��Y�ᷭת������=���ֵ���ײ�=��Сֵ��
            var value = maxValue - (normalizedY * range);
            
            return ClampValue((int)Math.Round(value), eventType, ccNumber);
        }

        /// <summary>
        /// ���¼���ֵת��Ϊ����Y����
        /// </summary>
        public double ValueToY(int value, double canvasHeight, EventType eventType, int ccNumber = 0)
        {
            var minValue = GetMinValue(eventType, ccNumber);
            var maxValue = GetMaxValue(eventType, ccNumber);
            var range = maxValue - minValue;

            if (range <= 0) return canvasHeight / 2;

            // ����ֵ��׼����0-1��Χ
            var normalizedValue = (double)(value - minValue) / range;
            
            // ת��ΪY���꣨Y�ᷭת�����ֵ�ڶ���=Y����0��
            var y = (1.0 - normalizedValue) * canvasHeight;
            
            return Math.Max(0, Math.Min(canvasHeight, y));
        }

        /// <summary>
        /// ������ֵ����Ч��Χ��
        /// </summary>
        public int ClampValue(int value, EventType eventType, int ccNumber = 0)
        {
            var minValue = GetMinValue(eventType, ccNumber);
            var maxValue = GetMaxValue(eventType, ccNumber);
            
            return Math.Max(minValue, Math.Min(maxValue, value));
        }

        /// <summary>
        /// ��ȡ�¼����͵���ֵ��Χ����
        /// </summary>
        public string GetValueRangeDescription(EventType eventType, int ccNumber = 0)
        {
            var minValue = GetMinValue(eventType, ccNumber);
            var maxValue = GetMaxValue(eventType, ccNumber);
            
            return eventType switch
            {
                EventType.Velocity => $"{minValue}-{maxValue}",
                EventType.PitchBend => $"{minValue}~{maxValue}",
                EventType.ControlChange => $"{minValue}-{maxValue}",
                EventType.Tempo => $"{minValue}-{maxValue} BPM",
                _ => $"{minValue}-{maxValue}"
            };
        }

        /// <summary>
        /// ��ȡ��ֵ��Χ���ܿ��
        /// </summary>
        public int GetValueRange(EventType eventType, int ccNumber = 0)
        {
            var minValue = GetMinValue(eventType, ccNumber);
            var maxValue = GetMaxValue(eventType, ccNumber);
            return maxValue - minValue;
        }

        /// <summary>
        /// ��ȡĬ��ֵ��ͨ�����м�ֵ��
        /// </summary>
        public int GetDefaultValue(EventType eventType, int ccNumber = 0)
        {
            return eventType switch
            {
                EventType.Velocity => 100,         // Ĭ������
                EventType.PitchBend => 0,          // ��������ֵ
                EventType.ControlChange => 64,     // CC�м�ֵ
                EventType.Tempo => 120,            // Ĭ��BPM
                _ => 64
            };
        }

        /// <summary>
        /// �����ֵ�Ƿ�����Ч��Χ��
        /// </summary>
        public bool IsValidValue(int value, EventType eventType, int ccNumber = 0)
        {
            var minValue = GetMinValue(eventType, ccNumber);
            var maxValue = GetMaxValue(eventType, ccNumber);
            return value >= minValue && value <= maxValue;
        }

        /// <summary>
        /// ��ȡ��ֵ�İٷֱ�λ�ã�0.0-1.0��
        /// </summary>
        public double GetValuePercentage(int value, EventType eventType, int ccNumber = 0)
        {
            var minValue = GetMinValue(eventType, ccNumber);
            var maxValue = GetMaxValue(eventType, ccNumber);
            var range = maxValue - minValue;
            
            if (range <= 0) return 0.5;
            
            return Math.Max(0.0, Math.Min(1.0, (double)(value - minValue) / range));
        }

        /// <summary>
        /// �Ӱٷֱ�λ�û�ȡ��ֵ
        /// </summary>
        public int GetValueFromPercentage(double percentage, EventType eventType, int ccNumber = 0)
        {
            var minValue = GetMinValue(eventType, ccNumber);
            var maxValue = GetMaxValue(eventType, ccNumber);
            var range = maxValue - minValue;
            
            var value = minValue + (range * Math.Max(0.0, Math.Min(1.0, percentage)));
            return ClampValue((int)Math.Round(value), eventType, ccNumber);
        }
    }
}