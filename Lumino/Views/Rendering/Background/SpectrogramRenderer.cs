using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lumino.Views.Rendering.Adapters;

namespace Lumino.Views.Rendering.Background
{
    /// <summary>
    /// 频谱图渲染器 - 将音频频谱绘制到钢琴卷帘背景
    /// </summary>
    public class SpectrogramRenderer
    {
        private WriteableBitmap? _cachedBitmap;
        private double[,]? _cachedData;
        private int _cachedSampleRate;
        private double _cachedDuration;
        private double _cachedMaxFrequency;

        /// <summary>
        /// 渲染频谱图到钢琴卷帘背景
        /// </summary>
        public void RenderSpectrogram(
            DrawingContext context,
            double[,] spectrogramData,
            int sampleRate,
            double duration,
            double maxFrequency,
            double opacity,
            Rect bounds,
            double scrollOffset,
            double verticalScrollOffset,
            double zoom,
            double verticalZoom)
        {
            if (spectrogramData == null || spectrogramData.GetLength(0) == 0 || spectrogramData.GetLength(1) == 0)
                return;

            try
            {
                // 检查是否需要重新生成位图
                bool needsRegeneration = _cachedBitmap == null ||
                    _cachedData != spectrogramData ||
                    _cachedSampleRate != sampleRate ||
                    Math.Abs(_cachedDuration - duration) > 0.001 ||
                    Math.Abs(_cachedMaxFrequency - maxFrequency) > 0.001;

                if (needsRegeneration)
                {
                    GenerateSpectrogramBitmap(spectrogramData, sampleRate, duration, maxFrequency);
                }

                if (_cachedBitmap == null)
                    return;

                // 计算频谱图在钢琴卷帘中的绘制位置和大小
                // 时间轴映射：duration秒 -> bounds.Width像素（考虑缩放）
                double timeScale = bounds.Width / duration * zoom;
                
                // 频率轴映射：使用MIDI音高范围（21-108对应A0-C8）
                double minFreq = MidiNoteToFrequency(21);  // A0: 27.5 Hz
                double maxFreq = MidiNoteToFrequency(108); // C8: 4186 Hz
                
                // 计算可见区域
                double visibleStartTime = scrollOffset / timeScale;
                double visibleEndTime = visibleStartTime + (bounds.Width / timeScale);
                
                // 裁剪到数据范围
                visibleStartTime = Math.Max(0, Math.Min(visibleStartTime, duration));
                visibleEndTime = Math.Max(0, Math.Min(visibleEndTime, duration));
                
                // 计算源矩形（频谱图位图中的区域）
                int sourceStartX = (int)(visibleStartTime / duration * _cachedBitmap.PixelSize.Width);
                int sourceEndX = (int)(visibleEndTime / duration * _cachedBitmap.PixelSize.Width);
                int sourceWidth = Math.Max(1, sourceEndX - sourceStartX);
                
                // 确保源矩形在位图范围内
                sourceStartX = Math.Max(0, Math.Min(sourceStartX, _cachedBitmap.PixelSize.Width - 1));
                sourceEndX = Math.Max(sourceStartX + 1, Math.Min(sourceEndX, _cachedBitmap.PixelSize.Width));
                sourceWidth = sourceEndX - sourceStartX;
                
                var sourceRect = new Rect(
                    sourceStartX, 
                    0, 
                    sourceWidth, 
                    _cachedBitmap.PixelSize.Height);
                
                // 计算目标矩形（画布上的位置）
                double destX = (visibleStartTime * timeScale) - scrollOffset;
                double destWidth = (visibleEndTime - visibleStartTime) * timeScale;
                
                // 确保目标矩形在可见范围内
                destX = Math.Max(0, Math.Min(destX, bounds.Width));
                destWidth = Math.Max(0, Math.Min(destWidth, bounds.Width - destX));
                
                // 应用垂直缩放和偏移
                double destY = verticalScrollOffset;
                double destHeight = bounds.Height * verticalZoom;
                
                var destRect = new Rect(
                    destX,
                    destY,
                    destWidth,
                    destHeight);
                
                // 绘制频谱图（带透明度）
                using (context.PushOpacity(opacity))
                {
                    context.DrawImage(_cachedBitmap, sourceRect, destRect);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"频谱图渲染错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成频谱图位图
        /// </summary>
        private void GenerateSpectrogramBitmap(
            double[,] spectrogramData,
            int sampleRate,
            double duration,
            double maxFrequency)
        {
            if (spectrogramData == null || spectrogramData.Length == 0)
            {
                _cachedBitmap = null;
                return;
            }
            
            int timeFrames = spectrogramData.GetLength(0);
            int frequencyBins = spectrogramData.GetLength(1);

            // 创建位图（宽度=时间帧数，高度=频率bin数）
            var pixelSize = new PixelSize(timeFrames, frequencyBins);
            _cachedBitmap = new WriteableBitmap(pixelSize, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

            using (var buffer = _cachedBitmap.Lock())
            {
                unsafe
                {
                    var ptr = (uint*)buffer.Address.ToPointer();
                    int stride = buffer.RowBytes / 4;

                    // 找到数据的最大值和最小值用于归一化
                    double minValue = double.MaxValue;
                    double maxValue = double.MinValue;
                    
                    for (int t = 0; t < timeFrames; t++)
                    {
                        for (int f = 0; f < frequencyBins; f++)
                        {
                            double value = spectrogramData[t, f];
                            if (!double.IsNaN(value) && !double.IsInfinity(value))
                            {
                                minValue = Math.Min(minValue, value);
                                maxValue = Math.Max(maxValue, value);
                            }
                        }
                    }

                    double range = maxValue - minValue;
                    if (range < 1e-10) range = 1.0;

                    // 填充像素数据（使用热力图配色：蓝->青->绿->黄->红）
                    for (int t = 0; t < timeFrames; t++)
                    {
                        for (int f = 0; f < frequencyBins; f++)
                        {
                            // 频率bin索引从0（低频）到frequencyBins-1（高频）
                            // 但在显示时，我们需要翻转Y轴，使高频在上，低频在下
                            int y = frequencyBins - 1 - f;
                            
                            double value = spectrogramData[t, f];
                            double normalized = (value - minValue) / range;
                            
                            // 限制范围
                            normalized = Math.Max(0, Math.Min(1, normalized));
                            
                            // 应用对数缩放以增强可见性
                            normalized = Math.Log10(1 + normalized * 9) / Math.Log10(10);
                            
                            // 热力图配色
                            var color = GetHeatmapColor(normalized);
                            
                            // 写入像素（BGRA格式）
                            int index = y * stride + t;
                            ptr[index] = (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
                        }
                    }
                }
            }

            // 缓存参数
            _cachedData = spectrogramData;
            _cachedSampleRate = sampleRate;
            _cachedDuration = duration;
            _cachedMaxFrequency = maxFrequency;
        }

        /// <summary>
        /// 获取热力图颜色（蓝色→青色→绿色→黄色→红色）
        /// </summary>
        private Color GetHeatmapColor(double value)
        {
            // 确保值在0-1范围内
            value = Math.Max(0, Math.Min(1, value));
            
            byte alpha = 255;
            byte r, g, b;

            if (value < 0.25)
            {
                // 蓝色 -> 青色
                r = 0;
                g = (byte)(value * 4 * 255);
                b = 255;
            }
            else if (value < 0.5)
            {
                // 青色 -> 绿色
                r = 0;
                g = 255;
                b = (byte)(255 - (value - 0.25) * 4 * 255);
            }
            else if (value < 0.75)
            {
                // 绿色 -> 黄色
                r = (byte)((value - 0.5) * 4 * 255);
                g = 255;
                b = 0;
            }
            else
            {
                // 黄色 -> 红色
                r = 255;
                g = (byte)(255 - (value - 0.75) * 4 * 255);
                b = 0;
            }

            return Color.FromArgb(alpha, r, g, b);
        }

        /// <summary>
        /// MIDI音符号转换为频率（Hz）
        /// </summary>
        private double MidiNoteToFrequency(int midiNote)
        {
            // MIDI 69 = A4 = 440 Hz
            return 440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0);
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public void ClearCache()
        {
            _cachedBitmap?.Dispose();
            _cachedBitmap = null;
            _cachedData = null;
        }
    }
}