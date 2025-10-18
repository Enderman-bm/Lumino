using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EnderAudioAnalyzer.Models;

namespace EnderAudioAnalyzer.Interfaces
{
    /// <summary>
    /// 音频分析服务接口
    /// </summary>
    public interface IAudioAnalysisService
    {
        /// <summary>
        /// 分析音频文件
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <param name="options">分析选项</param>
        /// <param name="progress">进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>音频分析结果</returns>
        Task<AudioAnalysisResult> AnalyzeAudioAsync(
            string filePath, 
            AudioAnalysisOptions options, 
            IProgress<(double Progress, string Status)>? progress = null, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取指定时间点的频谱数据
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <param name="time">时间点（秒）</param>
        /// <param name="windowSize">窗口大小（秒）</param>
        /// <returns>频谱数据</returns>
        Task<SpectrumData> GetSpectrumDataAsync(string filePath, double time, double windowSize = 0.1);

        /// <summary>
        /// 检测音频中的音符
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <param name="options">检测选项</param>
        /// <param name="progress">进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>检测到的音符列表</returns>
        Task<List<DetectedNote>> DetectNotesAsync(
            string filePath, 
            NoteDetectionOptions options, 
            IProgress<(double Progress, string Status)>? progress = null, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 分析音频节奏
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <param name="options">节奏分析选项</param>
        /// <returns>节奏分析结果</returns>
        Task<RhythmAnalysis> AnalyzeRhythmAsync(string filePath, RhythmAnalysisOptions? options = null);

        /// <summary>
        /// 获取支持的音频格式
        /// </summary>
        /// <returns>支持的格式列表</returns>
        List<string> GetSupportedFormats();

        /// <summary>
        /// 检查文件格式是否支持
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否支持</returns>
        Task<bool> IsSupportedFormatAsync(string filePath);
    }

}