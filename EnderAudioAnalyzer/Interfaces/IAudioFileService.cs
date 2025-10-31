using System;
using System.Threading.Tasks;
using EnderAudioAnalyzer.Models;

namespace EnderAudioAnalyzer.Interfaces
{
    /// <summary>
    /// 音频文件服务接口
    /// </summary>
    public interface IAudioFileService
    {
        /// <summary>
        /// 加载音频文件信息
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <returns>音频文件信息</returns>
        Task<AudioFileInfo> LoadAudioFileAsync(string filePath);
        
        /// <summary>
        /// 预处理音频文件
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <param name="targetSampleRate">目标采样率</param>
        /// <returns>预处理后的音频样本数据</returns>
        Task<float[]> PreprocessAudioAsync(string filePath, int targetSampleRate);
        
        /// <summary>
        /// 检查文件格式是否支持
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <returns>是否支持该格式</returns>
        Task<bool> IsSupportedFormatAsync(string filePath);
        
        /// <summary>
        /// 读取音频样本数据
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <param name="startSample">开始样本位置</param>
        /// <param name="sampleCount">要读取的样本数，-1表示读取全部</param>
        /// <returns>音频样本数据</returns>
        Task<float[]> ReadAudioSamplesAsync(string filePath, int startSample = 0, int sampleCount = -1);
    }
}