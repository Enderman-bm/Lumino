using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using Lumino.ViewModels.Editor;
using Lumino.ViewModels.Editor.Components;

namespace Lumino.Views.Rendering.Events
{
    /// <summary>
    /// 频谱图渲染器 - 在钢琴卷帘背景显示音频频谱
    /// </summary>
    public class SpectrogramRenderer
    {
        private WriteableBitmap? _spectrogramBitmap;
        private readonly object _bitmapLock = new object();
        
        /// <summary>
        /// 绘制频谱图到钢琴卷帘背景
        /// </summary>
        public void DrawSpectrogram(DrawingContext context, PianoRollViewModel viewModel, Rect canvasBounds)
        {
            if (viewModel?.SpectrogramData == null || viewModel.SpectrogramData.Length == 0)
                return;

            lock (_bitmapLock)
            {
                try
                {
                    // 确保频谱图像已生成
                    EnsureBitmapUpdated(viewModel);
                    
                    if (_spectrogramBitmap != null)
                    {
                        // 计算绘制区域
                        var pixelSize = _spectrogramBitmap.PixelSize;
                        var sourceRect = new Rect(0, 0, pixelSize.Width, pixelSize.Height);
                        
                        // 计算目标绘制区域（钢琴卷帘的整个可见区域）
                        var noteWidth = 20.0; // 默认音符宽度
                        var noteHeight = 12.0; // 默认音符高度
                        
                        var destRect = new Rect(
                            canvasBounds.X,
                            canvasBounds.Y,
                            Math.Max(canvasBounds.Width, pixelSize.Width * noteWidth),
                            Math.Max(canvasBounds.Height, pixelSize.Height * noteHeight)
                        );

                        // 使用半透明绘制频谱图
                        var opacity = 0.6; // 60%透明度，让音符仍然清晰可见
                        using (context.PushOpacity(opacity))
                        {
                            context.DrawImage(_spectrogramBitmap, sourceRect, destRect);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"频谱图绘制错误: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 确保频谱图像是最新的
        /// </summary>
        private void EnsureBitmapUpdated(PianoRollViewModel viewModel)
        {
            if (viewModel.SpectrogramData == null || viewModel.SpectrogramData.Length == 0)
                return;

            var data = viewModel.SpectrogramData;
            var width = data.GetLength(1); // 时间帧数
            var height = data.GetLength(0); // 频率bin数（MIDI音高）

            // 如果已有位图且尺寸匹配，直接更新
            if (_spectrogramBitmap != null &&
                _spectrogramBitmap.PixelSize.Width == width &&
                _spectrogramBitmap.PixelSize.Height == height)
            {
                UpdateBitmapData(data);
                return;
            }

            // 创建新位图
            _spectrogramBitmap?.Dispose();
            _spectrogramBitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul
            );

            UpdateBitmapData(data);
        }

        /// <summary>
        /// 更新位图数据
        /// </summary>
        private unsafe void UpdateBitmapData(double[,] spectrogramData)
        {
            if (_spectrogramBitmap == null) return;

            using var framebuffer = _spectrogramBitmap.Lock();
            var pixels = (uint*)framebuffer.Address;
            var width = _spectrogramBitmap.PixelSize.Width;
            var height = _spectrogramBitmap.PixelSize.Height;

            // 找出最大值用于归一化
            double maxValue = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    maxValue = Math.Max(maxValue, spectrogramData[y, x]);
                }
            }

            maxValue = Math.Max(maxValue, 0.001); // 防止除以零

            // 填充像素数据
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var intensity = (float)(spectrogramData[y, x] / maxValue);
                    
                    // 使用热力图配色 - 从蓝色到红色的渐变
                    var color = GetHeatmapColor(intensity);
                    
                    pixels[y * width + x] = color;
                }
            }
        }

        /// <summary>
        /// 获取热力图颜色
        /// </summary>
        private uint GetHeatmapColor(float intensity)
        {
            // 限制强度在0-1范围内
            intensity = Math.Clamp(intensity, 0f, 1f);

            // 热力图配色方案：蓝色→青色→绿色→黄色→红色
            byte r, g, b;

            if (intensity < 0.25f)
            {
                // 蓝色到青色
                float t = intensity * 4;
                r = 0;
                g = (byte)(t * 255);
                b = 255;
            }
            else if (intensity < 0.5f)
            {
                // 青色到绿色
                float t = (intensity - 0.25f) * 4;
                r = 0;
                g = 255;
                b = (byte)((1 - t) * 255);
            }
            else if (intensity < 0.75f)
            {
                // 绿色到黄色
                float t = (intensity - 0.5f) * 4;
                r = (byte)(t * 255);
                g = 255;
                b = 0;
            }
            else
            {
                // 黄色到红色
                float t = (intensity - 0.75f) * 4;
                r = 255;
                g = (byte)((1 - t) * 255);
                b = 0;
            }

            // 返回BGRA格式的颜色（Avalonia使用BGRA）
            return (uint)((255 << 24) | (r << 16) | (g << 8) | b);
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            lock (_bitmapLock)
            {
                _spectrogramBitmap?.Dispose();
                _spectrogramBitmap = null;
            }
        }
    }
}