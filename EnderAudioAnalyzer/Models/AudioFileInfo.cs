using System;

namespace EnderAudioAnalyzer.Models
{
    /// <summary>
    /// 音频文件信息
    /// </summary>
    public class AudioFileInfo
    {
        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 采样率（Hz）
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// 位深度
        /// </summary>
        public int BitDepth { get; set; }

        /// <summary>
        /// 声道数
        /// </summary>
        public int Channels { get; set; }

        /// <summary>
        /// 音频时长（秒）
        /// </summary>
        public double Duration { get; set; }

        /// <summary>
        /// 总采样点数
        /// </summary>
        public long TotalSamples { get; set; }

        /// <summary>
        /// 文件格式
        /// </summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>
        /// 是否支持该格式
        /// </summary>
        public bool IsSupported { get; set; }
    }
}