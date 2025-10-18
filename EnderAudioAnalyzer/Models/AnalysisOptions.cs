using System;

namespace EnderAudioAnalyzer.Models
{
    /// <summary>
    /// 频谱分析选项
    /// </summary>
    public class SpectrumAnalysisOptions
    {
        /// <summary>
        /// 窗函数类型
        /// </summary>
        public WindowType WindowType { get; set; } = WindowType.Hann;

        /// <summary>
        /// 窗口大小（样本数）
        /// </summary>
        public int WindowSize { get; set; } = 2048;

        /// <summary>
        /// 窗口重叠比例（0.0 - 1.0）
        /// </summary>
        public double OverlapRatio { get; set; } = 0.5;

        /// <summary>
        /// 最小峰值高度（dB）
        /// </summary>
        public double MinPeakHeight { get; set; } = -60.0;

        /// <summary>
        /// 峰值检测阈值
        /// </summary>
        public double PeakThreshold { get; set; } = 3.0;

        /// <summary>
        /// 最大频率（Hz），0表示使用奈奎斯特频率
        /// </summary>
        public double MaxFrequency { get; set; } = 0;

        /// <summary>
        /// 是否启用谐波分析
        /// </summary>
        public bool EnableHarmonicAnalysis { get; set; } = true;

        /// <summary>
        /// 频谱平滑参数
        /// </summary>
        public int SmoothingFactor { get; set; } = 3;

        public SpectrumAnalysisOptions()
        {
        }

        public SpectrumAnalysisOptions(int windowSize, double overlapRatio)
        {
            WindowSize = windowSize;
            OverlapRatio = overlapRatio;
        }
    }

    /// <summary>
    /// 音高检测选项
    /// </summary>
    public class PitchDetectionOptions
    {
        /// <summary>
        /// 最小频率（Hz）
        /// </summary>
        public double MinFrequency { get; set; } = 80.0;

        /// <summary>
        /// 最大频率（Hz）
        /// </summary>
        public double MaxFrequency { get; set; } = 1000.0;

        /// <summary>
        /// 音高检测阈值
        /// </summary>
        public double Threshold { get; set; } = 0.1;

        /// <summary>
        /// 音高检测算法
        /// </summary>
        public PitchDetectionAlgorithm Algorithm { get; set; } = PitchDetectionAlgorithm.YIN;

        /// <summary>
        /// 是否启用音高平滑
        /// </summary>
        public bool EnableSmoothing { get; set; } = true;

        /// <summary>
        /// 音高平滑窗口大小
        /// </summary>
        public int SmoothingWindow { get; set; } = 5;

        /// <summary>
        /// 音高变化阈值（半音）
        /// </summary>
        public double PitchChangeThreshold { get; set; } = 0.5;

        /// <summary>
        /// 音高置信度阈值
        /// </summary>
        public double ConfidenceThreshold { get; set; } = 0.7;

        public PitchDetectionOptions()
        {
        }

        public PitchDetectionOptions(double minFreq, double maxFreq)
        {
            MinFrequency = minFreq;
            MaxFrequency = maxFreq;
        }
    }

    /// <summary>
    /// 节奏分析选项
    /// </summary>
    public class RhythmAnalysisOptions
    {
        /// <summary>
        /// 节奏检测算法
        /// </summary>
        public RhythmDetectionAlgorithm Algorithm { get; set; } = RhythmDetectionAlgorithm.CombFilter;

        /// <summary>
        /// 最小BPM
        /// </summary>
        public double MinBPM { get; set; } = 60.0;

        /// <summary>
        /// 最大BPM
        /// </summary>
        public double MaxBPM { get; set; } = 240.0;

        /// <summary>
        /// BPM检测精度
        /// </summary>
        public double BPMResolution { get; set; } = 1.0;

        /// <summary>
        /// 节拍检测阈值
        /// </summary>
        public double BeatThreshold { get; set; } = 0.3;

        /// <summary>
        /// 是否启用节拍跟踪
        /// </summary>
        public bool EnableBeatTracking { get; set; } = true;

        /// <summary>
        /// 节拍跟踪窗口大小（秒）
        /// </summary>
        public double TrackingWindow { get; set; } = 3.0;

        /// <summary>
        /// 时间签名检测范围
        /// </summary>
        public (int, int) TimeSignatureRange { get; set; } = (2, 7);

        /// <summary>
        /// 是否启用小节检测
        /// </summary>
        public bool EnableBarDetection { get; set; } = true;

        public RhythmAnalysisOptions()
        {
        }

        public RhythmAnalysisOptions(double minBpm, double maxBpm)
        {
            MinBPM = minBpm;
            MaxBPM = maxBpm;
        }
    }

    /// <summary>
    /// 音符检测选项
    /// </summary>
    public class NoteDetectionOptions
    {
        /// <summary>
        /// 音符检测模式
        /// </summary>
        public NoteDetectionMode Mode { get; set; } = NoteDetectionMode.Hybrid;

        /// <summary>
        /// 最小音符时长（秒）
        /// </summary>
        public double MinNoteDuration { get; set; } = 0.05;

        /// <summary>
        /// 最大音符时长（秒）
        /// </summary>
        public double MaxNoteDuration { get; set; } = 5.0;

