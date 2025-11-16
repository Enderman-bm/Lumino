using ImageToMidi.Config;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using ImageMagick;
using Melanchall.DryWetMidi;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Interaction;
using Note = Melanchall.DryWetMidi.MusicTheory.Note;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Common;

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
            // 使用DryWetMidi创建标准的MIDI文件格式

            // 创建MIDI文件
            var midiFile = new MidiFile();

            // 创建轨道列表
            var tracks = new List<TrackChunk>();

            // 创建主轨道（包含全局信息）
            var tempoTrack = new TrackChunk();

            // 设置节拍（假设120 BPM）
            var tempo = new Tempo(120);
            tempoTrack.Events.Add(new SetTempoEvent(tempo.MicrosecondsPerQuarterNote));

            // 添加主轨道
            tracks.Add(tempoTrack);

            // 为每个通道创建轨道
            var channelGroups = midiData.Notes.GroupBy(n => n.Channel);

            foreach (var channelGroup in channelGroups)
            {
                var track = new TrackChunk();
                var channel = channelGroup.Key;

                // 按时间排序音符
                var sortedNotes = channelGroup.OrderBy(n => n.StartTime).ToList();

                int lastTime = 0;

                foreach (var note in sortedNotes)
                {
                    // 计算增量时间（转换为MIDI ticks）
                    var deltaTime = note.StartTime - lastTime;
                    var deltaTicks = (int)ConvertTimeToTicks(deltaTime);

                    // 创建音符开事件
                    var noteOn = new NoteOnEvent(
                        (SevenBitNumber)note.Pitch,
                        (SevenBitNumber)note.Velocity
                    )
                    {
                        Channel = (FourBitNumber)channel,
                        DeltaTime = deltaTicks
                    };
                    track.Events.Add(noteOn);

                    // 创建音符关事件
                    var noteDurationTicks = (int)ConvertTimeToTicks(note.Duration);
                    var noteOff = new NoteOffEvent(
                        (SevenBitNumber)note.Pitch,
                        (SevenBitNumber)0
                    )
                    {
                        Channel = (FourBitNumber)channel,
                        DeltaTime = noteDurationTicks
                    };
                    track.Events.Add(noteOff);

                    lastTime = (int)(note.StartTime + note.Duration);
                }

                tracks.Add(track);
            }

            // 将所有轨道添加到MIDI文件
            foreach (var track in tracks)
            {
                midiFile.Chunks.Add(track);
            }

            // 保存MIDI文件
            midiFile.Write(outputPath, true);
        }

        /// <summary>
        /// 将时间（毫秒）转换为MIDI ticks
        /// </summary>
        private int ConvertTimeToTicks(int milliseconds)
        {
            // 假设120 BPM，480 PPQ（每四分音符的ticks数）
            const int ppq = 480;
            const int bpm = 120;

            // 计算每毫秒的ticks数
            double millisecondsPerQuarter = 60000.0 / bpm; // 120 BPM = 500ms per quarter note
            double ticksPerMillisecond = (double)ppq / millisecondsPerQuarter;

            return (int)(milliseconds * ticksPerMillisecond);
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
