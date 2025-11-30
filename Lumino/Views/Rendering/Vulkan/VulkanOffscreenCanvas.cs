using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Lumino.Services.Implementation;
using Lumino.ViewModels.Editor;
using Silk.NET.Vulkan;
using Silk.NET.Maths;
using EnderDebugger;

namespace Lumino.Views.Rendering.Vulkan
{
    /// <summary>
    /// Vulkan离屏渲染画布 - 将Vulkan渲染结果显示为Avalonia位图
    /// 用于高性能音符渲染，避免Vulkan和Avalonia直接渲染到窗口的冲突
    /// </summary>
    public class VulkanOffscreenCanvas : Control
    {
        #region 依赖属性

        public static readonly StyledProperty<PianoRollViewModel?> ViewModelProperty =
            AvaloniaProperty.Register<VulkanOffscreenCanvas, PianoRollViewModel?>(nameof(ViewModel));

        public PianoRollViewModel? ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        #endregion

        #region 私有字段

        private VulkanOffscreenRenderer? _renderer;
        private VulkanTextureInterop? _textureInterop;
        private WriteableBitmap? _currentBitmap;
        private bool _isInitialized = false;
        private bool _needsRender = true;
        private uint _lastWidth = 0;
        private uint _lastHeight = 0;

        // 渲染数据缓存
        private List<NoteRenderData>? _cachedNotes;
        private bool _noteCacheValid = false;

        // 性能统计
        private int _frameCount = 0;
        private DateTime _lastFpsUpdate = DateTime.Now;
        private double _currentFps = 0;

        #endregion

        #region 数据结构

        private struct NoteRenderData
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

        #endregion

        #region 构造函数

        public VulkanOffscreenCanvas()
        {
            // 监听属性变化
            ViewModelProperty.Changed.AddClassHandler<VulkanOffscreenCanvas>((canvas, args) =>
            {
                canvas.OnViewModelChanged();
            });
        }

        #endregion

        #region 初始化

        private void EnsureInitialized()
        {
            if (_isInitialized)
                return;

            var service = VulkanRenderService.Instance;
            if (!service.IsInitialized || !service.IsOffscreenRenderingAvailable)
            {
                EnderLogger.Instance.Debug("VulkanOffscreenCanvas", "离屏渲染服务不可用");
                return;
            }

            _renderer = service.OffscreenRenderer;
            _textureInterop = service.TextureInterop;
            _isInitialized = true;

            EnderLogger.Instance.Info("VulkanOffscreenCanvas", "离屏画布初始化成功");
        }

        #endregion

        #region ViewModel变化处理

        private void OnViewModelChanged()
        {
            InvalidateNoteCache();
            InvalidateVisual();
        }

        private void InvalidateNoteCache()
        {
            _noteCacheValid = false;
            _needsRender = true;
        }

        #endregion

        #region 渲染

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var bounds = Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            EnsureInitialized();

            // 如果Vulkan不可用，使用Skia回退渲染
            if (!_isInitialized || _renderer == null || _textureInterop == null)
            {
                RenderWithSkia(context, bounds);
                return;
            }

