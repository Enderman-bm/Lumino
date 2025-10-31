using NAudio.Wave;
using EnderAudioAnalyzer.Models;
using EnderDebugger;
using System;
using System.IO;
using System.Threading.Tasks;

namespace EnderAudioAnalyzer.Services
{
    /// <summary>
    /// 使用NAudio库的MP3解码服务
    /// </summary>
    public class NAudioMp3DecoderService
    {
        private readonly EnderLogger _logger;

        public NAudioMp3DecoderService(EnderLogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 解析MP3文件获取基本信息
        /// </summary>
        public async Task<AudioFileInfo?> ParseMp3FileAsync(string filePath)
        {
            try
            {
                _logger.Debug("NAudioMp3DecoderService", $"使用NAudio解析MP3文件: {filePath}");
                
                using var mp3Reader = new Mp3FileReader(filePath);
                
                var info = new AudioFileInfo
                {
                    FilePath = filePath,
                    Format = "MP3",
                    SampleRate = mp3Reader.WaveFormat.SampleRate,
                    Channels = mp3Reader.WaveFormat.Channels,
                    BitDepth = mp3Reader.WaveFormat.BitsPerSample,
                    Duration = mp3Reader.TotalTime.TotalSeconds,
                    TotalSamples = (long)(mp3Reader.TotalTime.TotalSeconds * mp3Reader.WaveFormat.SampleRate * mp3Reader.WaveFormat.Channels),
                    IsSupported = true
                };

                // 获取文件大小
                if (File.Exists(filePath))
                {
                    info.FileSize = new FileInfo(filePath).Length;
                }

                _logger.Debug("NAudioMp3DecoderService", $"MP3文件解析完成: 采样率={info.SampleRate}, 声道数={info.Channels}, 时长={info.Duration}秒");
                return info;
            }
            catch (Exception ex)
            {
                _logger.Error("NAudioMp3DecoderService", $"使用NAudio解析MP3文件失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 读取MP3文件的音频样本数据
        /// </summary>
        public async Task<float[]> ReadMp3SamplesAsync(string filePath, int startSample = 0, int maxSamples = -1)
        {
            try
            {
                _logger.Debug("NAudioMp3DecoderService", $"使用NAudio读取MP3样本: {filePath}, 起始样本={startSample}, 最大样本数={maxSamples}");
                
                using var mp3Reader = new Mp3FileReader(filePath);
                var waveProvider = mp3Reader.ToSampleProvider();
                
                // 计算总样本数
                var totalSamples = (int)(mp3Reader.TotalTime.TotalSeconds * mp3Reader.WaveFormat.SampleRate * mp3Reader.WaveFormat.Channels);
                
                // 确定要读取的样本数
                var samplesToRead = maxSamples > 0 ? Math.Min(maxSamples, totalSamples - startSample) : totalSamples - startSample;
                if (samplesToRead <= 0)
                {
                    return Array.Empty<float>();
                }

                // 跳过起始样本
                if (startSample > 0)
                {
                    var bytesToSkip = startSample * sizeof(float);
                    mp3Reader.CurrentTime = TimeSpan.FromSeconds((double)startSample / (mp3Reader.WaveFormat.SampleRate * mp3Reader.WaveFormat.Channels));
                }

                // 读取样本数据
                var samples = new float[samplesToRead];
                var samplesRead = 0;
                
                while (samplesRead < samplesToRead)
                {
                    var read = waveProvider.Read(samples, samplesRead, samplesToRead - samplesRead);
                    if (read == 0) break;
                    samplesRead += read;
                }

                // 如果读取的样本数少于请求的样本数，调整数组大小
                if (samplesRead < samplesToRead)
                {
                    Array.Resize(ref samples, samplesRead);
                }

                _logger.Debug("NAudioMp3DecoderService", $"成功读取MP3样本: 实际读取={samplesRead}个样本");
                return samples;
            }
            catch (Exception ex)
            {
                _logger.Error("NAudioMp3DecoderService", $"使用NAudio读取MP3样本失败: {ex.Message}");
                return Array.Empty<float>();
            }
        }
    }
}