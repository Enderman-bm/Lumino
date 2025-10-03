using System.Threading.Tasks;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// 应用程序生命周期服务接口 - 管理应用程序的生命周期操作
    /// </summary>
    public interface IApplicationService
    {
        /// <summary>
        /// 退出应用程序
        /// </summary>
        void Shutdown();

        /// <summary>
        /// 重启应用程序
        /// </summary>
        void Restart();

        /// <summary>
        /// 最小化应用程序到系统托盘
        /// </summary>
        void MinimizeToTray();

        /// <summary>
        /// 从系统托盘还原应用程序
        /// </summary>
        void RestoreFromTray();

        /// <summary>
        /// 检查是否可以安全退出（例如是否有未保存的更改）
        /// </summary>
        /// <returns>是否可以安全退出</returns>
        Task<bool> CanShutdownSafelyAsync();

        /// <summary>
        /// 获取应用程序信息
        /// </summary>
        /// <returns>应用程序版本、名称等信息</returns>
        (string Name, string Version, string Description) GetApplicationInfo();
    }
}