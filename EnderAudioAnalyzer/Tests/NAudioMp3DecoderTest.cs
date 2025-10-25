using System;
using System.IO;
using System.Threading.Tasks;
using EnderAudioAnalyzer.Models;
using EnderAudioAnalyzer.Services;
using EnderDebugger;

namespace EnderAudioAnalyzer.Tests
{
    /// <summary>
    /// NAudio MP3解码器测试类
    /// </summary>
    public class NAudioMp3DecoderTest
    {
        private readonly NAudioMp3DecoderService _mp3DecoderService;
        private readonly EnderLogger _logger;

        public NAudioMp3DecoderTest()
        {
            _logger = new EnderLogger("NAudioMp3DecoderTest");
            _mp3DecoderService = new NAudioMp3DecoderService(_logger);
        }

        /// <summary>
        /// 测试MP3文件解析功能
        /// </summary>
        public async Task<bool> TestMp3Parsing(string mp3FilePath)
        {
            try
            {
                _logger.Info("NAudioMp3DecoderTest", $"开始测试MP3文件解析: {mp3FilePath}");

                if (!File.Exists(mp3FilePath))
                {
                    _logger.Error("NAudioMp3DecoderTest", $"测试文件不存在: {mp3FilePath}");
                    return false;
                }

                var audioInfo = await _mp3DecoderService.ParseMp3FileAsync(mp3FilePath);
                if (audioInfo == null)
                {
                    _logger.Error("NAudioMp3DecoderTest", "MP3文件解析失败");
                    return false;
                }

                _logger.Info("NAudioMp3DecoderTest", $"MP3文件解析成功:");
                _logger.Info("NAudioMp3DecoderTest", $"  文件路径: {audioInfo.FilePath}");
                _logger.Info("NAudioMp3DecoderTest", $"  文件格式: {audioInfo.Format}");
                _logger.Info("NAudioMp3DecoderTest", $"  文件大小: {audioInfo.FileSize} 字节");
                _logger.Info("NAudioMp3DecoderTest", $"  采样率: {audioInfo.SampleRate} Hz");
                _logger.Info("NAudioMp3DecoderTest", $"  位深度: {audioInfo.BitDepth} 位");
                _logger.Info("NAudioMp3DecoderTest", $"  声道数: {audioInfo.Channels}");
                _logger.Info("NAudioMp3DecoderTest", $"  时长: {audioInfo.Duration} 秒");
                _logger.Info("NAudioMp3DecoderTest", $"  总样本数: {audioInfo.TotalSamples}");
                _logger.Info("NAudioMp3DecoderTest", $"  是否支持: {audioInfo.IsSupported}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("NAudioMp3DecoderTest", $"MP3文件解析测试失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 测试MP3样本读取功能
        /// </summary>
        public async Task<bool> TestMp3SampleReading(string mp3FilePath, int maxSamples = 1024)
        {
            try
            {
                _logger.Info("NAudioMp3DecoderTest", $"开始测试MP3样本读取: {mp3FilePath}");

                if (!File.Exists(mp3FilePath))
                {
                    _logger.Error("NAudioMp3DecoderTest", $"测试文件不存在: {mp3FilePath}");
                    return false;
                }

                var samples = await _mp3DecoderService.ReadMp3SamplesAsync(mp3FilePath, 0, maxSamples);
                if (samples == null || samples.Length == 0)
                {
                    _logger.Error("NAudioMp3DecoderTest", "MP3样本读取失败");
                    return false;
                }

                _logger.Info("NAudioMp3DecoderTest", $"MP3样本读取成功:");
                _logger.Info("NAudioMp3DecoderTest", $"  请求样本数: {maxSamples}");
                _logger.Info("NAudioMp3DecoderTest", $"  实际读取样本数: {samples.Length}");
                
                // 显示前10个样本值
                _logger.Info("NAudioMp3DecoderTest", "  前10个样本值:");
                for (int i = 0; i < Math.Min(10, samples.Length); i++)
                {
                    _logger.Info("NAudioMp3DecoderTest", $"    样本[{i}]: {samples[i]}");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("NAudioMp3DecoderTest", $"MP3样本读取测试失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 运行完整测试
        /// </summary>
        public async Task<bool> RunFullTest(string mp3FilePath)
        {
            _logger.Info("NAudioMp3DecoderTest", "开始运行完整MP3解码器测试");
            
            var parseResult = await TestMp3Parsing(mp3FilePath);
            var sampleReadResult = await TestMp3SampleReading(mp3FilePath);
            
            var success = parseResult && sampleReadResult;
            
            if (success)
            {
                _logger.Info("NAudioMp3DecoderTest", "所有测试通过");
            }
            else
            {
                _logger.Error("NAudioMp3DecoderTest", "部分测试失败");
            }
            
            return success;
        }
    }
}