using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EnderAudioAnalyzer.Interfaces;
using EnderAudioAnalyzer.Models;
using EnderAudioAnalyzer.Decoders;
using EnderDebugger;

namespace EnderAudioAnalyzer.Services
{
    /// <summary>
    /// 音频文件服务 - 负责音频文件的读取和预处理
    /// </summary>
    public class AudioFileService : IAudioFileService
    {
        private readonly EnderLogger _logger;

        public AudioFileService()
        {
            _logger = new EnderLogger("AudioFileService");
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
                
                var decoder = new FlacDecoder();
                var flacData = decoder.Decode(audioInfo.FilePath);

                if (flacData == null)
                {
                    _logger.Error("AudioFileService", "FLAC解码器返回空数据");
                    audioInfo.IsSupported = false;
                    return;
                }

                if (flacData.SampleRate <= 0 || flacData.Channels <= 0)
                {
                    _logger.Error("AudioFileService", $"FLAC文件参数无效: 采样率={flacData.SampleRate}, 通道数={flacData.Channels}");
                    audioInfo.IsSupported = false;
                    return;
                }

                audioInfo.SampleRate = flacData.SampleRate;
                audioInfo.Channels = flacData.Channels;
                audioInfo.BitDepth = flacData.BitsPerSample;
                audioInfo.TotalSamples = flacData.TotalSamples;
                audioInfo.Duration = (double)flacData.TotalSamples / flacData.SampleRate;
                audioInfo.IsSupported = true;

                _logger.Info("AudioFileService",
                    $"FLAC解析成功: 采样率={flacData.SampleRate}Hz, " +
                    $"通道={flacData.Channels}, 位深={flacData.BitsPerSample}, " +
                    $"总样本={flacData.TotalSamples}, 时长={audioInfo.Duration:F2}s");

                await Task.CompletedTask; // 保持异步签名
            }
            catch (InvalidDataException ex)
            {
                _logger.Error("AudioFileService", $"FLAC文件格式无效: {ex.Message}");
                audioInfo.IsSupported = false;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"解析FLAC文件失败: {ex.Message}\n堆栈跟踪: {ex.StackTrace}");
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
                using var fileStream = new FileStream(audioInfo.FilePath, FileMode.Open, FileAccess.Read);
                var decoder = new Mp3Decoder();
                var mp3Info = await decoder.ParseAsync(fileStream);

