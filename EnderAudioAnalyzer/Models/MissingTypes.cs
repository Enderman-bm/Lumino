using System;
using System.Collections.Generic;
using System.IO;

namespace EnderAudioAnalyzer.Models
{
    /// <summary>
    /// 音频分析结果
    /// </summary>
    public class AudioAnalysisResult
    {
        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 文件信息
        /// </summary>
        public FileInfo? FileInfo { get; set; }

        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// 分析状态
        /// </summary>
        public AnalysisStatus Status { get; set; }

        /// <summary>
        /// 分析持续时间
        /// </summary>
        public TimeSpan AnalysisDuration => EndTime - StartTime;

        /// <summary>
        /// 分析时间
        /// </summary>
        public double AnalysisTime => AnalysisDuration.TotalSeconds;

        /// <summary>
        /// 音频文件信息
        /// </summary>
        public AudioFileInfo AudioInfo { get; set; } = new();

        /// <summary>
        /// 检测到的音符
        /// </summary>
        public List<DetectedNote> DetectedNotes { get; set; } = new();

        /// <summary>
        /// 检测到的和弦
        /// </summary>
        public List<DetectedChord> DetectedChords { get; set; } = new();

        /// <summary>
        /// 节奏分析结果
        /// </summary>
        public RhythmAnalysis? RhythmAnalysis { get; set; }

        /// <summary>
        /// 频谱分析结果
        /// </summary>
        public List<SpectrumData> SpectrumData { get; set; } = new();

        /// <summary>
        /// 处理后的样本
        /// </summary>
        public float[]? ProcessedSamples { get; set; }

        /// <summary>
        /// 处理后的采样率
        /// </summary>
        public int ProcessedSampleRate { get; set; }

