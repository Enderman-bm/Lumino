using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Lumino.Services.Interfaces;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// Ӧ�ó����������ڷ���ʵ�� - ����Ӧ�ó�����������ڲ���
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
                    // ���÷�����ֱ���˳�����
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ӧ�ó����˳�ʱ��������: {ex.Message}");
                // ǿ���˳�
                Environment.Exit(1);
            }
        }

        public void Restart()
        {
            try
            {
                // ��ȡ��ǰӦ�ó����·��
                var currentProcess = Process.GetCurrentProcess();
                var applicationPath = currentProcess.MainModule?.FileName;

                if (!string.IsNullOrEmpty(applicationPath))
                {
                    // �����µ�Ӧ�ó���ʵ��
                    Process.Start(applicationPath);
                    
                    // �رյ�ǰʵ��
                    Shutdown();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("�޷���ȡӦ�ó���·��������ʧ��");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"����Ӧ�ó���ʱ��������: {ex.Message}");
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
                    // ע�⣺����ֻ����С�����ڣ�������ϵͳ���̹�����Ҫ����ʵ��
                    System.Diagnostics.Debug.WriteLine("Ӧ�ó�������С��");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��С��������ʱ��������: {ex.Message}");
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
                    System.Diagnostics.Debug.WriteLine("Ӧ�ó����Ѵ����̻�ԭ");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"�����̻�ԭʱ��������: {ex.Message}");
            }
        }

        public async Task<bool> CanShutdownSafelyAsync()
        {
            try
            {
                // ����Ƿ���δ��������ø���
                if (_settingsService?.Settings != null)
                {
                    // ������Լ�����÷����Ƿ���δ����ĸ���
                    // Ŀǰ����true����ʾ���԰�ȫ�˳�
                }

                // ����Ƿ���δ�������Ŀ�ļ�
                // TODO: ��ʵ������Ŀ�������ܺ�����������Ŀ״̬

                // ����Ƿ������ڽ��еĲ���
                // TODO: ����Ƿ������ڽ��е��ļ���������Ⱦ�����

                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"����Ƿ���԰�ȫ�˳�ʱ��������: {ex.Message}");
                // ��������ʱ���ش����������˳�
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
                var description = "רҵ��MIDI���ֱ༭��";

                // ���Դӳ������Ի�ȡ����ϸ����Ϣ
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
                System.Diagnostics.Debug.WriteLine($"��ȡӦ�ó�����Ϣʱ��������: {ex.Message}");
                return ("DominoNext", "1.0.0", "רҵ��MIDI���ֱ༭��");
            }
        }
    }
}