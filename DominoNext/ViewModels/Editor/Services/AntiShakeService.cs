using System;
using Avalonia;

namespace DominoNext.ViewModels.Editor.Services
{
    /// <summary>
    /// 防抖动配置
    /// </summary>
    public class AntiShakeConfig
    {
        /// <summary>
        /// 像素阈值
        /// </summary>
        public double PixelThreshold { get; set; } = 1.0;

        /// <summary>
        /// 时间阈值（毫秒）
        /// </summary>
        public double TimeThresholdMs { get; set; } = 100.0;

        /// <summary>
        /// 是否启用像素防抖
        /// </summary>
        public bool EnablePixelAntiShake { get; set; } = true;

        /// <summary>
        /// 是否启用时间防抖
        /// </summary>
        public bool EnableTimeAntiShake { get; set; } = true;

        /// <summary>
        /// 预设配置 - 极简防抖（仅过滤微小移动）
        /// </summary>
        public static AntiShakeConfig Minimal => new()
        {
            PixelThreshold = 1.0,
            TimeThresholdMs = 50.0,
            EnablePixelAntiShake = true,
            EnableTimeAntiShake = false
        };

        /// <summary>
        /// 预设配置 - 标准防抖
        /// </summary>
        public static AntiShakeConfig Standard => new()
        {
            PixelThreshold = 2.0,
            TimeThresholdMs = 100.0,
            EnablePixelAntiShake = true,
            EnableTimeAntiShake = true
        };

        /// <summary>
        /// 预设配置 - 严格防抖（适合手抖严重的用户）
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
    /// 防抖动服务 - 提供统一的防抖动逻辑
    /// 支持像素距离和时间双重防抖策略
    /// </summary>
    public class AntiShakeService
    {
        private readonly AntiShakeConfig _config;

        public AntiShakeService(AntiShakeConfig? config = null)
        {
            _config = config ?? AntiShakeConfig.Standard;
        }

        /// <summary>
        /// 检查移动是否应该被忽略（防抖）
        /// </summary>
        /// <param name="startPosition">起始位置</param>
        /// <param name="currentPosition">当前位置</param>
        /// <param name="startTime">开始时间（可选）</param>
        /// <returns>true表示应该忽略这次移动</returns>
        public bool ShouldIgnoreMovement(Point startPosition, Point currentPosition, DateTime? startTime = null)
        {
            // 像素防抖检查
            if (_config.EnablePixelAntiShake)
            {
                var deltaX = currentPosition.X - startPosition.X;
                var deltaY = currentPosition.Y - startPosition.Y;
                var totalMovement = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                
                if (totalMovement < _config.PixelThreshold)
                {
                    return true; // 移动距离太小，忽略
                }
            }

            // 时间防抖检查
            if (_config.EnableTimeAntiShake && startTime.HasValue)
            {
                var elapsedMs = (DateTime.Now - startTime.Value).TotalMilliseconds;
                if (elapsedMs < _config.TimeThresholdMs)
                {
                    return true; // 时间太短，忽略
                }
            }

            return false; // 不应该忽略
        }

        /// <summary>
        /// 检查按住时间是否为短按（防抖）
        /// </summary>
        /// <param name="startTime">开始时间</param>
        /// <returns>true表示是短按</returns>
        public bool IsShortPress(DateTime startTime)
        {
            var elapsedMs = (DateTime.Now - startTime).TotalMilliseconds;
            return elapsedMs < _config.TimeThresholdMs;
        }

        /// <summary>
        /// 获取当前配置
        /// </summary>
        public AntiShakeConfig Config => _config;

        #region 静态便捷方法
        /// <summary>
        /// 使用标准配置进行防抖检查
        /// </summary>
        public static bool ShouldIgnoreMovementStandard(Point startPosition, Point currentPosition)
        {
            var service = new AntiShakeService(AntiShakeConfig.Standard);
            return service.ShouldIgnoreMovement(startPosition, currentPosition);
        }

        /// <summary>
        /// 使用极简配置进行防抖检查
        /// </summary>
        public static bool ShouldIgnoreMovementMinimal(Point startPosition, Point currentPosition)
        {
            var service = new AntiShakeService(AntiShakeConfig.Minimal);
            return service.ShouldIgnoreMovement(startPosition, currentPosition);
        }
        #endregion
    }
}