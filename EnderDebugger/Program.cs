using System;
using Avalonia;

namespace EnderDebugger;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"EnderDebugger启动失败: {ex.Message}");
            Console.WriteLine($"异常类型: {ex.GetType().FullName}");
            Console.WriteLine($"堆栈跟踪:\n{ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"\n内部异常: {ex.InnerException.Message}");
                Console.WriteLine($"内部异常堆栈:\n{ex.InnerException.StackTrace}");
            }
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}