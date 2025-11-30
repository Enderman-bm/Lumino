using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Lumino.Services.Implementation;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor;
using EnderDebugger;
using Silk.NET.Vulkan;

namespace Lumino.Views.Rendering.Vulkan
{
    /// <summary>
    /// Vulkan音符离屏渲染器 - 将音符渲染到Vulkan离屏缓冲区，然后转换为Avalonia可用的位图
    /// 这个类负责：
    /// 1. 管理离屏渲染的生命周期
    /// 2. 将音符数据转换为Vulkan渲染命令
    /// 3. 将渲染结果转换为Avalonia WriteableBitmap
    /// </summary>
    public class VulkanNoteOffscreenRenderer : IDisposable
    {
        private readonly IVulkanRenderService _vulkanRenderService;
        private VulkanOffscreenRenderer? _offscreenRenderer;
        private VulkanTextureInterop? _textureInterop;
        
        private uint _lastWidth;
        private uint _lastHeight;
        private bool _initialized;
        private bool _disposed;
        
        // 渲染数据缓存
        private List<NoteRenderData> _noteRenderData = new();
        private bool _noteDataDirty = true;
        
        /// <summary>
        /// 音符渲染数据结构
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct NoteRenderData
        {
            public float X;
            public float Y;
            public float Width;
            public float Height;
            public float R;
            public float G;
            public float B;
            public float A;
            public float CornerRadius;
            public bool IsSelected;
        }
        
        /// <summary>
        /// 获取当前渲染的位图
        /// </summary>
        public WriteableBitmap? Bitmap => _textureInterop?.Bitmap;
        
        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized => _initialized;
        
        /// <summary>
        /// 离屏渲染是否可用
        /// </summary>
        public bool IsAvailable => _vulkanRenderService.IsOffscreenRenderingAvailable;
        
        public VulkanNoteOffscreenRenderer(IVulkanRenderService vulkanRenderService)
        {
            _vulkanRenderService = vulkanRenderService ?? throw new ArgumentNullException(nameof(vulkanRenderService));
        }
        
        /// <summary>
        /// 初始化离屏渲染器
        /// </summary>
        public bool Initialize(uint width, uint height)
        {
            if (_disposed) return false;
            
            if (width == 0 || height == 0)
            {
                EnderLogger.Instance.Warn("VulkanNoteOffscreenRenderer", "无效的尺寸，跳过初始化");
                return false;
            }
            
            // 检查是否需要重新初始化
            if (_initialized && _lastWidth == width && _lastHeight == height)
            {
                return true;
            }
            
            try
            {
                // 从服务获取离屏渲染器
                if (_vulkanRenderService is VulkanRenderService service)
                {
                    _offscreenRenderer = service.OffscreenRenderer;
                    _textureInterop = service.TextureInterop;
                }
                
                if (_offscreenRenderer == null || _textureInterop == null)
                {
                    EnderLogger.Instance.Warn("VulkanNoteOffscreenRenderer", "离屏渲染组件不可用");
                    return false;
                }
                
                // 初始化离屏渲染器
                if (!_offscreenRenderer.Initialize(width, height))
                {
                    EnderLogger.Instance.Error("VulkanNoteOffscreenRenderer", "离屏渲染器初始化失败");
                    return false;
                }
                
                // 确保纹理互操作对象尺寸正确
                _textureInterop.EnsureSize((int)width, (int)height);
                
                _lastWidth = width;
                _lastHeight = height;
                _initialized = true;
                _noteDataDirty = true;
                
                EnderLogger.Instance.Info("VulkanNoteOffscreenRenderer", $"初始化成功，尺寸: {width}x{height}");
                return true;
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "VulkanNoteOffscreenRenderer", "初始化失败");
                return false;
            }
        }
        
        /// <summary>
        /// 准备音符渲染数据
        /// </summary>
        public void PrepareNoteData(
            IEnumerable<NoteViewModel> notes,
            double scrollOffset,
            double verticalScrollOffset,
            double zoom,
            double verticalZoom,
            double keyHeight,
            double viewportWidth,
            double viewportHeight)
        {
            _noteRenderData.Clear();
            
            if (notes == null) return;
            
            foreach (var note in notes)
            {
                // 计算音符位置 - 使用 MusicalFraction 的 ToDouble() 方法
                var startPos = note.StartPosition.ToDouble();
                var duration = note.Duration.ToDouble();
                
                var x = (float)(startPos * zoom - scrollOffset);
                var y = (float)((127 - note.Pitch) * keyHeight * verticalZoom - verticalScrollOffset);
                var width = (float)(duration * zoom);
                var height = (float)(keyHeight * verticalZoom);
                
                // 可见性裁剪
                if (x + width < 0 || x > viewportWidth ||
                    y + height < 0 || y > viewportHeight)
                {
                    continue;
                }
                
                // 获取音符颜色
                Color color;
                if (note.IsSelected)
                {
                    color = Color.FromArgb(255, 255, 200, 100); // 选中的橙色
                }
                else
                {
                    // 根据力度调整透明度
                    var alpha = (byte)Math.Max(180, note.Velocity * 2);
                    color = Color.FromArgb(alpha, 100, 200, 100); // 绿色音符
                }
                
                _noteRenderData.Add(new NoteRenderData
                {
                    X = x,
                    Y = y,
                    Width = Math.Max(width, 2.0f), // 最小宽度
                    Height = Math.Max(height, 2.0f), // 最小高度
                    R = color.R / 255f,
                    G = color.G / 255f,
                    B = color.B / 255f,
                    A = color.A / 255f,
                    CornerRadius = 3.0f,
                    IsSelected = note.IsSelected
                });
            }
            
            _noteDataDirty = true;
        }
        
