using System;

namespace EnderAudioAnalyzer.Models
{
    /// <summary>
    /// 窗函数类型
    /// </summary>
    public enum WindowType
    {
        /// <summary>
        /// 矩形窗
        /// </summary>
        Rectangular,

        /// <summary>
        /// 汉宁窗
        /// </summary>
        Hann,

        /// <summary>
        /// 汉明窗
        /// </summary>
        Hamming,

        /// <summary>
        /// 布莱克曼窗
        /// </summary>
        Blackman
    }

    /// <summary>
    /// 音高检测算法
    /// </summary>
    public enum PitchDetectionAlgorithm
    {
        /// <summary>
        /// YIN算法
        /// </summary>
        YIN,

        /// <summary>
        /// 自相关算法
        /// </summary>
        Autocorrelation,

        /// <summary>
        /// 频谱峰值算法
        /// </summary>
        SpectralPeak
    }

    /// <summary>
    /// 节奏检测算法
    /// </summary>
    public enum RhythmDetectionAlgorithm
    {
        /// <summary>
        /// 梳状滤波器
        /// </summary>
        CombFilter,

        /// <summary>
        /// 频谱通量
        /// </summary>
        SpectralFlux,

        /// <summary>
        /// 自相关
        /// </summary>
        Autocorrelation
    }

    /// <summary>
    /// 音符识别模式
    /// </summary>
    public enum NoteDetectionMode
    {
        /// <summary>
        /// 实时模式（快速但精度较低）
        /// </summary>
        Realtime,

        /// <summary>
        /// 精确模式（较慢但精度高）
        /// </summary>
        Accurate,

        /// <summary>
        /// 混合模式
        /// </summary>
        Hybrid
    }

    /// <summary>
    /// 音频分析状态
    /// </summary>
    public enum AnalysisStatus
    {
        /// <summary>
        /// 未开始
        /// </summary>
        NotStarted,

        /// <summary>
        /// 正在分析
        /// </summary>
        InProgress,

        /// <summary>
        /// 已完成
        /// </summary>
        Completed,

        /// <summary>
        /// 失败
        /// </summary>
        Failed,

        /// <summary>
        /// 已取消
        /// </summary>
        Cancelled
    }

    /// <summary>
    /// 音符量化精度
    /// </summary>
    public enum QuantizationPrecision
    {
        /// <summary>
        /// 全音符
        /// </summary>
        WholeNote,

        /// <summary>
        /// 二分音符
        /// </summary>
        HalfNote,

        /// <summary>
        /// 四分音符
        /// </summary>
        QuarterNote,

        /// <summary>
        /// 八分音符
        /// </summary>
        EighthNote,

        /// <summary>
        /// 十六分音符
        /// </summary>
        SixteenthNote,

        /// <summary>
        /// 三十二分音符
        /// </summary>
        ThirtySecondNote,

        /// <summary>
        /// 六十四分音符
        /// </summary>
        SixtyFourthNote
    }

    /// <summary>
    /// 音频通道选择
    /// </summary>
    public enum AudioChannel
    {
        /// <summary>
        /// 左声道
        /// </summary>
        Left,

        /// <summary>
        /// 右声道
        /// </summary>
        Right,

        /// <summary>
        /// 混合声道
        /// </summary>
        Mixed,

        /// <summary>
        /// 两个声道分别处理
        /// </summary>
        Both
    }

    /// <summary>
    /// 频谱显示模式
    /// </summary>
    public enum SpectrumDisplayMode
    {
        /// <summary>
        /// 线性显示
        /// </summary>
        Linear,

        /// <summary>
        /// 对数显示
        /// </summary>
        Logarithmic,

        /// <summary>
        /// 分贝显示
        /// </summary>
        Decibel
    }

    /// <summary>
    /// 音符置信度级别
    /// </summary>
    public enum ConfidenceLevel
    {
        /// <summary>
        /// 低置信度
        /// </summary>
        Low,

        /// <summary>
        /// 中等置信度
        /// </summary>
        Medium,

        /// <summary>
        /// 高置信度
        /// </summary>
        High,

        /// <summary>
        /// 非常高置信度
        /// </summary>
        VeryHigh
    }

    /// <summary>
    /// 音频质量级别
    /// </summary>
    public enum AudioQuality
    {
        /// <summary>
        /// 低质量（快速处理）
        /// </summary>
        Low,

        /// <summary>
        /// 中等质量
        /// </summary>
        Medium,

        /// <summary>
        /// 高质量
        /// </summary>
        High,

        /// <summary>
        /// 专业质量（最精确但最慢）
        /// </summary>
        Professional
    }
}