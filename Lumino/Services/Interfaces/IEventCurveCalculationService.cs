using Lumino.ViewModels.Editor.Enums;
using System;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// 事件曲线数值计算服务接口
    /// 负责根据不同事件类型计算对应的数值范围和坐标转换
    /// </summary>
    public interface IEventCurveCalculationService
    {
        /// <summary>
        /// 获取指定事件类型的最小值
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="ccNumber">CC控制器号（仅当事件类型为ControlChange时使用）</param>
        /// <returns>最小值</returns>
        int GetMinValue(EventType eventType, int ccNumber = 0);

        /// <summary>
        /// 获取指定事件类型的最大值
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="ccNumber">CC控制器号（仅当事件类型为ControlChange时使用）</param>
        /// <returns>最大值</returns>
        int GetMaxValue(EventType eventType, int ccNumber = 0);

        /// <summary>
        /// 将画布Y坐标转换为事件数值
        /// </summary>
        /// <param name="y">画布Y坐标</param>
        /// <param name="canvasHeight">画布高度</param>
        /// <param name="eventType">事件类型</param>
        /// <param name="ccNumber">CC控制器号（仅当事件类型为ControlChange时使用）</param>
        /// <returns>事件数值</returns>
        int YToValue(double y, double canvasHeight, EventType eventType, int ccNumber = 0);

        /// <summary>
        /// 将事件数值转换为画布Y坐标
        /// </summary>
        /// <param name="value">事件数值</param>
        /// <param name="canvasHeight">画布高度</param>
        /// <param name="eventType">事件类型</param>
        /// <param name="ccNumber">CC控制器号（仅当事件类型为ControlChange时使用）</param>
        /// <returns>画布Y坐标</returns>
        double ValueToY(int value, double canvasHeight, EventType eventType, int ccNumber = 0);

        /// <summary>
        /// 限制数值在有效范围内
        /// </summary>
        /// <param name="value">要限制的数值</param>
        /// <param name="eventType">事件类型</param>
        /// <param name="ccNumber">CC控制器号（仅当事件类型为ControlChange时使用）</param>
        /// <returns>限制后的数值</returns>
        int ClampValue(int value, EventType eventType, int ccNumber = 0);

        /// <summary>
        /// 获取事件类型的数值范围描述
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="ccNumber">CC控制器号（仅当事件类型为ControlChange时使用）</param>
        /// <returns>范围描述字符串</returns>
        string GetValueRangeDescription(EventType eventType, int ccNumber = 0);
    }
}