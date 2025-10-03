using System;
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
            EnderLogger.Instance.Info("Program", "[EnderDebugger][2025-10-02 18:41:03.114][EnderLogger][Program]程序入口启动");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
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