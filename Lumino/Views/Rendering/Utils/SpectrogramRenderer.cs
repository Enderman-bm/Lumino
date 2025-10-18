using Avalonia;
using Avalonia.Media;
using System;

namespace Lumino.Views.Rendering.Utils
{
    public class SpectrogramRenderer
    {
        private static readonly IBrush[] HeatmapColors = {
            new SolidColorBrush(Color.FromRgb(0, 0, 0)),      // 最低强度：黑色
            new SolidColorBrush(Color.FromRgb(0, 0, 139)),   // 深蓝
            new SolidColorBrush(Color.FromRgb(0, 100, 0)),   // 深绿
            new SolidColorBrush(Color.FromRgb(0, 255, 0)),   // 亮绿
            new SolidColorBrush(Color.FromRgb(255, 255, 0)), // 黄色
            new SolidColorBrush(Color.FromRgb(255, 165, 0)), // 橙色
            new SolidColorBrush(Color.FromRgb(255, 0, 0))    // 最高强度：红色
        };

        /// <summary>
        /// 渲染频谱图到DrawingContext
        /// </summary>
        /// <param name="context">绘制上下文</param>
        /// <param name="spectrogramData">频谱数据 (time x midiNotes 2D数组)</param>
        /// <param name="sampleRate">采样率</param>
        /// <param name="duration">音频时长(秒)</param>
        /// <param name="maxFrequency">最大频率(Hz)</param>
        /// <param name="opacity">透明度(0-1)</param>
        /// <param name="bounds">画布边界</param>
        /// <param name="scrollOffset">水平滚动偏移</param>
        /// <param name="verticalScrollOffset">垂直滚动偏移</param>
        /// <param name="zoom">水平缩放</param>
        /// <param name="verticalZoom">垂直缩放</param>
        public void RenderSpectrogram(
            DrawingContext context,
            double[,] spectrogramData,
            double sampleRate,
            double duration,
            double maxFrequency,
            double opacity,
            Rect bounds,
            double scrollOffset,
            double verticalScrollOffset,
            double zoom,
            double verticalZoom)
        {
            if (spectrogramData == null || spectrogramData.Length == 0)
                return;

            int timeFrames = spectrogramData.GetLength(0);
            int midiNotes = spectrogramData.GetLength(1);

            if (timeFrames == 0 || midiNotes == 0)
                return;

            // 计算每个帧的像素宽度（考虑缩放和滚动）
            double pixelWidth = (bounds.Width / zoom) / timeFrames;
            double pixelHeight = (bounds.Height / verticalZoom) / midiNotes;

            if (pixelWidth <= 0 || pixelHeight <= 0)
                return;

            // 绘制每个时间-频率bin
            for (int t = 0; t < timeFrames; t++)
            {
                double worldX = (t / (double)timeFrames) * duration * zoom - scrollOffset;
                if (worldX < -pixelWidth || worldX > bounds.Width)
                    continue;

                for (int n = 0; n < midiNotes; n++)
                {
                    double intensity = spectrogramData[t, n];
                    if (intensity <= 0)
                        continue;

                    // 反转Y轴（MIDI音高从低到高，画布从上到下）
                    double worldY = (midiNotes - n - 1) * pixelHeight * verticalZoom - verticalScrollOffset;
                    if (worldY < -pixelHeight || worldY > bounds.Height)
                        continue;

                    // 获取颜色（基于强度映射到热力图）
                    IBrush color = GetHeatmapColor(intensity);
                    var rect = new Rect(worldX, worldY, pixelWidth, pixelHeight);

                    // 应用透明度
                    if (opacity < 1.0)
                    {
                        var originalColor = ((SolidColorBrush)color).Color;
                        var alpha = (byte)(255 * opacity);
                        var newColor = Color.FromArgb(alpha, originalColor.R, originalColor.G, originalColor.B);
                        var semiTransparentBrush = new SolidColorBrush(newColor);
                        context.DrawRectangle(semiTransparentBrush, null, rect);
                    }
                    else
                    {
                        context.DrawRectangle(color, null, rect);
                    }
                }
            }
        }

        /// <summary>
        /// 根据强度获取热力图颜色
        /// </summary>
        private IBrush GetHeatmapColor(double intensity)
        {
            // 归一化强度到0-1范围
            double normalized = Math.Max(0, Math.Min(1, intensity));
            int colorIndex = (int)(normalized * (HeatmapColors.Length - 1));
            return HeatmapColors[colorIndex];
        }
    }
}