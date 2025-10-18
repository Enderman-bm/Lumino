using System.Threading.Tasks;
using EnderAudioAnalyzer.Models;

namespace EnderAudioAnalyzer.Interfaces
{
    /// <summary>
    /// 频谱分析服务接口
    /// </summary>
    public interface ISpectrumAnalysisService
    {
        /// <summary>
        /// 执行频谱分析
        /// </summary>
        Task<SpectrumData> AnalyzeSpectrumAsync(float[] audioSamples, int sampleRate, SpectrumAnalysisOptions options);

        /// <summary>
        /// 生成频谱图（STFT - 短时傅里叶变换）
        /// </summary>
        /// <param name="audioSamples">音频样本</param>
        /// <param name="sampleRate">采样率</param>
        /// <param name="options">频谱图选项</param>
        /// <returns>2D数组 [时间帧, 频率bin]，值为dB幅度</returns>
        Task<double[,]> GenerateSpectrogramAsync(float[] audioSamples, int sampleRate, SpectrogramOptions options);

        /// <summary>
        /// 检测音高
        /// </summary>
        Task<double?> DetectPitchAsync(float[] audioSamples, int sampleRate, PitchDetectionOptions options);

        /// <summary>
        /// 分析谐波结构
        /// </summary>
        Task<HarmonicAnalysis> AnalyzeHarmonicsAsync(SpectrumData spectrumData, double fundamentalFrequency);
    }
}