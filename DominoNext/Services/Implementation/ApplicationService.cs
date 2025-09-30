using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using DominoNext.Services.Interfaces;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// 应用程序生命周期服务实现 - 管理应用程序的生命周期操作
    /// </summary>
    public class ApplicationService : IApplicationService
    {
        private readonly ISettingsService? _settingsService;

        public ApplicationService(ISettingsService? settingsService = null)
        {
            _settingsService = settingsService;
        }

        public void Shutdown()
        {
            try
            {
                if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
                else
                {
                    // 备用方案：直接退出进程
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用程序退出时发生错误: {ex.Message}");
                // 强制退出
                Environment.Exit(1);
            }
        }

        public void Restart()
        {
            try
            {
                // 获取当前应用程序的路径
                var currentProcess = Process.GetCurrentProcess();
                var applicationPath = currentProcess.MainModule?.FileName;

                if (!string.IsNullOrEmpty(applicationPath))
                {
                    // 启动新的应用程序实例
                    Process.Start(applicationPath);
                    
                    // 关闭当前实例
                    Shutdown();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("无法获取应用程序路径，重启失败");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"重启应用程序时发生错误: {ex.Message}");
            }
        }

        public void MinimizeToTray()
        {
            try
            {
                if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow != null)
                {
                    desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Minimized;
                    // 注意：这里只是最小化窗口，真正的系统托盘功能需要额外实现
                    System.Diagnostics.Debug.WriteLine("应用程序已最小化");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"最小化到托盘时发生错误: {ex.Message}");
            }
        }

        public void RestoreFromTray()
        {
            try
            {
                if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow != null)
                {
                    desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
                    desktop.MainWindow.Activate();
                    System.Diagnostics.Debug.WriteLine("应用程序已从托盘还原");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从托盘还原时发生错误: {ex.Message}");
            }
        }

        public async Task<bool> CanShutdownSafelyAsync()
        {
            try
            {
                // 检查是否有未保存的设置更改
                if (_settingsService?.Settings != null)
                {
                    // 这里可以检查设置服务是否有未保存的更改
                    // 目前返回true，表示可以安全退出
                }

                // 检查是否有未保存的项目文件
                // TODO: 当实现了项目管理功能后，在这里检查项目状态

                // 检查是否有正在进行的操作
                // TODO: 检查是否有正在进行的文件操作、渲染任务等

                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查是否可以安全退出时发生错误: {ex.Message}");
                // 发生错误时保守处理，允许退出
                return true;
            }
        }

        public (string Name, string Version, string Description) GetApplicationInfo()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var name = assembly.GetName().Name ?? "DominoNext";
                var version = assembly.GetName().Version?.ToString() ?? "1.0.0.0";
                var description = "专业的MIDI音乐编辑器";

                // 尝试从程序集属性获取更详细的信息
                var titleAttribute = assembly.GetCustomAttribute<AssemblyTitleAttribute>();
                var descriptionAttribute = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();
                
                if (titleAttribute != null)
                    name = titleAttribute.Title;
                
                if (descriptionAttribute != null)
                    description = descriptionAttribute.Description;

                return (name, version, description);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取应用程序信息时发生错误: {ex.Message}");
                return ("DominoNext", "1.0.0", "专业的MIDI音乐编辑器");
            }
        }
    }
}