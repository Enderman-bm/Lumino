using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.VisualTree;
using EnderDebugger;
using Lumino.Services.Implementation;
using Lumino.Services.Interfaces;
using Lumino.Views.Rendering.Utils;
using GpuComputeAcceleration = Lumino.Views.Rendering.Utils.GpuComputeAcceleration;
using Silk.NET.Vulkan;

namespace Lumino.Views.Rendering.Vulkan
{
    /// <summary>
    /// Vulkan全局渲染器 - 提供高性能的Vulkan渲染实现
    /// 集成到Avalonia渲染管线中，支持全局渲染优化
    /// </summary>
    public class VulkanGlobalRenderer : IDisposable
    {
        private readonly EnderLogger _logger = EnderLogger.Instance;
        private readonly object _renderLock = new object();
        
        // Vulkan资源
        private VulkanRenderManager? _renderManager;
        private VulkanRenderService? _renderService;
        private GpuComputeAcceleration? _computeAcceleration;
        
        // 渲染状态
        private bool _isInitialized = false;
        private bool _isDisposed = false;
        private int _frameCount = 0;
        private double _lastFrameTime = 0.0;
        
        // 性能监控
        private readonly Stopwatch _frameTimer = new Stopwatch();
        private readonly Queue<double> _frameTimeHistory = new Queue<double>();
        private const int MAX_FRAME_HISTORY = 60;
        
        // 性能统计
        public Lumino.Services.Interfaces.VulkanRenderStats Stats { get; private set; }
        
        /// <summary>
        /// 是否启用Vulkan渲染
        /// </summary>
        public bool IsEnabled { get; private set; }
        
        /// <summary>
        /// 当前帧时间（毫秒）
        /// </summary>
        public double CurrentFrameTime => _lastFrameTime;
        
        /// <summary>
        /// 平均帧时间（毫秒）
        /// </summary>
        public double AverageFrameTime { get; private set; }
        
        /// <summary>
        /// 帧率
        /// </summary>
        public double FrameRate => _lastFrameTime > 0 ? 1000.0 / _lastFrameTime : 0;
        
        /// <summary>
        /// 场景无效事件
        /// </summary>
        public event EventHandler<SceneInvalidatedEventArgs>? SceneInvalidated;
        
        public VulkanGlobalRenderer()
        {
            Stats = new Lumino.Services.Interfaces.VulkanRenderStats();
            _logger.Info("VulkanGlobalRenderer", "创建Vulkan全局渲染器");
        }
        
