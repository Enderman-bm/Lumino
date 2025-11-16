using ImageToMidi.Config;
using System;
using System.IO;
using ImageMagick;

namespace ImageToMidi
{
    /// <summary>
    /// 图片转MIDI转换器主类
    /// </summary>
    public class ImageToMidiConverter
    {
        private readonly ConversionConfig _config;

        /// <summary>
        /// 初始化转换器
        /// </summary>
        /// <param name="config">转换配置</param>
        public ImageToMidiConverter(ConversionConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// 将图片转换为MIDI文件
        /// </summary>
        /// <param name="imagePath">输入图片路径</param>
        /// <param name="outputPath">输出MIDI文件路径</param>
        public void ConvertImageToMidi(string imagePath, string outputPath)
        {
            if (string.IsNullOrEmpty(imagePath))
                throw new ArgumentNullException(nameof(imagePath));
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentNullException(nameof(outputPath));
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"图片文件不存在: {imagePath}", imagePath);

            try
            {
                // 加载图片
                using var image = new MagickImage(imagePath);

                // 调整图片大小以提高处理性能
                if (_config.PixelSampleStep > 1)
                {
                    uint newWidth = (uint)(image.Width / _config.PixelSampleStep);
                    uint newHeight = (uint)(image.Height / _config.PixelSampleStep);
                    image.Resize(newWidth, newHeight);
                }

                // 创建MIDI数据
                var midiData = ProcessImageToMidi(image);

                // 保存MIDI文件
                SaveMidiFile(midiData, outputPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"图片转换MIDI失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 处理图片并生成MIDI数据
        /// </summary>
        private MidiData ProcessImageToMidi(MagickImage image)
        {
            var midiData = new MidiData();

            // 获取像素数据
            var pixels = image.GetPixels();
            int width = (int)image.Width;
            int height = (int)image.Height;

            // 遍历图片像素
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    try
                    {
                        var pixel = pixels.GetPixel(x, y);
                        var color = pixel.ToColor();

                        if (color == null) continue;

                        // 将MagickColor<float>转换为MagickColor<byte>
                        var byteColor = new MagickColor(
                            (byte)(color.R * 255),
                            (byte)(color.G * 255),
                            (byte)(color.B * 255)
                        );

                        // 将颜色转换为音符
                        var note = ConvertPixelToNote(byteColor, x, y, width, height);

                        if (note != null)
                        {
                            midiData.Notes.Add(note);
                        }
                    }
                    catch
                    {
                        // 跳过无法读取的像素
                        continue;
                    }
                }
            }

            // 应用量化
            if (_config.EnableQuantization)
            {
                ApplyQuantization(midiData);
            }

            // 排序音符
            midiData.Notes.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            return midiData;
        }

        /// <summary>
        /// 将像素颜色转换为音符
        /// </summary>
        private NoteData ConvertPixelToNote(IMagickColor<byte> color, int x, int y, int width, int height)
        {
            // 根据颜色映射模式提取亮度值
            double brightness = ExtractBrightness(color);

            // 边缘检测（可选）
            if (_config.EnableEdgeDetection && !IsEdgePixel(brightness))
            {
                return null;
            }

            // 计算音符属性
            int pitch = CalculatePitch(brightness, y, height);
            int velocity = CalculateVelocity(brightness);
            int startTime = CalculateStartTime(x, width);
            int duration = CalculateDuration(brightness);

            return new NoteData
            {
                Pitch = pitch,
                Velocity = velocity,
                StartTime = startTime,
                Duration = duration,
                Channel = _config.OutputChannel
            };
        }

        /// <summary>
        /// 提取亮度值
        /// </summary>
        private double ExtractBrightness(IMagickColor<byte> color)
        {
            return _config.ColorMappingMode switch
            {
                1 => color.R / 255.0, // 红色通道
                2 => color.G / 255.0, // 绿色通道
                3 => color.B / 255.0, // 蓝色通道
                _ => (color.R + color.G + color.B) / 3.0 / 255.0 // 平均灰度
            };
        }

        /// <summary>
        /// 计算音高
        /// </summary>
        private int CalculatePitch(double brightness, int y, int height)
        {
            // 根据亮度计算音高偏移
            double brightnessFactor = brightness;

            // 根据垂直位置计算音高（可选）
            double verticalFactor = 1.0 - (double)y / height;

            // 合并因素
            double combinedFactor = _config.InvertPitchMapping
                ? 1.0 - (brightnessFactor * 0.7 + verticalFactor * 0.3)
                : brightnessFactor * 0.7 + verticalFactor * 0.3;

            // 计算最终音高
            int pitchOffset = (int)(combinedFactor * _config.PitchRange);
            return Math.Clamp(_config.BasePitch + pitchOffset, 0, 127);
        }

        /// <summary>
        /// 计算速度
        /// </summary>
        private int CalculateVelocity(double brightness)
        {
            if (!_config.EnableVelocityMapping)
                return _config.MaxVelocity;

            double factor = _config.InvertVelocityMapping
                ? 1.0 - brightness
                : brightness;

            int velocity = (int)(_config.MinVelocity + factor * (_config.MaxVelocity - _config.MinVelocity));
            return Math.Clamp(velocity, 0, 127);
        }

        /// <summary>
        /// 计算开始时间
        /// </summary>
        private int CalculateStartTime(int x, int width)
        {
            // 将水平位置映射到时间（毫秒）
            double factor = (double)x / width;
            return (int)(factor * 1000); // 1秒时间轴
        }

        /// <summary>
        /// 计算持续时间
        /// </summary>
        private int CalculateDuration(double brightness)
        {
            // 亮度越高，音符越短
            double factor = 1.0 - brightness;
            return (int)(_config.MinNoteDuration + factor * (_config.MaxNoteDuration - _config.MinNoteDuration));
        }

        /// <summary>
        /// 边缘检测
        /// </summary>
        private bool IsEdgePixel(double brightness)
        {
            // 简单的阈值检测
            return brightness < _config.EdgeDetectionSensitivity;
        }

        /// <summary>
        /// 应用量化
        /// </summary>
        private void ApplyQuantization(MidiData midiData)
        {
            int quantizationTicks = 60000 / (_config.QuantizationPrecision * 120); // 假设120BPM

            foreach (var note in midiData.Notes)
            {
                // 量化开始时间
                note.StartTime = (note.StartTime / quantizationTicks) * quantizationTicks;

                // 量化持续时间
                note.Duration = (note.Duration / quantizationTicks) * quantizationTicks;
                if (note.Duration < _config.MinNoteDuration)
                    note.Duration = _config.MinNoteDuration;
            }
        }

        /// <summary>
        /// 保存MIDI文件
        /// </summary>
        private void SaveMidiFile(MidiData midiData, string outputPath)
        {
            // 使用简化的MIDI文件格式
            // 实际项目中应该使用专业的MIDI库

            var midiBuilder = new System.Text.StringBuilder();

            // MIDI文件头和轨道
            // 这里使用简化的表示方式

            // 实际实现应该写入二进制MIDI格式
            // 参考：https://www.music.mcgill.ca/~ich/classes/mumt306/StandardMIDIfileformat.html

            // 由于复杂性，这里使用简化的实现
            // 实际项目中建议使用：DryWetMIDI 或 NAudio.Midi

            // 保存音符信息到文本格式（简化版本）
            using var writer = new StreamWriter(outputPath);
            writer.WriteLine("MIDI Data Generated from Image");
            writer.WriteLine($"Note Count: {midiData.Notes.Count}");
            writer.WriteLine();

            foreach (var note in midiData.Notes)
            {
                writer.WriteLine($"Note: Pitch={note.Pitch}, Velocity={note.Velocity}, Start={note.StartTime}ms, Duration={note.Duration}ms, Channel={note.Channel}");
            }
        }
    }

    /// <summary>
    /// MIDI数据容器
    /// </summary>
    internal class MidiData
    {
        public System.Collections.Generic.List<NoteData> Notes { get; set; } = new();
    }

    /// <summary>
    /// 音符数据
    /// </summary>
    internal class NoteData
    {
        public int Pitch { get; set; }
        public int Velocity { get; set; }
        public int StartTime { get; set; }
        public int Duration { get; set; }
        public int Channel { get; set; }
    }
}