                audioInfo.SampleRate = mp3Info.SampleRate;
                audioInfo.Channels = mp3Info.Channels;
                audioInfo.BitDepth = mp3Info.BitsPerSample;
                audioInfo.Duration = mp3Info.Duration.TotalSeconds;
                audioInfo.IsSupported = true;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"解析MP3文件失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 解析AAC文件
        /// </summary>
        private async Task ParseAacFileAsync(AudioFileInfo audioInfo)
        {
            try
            {
                using var fileStream = new FileStream(audioInfo.FilePath, FileMode.Open, FileAccess.Read);
                var decoder = new AacDecoder();
                var aacInfo = await decoder.ParseAsync(fileStream);

                audioInfo.SampleRate = aacInfo.SampleRate;
                audioInfo.Channels = aacInfo.Channels;
                audioInfo.BitDepth = aacInfo.BitsPerSample;
                audioInfo.Duration = aacInfo.Duration.TotalSeconds;
                audioInfo.IsSupported = true;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"解析AAC文件失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 解析OGG Vorbis文件
        /// </summary>
        private async Task ParseOggFileAsync(AudioFileInfo audioInfo)
        {
            try
            {
                using var fileStream = new FileStream(audioInfo.FilePath, FileMode.Open, FileAccess.Read);
                var decoder = new VorbisDecoder();
                var vorbisInfo = await decoder.ParseAsync(fileStream);

                audioInfo.SampleRate = vorbisInfo.SampleRate;
                audioInfo.Channels = vorbisInfo.Channels;
                audioInfo.BitDepth = vorbisInfo.BitsPerSample;
                audioInfo.Duration = vorbisInfo.Duration.TotalSeconds;
                audioInfo.IsSupported = true;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"解析OGG文件失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 解析OPUS文件
        /// </summary>
        private async Task ParseOpusFileAsync(AudioFileInfo audioInfo)
        {
            try
            {
                using var fileStream = new FileStream(audioInfo.FilePath, FileMode.Open, FileAccess.Read);
                var decoder = new OpusDecoder();
                var opusInfo = await decoder.ParseAsync(fileStream);

                audioInfo.SampleRate = opusInfo.SampleRate;
                audioInfo.Channels = opusInfo.Channels;
                audioInfo.BitDepth = opusInfo.BitsPerSample;
                audioInfo.Duration = opusInfo.Duration.TotalSeconds;
                audioInfo.IsSupported = true;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"解析OPUS文件失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 解析ALAC文件
        /// </summary>
        private async Task ParseAlacFileAsync(AudioFileInfo audioInfo)
        {
            try
            {
                using var fileStream = new FileStream(audioInfo.FilePath, FileMode.Open, FileAccess.Read);
                var decoder = new AlacDecoder();
                var alacInfo = await decoder.ParseAsync(fileStream);

                audioInfo.SampleRate = alacInfo.SampleRate;
                audioInfo.Channels = alacInfo.Channels;
                audioInfo.BitDepth = alacInfo.BitDepth;
                audioInfo.Duration = alacInfo.Duration.TotalSeconds;
                audioInfo.IsSupported = true;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"解析ALAC文件失败: {ex.Message}");
                throw;
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
        private async Task<float[]> ReadWavSamplesAsync(AudioFileInfo audioInfo, int startSample, int sampleCount)
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

                    if (startSample + sampleCount > totalSamples)
                    {
                        throw new ArgumentOutOfRangeException("请求的样本范围超出文件范围");
                    }

                    // 定位到起始样本
                    long dataStartPosition = fileStream.Position;
                    long sampleByteSize = audioInfo.Channels * (audioInfo.BitDepth / 8);
                    fileStream.Position = dataStartPosition + (startSample * sampleByteSize);

                    // 读取样本数据
                    var samples = new float[sampleCount * audioInfo.Channels];
                    int bytesPerSample = audioInfo.BitDepth / 8;

                    for (int i = 0; i < sampleCount * audioInfo.Channels; i++)
                    {
                        float sampleValue = 0.0f;

                        switch (audioInfo.BitDepth)
                        {
                            case 8:
                                // 8位无符号
                                byte byteSample = binaryReader.ReadByte();
                                sampleValue = (byteSample - 128) / 128.0f;
                                break;
                            case 16:
                                // 16位有符号
                                short shortSample = binaryReader.ReadInt16();
                                sampleValue = shortSample / 32768.0f;
                                break;
                            case 24:
                                // 24位有符号（读取为32位）
                                byte[] bytes24 = binaryReader.ReadBytes(3);
                                int intSample24 = (bytes24[0] << 8) | (bytes24[1] << 16) | (bytes24[2] << 24);
                                sampleValue = intSample24 / 8388608.0f;
                                break;
                            case 32:
                                // 32位有符号
                                int intSample32 = binaryReader.ReadInt32();
                                sampleValue = intSample32 / 2147483648.0f;
                                break;
                            default:
                                throw new NotSupportedException($"不支持的位深度: {audioInfo.BitDepth}");
                        }

                        samples[i] = sampleValue;
                    }

                    return samples;
                }
                else
                {
                    // 跳过未知块
                    fileStream.Seek(chunkSize, SeekOrigin.Current);
                }
            }

            throw new InvalidDataException("未找到data块");
        }

        /// <summary>
        /// 读取FLAC文件样本数据
        /// </summary>
        private async Task<float[]> ReadFlacSamplesAsync(AudioFileInfo audioInfo, int startSample, int sampleCount)
        {
            try
            {
                var decoder = new FlacDecoder();
                var flacData = decoder.Decode(audioInfo.FilePath);

                // 如果请求所有样本
                if (sampleCount == -1)
                {
                    sampleCount = (int)(flacData.TotalSamples - startSample);
                }

                // 验证范围
                if (startSample + sampleCount > flacData.TotalSamples)
                {
                    throw new ArgumentOutOfRangeException("请求的样本范围超出文件范围");
                }

                // 提取请求的样本范围
                int startIndex = startSample * audioInfo.Channels;
                int length = sampleCount * audioInfo.Channels;
                float[] samples = new float[length];
                Array.Copy(flacData.Samples, startIndex, samples, 0, length);

                return await Task.FromResult(samples);
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
        private async Task<float[]> ReadMp3SamplesAsync(AudioFileInfo audioInfo, int startSample, int sampleCount)
        {
            try
            {
                using var fileStream = new FileStream(audioInfo.FilePath, FileMode.Open, FileAccess.Read);
                var decoder = new Mp3Decoder();
                var samples = await decoder.DecodeAsync(fileStream, sampleCount > 0 ? sampleCount * audioInfo.Channels : -1);
                
                // 跳过起始样本
                if (startSample > 0)
                {
                    int skipCount = startSample * audioInfo.Channels;
                    samples = samples.Skip(skipCount).ToArray();
                }
                
                return samples;
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
        private async Task<float[]> ReadAacSamplesAsync(AudioFileInfo audioInfo, int startSample, int sampleCount)
        {
            try
            {
                using var fileStream = new FileStream(audioInfo.FilePath, FileMode.Open, FileAccess.Read);
                var decoder = new AacDecoder();
                var samples = await decoder.DecodeAsync(fileStream, sampleCount > 0 ? sampleCount * audioInfo.Channels : -1);
                
                if (startSample > 0)
                {
                    int skipCount = startSample * audioInfo.Channels;
                    samples = samples.Skip(skipCount).ToArray();
                }
                
                return samples;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"读取AAC样本失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 读取OGG Vorbis文件样本数据
        /// </summary>
        private async Task<float[]> ReadOggSamplesAsync(AudioFileInfo audioInfo, int startSample, int sampleCount)
        {
            try
            {
                using var fileStream = new FileStream(audioInfo.FilePath, FileMode.Open, FileAccess.Read);
                var decoder = new VorbisDecoder();
                var samples = await decoder.DecodeAsync(fileStream, sampleCount > 0 ? sampleCount * audioInfo.Channels : -1);
                
                if (startSample > 0)
                {
                    int skipCount = startSample * audioInfo.Channels;
                    samples = samples.Skip(skipCount).ToArray();
                }
                
                return samples;
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
        private async Task<float[]> ReadOpusSamplesAsync(AudioFileInfo audioInfo, int startSample, int sampleCount)
        {
            try
            {
                using var fileStream = new FileStream(audioInfo.FilePath, FileMode.Open, FileAccess.Read);
                var decoder = new OpusDecoder();
                var samples = await decoder.DecodeAsync(fileStream, sampleCount > 0 ? sampleCount * audioInfo.Channels : -1);
                
                if (startSample > 0)
                {
                    int skipCount = startSample * audioInfo.Channels;
                    samples = samples.Skip(skipCount).ToArray();
                }
                
                return samples;
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
        private async Task<float[]> ReadAlacSamplesAsync(AudioFileInfo audioInfo, int startSample, int sampleCount)
        {
            try
            {
                using var fileStream = new FileStream(audioInfo.FilePath, FileMode.Open, FileAccess.Read);
                var decoder = new AlacDecoder();
                var samples = await decoder.DecodeAsync(fileStream, sampleCount > 0 ? sampleCount * audioInfo.Channels : -1);
                
                if (startSample > 0)
                {
                    int skipCount = startSample * audioInfo.Channels;
                    samples = samples.Skip(skipCount).ToArray();
                }
                
                return samples;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"读取ALAC样本失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 预处理音频数据（归一化、重采样等）
        /// </summary>
        public async Task<float[]> PreprocessAudioAsync(string filePath, int targetSampleRate = 44100)
        {
            try
            {
                _logger.Debug("AudioFileService", $"开始预处理音频: {filePath}");

                // 读取原始样本
                var originalSamples = await ReadAudioSamplesAsync(filePath);
                var audioInfo = await LoadAudioFileAsync(filePath);

                // 检查读取的样本数据
                if (originalSamples == null || originalSamples.Length == 0)
                {
                    _logger.Error("AudioFileService", "读取的音频样本为空");
                    throw new InvalidDataException("音频样本为空");
                }

                // 详细检查样本数据
                bool allZero = true;
                float maxSample = 0;
                float minSample = 0;
                int nonZeroCount = 0;
                
                // 检查前100个样本
                for (int i = 0; i < Math.Min(100, originalSamples.Length); i++)
                {
                    if (Math.Abs(originalSamples[i]) > 1e-10f)
                    {
                        allZero = false;
                        nonZeroCount++;
                    }
                    maxSample = Math.Max(maxSample, originalSamples[i]);
                    minSample = Math.Min(minSample, originalSamples[i]);
                }

                // 详细日志输出
                if (allZero)
                {
                    _logger.Warn("AudioFileService", $"警告：原始音频样本全为零，总样本数: {originalSamples.Length}");
                    _logger.Debug("AudioFileService", $"前10个样本值: [{originalSamples[0]:F6}, {originalSamples[1]:F6}, {originalSamples[2]:F6}, {originalSamples[3]:F6}, {originalSamples[4]:F6}, {originalSamples[5]:F6}, {originalSamples[6]:F6}, {originalSamples[7]:F6}, {originalSamples[8]:F6}, {originalSamples[9]:F6}]");
                }
                else
                {
                    _logger.Debug("AudioFileService", $"原始音频样本范围: [{minSample:F6}, {maxSample:F6}], 长度: {originalSamples.Length}, 非零样本数: {nonZeroCount}/100");
                    _logger.Debug("AudioFileService", $"前10个样本值: [{originalSamples[0]:F6}, {originalSamples[1]:F6}, {originalSamples[2]:F6}, {originalSamples[3]:F6}, {originalSamples[4]:F6}, {originalSamples[5]:F6}, {originalSamples[6]:F6}, {originalSamples[7]:F6}, {originalSamples[8]:F6}, {originalSamples[9]:F6}]");
                }

                // 如果已经是目标采样率，直接返回
                if (audioInfo.SampleRate == targetSampleRate)
                {
                    _logger.Debug("AudioFileService", "采样率相同，无需重采样");
                    return originalSamples;
                }

                _logger.Debug("AudioFileService", $"开始重采样: {audioInfo.SampleRate}Hz -> {targetSampleRate}Hz");
                
                // 重采样到目标采样率
                var resampledSamples = ResampleAudio(originalSamples, audioInfo.SampleRate, targetSampleRate, audioInfo.Channels);

                // 归一化处理
                var normalizedSamples = NormalizeAudio(resampledSamples);

                // 检查归一化后的样本
                float normalizedMax = normalizedSamples.Max();
                float normalizedMin = normalizedSamples.Min();
                _logger.Debug("AudioFileService", $"归一化后样本范围: [{normalizedMin:F6}, {normalizedMax:F6}]");
                _logger.Debug("AudioFileService", $"归一化后前10个样本值: [{normalizedSamples[0]:F6}, {normalizedSamples[1]:F6}, {normalizedSamples[2]:F6}, {normalizedSamples[3]:F6}, {normalizedSamples[4]:F6}, {normalizedSamples[5]:F6}, {normalizedSamples[6]:F6}, {normalizedSamples[7]:F6}, {normalizedSamples[8]:F6}, {normalizedSamples[9]:F6}]");

                _logger.Info("AudioFileService", $"音频预处理完成: 原始采样率 {audioInfo.SampleRate}Hz -> 目标采样率 {targetSampleRate}Hz, 样本数: {normalizedSamples.Length}");
                return normalizedSamples;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioFileService", $"音频预处理失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 音频重采样
        /// </summary>
        private float[] ResampleAudio(float[] originalSamples, int originalSampleRate, int targetSampleRate, int channels)
        {
            if (originalSampleRate == targetSampleRate)
                return originalSamples;

            double ratio = (double)targetSampleRate / originalSampleRate;
            int newLength = (int)(originalSamples.Length * ratio);
            var resampled = new float[newLength];

            // 简单的线性插值重采样
            for (int i = 0; i < newLength; i++)
            {
                double originalIndex = i / ratio;
                int index1 = (int)Math.Floor(originalIndex);
                int index2 = Math.Min(index1 + 1, originalSamples.Length - 1);

                if (index1 == index2)
                {
                    resampled[i] = originalSamples[index1];
                }
                else
                {
                    double fraction = originalIndex - index1;
                    resampled[i] = (float)(originalSamples[index1] * (1 - fraction) + originalSamples[index2] * fraction);
                }
            }

            return resampled;
        }

        /// <summary>
        /// 音频归一化
        /// </summary>
        private float[] NormalizeAudio(float[] samples)
        {
            if (samples.Length == 0)
                return samples;

            // 找到最大绝对值
            float maxAbs = 0.0f;
            foreach (float sample in samples)
            {
                float absSample = Math.Abs(sample);
                if (absSample > maxAbs)
                    maxAbs = absSample;
            }

            // 如果最大绝对值接近0，直接返回
            if (maxAbs < 0.0001f)
                return samples;

            // 归一化到[-1, 1]范围
            float scale = 1.0f / maxAbs;
            var normalized = new float[samples.Length];

            for (int i = 0; i < samples.Length; i++)
            {
                normalized[i] = samples[i] * scale;
            }

            return normalized;
        }
    }

    /// <summary>
    /// 音频文件服务接口
    /// </summary>
    public interface IAudioFileService
    {
        /// <summary>
        /// 加载音频文件信息
        /// </summary>
        Task<AudioFileInfo> LoadAudioFileAsync(string filePath);

        /// <summary>
        /// 检查文件格式是否支持
        /// </summary>
        Task<bool> IsSupportedFormatAsync(string filePath);

        /// <summary>
        /// 获取支持的音频格式
        /// </summary>
        List<string> GetSupportedFormats();

        /// <summary>
        /// 读取音频样本数据
        /// </summary>
        Task<float[]> ReadAudioSamplesAsync(string filePath, int startSample = 0, int sampleCount = -1);

        /// <summary>
        /// 预处理音频数据
        /// </summary>
        Task<float[]> PreprocessAudioAsync(string filePath, int targetSampleRate = 44100);
    }
}