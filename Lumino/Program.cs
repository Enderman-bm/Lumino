using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Skia;
using EnderDebugger;
using Lumino.Models.Settings;

namespace Lumino
{
    internal sealed class Program
    {
        private static readonly string GraphicsSettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Lumino",
            "graphics_settings.json");

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
            
            // 读取渲染模式设置
            var renderingMode = GetSavedRenderingMode();
            EnderLogger.Instance.Info("Program", $"渲染模式设置: {renderingMode}");
            
            BuildAvaloniaApp(renderingMode).StartWithClassicDesktopLifetime(args);
        }
        
        /// <summary>
        /// 获取保存的渲染模式设置
        /// </summary>
        private static RenderingModeType GetSavedRenderingMode()
        {
            try
            {
                if (File.Exists(GraphicsSettingsFilePath))
                {
                    var json = File.ReadAllText(GraphicsSettingsFilePath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("RenderingMode", out var modeElement))
                    {
                        var modeValue = modeElement.GetInt32();
                        return (RenderingModeType)modeValue;
                    }
                }
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.Error("Program", $"读取渲染模式设置失败: {ex.Message}");
            }
            return RenderingModeType.Hardware;
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
            => BuildAvaloniaApp(RenderingModeType.Hardware);
        
        public static AppBuilder BuildAvaloniaApp(RenderingModeType renderingMode)
        {
            var builder = AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                // 启用Skia渲染后端
                .UseSkia();
            
            if (renderingMode == RenderingModeType.Software)
            {
                // 软件渲染模式 - 禁用 GPU 加速
                EnderLogger.Instance.Info("Program", "使用软件渲染模式 (CPU)");
                builder = builder.With(new SkiaOptions 
                { 
                    MaxGpuResourceSizeBytes = 0  // 设置为0禁用GPU资源
                });
            }
            else
            {
                // 硬件加速模式 - 启用 GPU 加速
                EnderLogger.Instance.Info("Program", "使用硬件加速渲染模式 (GPU)");
                builder = builder.With(new SkiaOptions 
                { 
                    // 设置最大GPU资源大小以支持更复杂的图形渲染
                    MaxGpuResourceSizeBytes = 512 * 1024 * 1024
                });
            }
            
            return builder;
        }
    }
}