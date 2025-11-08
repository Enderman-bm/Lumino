using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Services.Interfaces;
using Lumino.Services.Implementation;
using System;
using EnderDebugger;

namespace Lumino.Views.Rendering.Spectrogram
{
    /// <summary>
    /// 频谱背景渲染器
    /// 负责将频谱数据渲染为钢琴卷帘的背景
    /// </summary>
    public class SpectrogramRenderer
    {
        private readonly IEditorStateService _editorStateService;

        public SpectrogramRenderer(IEditorStateService editorStateService)
        {
            _editorStateService = editorStateService;
        }

        /// <summary>
        /// 渲染频谱背景
        /// </summary>
        /// <param name="context">绘图上下文</param>
        /// <param name="vulkanAdapter">Vulkan适配器（可选）</param>
        /// <param name="viewModel">视图模型</param>
        /// <param name="bounds">渲染区域</param>
        /// <param name="scrollOffset">水平滚动偏移</param>
        /// <param name="verticalScrollOffset">垂直滚动偏移</param>
        public void RenderSpectrogram(DrawingContext context, object? vulkanAdapter,
            PianoRollViewModel viewModel, Rect bounds, double scrollOffset, double verticalScrollOffset)
        {
            // 检查是否有可用的频谱数据
            if (!(_editorStateService is EditorStateService editorStateService) ||
                editorStateService.SpectrogramData == null || !editorStateService.IsSpectrogramVisible)
                return;

            try
            {
                var spectrogramData = editorStateService.SpectrogramData;
                var sampleRate = editorStateService.SpectrogramSampleRate;
                var duration = editorStateService.SpectrogramDuration;
                var maxFrequency = editorStateService.SpectrogramMaxFrequency;

                // 计算频谱在钢琴卷帘中的显示参数
                var timeScale = bounds.Width / duration; // 时间到像素的缩放
                var frequencyScale = bounds.Height / maxFrequency; // 频率到像素的缩放

                // 获取频谱数据的维度
                var timeFrames = spectrogramData.GetLength(0);
                var frequencyBins = spectrogramData.GetLength(1);

                // 计算每个像素对应的频谱帧和频率bin
                var pixelsPerFrame = timeFrames / duration / timeScale;
                var pixelsPerBin = frequencyBins / maxFrequency / frequencyScale;

                // 简化渲染：将频谱数据渲染为矩形区域
                // 在实际实现中，这里应该使用更高效的纹理映射或逐像素渲染
                RenderSimplifiedSpectrogram(context, bounds, spectrogramData, timeFrames, frequencyBins, 
                    timeScale, frequencyScale, scrollOffset, verticalScrollOffset, viewModel.SpectrogramOpacity);
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "SpectrogramRenderer", "频谱渲染错误");
            }
        }

        /// <summary>
        /// 简化频谱渲染 - 将频谱数据渲染为矩形块
        /// </summary>
        private void RenderSimplifiedSpectrogram(DrawingContext context, Rect bounds, 
            double[,] spectrogramData, int timeFrames, int frequencyBins,
            double timeScale, double frequencyScale, double scrollOffset, double verticalScrollOffset, double opacity)
        {
            // 计算可见区域
            var visibleStartTime = scrollOffset / timeScale;
            var visibleEndTime = (scrollOffset + bounds.Width) / timeScale;
            var visibleStartFreq = verticalScrollOffset / frequencyScale;
            var visibleEndFreq = (verticalScrollOffset + bounds.Height) / frequencyScale;

            // 限制在频谱数据范围内
            visibleStartTime = Math.Max(0, Math.Min(spectrogramData.GetLength(0) - 1, visibleStartTime));
            visibleEndTime = Math.Max(0, Math.Min(spectrogramData.GetLength(0) - 1, visibleEndTime));
            visibleStartFreq = Math.Max(0, Math.Min(spectrogramData.GetLength(1) - 1, visibleStartFreq));
            visibleEndFreq = Math.Max(0, Math.Min(spectrogramData.GetLength(1) - 1, visibleEndFreq));

            // 简化渲染：绘制一个半透明的背景矩形表示频谱区域
            var alpha = (byte)(opacity * 128);
            var spectrogramBrush = new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 255)); // 蓝色带透明度
            var spectrogramRect = new Rect(0, 0, bounds.Width, bounds.Height);
            
            context.DrawRectangle(spectrogramBrush, null, spectrogramRect);

            // 在实际实现中，这里应该：
            // 1. 将频谱数据转换为纹理
            // 2. 使用Shader进行高效的纹理映射
            // 3. 根据频谱强度值应用颜色映射
            // 4. 考虑使用Vulkan或Skia进行硬件加速渲染
        }

        /// <summary>
        /// 获取频谱颜色（根据强度值）
        /// </summary>
        private Color GetSpectrogramColor(double intensity, SpectrogramColorMap colorMap)
        {
            intensity = Math.Max(0, Math.Min(1, intensity));

            return colorMap switch
            {
                SpectrogramColorMap.Viridis => GetViridisColor(intensity),
                SpectrogramColorMap.Heat => GetHeatColor(intensity),
                SpectrogramColorMap.Cool => GetCoolColor(intensity),
                SpectrogramColorMap.Grayscale => GetGrayscaleColor(intensity),
                _ => GetViridisColor(intensity)
            };
        }

        /// <summary>
        /// Viridis配色方案
        /// </summary>
        private Color GetViridisColor(double intensity)
        {
            // 简化的Viridis配色
            var r = (byte)(intensity * 100 + 50);
            var g = (byte)(intensity * 150 + 50);
            var b = (byte)(intensity * 200 + 50);
            return Color.FromRgb(r, g, b);
        }

        /// <summary>
        /// 热力图配色方案
        /// </summary>
        private Color GetHeatColor(double intensity)
        {
            var r = (byte)(intensity * 255);
            var g = (byte)(intensity * 128);
            var b = (byte)(intensity * 64);
            return Color.FromRgb(r, g, b);
        }

        /// <summary>
        /// 冷色调配色方案
        /// </summary>
        private Color GetCoolColor(double intensity)
        {
            var r = (byte)(intensity * 64);
            var g = (byte)(intensity * 128);
            var b = (byte)(intensity * 255);
            return Color.FromRgb(r, g, b);
        }

        /// <summary>
        /// 灰度配色方案
        /// </summary>
        private Color GetGrayscaleColor(double intensity)
        {
            var value = (byte)(intensity * 255);
            return Color.FromRgb(value, value, value);
        }
    }
}