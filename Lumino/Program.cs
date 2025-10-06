using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using EnderDebugger;
using Lumino.Services.Implementation;
using Lumino.Services.Interfaces;
using Lumino.Views;
using SkiaSharp;

namespace Lumino
{
    public class Program
    {
        private static VulkanRenderManager? _vulkanRenderManager;
        
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                EnderLogger.Instance.Info("Program", "启动 Lumino 应用程序");
                
                // 初始化全局Vulkan渲染管理器
                InitializeVulkanRenderManager();
                
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.Error("Program", $"应用程序启动失败: {ex.Message}");
                throw;
            }
            finally
            {
                // 清理Vulkan资源
                CleanupVulkanRenderManager();
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer
        public static AppBuilder BuildAvaloniaApp()
        {
            var appBuilder = AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
                
            // 检查Vulkan支持并选择渲染后端
            if (IsVulkanSupported())
            {
                EnderLogger.Instance.Info("Program", "检测到Vulkan支持，启用Vulkan渲染后端");
                
                // 配置Vulkan选项
                var vulkanOptions = new SkiaOptions
                {
                    MaxGpuResourceSizeBytes = 1024 * 1024 * 1024 // 1GB GPU资源限制用于Vulkan
                };
                
                appBuilder = appBuilder
                    .UseSkia()
                    .With(vulkanOptions);
                    
                // 设置Vulkan渲染服务
                appBuilder.AfterSetup(builder =>
                {
                    if (_vulkanRenderManager != null)
                    {
                        VulkanRenderService.SetGlobalRenderManager(_vulkanRenderManager);
                        EnderLogger.Instance.Info("Program", "Vulkan渲染服务已设置");
                    }
                });
            }
            else
            {
                EnderLogger.Instance.Info("Program", "未检测到Vulkan支持，使用Skia渲染后端");
                
                appBuilder = appBuilder
                    .With(new SkiaOptions
                    {
                        MaxGpuResourceSizeBytes = 512 * 1024 * 1024 // 512MB GPU资源限制
                    })
                    .UseSkia();
            }
            
            return appBuilder;
        }
        
        /// <summary>
        /// 检查Vulkan支持
        /// </summary>
        private static bool IsVulkanSupported()
        {
            try
            {
                // 创建临时的Vulkan渲染管理器来检查支持
                using var tempManager = new VulkanRenderManager();
                return tempManager.IsSupported;
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.Warn("Program", $"Vulkan支持检查失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 初始化Vulkan渲染管理器
        /// </summary>
        private static void InitializeVulkanRenderManager()
        {
            try
            {
                _vulkanRenderManager = new VulkanRenderManager();
                EnderLogger.Instance.Info("Program", "Vulkan渲染管理器已创建");
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.Warn("Program", $"Vulkan渲染管理器初始化失败: {ex.Message}");
                _vulkanRenderManager = null;
            }
        }
        
        /// <summary>
        /// 清理Vulkan渲染管理器
        /// </summary>
        private static void CleanupVulkanRenderManager()
        {
            try
            {
                _vulkanRenderManager?.Dispose();
                _vulkanRenderManager = null;
                EnderLogger.Instance.Info("Program", "Vulkan渲染管理器已清理");
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.Warn("Program", $"Vulkan渲染管理器清理失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取Vulkan渲染服务
        /// </summary>
        public static VulkanRenderManager? GetVulkanRenderManager()
        {
            return _vulkanRenderManager;
        }
    }
}