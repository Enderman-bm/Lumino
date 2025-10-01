using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using DominoNext.Services.Interfaces;
using EnderDebugger;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// Ӧó����������ڷ���ʵ�� - ����Ӧó�����������ڲ���
    /// </summary>
    public class ApplicationService : IApplicationService
    {
        private readonly ISettingsService? _settingsService;
        private readonly EnderLogger _logger;

        public ApplicationService(ISettingsService? settingsService = null)
        {
            _settingsService = settingsService;
            _logger = EnderLogger.Instance;
        }

        public void Shutdown()
        {
            try
            {
                _logger.Info("ApplicationService", "正在关闭应用程序");
                if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
                else
                {
                    // ÷ֱ˳
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("ApplicationService", $"应用程序关闭时发生错误: {ex.Message}");
                // ǿ˳
                Environment.Exit(1);
            }
        }

        public void Restart()
        {
            try
            {
                _logger.Info("ApplicationService", "正在重启应用程序");
                // ȡǰӦó·
                var currentProcess = Process.GetCurrentProcess();
                var applicationPath = currentProcess.MainModule?.FileName;

                if (!string.IsNullOrEmpty(applicationPath))
                {
                    // µӦóʵ
                    Process.Start(applicationPath);
                    
                    // رյǰʵ
                    Shutdown();
                }
                else
                {
                    _logger.Error("ApplicationService", "无法获取应用程序路径，重启失败");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("ApplicationService", $"重启应用程序时发生错误: {ex.Message}");
            }
        }

        public void MinimizeToTray()
        {
            try
            {
                _logger.Info("ApplicationService", "正在最小化应用程序到托盘");
                if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow != null)
                {
                    desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Minimized;
                    // ע⣺ֻСڣϵͳ̹Ҫʵ
                    _logger.Info("ApplicationService", "应用程序已最小化到托盘");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("ApplicationService", $"最小化到托盘时发生错误: {ex.Message}");
            }
        }

        public void RestoreFromTray()
        {
            try
            {
                _logger.Info("ApplicationService", "正在从托盘恢复应用程序");
                if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow != null)
                {
                    desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
                    desktop.MainWindow.Activate();
                    _logger.Info("ApplicationService", "应用程序已从托盘恢复");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("ApplicationService", $"从托盘恢复时发生错误: {ex.Message}");
            }
        }

        public async Task<bool> CanShutdownSafelyAsync()
        {
            try
            {
                _logger.Info("ApplicationService", "正在检查是否可以安全关闭应用程序");
                // Ƿδø
                if (_settingsService?.Settings != null)
                {
                    // Լ÷Ƿδø
                    // Ŀtrueʾ°²ȫ˳
                }

                // ǷδĿļ
                // TODO: ʵĿļܺ״̬

                // ǷڽеĲ
                // TODO: ڽеļȾ

                var result = await Task.FromResult(true);
                _logger.Info("ApplicationService", $"安全关闭检查结果: {result}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error("ApplicationService", $"检查是否可以安全关闭时发生错误: {ex.Message}");
                // ʱشtrue˳
                return true;
            }
        }

        public (string Name, string Version, string Description) GetApplicationInfo()
        {
            try
            {
                _logger.Info("ApplicationService", "正在获取应用程序信息");
                var assembly = Assembly.GetExecutingAssembly();
                var name = assembly.GetName().Name ?? "DominoNext";
                var version = assembly.GetName().Version?.ToString() ?? "1.0.0.0";
                var description = "专业的MIDI音乐编辑器";

                // ³¢ԡϢ
                var titleAttribute = assembly.GetCustomAttribute<AssemblyTitleAttribute>();
                var descriptionAttribute = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();
                
                if (titleAttribute != null)
                    name = titleAttribute.Title;
                
                if (descriptionAttribute != null)
                    description = descriptionAttribute.Description;

                var result = (name, version, description);
                _logger.Info("ApplicationService", $"获取应用程序信息成功: {name} v{version}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error("ApplicationService", $"获取应用程序信息时发生错误: {ex.Message}");
                return ("DominoNext", "1.0.0", "专业的MIDI音乐编辑器");
            }
        }
    }
}