            try
            {
                // 检查是否需要更新渲染尺寸
                uint width = (uint)bounds.Width;
                uint height = (uint)bounds.Height;

                if (width != _lastWidth || height != _lastHeight)
                {
                    _lastWidth = width;
                    _lastHeight = height;
                    _needsRender = true;

                    // 重新初始化离屏渲染器
                    if (!_renderer.Initialize(width, height))
                    {
                        EnderLogger.Instance.Warn("VulkanOffscreenCanvas", $"无法初始化离屏渲染器: {width}x{height}");
                        RenderWithSkia(context, bounds);
                        return;
                    }
                }

                // 如果需要渲染
                if (_needsRender && _renderer.IsInitialized)
                {
                    RenderWithVulkan();
                    _needsRender = false;
                }

                // 将Vulkan渲染结果绘制到Avalonia上下文
                if (_currentBitmap != null)
                {
                    context.DrawImage(_currentBitmap, new Rect(0, 0, _currentBitmap.PixelSize.Width, _currentBitmap.PixelSize.Height), bounds);
                }

                // 更新FPS统计
                UpdateFpsCounter();
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "VulkanOffscreenCanvas", "渲染失败");
                RenderWithSkia(context, bounds);
            }
        }

        /// <summary>
        /// 使用Vulkan进行离屏渲染
        /// </summary>
        private unsafe void RenderWithVulkan()
        {
            if (_renderer == null || _textureInterop == null || ViewModel == null)
                return;

            try
            {
                var service = VulkanRenderService.Instance;
                var vulkanManager = service.VulkanManager;
                if (vulkanManager == null)
                    return;

                // 准备渲染数据
                PrepareNoteData();

                if (_cachedNotes == null || _cachedNotes.Count == 0)
                {
                    // 没有音符，清空画布
                    _renderer.BeginRender(0, 0, 0, 0);
                    _renderer.EndRender();
                    _textureInterop.UpdateFromOffscreenRenderer(_renderer);
                    _currentBitmap = _textureInterop.Bitmap;
                    return;
                }

                // 开始离屏渲染（透明背景）
                _renderer.BeginRender(0, 0, 0, 0);

                var cmdBuffer = _renderer.GetCommandBuffer();
                var vk = vulkanManager.GetVk();
                var pipeline = vulkanManager.GetNotePipeline();
                var layout = vulkanManager.GetNotePipelineLayout();

                // 绑定管线
                vk.CmdBindPipeline(cmdBuffer, PipelineBindPoint.Graphics, pipeline);

                // 创建正交投影矩阵
                var projection = Matrix4X4.CreateOrthographicOffCenter<float>(
                    0, (float)_renderer.Width, (float)_renderer.Height, 0, -1, 1);

                // 渲染每个音符
                foreach (var note in _cachedNotes)
                {
                    RenderNote(vk, cmdBuffer, layout, projection, note);
                }

                // 结束渲染并复制到CPU缓冲区
                _renderer.EndRender();

                // 更新Avalonia位图
                _textureInterop.UpdateFromOffscreenRenderer(_renderer);
                _currentBitmap = _textureInterop.Bitmap;

                EnderLogger.Instance.Debug("VulkanOffscreenCanvas", $"Vulkan渲染完成: {_cachedNotes.Count}个音符");
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "VulkanOffscreenCanvas", "Vulkan渲染失败");
            }
        }

        /// <summary>
        /// 渲染单个音符
        /// </summary>
        private unsafe void RenderNote(Vk vk, CommandBuffer cmdBuffer, PipelineLayout layout,
            Matrix4X4<float> projection, NoteRenderData note)
        {
            // 构建MVP矩阵
            // Silk.NET行主序 -> GLSL列主序需要构建转置的MVP
            var mvp = Matrix4X4.CreateScale<float>(note.Width, note.Height, 1.0f) *
                      Matrix4X4.CreateTranslation<float>(note.X, note.Y, 0.0f) *
                      projection;

            // 创建push constants
            var pushConstants = new PushConstants
            {
                Projection = mvp,
                Color = new Vector4D<float>(note.R, note.G, note.B, note.A),
                Size = new Vector2D<float>(note.Width, note.Height),
                Radius = note.CornerRadius,
                Padding = 0
            };

            // 推送常量
            vk.CmdPushConstants(cmdBuffer, layout, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
                0, (uint)sizeof(PushConstants), &pushConstants);

            // 绘制四边形（6个顶点）
            vk.CmdDraw(cmdBuffer, 6, 1, 0, 0);
        }

        /// <summary>
        /// Push Constants结构（与着色器匹配）
        /// </summary>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct PushConstants
        {
            public Matrix4X4<float> Projection; // 64字节
            public Vector4D<float> Color;       // 16字节
            public Vector2D<float> Size;        // 8字节
            public float Radius;                // 4字节
            public float Padding;               // 4字节 (对齐)
        }

        /// <summary>
        /// 准备音符渲染数据
        /// </summary>
        private void PrepareNoteData()
        {
            if (_noteCacheValid && _cachedNotes != null)
                return;

            _cachedNotes = new List<NoteRenderData>();

            if (ViewModel == null)
            {
                _noteCacheValid = true;
                return;
            }

            var notes = ViewModel.Notes;
            if (notes == null || notes.Count == 0)
            {
                _noteCacheValid = true;
                return;
            }

            var scrollOffset = ViewModel.CurrentScrollOffset;
            var verticalScrollOffset = ViewModel.VerticalScrollOffset;
            var zoom = ViewModel.Zoom;
            var verticalZoom = ViewModel.VerticalZoom;
            var keyHeight = ViewModel.KeyHeight;

            foreach (var note in notes)
            {
                // 计算音符位置 - 使用StartPosition和Duration的ToDouble()方法转换MusicalFraction
                var startPos = note.StartPosition.ToDouble();
                var duration = note.Duration.ToDouble();
                var x = (float)(startPos * zoom - scrollOffset);
                var y = (float)((127 - note.Pitch) * keyHeight * verticalZoom - verticalScrollOffset);
                var width = (float)(duration * zoom);
                var height = (float)(keyHeight * verticalZoom);

                // 可见性检测
                if (x + width < 0 || x > _lastWidth ||
                    y + height < 0 || y > _lastHeight)
                {
                    continue;
                }

                // 获取音符颜色
                var color = GetNoteColor(note);

                _cachedNotes.Add(new NoteRenderData
                {
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                    R = color.R / 255f,
                    G = color.G / 255f,
                    B = color.B / 255f,
                    A = color.A / 255f,
                    CornerRadius = 3.0f,
                    IsSelected = note.IsSelected
                });
            }

            _noteCacheValid = true;
        }

        /// <summary>
        /// 获取音符颜色
        /// </summary>
        private Color GetNoteColor(NoteViewModel note)
        {
            if (note.IsSelected)
            {
                return Color.FromArgb(255, 255, 200, 100); // 选中的橙色
            }

            // 根据力度调整透明度
            var alpha = (byte)(Math.Max(180, note.Velocity * 2));
            return Color.FromArgb(alpha, 100, 200, 100); // 绿色音符
        }

        /// <summary>
        /// 使用Skia回退渲染
        /// </summary>
        private void RenderWithSkia(DrawingContext context, Rect bounds)
        {
            if (ViewModel == null)
                return;

            var notes = ViewModel.Notes;
            if (notes == null || notes.Count == 0)
                return;

            var scrollOffset = ViewModel.CurrentScrollOffset;
            var verticalScrollOffset = ViewModel.VerticalScrollOffset;
            var zoom = ViewModel.Zoom;
            var verticalZoom = ViewModel.VerticalZoom;
            var keyHeight = ViewModel.KeyHeight;

            var normalBrush = new SolidColorBrush(Color.FromArgb(255, 100, 200, 100));
            var selectedBrush = new SolidColorBrush(Color.FromArgb(255, 255, 200, 100));
            var borderPen = new Pen(new SolidColorBrush(Colors.DarkGreen), 1);

            foreach (var note in notes)
            {
                // 使用StartPosition和Duration的ToDouble()方法转换MusicalFraction
                var startPos = note.StartPosition.ToDouble();
                var duration = note.Duration.ToDouble();
                var x = startPos * zoom - scrollOffset;
                var y = (127 - note.Pitch) * keyHeight * verticalZoom - verticalScrollOffset;
                var width = duration * zoom;
                var height = keyHeight * verticalZoom;

                // 可见性检测
                if (x + width < 0 || x > bounds.Width ||
                    y + height < 0 || y > bounds.Height)
                {
                    continue;
                }

                var rect = new Rect(x, y, width, height);
                var roundedRect = new RoundedRect(rect, 3);
                var brush = note.IsSelected ? selectedBrush : normalBrush;

                context.DrawRectangle(brush, borderPen, roundedRect);
            }
        }

        /// <summary>
        /// 更新FPS计数器
        /// </summary>
        private void UpdateFpsCounter()
        {
            _frameCount++;
            var now = DateTime.Now;
            var elapsed = (now - _lastFpsUpdate).TotalSeconds;

            if (elapsed >= 1.0)
            {
                _currentFps = _frameCount / elapsed;
                _frameCount = 0;
                _lastFpsUpdate = now;

                EnderLogger.Instance.Debug("VulkanOffscreenCanvas", $"FPS: {_currentFps:F1}");
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 请求重新渲染
        /// </summary>
        public void RequestRender()
        {
            _needsRender = true;
            InvalidateVisual();
        }

        /// <summary>
        /// 使音符缓存无效
        /// </summary>
        public void InvalidateNotes()
        {
            InvalidateNoteCache();
        }

        #endregion
    }
}