        /// <summary>
        /// 初始化Vulkan全局渲染器
        /// </summary>
        public bool Initialize(IPlatformHandle? windowHandle = null)
        {
            if (_isInitialized || _isDisposed)
                return false;
                
            try
            {
                _logger.Info("VulkanGlobalRenderer", "开始初始化Vulkan全局渲染器");
                
                // 获取全局Vulkan渲染管理器
                _renderManager = Program.GetVulkanRenderManager();
                if (_renderManager == null)
                {
                    _logger.Error("VulkanGlobalRenderer", "全局Vulkan渲染管理器未初始化");
                    return false;
                }
                
                // 初始化渲染服务 - 使用单例模式
                _renderService = VulkanRenderService.Instance;
                
                if (windowHandle != null)
                {
                    // 创建渲染表面
                    var surface = _renderService.CreateRenderSurface(windowHandle);
                    if (surface == null)
                    {
                        _logger.Error("VulkanGlobalRenderer", "Vulkan渲染表面创建失败");
                        return false;
                    }
                }
                
                // 获取GPU计算加速
                _computeAcceleration = _renderManager.GetComputeAcceleration();
                
                _isInitialized = true;
                IsEnabled = true;
                
                _logger.Info("VulkanGlobalRenderer", "Vulkan全局渲染器初始化完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("VulkanGlobalRenderer", $"Vulkan全局渲染器初始化失败: {ex.Message}");
                Cleanup();
                return false;
            }
        }
        
        /// <summary>
        /// 开始渲染操作
        /// </summary>
        public void StartRender()
        {
            if (!_isInitialized || _isDisposed)
                return;
                
            lock (_renderLock)
            {
                try
                {
                    _frameTimer.Restart();
                    _renderService?.BeginFrame();
                    _frameCount++;
                }
                catch (Exception ex)
                {
                    _logger.Error("VulkanGlobalRenderer", $"开始渲染失败: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 结束渲染操作
        /// </summary>
        public void EndRender()
        {
            if (!_isInitialized || _isDisposed)
                return;
                
            lock (_renderLock)
            {
                try
                {
                    _renderService?.EndFrame();
                    _frameTimer.Stop();
                    
                    _lastFrameTime = _frameTimer.Elapsed.TotalMilliseconds;
                    
                    // 更新帧时间历史
                    _frameTimeHistory.Enqueue(_lastFrameTime);
                    if (_frameTimeHistory.Count > MAX_FRAME_HISTORY)
                        _frameTimeHistory.Dequeue();
                    
                    // 计算平均帧时间
                    double sum = 0;
                    foreach (var time in _frameTimeHistory)
                        sum += time;
                    AverageFrameTime = sum / _frameTimeHistory.Count;
                    
                    // 更新统计信息
                    UpdateStats();
                }
                catch (Exception ex)
                {
                    _logger.Error("VulkanGlobalRenderer", $"结束渲染失败: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 执行GPU计算加速
        /// </summary>
        public async Task<T?> ComputeAsync<T>(Func<GpuComputeAcceleration, Task<T>> computeFunc) where T : class
        {
            if (_computeAcceleration == null || !_isInitialized)
                return null;
                
            try
            {
                return await computeFunc(_computeAcceleration);
            }
            catch (Exception ex)
            {
                _logger.Error("VulkanGlobalRenderer", $"GPU计算失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 触发场景无效事件
        /// </summary>
        protected virtual void OnSceneInvalidated(SceneInvalidatedEventArgs e)
        {
            SceneInvalidated?.Invoke(this, e);
        }
        
        /// <summary>
        /// 获取GPU计算加速实例
        /// </summary>
        public GpuComputeAcceleration? GetComputeAcceleration() => _computeAcceleration;
        
        /// <summary>
        /// 获取Vulkan渲染服务
        /// </summary>
        public VulkanRenderService? GetRenderService() => _renderService;
        
        /// <summary>
        /// 获取Vulkan渲染管理器
        /// </summary>
        public VulkanRenderManager? GetRenderManager() => _renderManager;
        
        /// <summary>
        /// 更新渲染统计信息
        /// </summary>
        private void UpdateStats()
        {
            if (_renderManager != null && _renderManager.Stats != null)
            {
                // 使用正确的属性名称访问VulkanRenderStats
                Stats.FrameCount = (int)_renderManager.Stats.TotalFrames;
                Stats.DrawCalls = (int)_renderManager.Stats.TotalDrawCalls;
                Stats.VerticesRendered = (int)_renderManager.Stats.TotalVertices;
                Stats.MemoryUsed = _renderManager.Stats.MemoryUsage;
            }
            
            Stats.FrameTime = _lastFrameTime;
        }
        
        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            if (_isDisposed)
                return;
                
            try
            {
                _logger.Info("VulkanGlobalRenderer", "开始清理Vulkan全局渲染器资源");
                
                _renderService?.Dispose();
                _renderService = null;
                
                _computeAcceleration = null;
                
                _isInitialized = false;
                IsEnabled = false;
                
                _logger.Info("VulkanGlobalRenderer", "Vulkan全局渲染器资源清理完成");
            }
            catch (Exception ex)
            {
                _logger.Error("VulkanGlobalRenderer", $"清理资源失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Cleanup();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// 析构函数
        /// </summary>
        ~VulkanGlobalRenderer()
        {
            Cleanup();
        }
    }
}