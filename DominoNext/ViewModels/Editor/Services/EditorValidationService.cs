using System;
using Avalonia;
using DominoNext.Models.Music;

namespace DominoNext.ViewModels.Editor.Services
{
    /// <summary>
    /// 编辑器验证服务 - 提供通用验证逻辑
    /// 遵循单一职责原则，专门处理编辑器相关的验证
    /// </summary>
    public static class EditorValidationService
    {
        #region 音符验证
        /// <summary>
        /// 验证音符位置是否有效
        /// </summary>
        /// <param name="pitch">音高 (0-127)</param>
        /// <param name="timeValue">时间值 (>= 0)</param>
        /// <returns>位置是否有效</returns>
        public static bool IsValidNotePosition(int pitch, double timeValue)
        {
            return pitch >= 0 && pitch <= 127 && timeValue >= 0;
        }

        /// <summary>
        /// 验证音符位置是否有效 - 重载版本
        /// </summary>
        /// <param name="pitch">音高</param>
        /// <param name="startPosition">开始位置</param>
        /// <returns>位置是否有效</returns>
        public static bool IsValidNotePosition(int pitch, MusicalFraction startPosition)
        {
            return IsValidNotePosition(pitch, startPosition.ToDouble());
        }

        /// <summary>
        /// 验证音符力度是否有效
        /// </summary>
        /// <param name="velocity">力度值 (1-127)</param>
        /// <returns>力度是否有效</returns>
        public static bool IsValidVelocity(int velocity)
        {
            return velocity >= 1 && velocity <= 127;
        }

        /// <summary>
        /// 验证音符时长是否有效
        /// </summary>
        /// <param name="duration">时长</param>
        /// <param name="minimumDuration">最小允许时长</param>
        /// <returns>时长是否有效</returns>
        public static bool IsValidDuration(MusicalFraction duration, MusicalFraction? minimumDuration = null)
        {
            // 检查是否为0或负数：通过检查分子是否<=0且分母>0
            if (duration.Numerator <= 0 && duration.Denominator > 0)
                return false;

            if (minimumDuration.HasValue)
                return duration.CompareTo(minimumDuration.Value) >= 0;

            return true;
        }
        #endregion

        #region 防抖动验证
        /// <summary>
        /// 检查移动是否超过像素阈值
        /// </summary>
        /// <param name="startPosition">起始位置</param>
        /// <param name="currentPosition">当前位置</param>
        /// <param name="threshold">像素阈值</param>
        /// <returns>是否超过阈值</returns>
        public static bool IsMovementAbovePixelThreshold(Point startPosition, Point currentPosition, double threshold)
        {
            var deltaX = currentPosition.X - startPosition.X;
            var deltaY = currentPosition.Y - startPosition.Y;
            var totalMovement = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            return totalMovement >= threshold;
        }

        /// <summary>
        /// 检查时间是否超过阈值
        /// </summary>
        /// <param name="startTime">开始时间</param>
        /// <param name="thresholdMs">时间阈值（毫秒）</param>
        /// <returns>是否超过阈值</returns>
        public static bool IsTimeAboveThreshold(DateTime startTime, double thresholdMs)
        {
            return (DateTime.Now - startTime).TotalMilliseconds >= thresholdMs;
        }
        #endregion

        #region 范围验证
        /// <summary>
        /// 将音高限制在有效范围内
        /// </summary>
        /// <param name="pitch">音高</param>
        /// <returns>限制后的音高</returns>
        public static int ClampPitch(int pitch)
        {
            return Math.Max(0, Math.Min(127, pitch));
        }

        /// <summary>
        /// 将力度限制在有效范围内
        /// </summary>
        /// <param name="velocity">力度</param>
        /// <returns>限制后的力度</returns>
        public static int ClampVelocity(int velocity)
        {
            return Math.Max(1, Math.Min(127, velocity));
        }

        /// <summary>
        /// 将时间值限制在有效范围内
        /// </summary>
        /// <param name="timeValue">时间值</param>
        /// <param name="minimum">最小值（默认为0）</param>
        /// <returns>限制后的时间值</returns>
        public static double ClampTimeValue(double timeValue, double minimum = 0.0)
        {
            return Math.Max(minimum, timeValue);
        }
        #endregion
    }
}