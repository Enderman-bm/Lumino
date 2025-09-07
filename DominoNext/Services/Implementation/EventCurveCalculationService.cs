using DominoNext.Services.Interfaces;
using DominoNext.ViewModels.Editor.Enums;
using System;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// 事件曲线数值计算服务实现
    /// 根据不同事件类型计算对应的数值范围和坐标转换
    /// </summary>
    public class EventCurveCalculationService : IEventCurveCalculationService
    {
        /// <summary>
        /// 获取指定事件类型的最小值
        /// </summary>
        public int GetMinValue(EventType eventType, int ccNumber = 0)
        {
            return eventType switch
            {
                EventType.Velocity => 1,           // MIDI力度范围：1-127
                EventType.PitchBend => -8192,      // MIDI弯音范围：-8192～8191
                EventType.ControlChange => 0,       // MIDI CC范围：0-127
                EventType.Tempo => 20,             // BPM范围：20-300
                _ => 0
            };
        }

        /// <summary>
        /// 获取指定事件类型的最大值
        /// </summary>
        public int GetMaxValue(EventType eventType, int ccNumber = 0)
        {
            return eventType switch
            {
                EventType.Velocity => 127,         // MIDI力度范围：1-127
                EventType.PitchBend => 8191,       // MIDI弯音范围：-8192～8191
                EventType.ControlChange => 127,    // MIDI CC范围：0-127
                EventType.Tempo => 300,            // BPM范围：20-300
                _ => 127
            };
        }

        /// <summary>
        /// 将画布Y坐标转换为事件数值
        /// Y坐标：0为顶部（最大值），canvasHeight为底部（最小值）
        /// </summary>
        public int YToValue(double y, double canvasHeight, EventType eventType, int ccNumber = 0)
        {
            if (canvasHeight <= 0) return GetMinValue(eventType, ccNumber);

            var minValue = GetMinValue(eventType, ccNumber);
            var maxValue = GetMaxValue(eventType, ccNumber);
            var range = maxValue - minValue;

            // 将Y坐标标准化到0-1范围（注意Y轴翻转：0为顶部=最大值）
            var normalizedY = Math.Max(0, Math.Min(1, y / canvasHeight));
            
            // 计算对应的数值（Y轴翻转：顶部=最大值，底部=最小值）
            var value = maxValue - (normalizedY * range);
            
            return ClampValue((int)Math.Round(value), eventType, ccNumber);
        }

        /// <summary>
        /// 将事件数值转换为画布Y坐标
        /// </summary>
        public double ValueToY(int value, double canvasHeight, EventType eventType, int ccNumber = 0)
        {
            var minValue = GetMinValue(eventType, ccNumber);
            var maxValue = GetMaxValue(eventType, ccNumber);
            var range = maxValue - minValue;

            if (range <= 0) return canvasHeight / 2;

            // 将数值标准化到0-1范围
            var normalizedValue = (double)(value - minValue) / range;
            
            // 转换为Y坐标（Y轴翻转：最大值在顶部=Y坐标0）
            var y = (1.0 - normalizedValue) * canvasHeight;
            
            return Math.Max(0, Math.Min(canvasHeight, y));
        }

        /// <summary>
        /// 限制数值在有效范围内
        /// </summary>
        public int ClampValue(int value, EventType eventType, int ccNumber = 0)
        {
            var minValue = GetMinValue(eventType, ccNumber);
            var maxValue = GetMaxValue(eventType, ccNumber);
            
            return Math.Max(minValue, Math.Min(maxValue, value));
        }

        /// <summary>
        /// 获取事件类型的数值范围描述
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
        /// 获取数值范围的总跨度
        /// </summary>
        public int GetValueRange(EventType eventType, int ccNumber = 0)
        {
            var minValue = GetMinValue(eventType, ccNumber);
            var maxValue = GetMaxValue(eventType, ccNumber);
            return maxValue - minValue;
        }

        /// <summary>
        /// 获取默认值（通常是中间值）
        /// </summary>
        public int GetDefaultValue(EventType eventType, int ccNumber = 0)
        {
            return eventType switch
            {
                EventType.Velocity => 100,         // 默认力度
                EventType.PitchBend => 0,          // 弯音中性值
                EventType.ControlChange => 64,     // CC中间值
                EventType.Tempo => 120,            // 默认BPM
                _ => 64
            };
        }

        /// <summary>
        /// 检查数值是否在有效范围内
        /// </summary>
        public bool IsValidValue(int value, EventType eventType, int ccNumber = 0)
        {
            var minValue = GetMinValue(eventType, ccNumber);
            var maxValue = GetMaxValue(eventType, ccNumber);
            return value >= minValue && value <= maxValue;
        }

        /// <summary>
        /// 获取数值的百分比位置（0.0-1.0）
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
        /// 从百分比位置获取数值
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