        /// <summary>
        /// 音符分离阈值（秒）
        /// </summary>
        public double NoteSeparationThreshold { get; set; } = 0.02;

        /// <summary>
        /// 音高稳定阈值（半音）
        /// </summary>
        public double PitchStabilityThreshold { get; set; } = 0.25;

        /// <summary>
        /// 音量阈值（0.0 - 1.0）
        /// </summary>
        public double VolumeThreshold { get; set; } = 0.1;

        /// <summary>
        /// 音符量化精度
        /// </summary>
        public QuantizationPrecision Quantization { get; set; } = QuantizationPrecision.SixteenthNote;

        /// <summary>
        /// 是否启用和弦检测
        /// </summary>
        public bool EnableChordDetection { get; set; } = true;

        /// <summary>
        /// 和弦检测阈值（半音）
        /// </summary>
        public double ChordDetectionThreshold { get; set; } = 1.0;

        /// <summary>
        /// 是否启用滑音检测
        /// </summary>
        public bool EnableGlissandoDetection { get; set; } = true;

        /// <summary>
        /// 滑音检测阈值（半音/秒）
        /// </summary>
        public double GlissandoThreshold { get; set; } = 10.0;

        public NoteDetectionOptions()
        {
        }

        public NoteDetectionOptions(double minDuration, double volumeThreshold)
        {
            MinNoteDuration = minDuration;
            VolumeThreshold = volumeThreshold;
        }
    }

    /// <summary>
    /// 音频分析全局选项
    /// </summary>
    public class AudioAnalysisOptions
    {
        /// <summary>
        /// 目标采样率（Hz）
        /// </summary>
        public int TargetSampleRate { get; set; } = 44100;

        /// <summary>
        /// 音频通道选择
        /// </summary>
        public AudioChannel Channel { get; set; } = AudioChannel.Mixed;

        /// <summary>
        /// 音频质量级别
        /// </summary>
        public AudioQuality Quality { get; set; } = AudioQuality.Medium;

        /// <summary>
        /// 是否启用实时分析
        /// </summary>
        public bool EnableRealtimeAnalysis { get; set; } = false;

        /// <summary>
        /// 实时分析缓冲区大小（秒）
        /// </summary>
        public double RealtimeBufferSize { get; set; } = 2.0;

        /// <summary>
        /// 是否启用多线程处理
        /// </summary>
        public bool EnableMultithreading { get; set; } = true;

        /// <summary>
        /// 最大线程数
        /// </summary>
        public int MaxThreads { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// 是否保存中间结果
        /// </summary>
        public bool SaveIntermediateResults { get; set; } = false;

        /// <summary>
        /// 中间结果保存路径
        /// </summary>
        public string IntermediateResultsPath { get; set; } = string.Empty;

        /// <summary>
        /// 频谱分析选项
        /// </summary>
        public SpectrumAnalysisOptions SpectrumOptions { get; set; } = new SpectrumAnalysisOptions();

        /// <summary>
        /// 音高检测选项
        /// </summary>
        public PitchDetectionOptions PitchOptions { get; set; } = new PitchDetectionOptions();

        /// <summary>
        /// 节奏分析选项
        /// </summary>
        public RhythmAnalysisOptions RhythmOptions { get; set; } = new RhythmAnalysisOptions();

        /// <summary>
        /// 音符检测选项
        /// </summary>
        public NoteDetectionOptions NoteOptions { get; set; } = new NoteDetectionOptions();

        public AudioAnalysisOptions()
        {
        }

        public AudioAnalysisOptions(int sampleRate, AudioQuality quality)
        {
            TargetSampleRate = sampleRate;
            Quality = quality;
        }

        /// <summary>
        /// 根据质量级别自动配置选项
        /// </summary>
        public void ConfigureForQuality()
        {
            switch (Quality)
            {
                case AudioQuality.Low:
                    SpectrumOptions.WindowSize = 1024;
                    SpectrumOptions.OverlapRatio = 0.25;
                    PitchOptions.SmoothingWindow = 3;
                    NoteOptions.Mode = NoteDetectionMode.Realtime;
                    EnableMultithreading = true;
                    break;

                case AudioQuality.Medium:
                    SpectrumOptions.WindowSize = 2048;
                    SpectrumOptions.OverlapRatio = 0.5;
                    PitchOptions.SmoothingWindow = 5;
                    NoteOptions.Mode = NoteDetectionMode.Hybrid;
                    EnableMultithreading = true;
                    break;

                case AudioQuality.High:
                    SpectrumOptions.WindowSize = 4096;
                    SpectrumOptions.OverlapRatio = 0.75;
                    PitchOptions.SmoothingWindow = 7;
                    NoteOptions.Mode = NoteDetectionMode.Accurate;
                    EnableMultithreading = true;
                    break;

                case AudioQuality.Professional:
                    SpectrumOptions.WindowSize = 8192;
                    SpectrumOptions.OverlapRatio = 0.875;
                    PitchOptions.SmoothingWindow = 9;
                    NoteOptions.Mode = NoteDetectionMode.Accurate;
                    EnableMultithreading = true;
                    MaxThreads = Math.Max(1, Environment.ProcessorCount / 2);
                    break;
            }
        }
    }
}