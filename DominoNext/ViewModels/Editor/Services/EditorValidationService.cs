using System;
using Avalonia;
using Lumino.Models.Music;

namespace Lumino.ViewModels.Editor.Services
{
    /// <summary>
    /// �༭����֤���� - �ṩͨ����֤�߼�
    /// ��ѭ��һְ��ԭ��ר�Ŵ����༭����ص���֤
    /// </summary>
    public static class EditorValidationService
    {
        #region ������֤
        /// <summary>
        /// ��֤����λ���Ƿ���Ч
        /// </summary>
        /// <param name="pitch">���� (0-127)</param>
        /// <param name="timeValue">ʱ��ֵ (>= 0)</param>
        /// <returns>λ���Ƿ���Ч</returns>
        public static bool IsValidNotePosition(int pitch, double timeValue)
        {
            return pitch >= 0 && pitch <= 127 && timeValue >= 0;
        }

        /// <summary>
        /// ��֤����λ���Ƿ���Ч - ���ذ汾
        /// </summary>
        /// <param name="pitch">����</param>
        /// <param name="startPosition">��ʼλ��</param>
        /// <returns>λ���Ƿ���Ч</returns>
        public static bool IsValidNotePosition(int pitch, MusicalFraction startPosition)
        {
            return IsValidNotePosition(pitch, startPosition.ToDouble());
        }

        /// <summary>
        /// ��֤���������Ƿ���Ч
        /// </summary>
        /// <param name="velocity">����ֵ (1-127)</param>
        /// <returns>�����Ƿ���Ч</returns>
        public static bool IsValidVelocity(int velocity)
        {
            return velocity >= 1 && velocity <= 127;
        }

        /// <summary>
        /// ��֤����ʱ���Ƿ���Ч
        /// </summary>
        /// <param name="duration">ʱ��</param>
        /// <param name="minimumDuration">��С����ʱ��</param>
        /// <returns>ʱ���Ƿ���Ч</returns>
        public static bool IsValidDuration(MusicalFraction duration, MusicalFraction? minimumDuration = null)
        {
            // ����Ƿ�Ϊ0������ͨ���������Ƿ�<=0�ҷ�ĸ>0
            if (duration.Numerator <= 0 && duration.Denominator > 0)
                return false;

            if (minimumDuration.HasValue)
                return duration.CompareTo(minimumDuration.Value) >= 0;

            return true;
        }
        #endregion

        #region ��������֤
        /// <summary>
        /// ����ƶ��Ƿ񳬹�������ֵ
        /// </summary>
        /// <param name="startPosition">��ʼλ��</param>
        /// <param name="currentPosition">��ǰλ��</param>
        /// <param name="threshold">������ֵ</param>
        /// <returns>�Ƿ񳬹���ֵ</returns>
        public static bool IsMovementAbovePixelThreshold(Point startPosition, Point currentPosition, double threshold)
        {
            var deltaX = currentPosition.X - startPosition.X;
            var deltaY = currentPosition.Y - startPosition.Y;
            var totalMovement = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            return totalMovement >= threshold;
        }

        /// <summary>
        /// ���ʱ���Ƿ񳬹���ֵ
        /// </summary>
        /// <param name="startTime">��ʼʱ��</param>
        /// <param name="thresholdMs">ʱ����ֵ�����룩</param>
        /// <returns>�Ƿ񳬹���ֵ</returns>
        public static bool IsTimeAboveThreshold(DateTime startTime, double thresholdMs)
        {
            return (DateTime.Now - startTime).TotalMilliseconds >= thresholdMs;
        }
        #endregion

        #region ��Χ��֤
        /// <summary>
        /// ��������������Ч��Χ��
        /// </summary>
        /// <param name="pitch">����</param>
        /// <returns>���ƺ������</returns>
        public static int ClampPitch(int pitch)
        {
            return Math.Max(0, Math.Min(127, pitch));
        }

        /// <summary>
        /// ��������������Ч��Χ��
        /// </summary>
        /// <param name="velocity">����</param>
        /// <returns>���ƺ������</returns>
        public static int ClampVelocity(int velocity)
        {
            return Math.Max(1, Math.Min(127, velocity));
        }

        /// <summary>
        /// ��ʱ��ֵ��������Ч��Χ��
        /// </summary>
        /// <param name="timeValue">ʱ��ֵ</param>
        /// <param name="minimum">��Сֵ��Ĭ��Ϊ0��</param>
        /// <returns>���ƺ��ʱ��ֵ</returns>
        public static double ClampTimeValue(double timeValue, double minimum = 0.0)
        {
            return Math.Max(minimum, timeValue);
        }
        #endregion
    }
}