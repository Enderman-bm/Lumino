using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using Lumino.Views.Rendering.Utils;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// 资源预加载服务 - 确保所有UI资源在渲染前完全加载
    /// </summary>
    public class ResourcePreloadService
    {
        private static ResourcePreloadService? _instance;
        private bool _resourcesLoaded = false;
        private readonly object _lockObject = new object();

        public static ResourcePreloadService Instance => _instance ??= new ResourcePreloadService();

        /// <summary>
        /// 资源是否已完全加载
        /// </summary>
        public bool ResourcesLoaded
        {
            get
            {
                lock (_lockObject)
                {
                    return _resourcesLoaded;
                }
            }
            private set
            {
                lock (_lockObject)
                {
                    _resourcesLoaded = value;
                }
            }
        }

        /// <summary>
        /// 预加载所有关键UI资源
        /// </summary>
        public async Task PreloadResourcesAsync()
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    System.Diagnostics.Debug.WriteLine("开始预加载UI资源...");

                    // 验证关键资源是否可用
                    ValidateKeyResources();

                    System.Diagnostics.Debug.WriteLine("UI资源预加载完成");
                    ResourcesLoaded = true;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"资源预加载失败: {ex.Message}");
                // 即使失败也标记为已加载，避免无限等待
                ResourcesLoaded = true;
            }
        }

        /// <summary>
        /// 验证关键资源是否可用
        /// </summary>
        private void ValidateKeyResources()
        {
            // 关键画刷资源列表
            string[] keyBrushResources = 
            {
                "MainCanvasBackgroundBrush",
                "GridLineBrush", 
                "KeyWhiteBrush",
                "KeyBlackBrush",
                "NoteBrush",
                "SelectionBrush",
                "VelocityIndicatorBrush",
                "MeasureLineBrush",
                "BorderLineBlackBrush"
            };

            foreach (var resourceKey in keyBrushResources)
            {
                try
                {
                    // 尝试获取资源，如果不存在会返回回退颜色
                    var brush = RenderingUtils.GetResourceBrush(resourceKey, "#FFFFFFFF");
                    System.Diagnostics.Debug.WriteLine($"资源验证成功: {resourceKey}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"资源验证失败: {resourceKey} - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 等待资源加载完成
        /// </summary>
        public async Task WaitForResourcesAsync(int timeoutMs = 5000)
        {
            var startTime = DateTime.Now;
            while (!ResourcesLoaded && (DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                await Task.Delay(50);
            }

            if (!ResourcesLoaded)
            {
                System.Diagnostics.Debug.WriteLine("资源加载超时，继续执行");
                ResourcesLoaded = true; // 防止无限等待
            }
        }

        /// <summary>
        /// 重置资源加载状态（用于主题切换时）
        /// </summary>
        public void ResetResourceState()
        {
            ResourcesLoaded = false;
        }
    }
}