using System;

namespace EnderAudioAnalyzer.Models
{
    /// <summary>
    /// 频谱图生成选项
    /// </summary>
    public class SpectrogramOptions
    {
        /// <summary>
        /// 窗口大小（默认 1024）
        /// </summary>
        public int WindowSize { get; set; } = 1024;

        /// <summary>
        /// 跳跃大小（默认 512）
        /// </summary>
        public int HopSize { get; set; } = 512;

        /// <summary>
        /// 窗口类型（引用 Enums.cs 中的 WindowType）
        /// </summary>
        public WindowType WindowType { get; set; } = WindowType.Hann;
    }
}