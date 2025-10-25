using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Skia;
using EnderDebugger;

namespace Lumino
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // 检查是否附带--debug参数
            if (args.Contains("--debug"))
            {
                LaunchLogViewer();
            }
            
            EnderLogger.Instance.Info("Program", "[EnderDebugger][2025-10-02 18:41:03.114][EnderLogger][Program]程序入口启动");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        
        /// <summary>
        /// 启动独立的日志查看器程序
        /// </summary>
        private static void LaunchLogViewer()
        {
            try
            {
                // 获取当前可执行文件目录
                string currentDir = Directory.GetCurrentDirectory();
                string logViewerPath = Path.Combine(currentDir, "LuminoLogViewer.exe");
                
                // 如果在当前目录找不到，尝试在bin目录查找
                if (!File.Exists(logViewerPath))
                {
                    string binDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
                    logViewerPath = Path.Combine(binDir, "LuminoLogViewer.exe");
                }
                
                // 如果还是找不到，尝试在父目录查找
                if (!File.Exists(logViewerPath))
                {
                    string parentDir = Path.GetDirectoryName(currentDir) ?? "";
                    logViewerPath = Path.Combine(parentDir, "LuminoLogViewer.exe");
                }
                
                if (File.Exists(logViewerPath))
                {
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = logViewerPath,
                            UseShellExecute = false,
                            CreateNoWindow = false,
                            RedirectStandardOutput = false,
                            RedirectStandardError = false,
                            WorkingDirectory = Path.GetDirectoryName(logViewerPath)
                        }
                    };
                    process.Start();
                    EnderLogger.Instance.Info("Program", "已启动日志查看器程序");
                }
                else
                {
                    EnderLogger.Instance.Warn("Program", "日志查看器程序不存在，路径: " + logViewerPath);
                }
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.Error("Program", "启动日志查看器时出错: " + ex.Message);
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                // 启用Skia渲染后端以支持GPU加速
                .UseSkia()
                // 配置Skia选项以优化GPU加速
                .With(new SkiaOptions 
                { 
                    // 设置最大GPU资源大小以支持更复杂的图形渲染
                    MaxGpuResourceSizeBytes = 512 * 1024 * 1024
                });
    }
}