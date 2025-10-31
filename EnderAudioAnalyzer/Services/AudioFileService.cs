using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EnderAudioAnalyzer.Interfaces;
using EnderAudioAnalyzer.Models;
using EnderDebugger;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace EnderAudioAnalyzer.Services
{
    /// <summary>
    /// 音频文件服务 - 负责音频文件的读取和预处理
    /// </summary>
    public class AudioFileService : IAudioFileService
    {
        private readonly EnderLogger _logger;
        private readonly NAudioMp3DecoderService _mp3DecoderService;

        public AudioFileService()
        {
            _logger = new EnderLogger("AudioFileService");
            _mp3DecoderService = new NAudioMp3DecoderService(_logger);
        }

        /// <summary>
        /// 加载音频文件信息
        /// </summary>
        public async Task<AudioFileInfo> LoadAudioFileAsync(string filePath)
        {
            try
            {
                _logger.Debug("AudioFileService", $"开始加载音频文件: {filePath}");

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"音频文件不存在: {filePath}");
                }

                var fileInfo = new FileInfo(filePath);
                var audioInfo = new AudioFileInfo
                {
                    FilePath = filePath,
                    FileSize = fileInfo.Length,
                    Format = Path.GetExtension(filePath).ToLowerInvariant()
                };

                // 根据文件扩展名选择解析方法
                switch (audioInfo.Format)
                {
                    case ".wav":
                        await ParseWavFileAsync(audioInfo);
                        break;
                    case ".flac":
                        await ParseFlacFileAsync(audioInfo);
                        break;
                    case ".mp3":
                        await ParseMp3FileAsync(audioInfo);
                        break;
                    case ".aac":
                    case ".m4a":
                        await ParseAacFileAsync(audioInfo);
                        break;
                    case ".ogg":
                        await ParseOggFileAsync(audioInfo);
                        break;
                    case ".opus":
                        await ParseOpusFileAsync(audioInfo);
                        break;
                    case ".alac":
                        await ParseAlacFileAsync(audioInfo);
                        break;
                    default:
                        audioInfo.IsSupported = false;
                        _logger.Warn("AudioFileService", $"不支持的音频格式: {audioInfo.Format}");
                        break;
                }

                _logger.Info("AudioFileService", $"音频文件加载完成: {audioInfo.Format}, 时长: {audioInfo.Duration:F2}s, 采样率: {audioInfo.SampleRate}Hz");
                return audioInfo;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"加载音频文件失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 预处理音频文件
        /// </summary>
        public async Task<float[]> PreprocessAudioAsync(string filePath, int targetSampleRate)
        {
            try
            {
                _logger.Debug("AudioFileService", $"预处理音频文件: {filePath}, 目标采样率: {targetSampleRate}");

                // 加载音频文件信息
                var audioInfo = await LoadAudioFileAsync(filePath);
                
                // 读取音频样本数据
                var samples = await ReadAudioSamplesAsync(filePath, 0, -1);
                
                // 如果需要重采样
                if (audioInfo.SampleRate != targetSampleRate)
                {
                    samples = await ResampleAudioAsync(samples, audioInfo.SampleRate, targetSampleRate);
                }
                
                // 归一化音频数据
                samples = NormalizeAudio(samples);
                
                _logger.Info("AudioFileService", $"音频预处理完成: {filePath}, 样本数: {samples.Length}");
                return samples;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"预处理音频文件失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 重采样音频数据
        /// </summary>
        private async Task<float[]> ResampleAudioAsync(float[] samples, int sourceSampleRate, int targetSampleRate)
        {
            try
            {
                if (sourceSampleRate == targetSampleRate)
                {
                    return samples;
                }

                double ratio = (double)targetSampleRate / sourceSampleRate;
                int newLength = (int)(samples.Length * ratio);
                var resampledSamples = new float[newLength];

                // 简单的线性插值重采样
                for (int i = 0; i < newLength; i++)
                {
                    double sourceIndex = i / ratio;
                    int index1 = (int)Math.Floor(sourceIndex);
                    int index2 = Math.Min(index1 + 1, samples.Length - 1);
                    double fraction = sourceIndex - index1;

                    resampledSamples[i] = (float)((1 - fraction) * samples[index1] + fraction * samples[index2]);
                }

                _logger.Debug("AudioFileService", $"音频重采样完成: {sourceSampleRate}Hz -> {targetSampleRate}Hz, 样本数: {samples.Length} -> {newLength}");
                return resampledSamples;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"音频重采样失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 归一化音频数据
        /// </summary>
        private float[] NormalizeAudio(float[] samples)
        {
            try
            {
                if (samples == null || samples.Length == 0)
                {
                    return samples;
                }

                // 找到最大绝对值
                float maxValue = 0;
                foreach (var sample in samples)
                {
                    float absValue = Math.Abs(sample);
                    if (absValue > maxValue)
                    {
                        maxValue = absValue;
                    }
                }

                // 如果最大值为0，直接返回
                if (maxValue == 0)
                {
                    return samples;
                }

                // 归一化到[-1, 1]范围
                var normalizedSamples = new float[samples.Length];
                for (int i = 0; i < samples.Length; i++)
                {
                    normalizedSamples[i] = samples[i] / maxValue;
                }

                _logger.Debug("AudioFileService", $"音频归一化完成, 最大值: {maxValue}");
                return normalizedSamples;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"音频归一化失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 解析WAV文件
        /// </summary>
        private async Task ParseWavFileAsync(AudioFileInfo audioInfo)
        {
            try
            {
                using var fileStream = new FileStream(audioInfo.FilePath, FileMode.Open, FileAccess.Read);
                using var binaryReader = new BinaryReader(fileStream);

                // 读取RIFF头
                string riffHeader = new string(binaryReader.ReadChars(4));
                if (riffHeader != "RIFF")
                {
                    throw new InvalidDataException("无效的WAV文件: RIFF头缺失");
                }

                // 文件大小（不包括RIFF头和大小字段）
                uint fileSize = binaryReader.ReadUInt32();

                // 读取WAVE标识
                string waveHeader = new string(binaryReader.ReadChars(4));
                if (waveHeader != "WAVE")
                {
                    throw new InvalidDataException("无效的WAV文件: WAVE标识缺失");
                }

                // 查找fmt块
                while (fileStream.Position < fileStream.Length)
                {
                    string chunkId = new string(binaryReader.ReadChars(4));
                    uint chunkSize = binaryReader.ReadUInt32();

                    if (chunkId == "fmt ")
                    {
                        // 读取fmt块数据
                        ushort audioFormat = binaryReader.ReadUInt16();
                        ushort numChannels = binaryReader.ReadUInt16();
                        uint sampleRate = binaryReader.ReadUInt32();
                        uint byteRate = binaryReader.ReadUInt32();
                        ushort blockAlign = binaryReader.ReadUInt16();
                        ushort bitsPerSample = binaryReader.ReadUInt16();

                        audioInfo.SampleRate = (int)sampleRate;
                        audioInfo.Channels = numChannels;
                        audioInfo.BitDepth = bitsPerSample;
                        audioInfo.IsSupported = true;

                        // 计算音频时长
                        await CalculateAudioDurationAsync(audioInfo, fileStream);
                        break;
                    }
                    else
                    {
                        // 跳过未知块
                        fileStream.Seek(chunkSize, SeekOrigin.Current);
                    }
                }

                if (!audioInfo.IsSupported)
                {
                    throw new InvalidDataException("未找到有效的fmt块");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"解析WAV文件失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 计算音频时长
        /// </summary>
        private async Task CalculateAudioDurationAsync(AudioFileInfo audioInfo, FileStream fileStream)
        {
            try
            {
                using var binaryReader = new BinaryReader(fileStream);
                long startPosition = fileStream.Position;

                // 查找data块
                while (fileStream.Position < fileStream.Length)
                {
                    string chunkId = new string(binaryReader.ReadChars(4));
                    uint chunkSize = binaryReader.ReadUInt32();

                    if (chunkId == "data")
                    {
                        // 计算音频数据大小
                        long dataSize = chunkSize;
                        audioInfo.TotalSamples = dataSize / (audioInfo.Channels * (audioInfo.BitDepth / 8));
                        audioInfo.Duration = (double)audioInfo.TotalSamples / audioInfo.SampleRate;
                        break;
                    }
                    else
                    {
                        // 跳过未知块
                        fileStream.Seek(chunkSize, SeekOrigin.Current);
                    }
                }

                // 重置流位置
                fileStream.Position = startPosition;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"计算音频时长失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 解析FLAC文件
        /// </summary>
        private async Task ParseFlacFileAsync(AudioFileInfo audioInfo)
        {
            try
            {
                _logger.Debug("AudioFileService", $"开始解析FLAC文件: {audioInfo.FilePath}");
                
                using var audioFile = new MediaFoundationReader(audioInfo.FilePath);
                
                audioInfo.SampleRate = audioFile.WaveFormat.SampleRate;
                audioInfo.Channels = audioFile.WaveFormat.Channels;
                audioInfo.BitDepth = audioFile.WaveFormat.BitsPerSample;
                audioInfo.Duration = (double)audioFile.TotalTime.TotalSeconds;
                audioInfo.IsSupported = true;
                
                _logger.Info("AudioFileService", 
                    $"FLAC解析成功: 采样率={audioInfo.SampleRate}Hz, " +
                    $"通道={audioInfo.Channels}, 位深={audioInfo.BitDepth}, " +
                    $"时长={audioInfo.Duration:F2}s");
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"解析FLAC文件失败: {ex.Message}");
                audioInfo.IsSupported = false;
            }
        }

        /// <summary>
        /// 解析MP3文件
        /// </summary>
        private async Task ParseMp3FileAsync(AudioFileInfo audioInfo)
        {
            try
            {
                _logger.Debug("AudioFileService", $"开始使用NAudio解析MP3文件: {audioInfo.FilePath}");
                
                var mp3Info = await _mp3DecoderService.ParseMp3FileAsync(audioInfo.FilePath);
                
                if (mp3Info == null)
                {
                    _logger.Error("AudioFileService", "NAudio MP3解码器返回空数据");
                    audioInfo.IsSupported = false;
                    return;
                }

                audioInfo.SampleRate = mp3Info.SampleRate;
                audioInfo.Channels = mp3Info.Channels;
                audioInfo.BitDepth = mp3Info.BitDepth;
                audioInfo.Duration = mp3Info.Duration;
                audioInfo.IsSupported = true;
                
                _logger.Info("AudioFileService", 
                    $"MP3解析成功: 采样率={mp3Info.SampleRate}Hz, " +
                    $"通道={mp3Info.Channels}, 位深={mp3Info.BitDepth}, " +
                    $"时长={audioInfo.Duration:F2}s");
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"解析MP3文件失败: {ex.Message}");
                audioInfo.IsSupported = false;
            }
        }

        /// <summary>
        /// 解析AAC文件
        /// </summary>
        private async Task ParseAacFileAsync(AudioFileInfo audioInfo)
        {
            try
            {
                _logger.Debug("AudioFileService", $"开始解析AAC文件: {audioInfo.FilePath}");
                
                using var audioFile = new MediaFoundationReader(audioInfo.FilePath);
                
                audioInfo.SampleRate = audioFile.WaveFormat.SampleRate;
                audioInfo.Channels = audioFile.WaveFormat.Channels;
                audioInfo.BitDepth = audioFile.WaveFormat.BitsPerSample;
                audioInfo.Duration = (double)audioFile.TotalTime.TotalSeconds;
                audioInfo.IsSupported = true;
                
                _logger.Info("AudioFileService", 
                    $"AAC解析成功: 采样率={audioInfo.SampleRate}Hz, " +
                    $"通道={audioInfo.Channels}, 位深={audioInfo.BitDepth}, " +
                    $"时长={audioInfo.Duration:F2}s");
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"解析AAC文件失败: {ex.Message}");
                audioInfo.IsSupported = false;
            }
        }

        /// <summary>
        /// 解析OGG Vorbis文件
        /// </summary>
        private async Task ParseOggFileAsync(AudioFileInfo audioInfo)
        {
            try
            {
                _logger.Debug("AudioFileService", $"开始解析OGG Vorbis文件: {audioInfo.FilePath}");
                
                using var audioFile = new MediaFoundationReader(audioInfo.FilePath);
                
                audioInfo.SampleRate = audioFile.WaveFormat.SampleRate;
                audioInfo.Channels = audioFile.WaveFormat.Channels;
                audioInfo.BitDepth = audioFile.WaveFormat.BitsPerSample;
                audioInfo.Duration = (double)audioFile.TotalTime.TotalSeconds;
                audioInfo.IsSupported = true;
                
                _logger.Info("AudioFileService", 
                    $"OGG Vorbis解析成功: 采样率={audioInfo.SampleRate}Hz, " +
                    $"通道={audioInfo.Channels}, 位深={audioInfo.BitDepth}, " +
                    $"时长={audioInfo.Duration:F2}s");
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"解析OGG文件失败: {ex.Message}");
                audioInfo.IsSupported = false;
            }
        }

        /// <summary>
        /// 解析OPUS文件
        /// </summary>
        private async Task ParseOpusFileAsync(AudioFileInfo audioInfo)
        {
            try
            {
                _logger.Debug("AudioFileService", $"开始解析OPUS文件: {audioInfo.FilePath}");
                
                using var audioFile = new MediaFoundationReader(audioInfo.FilePath);
                
                audioInfo.SampleRate = audioFile.WaveFormat.SampleRate;
                audioInfo.Channels = audioFile.WaveFormat.Channels;
                audioInfo.BitDepth = audioFile.WaveFormat.BitsPerSample;
                audioInfo.Duration = (double)audioFile.TotalTime.TotalSeconds;
                audioInfo.IsSupported = true;
                
                _logger.Info("AudioFileService", 
                    $"OPUS解析成功: 采样率={audioInfo.SampleRate}Hz, " +
                    $"通道={audioInfo.Channels}, 位深={audioInfo.BitDepth}, " +
                    $"时长={audioInfo.Duration:F2}s");
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"解析OPUS文件失败: {ex.Message}");
                audioInfo.IsSupported = false;
            }
        }

        /// <summary>
        /// 解析ALAC文件
        /// </summary>
        private async Task ParseAlacFileAsync(AudioFileInfo audioInfo)
        {
            try
            {
                _logger.Debug("AudioFileService", $"开始解析ALAC文件: {audioInfo.FilePath}");
                
                using var audioFile = new MediaFoundationReader(audioInfo.FilePath);
                
                audioInfo.SampleRate = audioFile.WaveFormat.SampleRate;
                audioInfo.Channels = audioFile.WaveFormat.Channels;
                audioInfo.BitDepth = audioFile.WaveFormat.BitsPerSample;
                audioInfo.Duration = (double)audioFile.TotalTime.TotalSeconds;
                audioInfo.IsSupported = true;
                
                _logger.Info("AudioFileService", 
                    $"ALAC解析成功: 采样率={audioInfo.SampleRate}Hz, " +
                    $"通道={audioInfo.Channels}, 位深={audioInfo.BitDepth}, " +
                    $"时长={audioInfo.Duration:F2}s");
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"解析ALAC文件失败: {ex.Message}");
                audioInfo.IsSupported = false;
            }
        }

        /// <summary>
        /// 检查文件格式是否支持
        /// </summary>
        public Task<bool> IsSupportedFormatAsync(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var supportedExtensions = new[] { ".wav", ".mp3", ".flac", ".aac", ".m4a", ".ogg", ".opus", ".alac" };
            var supported = Array.Exists(supportedExtensions, ext => ext == extension);
            return Task.FromResult(supported);
        }

        /// <summary>
        /// 获取支持的音频格式
        /// </summary>
        public List<string> GetSupportedFormats()
        {
            return new List<string> { ".wav", ".mp3", ".flac", ".aac", ".m4a", ".ogg", ".opus", ".alac" };
        }

        /// <summary>
        /// 读取音频样本数据
        /// </summary>
        public async Task<float[]> ReadAudioSamplesAsync(string filePath, int startSample = 0, int sampleCount = -1)
        {
            try
            {
                var audioInfo = await LoadAudioFileAsync(filePath);
                if (!audioInfo.IsSupported)
                {
                    throw new NotSupportedException($"不支持的音频格式: {audioInfo.Format}");
                }

                switch (audioInfo.Format)
                {
                    case ".wav":
                        return await ReadWavSamplesAsync(audioInfo, startSample, sampleCount);
                    case ".flac":
                        return await ReadFlacSamplesAsync(audioInfo, startSample, sampleCount);
                    case ".mp3":
                        return await ReadMp3SamplesAsync(audioInfo, startSample, sampleCount);
                    case ".aac":
                    case ".m4a":
                        return await ReadAacSamplesAsync(audioInfo, startSample, sampleCount);
                    case ".ogg":
                        return await ReadOggSamplesAsync(audioInfo, startSample, sampleCount);
                    case ".opus":
                        return await ReadOpusSamplesAsync(audioInfo, startSample, sampleCount);
                    case ".alac":
                        return await ReadAlacSamplesAsync(audioInfo, startSample, sampleCount);
                    default:
                        throw new NotSupportedException($"不支持的音频格式: {audioInfo.Format}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"读取音频样本失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 读取WAV文件样本数据
        /// </summary>
        private Task<float[]> ReadWavSamplesAsync(AudioFileInfo audioInfo, int startSample, int sampleCount)
        {
            using var fileStream = new FileStream(audioInfo.FilePath, FileMode.Open, FileAccess.Read);
            using var binaryReader = new BinaryReader(fileStream);

            // 查找data块
            while (fileStream.Position < fileStream.Length)
            {
                string chunkId = new string(binaryReader.ReadChars(4));
                uint chunkSize = binaryReader.ReadUInt32();

                if (chunkId == "data")
                {
                    // 计算实际要读取的样本数
                    int totalSamples = (int)(chunkSize / (audioInfo.Channels * (audioInfo.BitDepth / 8)));
                    if (sampleCount == -1)
                    {
                        sampleCount = totalSamples - startSample;
                    }

                    // 限制读取范围
                    sampleCount = Math.Min(sampleCount, totalSamples - startSample);
                    if (sampleCount <= 0)
                    {
                        return Task.FromResult(Array.Empty<float>());
                    }

                    // 跳过开始样本
                    int bytesPerSample = audioInfo.BitDepth / 8;
                    int skipBytes = startSample * audioInfo.Channels * bytesPerSample;
                    fileStream.Seek(skipBytes, SeekOrigin.Current);

                    // 读取样本数据
                    var samples = new float[sampleCount * audioInfo.Channels];
                    var buffer = new byte[sampleCount * audioInfo.Channels * bytesPerSample];
                    fileStream.Read(buffer, 0, buffer.Length);

                    // 转换为浮点数
                    for (int i = 0; i < samples.Length; i++)
                    {
                        if (audioInfo.BitDepth == 16)
                        {
                            short sample = BitConverter.ToInt16(buffer, i * 2);
                            samples[i] = sample / 32768f;
                        }
                        else if (audioInfo.BitDepth == 24)
                        {
                            int sample = (buffer[i * 3] << 8) | (buffer[i * 3 + 1] << 16) | (buffer[i * 3 + 2] << 24);
                            sample >>= 8; // 转换为有符号24位
                            samples[i] = sample / 8388608f;
                        }
                        else if (audioInfo.BitDepth == 32)
                        {
                            int sample = BitConverter.ToInt32(buffer, i * 4);
                            samples[i] = sample / 2147483648f;
                        }
                    }

                    return Task.FromResult(samples);
                }
                else
                {
                    // 跳过未知块
                    fileStream.Seek(chunkSize, SeekOrigin.Current);
                }
            }

            return Task.FromResult(Array.Empty<float>());
        }

        /// <summary>
        /// 读取FLAC文件样本数据
        /// </summary>
        private Task<float[]> ReadFlacSamplesAsync(AudioFileInfo audioInfo, int startSample, int sampleCount)
        {
            try
            {
                using var audioFile = new MediaFoundationReader(audioInfo.FilePath);
                var sampleProvider = audioFile.ToSampleProvider();
                
                // 计算总样本数
                int totalSamples = (int)(audioFile.TotalTime.TotalSeconds * audioFile.WaveFormat.SampleRate * audioFile.WaveFormat.Channels);
                
                if (sampleCount == -1)
                {
                    sampleCount = totalSamples - startSample;
                }
                
                // 限制读取范围
                sampleCount = Math.Min(sampleCount, totalSamples - startSample);
                if (sampleCount <= 0)
                {
                    return Task.FromResult(Array.Empty<float>());
                }
                
                // 跳过开始样本
                if (startSample > 0)
                {
                    var skipTime = TimeSpan.FromSeconds((double)startSample / audioFile.WaveFormat.SampleRate);
                    sampleProvider.Skip(skipTime);
                }
                
                // 读取样本数据
                var samples = new float[sampleCount];
                int read = sampleProvider.Read(samples, 0, sampleCount);
                
                // 如果读取的样本数少于请求的样本数，调整数组大小
                if (read < sampleCount)
                {
                    Array.Resize(ref samples, read);
                }
                
                return Task.FromResult(samples);
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"读取FLAC样本失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 读取MP3文件样本数据
        /// </summary>
        private Task<float[]> ReadMp3SamplesAsync(AudioFileInfo audioInfo, int startSample, int sampleCount)
        {
            try
            {
                return _mp3DecoderService.ReadMp3SamplesAsync(audioInfo.FilePath, startSample, sampleCount);
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"读取MP3样本失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 读取AAC文件样本数据
        /// </summary>
        private Task<float[]> ReadAacSamplesAsync(AudioFileInfo audioInfo, int startSample, int sampleCount)
        {
            try
            {
                using var audioFile = new MediaFoundationReader(audioInfo.FilePath);
                var sampleProvider = audioFile.ToSampleProvider();
                
                // 计算总样本数
                int totalSamples = (int)(audioFile.TotalTime.TotalSeconds * audioFile.WaveFormat.SampleRate * audioFile.WaveFormat.Channels);
                
                if (sampleCount == -1)
                {
                    sampleCount = totalSamples - startSample;
                }
                
                // 限制读取范围
                sampleCount = Math.Min(sampleCount, totalSamples - startSample);
                if (sampleCount <= 0)
                {
                    return Task.FromResult(Array.Empty<float>());
                }
                
                // 跳过开始样本
                if (startSample > 0)
                {
                    var skipTime = TimeSpan.FromSeconds((double)startSample / audioFile.WaveFormat.SampleRate);
                    sampleProvider.Skip(skipTime);
                }
                
                // 读取样本数据
                var samples = new float[sampleCount];
                int read = sampleProvider.Read(samples, 0, sampleCount);
                
                // 如果读取的样本数少于请求的样本数，调整数组大小
                if (read < sampleCount)
                {
                    Array.Resize(ref samples, read);
                }
                
                return Task.FromResult(samples);
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"读取AAC样本失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 读取OGG文件样本数据
        /// </summary>
        private Task<float[]> ReadOggSamplesAsync(AudioFileInfo audioInfo, int startSample, int sampleCount)
        {
            try
            {
                using var audioFile = new MediaFoundationReader(audioInfo.FilePath);
                var sampleProvider = audioFile.ToSampleProvider();
                
                // 计算总样本数
                int totalSamples = (int)(audioFile.TotalTime.TotalSeconds * audioFile.WaveFormat.SampleRate * audioFile.WaveFormat.Channels);
                
                if (sampleCount == -1)
                {
                    sampleCount = totalSamples - startSample;
                }
                
                // 限制读取范围
                sampleCount = Math.Min(sampleCount, totalSamples - startSample);
                if (sampleCount <= 0)
                {
                    return Task.FromResult(Array.Empty<float>());
                }
                
                // 跳过开始样本
                if (startSample > 0)
                {
                    var skipTime = TimeSpan.FromSeconds((double)startSample / audioFile.WaveFormat.SampleRate);
                    sampleProvider.Skip(skipTime);
                }
                
                // 读取样本数据
                var samples = new float[sampleCount];
                int read = sampleProvider.Read(samples, 0, sampleCount);
                
                // 如果读取的样本数少于请求的样本数，调整数组大小
                if (read < sampleCount)
                {
                    Array.Resize(ref samples, read);
                }
                
                return Task.FromResult(samples);
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"读取OGG样本失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 读取OPUS文件样本数据
        /// </summary>
        private Task<float[]> ReadOpusSamplesAsync(AudioFileInfo audioInfo, int startSample, int sampleCount)
        {
            try
            {
                using var audioFile = new MediaFoundationReader(audioInfo.FilePath);
                var sampleProvider = audioFile.ToSampleProvider();
                
                // 计算总样本数
                int totalSamples = (int)(audioFile.TotalTime.TotalSeconds * audioFile.WaveFormat.SampleRate * audioFile.WaveFormat.Channels);
                
                if (sampleCount == -1)
                {
                    sampleCount = totalSamples - startSample;
                }
                
                // 限制读取范围
                sampleCount = Math.Min(sampleCount, totalSamples - startSample);
                if (sampleCount <= 0)
                {
                    return Task.FromResult(Array.Empty<float>());
                }
                
                // 跳过开始样本
                if (startSample > 0)
                {
                    var skipTime = TimeSpan.FromSeconds((double)startSample / audioFile.WaveFormat.SampleRate);
                    sampleProvider.Skip(skipTime);
                }
                
                // 读取样本数据
                var samples = new float[sampleCount];
                int read = sampleProvider.Read(samples, 0, sampleCount);
                
                // 如果读取的样本数少于请求的样本数，调整数组大小
                if (read < sampleCount)
                {
                    Array.Resize(ref samples, read);
                }
                
                return Task.FromResult(samples);
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"读取OPUS样本失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 读取ALAC文件样本数据
        /// </summary>
        private Task<float[]> ReadAlacSamplesAsync(AudioFileInfo audioInfo, int startSample, int sampleCount)
        {
            try
            {
                using var audioFile = new MediaFoundationReader(audioInfo.FilePath);
                var sampleProvider = audioFile.ToSampleProvider();
                
                // 计算总样本数
                int totalSamples = (int)(audioFile.TotalTime.TotalSeconds * audioFile.WaveFormat.SampleRate * audioFile.WaveFormat.Channels);
                
                if (sampleCount == -1)
                {
                    sampleCount = totalSamples - startSample;
                }
                
                // 限制读取范围
                sampleCount = Math.Min(sampleCount, totalSamples - startSample);
                if (sampleCount <= 0)
                {
                    return Task.FromResult(Array.Empty<float>());
                }
                
                // 跳过开始样本
                if (startSample > 0)
                {
                    var skipTime = TimeSpan.FromSeconds((double)startSample / audioFile.WaveFormat.SampleRate);
                    sampleProvider.Skip(skipTime);
                }
                
                // 读取样本数据
                var samples = new float[sampleCount];
                int read = sampleProvider.Read(samples, 0, sampleCount);
                
                // 如果读取的样本数少于请求的样本数，调整数组大小
                if (read < sampleCount)
                {
                    Array.Resize(ref samples, read);
                }
                
                return Task.FromResult(samples);
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"读取ALAC样本失败: {ex.Message}");
                throw;
            }
        }
    }
}