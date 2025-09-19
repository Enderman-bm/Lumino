using System;
using Avalonia;

namespace Lumino.ViewModels.Editor.Services
{
    /// <summary>
    /// ����������
    /// </summary>
    public class AntiShakeConfig
    {
        /// <summary>
        /// ������ֵ
        /// </summary>
        public double PixelThreshold { get; set; } = 1.0;

        /// <summary>
        /// ʱ����ֵ�����룩
        /// </summary>
        public double TimeThresholdMs { get; set; } = 100.0;

        /// <summary>
        /// �Ƿ��������ط���
        /// </summary>
        public bool EnablePixelAntiShake { get; set; } = true;

        /// <summary>
        /// �Ƿ�����ʱ�����
        /// </summary>
        public bool EnableTimeAntiShake { get; set; } = true;

        /// <summary>
        /// Ԥ������ - ���������������΢С�ƶ���
        /// </summary>
        public static AntiShakeConfig Minimal => new()
        {
            PixelThreshold = 1.0,
            TimeThresholdMs = 50.0,
            EnablePixelAntiShake = true,
            EnableTimeAntiShake = false
        };

        /// <summary>
        /// Ԥ������ - ��׼����
        /// </summary>
        public static AntiShakeConfig Standard => new()
        {
            PixelThreshold = 2.0,
            TimeThresholdMs = 100.0,
            EnablePixelAntiShake = true,
            EnableTimeAntiShake = true
        };

        /// <summary>
        /// Ԥ������ - �ϸ�������ʺ��ֶ����ص��û���
        /// </summary>
        public static AntiShakeConfig Strict => new()
        {
            PixelThreshold = 5.0,
            TimeThresholdMs = 200.0,
            EnablePixelAntiShake = true,
            EnableTimeAntiShake = true
        };
    }

    /// <summary>
    /// ���������� - �ṩͳһ�ķ������߼�
    /// ֧�����ؾ����ʱ��˫�ط�������
    /// </summary>
    public class AntiShakeService
    {
        private readonly AntiShakeConfig _config;

        public AntiShakeService(AntiShakeConfig? config = null)
        {
            _config = config ?? AntiShakeConfig.Standard;
        }

        /// <summary>
        /// ����ƶ��Ƿ�Ӧ�ñ����ԣ�������
        /// </summary>
        /// <param name="startPosition">��ʼλ��</param>
        /// <param name="currentPosition">��ǰλ��</param>
        /// <param name="startTime">��ʼʱ�䣨��ѡ��</param>
        /// <returns>true��ʾӦ�ú�������ƶ�</returns>
        public bool ShouldIgnoreMovement(Point startPosition, Point currentPosition, DateTime? startTime = null)
        {
            // ���ط������
            if (_config.EnablePixelAntiShake)
            {
                var deltaX = currentPosition.X - startPosition.X;
                var deltaY = currentPosition.Y - startPosition.Y;
                var totalMovement = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                
                if (totalMovement < _config.PixelThreshold)
                {
                    return true; // �ƶ�����̫С������
                }
            }

            // ʱ��������
            if (_config.EnableTimeAntiShake && startTime.HasValue)
            {
                var elapsedMs = (DateTime.Now - startTime.Value).TotalMilliseconds;
                if (elapsedMs < _config.TimeThresholdMs)
                {
                    return true; // ʱ��̫�̣�����
                }
            }

            return false; // ��Ӧ�ú���
        }

        /// <summary>
        /// ��鰴סʱ���Ƿ�Ϊ�̰���������
        /// </summary>
        /// <param name="startTime">��ʼʱ��</param>
        /// <returns>true��ʾ�Ƕ̰�</returns>
        public bool IsShortPress(DateTime startTime)
        {
            var elapsedMs = (DateTime.Now - startTime).TotalMilliseconds;
            return elapsedMs < _config.TimeThresholdMs;
        }

        /// <summary>
        /// ��ȡ��ǰ����
        /// </summary>
        public AntiShakeConfig Config => _config;

        #region ��̬��ݷ���
        /// <summary>
        /// ʹ�ñ�׼���ý��з������
        /// </summary>
        public static bool ShouldIgnoreMovementStandard(Point startPosition, Point currentPosition)
        {
            var service = new AntiShakeService(AntiShakeConfig.Standard);
            return service.ShouldIgnoreMovement(startPosition, currentPosition);
        }

        /// <summary>
        /// ʹ�ü������ý��з������
        /// </summary>
        public static bool ShouldIgnoreMovementMinimal(Point startPosition, Point currentPosition)
        {
            var service = new AntiShakeService(AntiShakeConfig.Minimal);
            return service.ShouldIgnoreMovement(startPosition, currentPosition);
        }
        #endregion
    }
}