        /// <summary>
        /// 分析质量评分（0-1）
        /// </summary>
        public double QualityScore { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 频谱数据
    /// </summary>
    public class SpectrumData
    {
        /// <summary>
        /// 采样率
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// 频率分辨率
        /// </summary>
        public double FrequencyResolution { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 频率数组
        /// </summary>
        public double[] Frequencies { get; set; } = Array.Empty<double>();

        /// <summary>
        /// 幅度数组
        /// </summary>
        public double[] Magnitudes { get; set; } = Array.Empty<double>();

        /// <summary>
        /// 频谱峰值
        /// </summary>
        public List<SpectralPeak> Peaks { get; set; } = new();
    }

    /// <summary>
    /// 频谱峰值
    /// </summary>
    public class SpectralPeak
    {
        /// <summary>
        /// 频率
        /// </summary>
        public double Frequency { get; set; }

        /// <summary>
        /// 幅度
        /// </summary>
        public double Magnitude { get; set; }

        /// <summary>
        /// 二进制索引
        /// </summary>
        public int BinIndex { get; set; }
    }

    /// <summary>
    /// 频率幅度
    /// </summary>
    public class FrequencyMagnitude
    {
        /// <summary>
        /// 频率
        /// </summary>
        public double Frequency { get; set; }

        /// <summary>
        /// 幅度
        /// </summary>
        public double Magnitude { get; set; }
    }

    /// <summary>
    /// 谐波分析
    /// </summary>
    public class HarmonicAnalysis
    {
        /// <summary>
        /// 基频
        /// </summary>
        public double FundamentalFrequency { get; set; }

        /// <summary>
        /// 谐波列表
        /// </summary>
        public List<Harmonic> Harmonics { get; set; } = new();

        /// <summary>
        /// 谐波性
        /// </summary>
        public double Harmonicity { get; set; }
    }

    /// <summary>
    /// 谐波
    /// </summary>
    public class Harmonic
    {
        /// <summary>
        /// 谐波阶数
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// 频率
        /// </summary>
        public double Frequency { get; set; }

        /// <summary>
        /// 幅度
        /// </summary>
        public double Magnitude { get; set; }

        /// <summary>
        /// 偏差
        /// </summary>
        public double Deviation { get; set; }
    }


    /// <summary>
    /// 检测到的音符
    /// </summary>
    public class DetectedNote
    {
        /// <summary>
        /// 音符ID
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 开始时间
        /// </summary>
        public double StartTime { get; set; }

        /// <summary>
        /// 结束时间
        /// </summary>
        public double EndTime { get; set; }

        /// <summary>
        /// 持续时间
        /// </summary>
        public double Duration { get; set; }

        /// <summary>
        /// 音符名称
        /// </summary>
        public string NoteName { get; set; } = string.Empty;

        /// <summary>
        /// MIDI音符号
        /// </summary>
        public int MidiNote { get; set; }

        /// <summary>
        /// 频率
        /// </summary>
        public double Frequency { get; set; }

        /// <summary>
        /// 音量
        /// </summary>
        public double Velocity { get; set; }

        /// <summary>
        /// 置信度
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// 音轨索引
        /// </summary>
        public int TrackIndex { get; set; }

        /// <summary>
        /// 克隆音符
        /// </summary>
        public DetectedNote Clone()
        {
            return new DetectedNote
            {
                Id = Guid.NewGuid(),
                StartTime = StartTime,
                EndTime = EndTime,
                Duration = Duration,
                NoteName = NoteName,
                MidiNote = MidiNote,
                Frequency = Frequency,
                Velocity = Velocity,
                Confidence = Confidence,
                TrackIndex = TrackIndex
            };
        }
    }

    /// <summary>
    /// 检测到的和弦
    /// </summary>
    public class DetectedChord
    {
        /// <summary>
        /// 和弦ID
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 开始时间
        /// </summary>
        public double StartTime { get; set; }

        /// <summary>
        /// 结束时间
        /// </summary>
        public double EndTime { get; set; }

        /// <summary>
        /// 持续时间
        /// </summary>
        public double Duration { get; set; }

        /// <summary>
        /// 根音
        /// </summary>
        public int RootNote { get; set; }

        /// <summary>
        /// 和弦类型
        /// </summary>
        public string ChordType { get; set; } = string.Empty;

        /// <summary>
        /// 组成音符
        /// </summary>
        public List<int> Notes { get; set; } = new();

        /// <summary>
        /// 音量
        /// </summary>
        public double Velocity { get; set; }

        /// <summary>
        /// 置信度
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// 音轨索引
        /// </summary>
        public int TrackIndex { get; set; }
    }

    /// <summary>
    /// 和弦检测选项
    /// </summary>
    public class ChordDetectionOptions
    {
        /// <summary>
        /// 时间窗口
        /// </summary>
        public double TimeWindow { get; set; } = 0.1;

        /// <summary>
        /// 最少和弦音符数
        /// </summary>
        public int MinChordNotes { get; set; } = 2;

        /// <summary>
        /// 最大和弦音符数
        /// </summary>
        public int MaxChordNotes { get; set; } = 6;

        /// <summary>
        /// 和弦检测阈值
        /// </summary>
        public double DetectionThreshold { get; set; } = 0.7;
    }

    /// <summary>
    /// 滑音
    /// </summary>
    public class Glissando
    {
        /// <summary>
        /// 滑音ID
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 开始时间
        /// </summary>
        public double StartTime { get; set; }

        /// <summary>
        /// 结束时间
        /// </summary>
        public double EndTime { get; set; }

        /// <summary>
        /// 持续时间
        /// </summary>
        public double Duration { get; set; }

        /// <summary>
        /// 开始音符
        /// </summary>
        public DetectedNote? StartNote { get; set; }

        /// <summary>
        /// 结束音符
        /// </summary>
        public DetectedNote? EndNote { get; set; }

        /// <summary>
        /// 开始音高
        /// </summary>
        public double StartPitch { get; set; }

        /// <summary>
        /// 结束音高
        /// </summary>
        public double EndPitch { get; set; }

        /// <summary>
        /// 音高变化
        /// </summary>
        public double PitchChange { get; set; }

        /// <summary>
        /// 音高变化率
        /// </summary>
        public double Rate { get; set; }

        /// <summary>
        /// 置信度
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// 音轨索引
        /// </summary>
        public int TrackIndex { get; set; }
    }

    /// <summary>
    /// 滑音检测选项
    /// </summary>
    public class GlissandoDetectionOptions
    {
        /// <summary>
        /// 启用滑音检测
        /// </summary>
        public bool EnableGlissandoDetection { get; set; } = true;

        /// <summary>
        /// 最小滑音时长
        /// </summary>
        public double MinGlissandoDuration { get; set; } = 0.1;

        /// <summary>
        /// 最小音高变化
        /// </summary>
        public double MinPitchChange { get; set; } = 2.0;

        /// <summary>
        /// 音符间最大间隙
        /// </summary>
        public double MaxGapBetweenNotes { get; set; } = 0.05;

        /// <summary>
        /// 最小音高变化率
        /// </summary>
        public double MinPitchChangeRate { get; set; } = 1.0;

        /// <summary>
        /// 最大音高变化率
        /// </summary>
        public double MaxPitchChangeRate { get; set; } = 50.0;

        /// <summary>
        /// 滑音检测阈值
        /// </summary>
        public double DetectionThreshold { get; set; } = 0.6;
    }

    /// <summary>
    /// 节拍跟踪结果
    /// </summary>
    public class BeatTrackingResult
    {
        /// <summary>
        /// 当前BPM
        /// </summary>
        public double CurrentBPM { get; set; }

        /// <summary>
        /// BPM置信度
        /// </summary>
        public double BPMConfidence { get; set; }

        /// <summary>
        /// 检测到的节拍
        /// </summary>
        public List<Beat> DetectedBeats { get; set; } = new();

        /// <summary>
        /// 检测到的节拍时间
        /// </summary>
        public List<double> BeatTimes { get; set; } = new();

        /// <summary>
        /// 节拍强度
        /// </summary>
        public List<double> BeatStrengths { get; set; } = new();

        /// <summary>
        /// 相位偏移
        /// </summary>
        public double PhaseOffset { get; set; }

        /// <summary>
        /// 节拍稳定性
        /// </summary>
        public double BeatStability { get; set; }

        /// <summary>
        /// 置信度
        /// </summary>
        public double Confidence { get; set; }
    }

    /// <summary>
    /// 实时分析结果
    /// </summary>
    public class RealtimeAnalysisResult
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 采样率
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// 检测到的音高
        /// </summary>
        public double? DetectedPitch { get; set; }

        /// <summary>
        /// 频谱数据
        /// </summary>
        public SpectrumData? SpectrumData { get; set; }

        /// <summary>
        /// 节拍跟踪结果
        /// </summary>
        public BeatTrackingResult? BeatTracking { get; set; }

        /// <summary>
        /// 谐波分析
        /// </summary>
        public HarmonicAnalysis? HarmonicAnalysis { get; set; }

        /// <summary>
        /// 当前BPM
        /// </summary>
        public double CurrentBPM { get; set; }
    }

    /// <summary>
    /// 节奏分析结果
    /// </summary>
    public class RhythmAnalysis
    {
        /// <summary>
        /// 采样率
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// BPM
        /// </summary>
        public double BPM { get; set; }

        /// <summary>
        /// 节拍
        /// </summary>
        public List<Beat> Beats { get; set; } = new();

        /// <summary>
        /// 拍号
        /// </summary>
        public List<TimeSignature> TimeSignatures { get; set; } = new();

        /// <summary>
        /// 置信度
        /// </summary>
        public double Confidence { get; set; }
    }

    /// <summary>
    /// 节拍
    /// </summary>
    public class Beat
    {
        /// <summary>
        /// 时间位置
        /// </summary>
        public double Time { get; set; }

        /// <summary>
        /// 强度
        /// </summary>
        public double Strength { get; set; }

        /// <summary>
        /// 置信度
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// 节拍类型（强拍/弱拍）
        /// </summary>
        public BeatType Type { get; set; }
    }

    /// <summary>
    /// 拍号
    /// </summary>
    public class TimeSignature
    {
        /// <summary>
        /// 分子
        /// </summary>
        public int Numerator { get; set; }

        /// <summary>
        /// 分母
        /// </summary>
        public int Denominator { get; set; }

        /// <summary>
        /// 开始时间
        /// </summary>
        public double StartTime { get; set; }

        /// <summary>
        /// 置信度
        /// </summary>
        public double Confidence { get; set; }
    }

    /// <summary>
    /// 节拍类型
    /// </summary>
    public enum BeatType
    {
        /// <summary>
        /// 强拍
        /// </summary>
        Downbeat,

        /// <summary>
        /// 弱拍
        /// </summary>
        Upbeat
    }

}