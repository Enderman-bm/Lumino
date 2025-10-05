using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Lumino.Services.Interfaces;
using Lumino.Services.Implementation;
using System;
using System.Diagnostics;
using System.Globalization;

namespace Lumino.Views.Controls.Testing
{
    /// <summary>
    /// Vulkan渲染测试控件
    /// </summary>
    public class VulkanTestControl : Control
    {
        private readonly IVulkanRenderService _vulkanService;
        private readonly Stopwatch _frameTimer = new Stopwatch();
        private int _frameCount = 0;
        private double _lastFpsUpdate = 0;
        private double _currentFps = 0;

        /// <summary>
        /// 从HSV颜色空间创建Color
        /// </summary>
        /// <param name="h">色相 (0-360)</param>
        /// <param name="s">饱和度 (0-1)</param>
        /// <param name="v">明度 (0-1)</param>
        /// <returns>Color对象</returns>
        private static Color ColorFromHsv(double h, double s, double v)
        {
            h = h % 360;
            var c = v * s;
            var x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            var m = v - c;
            
            double r = 0, g = 0, b = 0;
            
            if (h < 60)
            {
                r = c; g = x; b = 0;
            }
            else if (h < 120)
            {
                r = x; g = c; b = 0;
            }
            else if (h < 180)
            {
                r = 0; g = c; b = x;
            }
            else if (h < 240)
            {
                r = 0; g = x; b = c;
            }
            else if (h < 300)
            {
                r = x; g = 0; b = c;
            }
            else
            {
                r = c; g = 0; b = x;
            }
            
            return Color.FromRgb(
                (byte)((r + m) * 255),
                (byte)((g + m) * 255),
                (byte)((b + m) * 255)
            );
        }

        public static readonly StyledProperty<bool> EnableVulkanProperty =
            AvaloniaProperty.Register<VulkanTestControl, bool>(nameof(EnableVulkan), true);

        public bool EnableVulkan
        {
            get => GetValue(EnableVulkanProperty);
            set => SetValue(EnableVulkanProperty, value);
        }

        public VulkanTestControl()
        {
            _vulkanService = VulkanRenderService.Instance;
            _frameTimer.Start();
            
            // 设置60FPS的渲染定时器
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16.67)
            };
            timer.Tick += OnRenderTimerTick;
            timer.Start();
        }

        private void OnRenderTimerTick(object? sender, EventArgs e)
        {
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            var bounds = Bounds;
            
            // 背景
            context.DrawRectangle(Brushes.Black, null, bounds);

            // 测试Vulkan渲染
            if (EnableVulkan && _vulkanService.IsInitialized)
            {
                RenderVulkanContent(context, bounds);
            }
            else
            {
                RenderSkiaContent(context, bounds);
            }

            // 显示FPS和状态信息
            RenderStatusInfo(context, bounds);

            // 更新FPS
            UpdateFps();
        }

        private void RenderVulkanContent(DrawingContext context, Rect bounds)
        {
            // 使用Vulkan渲染测试内容
            var centerX = bounds.Width / 2;
            var centerY = bounds.Height / 2;
            var time = _frameTimer.Elapsed.TotalSeconds;

            // 旋转的彩色矩形
            var angle = time * 2.0; // 每秒2弧度
            var matrix = Matrix.CreateRotation(angle);
            
            using (context.PushTransform(matrix))
            {
                var rect = new Rect(centerX - 50, centerY - 50, 100, 100);
                var brush = new SolidColorBrush(ColorFromHsv((time * 50) % 360, 1.0, 1.0));
                var pen = new Pen(Brushes.White, 2);
                
                context.DrawRectangle(brush, pen, rect);
            }

            // 动态网格
            RenderDynamicGrid(context, bounds, time);
        }

        private void RenderSkiaContent(DrawingContext context, Rect bounds)
        {
            // 使用Skia渲染测试内容
            var centerX = bounds.Width / 2;
            var centerY = bounds.Height / 2;
            var time = _frameTimer.Elapsed.TotalSeconds;

            // 简单的动画圆
            var radius = 30 + Math.Sin(time * 3) * 10;
            var circleBrush = new SolidColorBrush(ColorFromHsv((time * 30) % 360, 0.8, 0.8));
            
            context.DrawEllipse(circleBrush, new Pen(Brushes.White, 2), 
                new Point(centerX, centerY), radius, radius);

            // 静态网格
            RenderStaticGrid(context, bounds);
        }

        private void RenderDynamicGrid(DrawingContext context, Rect bounds, double time)
        {
            var gridSize = 20 + Math.Sin(time) * 5;
            var gridBrush = new SolidColorBrush(Colors.Gray, 0.3);
            var gridPen = new Pen(gridBrush, 1);

            // 垂直线
            for (double x = 0; x < bounds.Width; x += gridSize)
            {
                context.DrawLine(gridPen, new Point(x, 0), new Point(x, bounds.Height));
            }

            // 水平线
            for (double y = 0; y < bounds.Height; y += gridSize)
            {
                context.DrawLine(gridPen, new Point(0, y), new Point(bounds.Width, y));
            }
        }

        private void RenderStaticGrid(DrawingContext context, Rect bounds)
        {
            var gridSize = 25;
            var gridBrush = new SolidColorBrush(Colors.DarkGray, 0.2);
            var gridPen = new Pen(gridBrush, 1);

            // 垂直线
            for (double x = 0; x < bounds.Width; x += gridSize)
            {
                context.DrawLine(gridPen, new Point(x, 0), new Point(x, bounds.Height));
            }

            // 水平线
            for (double y = 0; y < bounds.Height; y += gridSize)
            {
                context.DrawLine(gridPen, new Point(0, y), new Point(bounds.Width, y));
            }
        }

        private void RenderStatusInfo(DrawingContext context, Rect bounds)
        {
            var statusText = $"Renderer: {(EnableVulkan && _vulkanService.IsInitialized ? "Vulkan" : "Skia")}\n" +
                           $"FPS: {_currentFps:F1}\n" +
                           $"Frame Time: {(1000.0 / _currentFps):F1}ms\n" +
                           $"Vulkan Status: {(_vulkanService.IsInitialized ? "Ready" : "Not Available")}";

            var textLayout = new FormattedText(
                statusText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                14,
                Brushes.White);

            var textRect = new Rect(10, 10, 200, 100);
            
            // 半透明背景
            var bgBrush = new SolidColorBrush(Colors.Black, 0.7);
            context.DrawRectangle(bgBrush, null, textRect.Inflate(5));
            
            context.DrawText(textLayout, new Point(15, 15));
        }

        private void UpdateFps()
        {
            _frameCount++;
            var currentTime = _frameTimer.Elapsed.TotalSeconds;
            
            if (currentTime - _lastFpsUpdate >= 1.0) // 每秒更新一次
            {
                _currentFps = _frameCount / (currentTime - _lastFpsUpdate);
                _frameCount = 0;
                _lastFpsUpdate = currentTime;
            }
        }
    }
}