        /// <summary>
        /// 渲染音符到离屏缓冲区并返回位图
        /// </summary>
        public unsafe WriteableBitmap? RenderTobitmap()
        {
            if (!_initialized || _offscreenRenderer == null || _textureInterop == null)
            {
                return null;
            }
            
            if (_noteRenderData.Count == 0)
            {
                // 没有音符，返回清空的位图
                return _textureInterop.Bitmap;
            }
            
            try
            {
                // 获取VulkanRenderService的VulkanManager
                VulkanManager? vulkanManager = null;
                if (_vulkanRenderService is VulkanRenderService service)
                {
                    vulkanManager = service.GetVulkanManager();
                }
                
                if (vulkanManager == null)
                {
                    EnderLogger.Instance.Warn("VulkanNoteOffscreenRenderer", "VulkanManager不可用");
                    return null;
                }
                
                // 开始离屏渲染 - 使用透明背景
                _offscreenRenderer.BeginRender(0, 0, 0, 0);
                
                // 渲染所有音符
                RenderNotesToOffscreen(vulkanManager);
                
                // 结束渲染并复制到staging buffer
                _offscreenRenderer.EndRender();
                
                // 将结果复制到WriteableBitmap
                _textureInterop.UpdateFromOffscreenRenderer(_offscreenRenderer);
                
                return _textureInterop.Bitmap;
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "VulkanNoteOffscreenRenderer", "渲染失败");
                return null;
            }
        }
        
        /// <summary>
        /// 渲染音符到离屏帧缓冲区
        /// </summary>
        private unsafe void RenderNotesToOffscreen(VulkanManager vulkanManager)
        {
            if (_noteRenderData.Count == 0 || _offscreenRenderer == null)
                return;
            
            var vk = vulkanManager.GetVk();
            var commandBuffer = _offscreenRenderer.GetCommandBuffer();
            var notePipeline = vulkanManager.GetNotePipeline();
            var pipelineLayout = vulkanManager.GetNotePipelineLayout();
            
            if (notePipeline.Handle == 0 || pipelineLayout.Handle == 0)
            {
                EnderLogger.Instance.Warn("VulkanNoteOffscreenRenderer", "音符渲染管线不可用");
                return;
            }
            
            // 绑定音符渲染管线
            vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, notePipeline);
            
            // 创建正交投影矩阵（用于2D渲染）
            var projection = CreateOrthographicProjection(_lastWidth, _lastHeight);
            
            // 渲染每个音符
            foreach (var note in _noteRenderData)
            {
                RenderSingleNote(vk, commandBuffer, pipelineLayout, projection, note);
            }
        }
        
        /// <summary>
        /// 渲染单个音符
        /// </summary>
        private unsafe void RenderSingleNote(
            Vk vk,
            CommandBuffer commandBuffer,
            PipelineLayout pipelineLayout,
            Matrix4x4 projection,
            NoteRenderData note)
        {
            // 创建变换矩阵：先缩放到音符大小，然后平移到位置
            var scale = Matrix4x4.CreateScale(note.Width, note.Height, 1.0f);
            var translation = Matrix4x4.CreateTranslation(note.X, note.Y, 0.0f);
            
            // 最终变换矩阵：Scale * Translation * Projection
            // 注意：Silk.NET使用行主序，GLSL使用列主序，所以乘法顺序需要调整
            var transform = scale * translation * projection;
            
            // 准备push constants数据
            var pushConstantData = new NotePushConstants
            {
                Transform = transform,
                Color = new Vector4(note.R, note.G, note.B, note.A),
                Size = new Vector2(note.Width, note.Height),
                CornerRadius = note.CornerRadius,
                Padding = 0.0f
            };
            
            // 推送常量
            vk.CmdPushConstants(
                commandBuffer,
                pipelineLayout,
                ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
                0,
                (uint)sizeof(NotePushConstants),
                &pushConstantData);
            
            // 绘制音符（使用6个顶点绘制两个三角形组成的矩形）
            vk.CmdDraw(commandBuffer, 6, 1, 0, 0);
        }
        
        /// <summary>
        /// 创建正交投影矩阵
        /// </summary>
        private Matrix4x4 CreateOrthographicProjection(uint width, uint height)
        {
            // 创建2D正交投影：左上角为原点，Y轴向下
            // left=0, right=width, top=0, bottom=height, near=-1, far=1
            return Matrix4x4.CreateOrthographicOffCenter(
                0, width,    // left, right
                height, 0,   // bottom, top (flipped for Y-down)
                -1, 1        // near, far
            );
        }
        
        /// <summary>
        /// 推送常量数据结构（必须与着色器中的布局匹配）
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct NotePushConstants
        {
            public Matrix4x4 Transform;  // 64 bytes
            public Vector4 Color;        // 16 bytes
            public Vector2 Size;         // 8 bytes
            public float CornerRadius;   // 4 bytes
            public float Padding;        // 4 bytes (对齐填充)
        }
        
        /// <summary>
        /// 使缓存失效
        /// </summary>
        public void InvalidateCache()
        {
            _noteDataDirty = true;
        }
        
        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            // 注意：不释放_offscreenRenderer和_textureInterop，因为它们由VulkanRenderService管理
            _noteRenderData.Clear();
            _initialized = false;
            
            GC.SuppressFinalize(this);
        }
    }
}
