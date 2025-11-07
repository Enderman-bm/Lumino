using System;
using System.IO;
using System.Threading.Tasks;
using EnderAudioAnalyzer.Models;
using EnderAudioAnalyzer.Services;
using EnderDebugger;

namespace EnderAudioAnalyzer.Tests
{
    /// <summary>
    /// NAudio音频解码器测试类（支持WAV文件）
    /// </summary>
    public class NAudioAudioDecoderTest
    {
        private readonly EnderLogger _logger;

        public NAudioAudioDecoderTest()
        {
            _logger = new EnderLogger("NAudioAudioDecoderTest");
        }

        /// <summary>
        /// 测试WAV文件解析功能
        /// </summary>
        public async Task<bool> TestWavParsing(string wavFilePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _logger.Info("NAudioAudioDecoderTest", $"开始测试WAV文件解析: {wavFilePath}");

                    if (!File.Exists(wavFilePath))
                    {
                        _logger.Error("NAudioAudioDecoderTest", $"测试文件不存在: {wavFilePath}");
                        return false;
                    }

                    // 使用NAudio读取WAV文件信息
                    using (var audioFile = new NAudio.Wave.AudioFileReader(wavFilePath))
                    {
                        var audioInfo = new AudioFileInfo
                        {
                            FilePath = wavFilePath,
                            Format = "WAV",
                            FileSize = new FileInfo(wavFilePath).Length,
                            SampleRate = audioFile.WaveFormat.SampleRate,
                            BitDepth = audioFile.WaveFormat.BitsPerSample,
                            Channels = audioFile.WaveFormat.Channels,
                            Duration = audioFile.TotalTime.TotalSeconds,
                            TotalSamples = (long)(audioFile.TotalTime.TotalSeconds * audioFile.WaveFormat.SampleRate * audioFile.WaveFormat.Channels),
                            IsSupported = true
                        };

                        _logger.Info("NAudioAudioDecoderTest", $"WAV文件解析成功:");
                        _logger.Info("NAudioAudioDecoderTest", $"  文件路径: {audioInfo.FilePath}");
                        _logger.Info("NAudioAudioDecoderTest", $"  文件格式: {audioInfo.Format}");
                        _logger.Info("NAudioAudioDecoderTest", $"  文件大小: {audioInfo.FileSize} 字节");
                        _logger.Info("NAudioAudioDecoderTest", $"  采样率: {audioInfo.SampleRate} Hz");
                        _logger.Info("NAudioAudioDecoderTest", $"  位深度: {audioInfo.BitDepth} 位");
                        _logger.Info("NAudioAudioDecoderTest", $"  声道数: {audioInfo.Channels}");
                        _logger.Info("NAudioAudioDecoderTest", $"  时长: {audioInfo.Duration} 秒");
                        _logger.Info("NAudioAudioDecoderTest", $"  总样本数: {audioInfo.TotalSamples}");
                        _logger.Info("NAudioAudioDecoderTest", $"  是否支持: {audioInfo.IsSupported}");

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("NAudioAudioDecoderTest", $"WAV文件解析测试失败: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// 测试WAV样本读取功能
        /// </summary>
        public async Task<bool> TestWavSampleReading(string wavFilePath, int maxSamples = 1024)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _logger.Info("NAudioAudioDecoderTest", $"开始测试WAV样本读取: {wavFilePath}");

                    if (!File.Exists(wavFilePath))
                    {
                        _logger.Error("NAudioAudioDecoderTest", $"测试文件不存在: {wavFilePath}");
                        return false;
                    }

                    // 使用NAudio读取WAV文件样本
                    using (var audioFile = new NAudio.Wave.AudioFileReader(wavFilePath))
                    {
                        var buffer = new float[maxSamples];
                        var samplesRead = audioFile.Read(buffer, 0, maxSamples);

                        // 调整数组大小为实际读取的样本数
                        var samples = new float[samplesRead];
                        Array.Copy(buffer, samples, samplesRead);

                        _logger.Info("NAudioAudioDecoderTest", $"WAV样本读取成功:");
                        _logger.Info("NAudioAudioDecoderTest", $"  请求样本数: {maxSamples}");
                        _logger.Info("NAudioAudioDecoderTest", $"  实际读取样本数: {samples.Length}");
                        
                        // 显示前10个样本值
                        _logger.Info("NAudioAudioDecoderTest", "  前10个样本值:");
                        for (int i = 0; i < Math.Min(10, samples.Length); i++)
                        {
                            _logger.Info("NAudioAudioDecoderTest", $"    样本[{i}]: {samples[i]}");
                        }

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("NAudioAudioDecoderTest", $"WAV样本读取测试失败: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// 运行完整测试
        /// </summary>
        public async Task<bool> RunFullTest(string audioFilePath)
        {
            _logger.Info("NAudioAudioDecoderTest", "开始运行完整音频解码器测试");
            
            var extension = Path.GetExtension(audioFilePath).ToLowerInvariant();
            bool success = false;
            
            if (extension == ".wav")
            {
                var parseResult = await TestWavParsing(audioFilePath);
                var sampleReadResult = await TestWavSampleReading(audioFilePath);
                success = parseResult && sampleReadResult;
            }
            else if (extension == ".mp3")
            {
                // 如果是MP3文件，使用原有的MP3测试
                var mp3Test = new NAudioMp3DecoderTest();
                success = await mp3Test.RunFullTest(audioFilePath);
            }
            else
            {
                _logger.Error("NAudioAudioDecoderTest", $"不支持的音频格式: {extension}");
                return false;
            }
            
            if (success)
            {
                _logger.Info("NAudioAudioDecoderTest", "所有测试通过");
            }
            else
            {
                _logger.Error("NAudioAudioDecoderTest", "部分测试失败");
            }
            
            return success;
        }
    }
}