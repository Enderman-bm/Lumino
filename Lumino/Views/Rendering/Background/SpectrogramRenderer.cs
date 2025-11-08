using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lumino.Views.Rendering.Adapters;
using EnderDebugger;

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
            {
                EnderLogger.Instance.Warn("SpectrogramRenderer", "频谱渲染失败: 数据为空或无效");
                return;
            }

            try
            {
                // 检查是否需要重新生成位图（数据或参数变化时）
                if (!IsCacheValid(spectrogramData, sampleRate, duration, maxFrequency))
                {
                    GenerateSpectrogramBitmap(spectrogramData, sampleRate, duration, maxFrequency);
                }

                if (_cachedBitmap == null)
                {
                    EnderLogger.Instance.Warn("SpectrogramRenderer", "频谱渲染失败: 无法生成位图");
                    return;
                }

                // 计算频谱图在钢琴卷帘中的绘制位置和大小
                // 时间轴映射：duration秒 -> bounds.Width像素（考虑缩放）
                double timeScale = bounds.Width / duration * zoom;
                
                // 计算可见区域
                double visibleStartTime = scrollOffset / timeScale;
                double visibleEndTime = visibleStartTime + (bounds.Width / timeScale);
                
                // 裁剪到数据范围
                visibleStartTime = Math.Max(0, Math.Min(visibleStartTime, duration));
                visibleEndTime = Math.Max(0, Math.Min(visibleEndTime, duration));
                
                // 计算源矩形（频谱图位图中的区域）
                int bitmapWidth = _cachedBitmap.PixelSize.Width;
                int bitmapHeight = _cachedBitmap.PixelSize.Height;
                
                int sourceStartX = (int)(visibleStartTime / duration * bitmapWidth);
                int sourceEndX = (int)(visibleEndTime / duration * bitmapWidth);
                int sourceWidth = Math.Max(1, sourceEndX - sourceStartX);
                
                // 确保源矩形在位图范围内
                sourceStartX = Math.Max(0, Math.Min(sourceStartX, bitmapWidth - 1));
                sourceEndX = Math.Max(sourceStartX + 1, Math.Min(sourceEndX, bitmapWidth));
                sourceWidth = sourceEndX - sourceStartX;
                
                var sourceRect = new Rect(
                    sourceStartX, 
                    0, 
                    sourceWidth, 
                    bitmapHeight);
                
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
                
                // 添加调试信息
                EnderLogger.Instance.Debug("SpectrogramRenderer", $"频谱渲染: 源矩形({sourceRect.X},{sourceRect.Y},{sourceRect.Width},{sourceRect.Height}) -> 目标矩形({destRect.X},{destRect.Y},{destRect.Width},{destRect.Height})");
                EnderLogger.Instance.Debug("SpectrogramRenderer", $"频谱数据: {spectrogramData.GetLength(0)}帧 × {spectrogramData.GetLength(1)}频率bin, 位图大小: {bitmapWidth}×{bitmapHeight}, 透明度: {opacity}");
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "SpectrogramRenderer", "频谱图渲染错误");
            }
        }

        /// <summary>
        /// 检查缓存是否有效
        /// </summary>
        private bool IsCacheValid(double[,] spectrogramData, int sampleRate, double duration, double maxFrequency)
        {
            if (_cachedBitmap == null || _cachedData == null)
                return false;
            
            // 检查维度是否相同
            if (_cachedData.GetLength(0) != spectrogramData.GetLength(0) || 
                _cachedData.GetLength(1) != spectrogramData.GetLength(1))
                return false;
            
            // 检查参数是否相同
            if (_cachedSampleRate != sampleRate || 
                Math.Abs(_cachedDuration - duration) > 1e-6 || 
                Math.Abs(_cachedMaxFrequency - maxFrequency) > 1e-6)
                return false;
            
            // 简单检查数据是否完全相同（可选，可根据性能需求调整）
            for (int i = 0; i < Math.Min(100, spectrogramData.GetLength(0)); i++)
            {
                for (int j = 0; j < Math.Min(10, spectrogramData.GetLength(1)); j++)
                {
                    if (Math.Abs(_cachedData[i, j] - spectrogramData[i, j]) > 1e-6)
                        return false;
                }
            }
            
            return true;
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
                EnderLogger.Instance.Warn("SpectrogramRenderer", "频谱图生成失败：数据为空");
                return;
            }
            
            int timeFrames = spectrogramData.GetLength(0);
            int frequencyBins = spectrogramData.GetLength(1);

            EnderLogger.Instance.Info("SpectrogramRenderer", $"开始生成频谱位图：{timeFrames}x{frequencyBins}，采样率：{sampleRate}，最大频率：{maxFrequency}");
            
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
                    int validCount = 0;
                    double sumValue = 0;
                    
                    for (int t = 0; t < timeFrames; t++)
                    {
                        for (int f = 0; f < frequencyBins; f++)
                        {
                            double value = spectrogramData[t, f];
                            if (!double.IsNaN(value) && !double.IsInfinity(value))
                            {
                                minValue = Math.Min(minValue, value);
                                maxValue = Math.Max(maxValue, value);
                                sumValue += value;
                                validCount++;
                            }
                        }
                    }

                    EnderLogger.Instance.Debug("SpectrogramRenderer", $"频谱数据统计：最小值={minValue}，最大值={maxValue}，有效值数量={validCount}");

                    // 填充像素数据（使用改进的热力图配色）
                    bool hasNonZero = false;
                    for (int t = 0; t < timeFrames; t++)
                    {
                        for (int f = 0; f < frequencyBins; f++)
                        {
                            // 频率bin索引从0（低频）到frequencyBins-1（高频）
                            // 在显示时，我们需要翻转Y轴，使高频在上，低频在下
                            int y = frequencyBins - 1 - f;
                            
                            double value = spectrogramData[t, f];
                            
                            // 检查数据有效性
                            if (double.IsNaN(value) || double.IsInfinity(value))
                            {
                                // 无效数据显示为黑色
                                int pixelIndex = y * stride + t;
                                ptr[pixelIndex] = 0; // 透明黑色
                                continue;
                            }
                            
                            // 检查是否有非零值
                            if (value != 0 && !hasNonZero)
                                hasNonZero = true;
                            
                            // 计算95%百分位数进行归一化，保留动态范围
                            double percentile95 = maxValue;
                            if (validCount > 0)
                            {
                                double valueRange = maxValue - minValue;
                                if (valueRange > 0)
                                {
                                    percentile95 = minValue + valueRange * 0.95;
                                }
                            }
                            
                            // 使用95%百分位数进行归一化
                            double normalized = Math.Max(0, Math.Min(1, (value - minValue) / (percentile95 - minValue)));
                            
                            // 改进的对数缩放，使用更平滑的曲线
                            normalized = Math.Log10(1 + normalized * 999) / 3;
                            
                            // 热力图配色
                            var color = GetHeatmapColor(normalized);
                            
                            // 写入像素（BGRA格式）
                            int index = y * stride + t;
                            ptr[index] = (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
                        }
                    }
                    
                    EnderLogger.Instance.Info("SpectrogramRenderer", $"频谱位图生成完成，有非零值：{hasNonZero}，位图尺寸：{_cachedBitmap.PixelSize.Width}x{_cachedBitmap.PixelSize.Height}");
                }
            }
            
            // 缓存参数
            _cachedData = spectrogramData;
            _cachedSampleRate = sampleRate;
            _cachedDuration = duration;
            _cachedMaxFrequency = maxFrequency;
        }

        /// <summary>
        /// 获取热力图颜色（改进的颜色映射，确保更好的对比度和细节可见性）
        /// </summary>
        private Color GetHeatmapColor(double value)
        {
            // 确保值在0-1范围内
            value = Math.Max(0, Math.Min(1, value));
            
            byte alpha = 255;
            byte r, g, b;

            // 改进的颜色映射，使用更平滑的过渡和更好的对比度
            if (value < 0.15)
            {
                // 接近0的值显示为深蓝色（几乎黑色）
                double intensity = value / 0.15;
                r = (byte)(intensity * 50);
                g = (byte)(intensity * 50);
                b = (byte)(100 + intensity * 155);
            }
            else if (value < 0.35)
            {
                // 蓝色 -> 青色
                double t = (value - 0.15) / 0.2;
                r = 0;
                g = (byte)(t * 255);
                b = 255;
            }
            else if (value < 0.55)
            {
                // 青色 -> 绿色
                double t = (value - 0.35) / 0.2;
                r = 0;
                g = 255;
                b = (byte)(255 - t * 255);
            }
            else if (value < 0.75)
            {
                // 绿色 -> 黄色
                double t = (value - 0.55) / 0.2;
                r = (byte)(t * 255);
                g = 255;
                b = 0;
            }
            else
            {
                // 黄色 -> 红色（增强对比度）
                double t = (value - 0.75) / 0.25;
                r = 255;
                g = (byte)(255 - t * 255);
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