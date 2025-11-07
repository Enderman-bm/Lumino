using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnderAudioAnalyzer.Interfaces;
using EnderAudioAnalyzer.Models;
using EnderDebugger;

namespace EnderAudioAnalyzer.Services
{
    /// <summary>
    /// 节奏分析服务 - 负责节奏检测和节拍跟踪
    /// </summary>
    public class RhythmAnalysisService : IRhythmAnalysisService
    {
        private readonly EnderLogger _logger;

        public RhythmAnalysisService()
        {
            _logger = new EnderLogger("RhythmAnalysisService");
        }

        /// <summary>
        /// 分析节奏和节拍
        /// </summary>
        public Task<RhythmAnalysis> AnalyzeRhythmAsync(float[] audioSamples, int sampleRate, RhythmAnalysisOptions options)
        {
            try
            {
                _logger.Debug("RhythmAnalysisService", $"开始节奏分析: 样本数={audioSamples.Length}, 采样率={sampleRate}Hz");

                var rhythmAnalysis = new RhythmAnalysis
                {
                    SampleRate = sampleRate,
                    Beats = new List<Beat>(),
                    TimeSignatures = new List<TimeSignature>()
                };

                // 计算能量包络
                var energyEnvelope = ComputeEnergyEnvelope(audioSamples, sampleRate, options);

                // 检测节拍
                var beats = DetectBeats(energyEnvelope, sampleRate, options);

                // 计算BPM
                rhythmAnalysis.BPM = CalculateBPM(beats, options);

                // 检测时间签名
                rhythmAnalysis.TimeSignatures = DetectTimeSignatures(beats, rhythmAnalysis.BPM, options);

                rhythmAnalysis.Beats = beats;
                rhythmAnalysis.Confidence = CalculateRhythmConfidence(beats, rhythmAnalysis.BPM);

                _logger.Info("RhythmAnalysisService", $"节奏分析完成: BPM={rhythmAnalysis.BPM:F1}, 节拍数={beats.Count}");
                return Task.FromResult(rhythmAnalysis);
            }
            catch (Exception ex)
            {
                _logger.Error("RhythmAnalysisService", $"节奏分析失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 计算能量包络
        /// </summary>
        private float[] ComputeEnergyEnvelope(float[] samples, int sampleRate, RhythmAnalysisOptions options)
        {
            int windowSize = (int)(0.1 * sampleRate); // 100ms窗口
            int hopSize = windowSize / 2;
            int envelopeLength = (samples.Length - windowSize) / hopSize + 1;
            var envelope = new float[envelopeLength];

            for (int i = 0; i < envelopeLength; i++)
            {
                int start = i * hopSize;
                float energy = 0.0f;

                for (int j = 0; j < windowSize && start + j < samples.Length; j++)
                {
                    energy += Math.Abs(samples[start + j]);
                }

                envelope[i] = energy / windowSize;
            }

            return envelope;
        }

        /// <summary>
        /// 检测节拍
        /// </summary>
        private List<Beat> DetectBeats(float[] energyEnvelope, int sampleRate, RhythmAnalysisOptions options)
        {
            var beats = new List<Beat>();
            int hopSize = (int)(0.1 * sampleRate) / 2; // 与ComputeEnergyEnvelope中的hopSize一致
            double timePerFrame = (double)hopSize / sampleRate;

            // 使用梳状滤波器检测节拍
            var combFilterResults = ApplyCombFilter(energyEnvelope, options.MinBPM, options.MaxBPM, options.BPMResolution, timePerFrame);

            // 找到最佳BPM
            double bestBPM = combFilterResults.OrderByDescending(c => c.Value).First().Key;

            // 根据最佳BPM检测节拍位置
            double beatInterval = 60.0 / bestBPM / timePerFrame; // 每拍之间的帧数

            for (int i = 1; i < energyEnvelope.Length - 1; i++)
            {
                // 检查局部最大值
                if (energyEnvelope[i] > energyEnvelope[i - 1] && energyEnvelope[i] > energyEnvelope[i + 1])
                {
                    // 检查是否接近预期的节拍位置
                    double expectedBeatPosition = i % beatInterval;
                    double distanceToBeat = Math.Min(expectedBeatPosition, beatInterval - expectedBeatPosition);

                    if (distanceToBeat < beatInterval * 0.2) // 允许20%的偏差
                    {
                        beats.Add(new Beat
                        {
                            Time = i * timePerFrame,
                            Strength = energyEnvelope[i],
                            Confidence = 1.0 - (distanceToBeat / (beatInterval * 0.2))
                        });
                    }
                }
            }

            return beats;
        }

        /// <summary>
        /// 应用梳状滤波器检测BPM
        /// </summary>
        private Dictionary<double, double> ApplyCombFilter(float[] energyEnvelope, double minBPM, double maxBPM, double bpmResolution, double timePerFrame)
        {
            var results = new Dictionary<double, double>();

            for (double bpm = minBPM; bpm <= maxBPM; bpm += bpmResolution)
            {
                double score = 0.0;
                double beatInterval = 60.0 / bpm / timePerFrame; // 转换为帧数

                for (int i = 0; i < energyEnvelope.Length; i++)
                {
                    // 检查每个可能的节拍位置
                    for (int k = 1; k <= 4; k++) // 考虑前4个谐波
                    {
                        int beatPosition = i - (int)(k * beatInterval);
                        if (beatPosition >= 0)
                        {
                            score += energyEnvelope[i] * energyEnvelope[beatPosition];
                        }
                    }
                }

                results[bpm] = score;
            }

            return results;
        }

        /// <summary>
        /// 计算BPM
        /// </summary>
        private double CalculateBPM(List<Beat> beats, RhythmAnalysisOptions options)
        {
            if (beats.Count < 2)
                return 120.0; // 默认BPM

            // 计算节拍间隔
            var intervals = new List<double>();
            for (int i = 1; i < beats.Count; i++)
            {
                intervals.Add(beats[i].Time - beats[i - 1].Time);
            }

            // 计算平均间隔（秒）
            double averageInterval = intervals.Average();

            // 转换为BPM
            double bpm = 60.0 / averageInterval;

            // 限制在合理范围内
            return Math.Max(options.MinBPM, Math.Min(options.MaxBPM, bpm));
        }

        /// <summary>
        /// 检测时间签名
        /// </summary>
        private List<TimeSignature> DetectTimeSignatures(List<Beat> beats, double bpm, RhythmAnalysisOptions options)
        {
            var timeSignatures = new List<TimeSignature>();

            if (beats.Count < 4)
                return timeSignatures;

            // 计算强拍和弱拍的模式
            var beatStrengths = beats.Select(b => (float)b.Strength).ToArray();
            var barLengths = new[] { 3, 4, 5, 6, 7 }; // 常见的小节长度

            foreach (int barLength in barLengths)
            {
                double confidence = CalculateTimeSignatureConfidence(beatStrengths, barLength);
                if (confidence > 0.5) // 置信度阈值
                {
                    timeSignatures.Add(new TimeSignature
                    {
                        Numerator = barLength,
                        Denominator = 4, // 假设四分音符为一拍
                        Confidence = confidence,
                        StartTime = beats.First().Time
                    });
                }
            }

            return timeSignatures.OrderByDescending(ts => ts.Confidence).ToList();
        }

        /// <summary>
        /// 计算时间签名置信度
        /// </summary>
        private double CalculateTimeSignatureConfidence(float[] beatStrengths, int barLength)
        {
            double confidence = 0.0;
            int patternLength = barLength;

            for (int i = 0; i < beatStrengths.Length - patternLength; i += patternLength)
            {
                // 检查模式是否重复：第一拍应该最强
                if (i + patternLength < beatStrengths.Length)
                {
                    float firstBeat = beatStrengths[i];
                    float nextFirstBeat = beatStrengths[i + patternLength];

                    // 第一拍应该比后续拍强
                    for (int j = 1; j < patternLength; j++)
                    {
                        if (beatStrengths[i + j] > firstBeat * 0.8) // 允许20%的偏差
                        {
                            confidence -= 0.1;
                        }
                    }

                    // 连续小节的第一个节拍强度应该相似
                    if (Math.Abs(firstBeat - nextFirstBeat) < firstBeat * 0.3) // 允许30%的偏差
                    {
                        confidence += 0.2;
                    }
                }
            }

            return Math.Max(0.0, Math.Min(1.0, confidence));
        }

        /// <summary>
        /// 计算节奏置信度
        /// </summary>
        private double CalculateRhythmConfidence(List<Beat> beats, double bpm)
        {
            if (beats.Count < 2)
                return 0.0;

            // 计算节拍间隔的一致性
            var intervals = new List<double>();
            for (int i = 1; i < beats.Count; i++)
            {
                intervals.Add(beats[i].Time - beats[i - 1].Time);
            }

            double expectedInterval = 60.0 / bpm;
            double totalDeviation = 0.0;

            foreach (double interval in intervals)
            {
                totalDeviation += Math.Abs(interval - expectedInterval);
            }

            double averageDeviation = totalDeviation / intervals.Count;
            double consistency = 1.0 - (averageDeviation / expectedInterval);

            return Math.Max(0.0, Math.Min(1.0, consistency));
        }

        /// <summary>
        /// 实时节拍跟踪
        /// </summary>
        public Task<Models.BeatTrackingResult> TrackBeatsAsync(float[] audioSamples, int sampleRate, double currentBPM, RhythmAnalysisOptions options)
        {
            try
            {
                _logger.Debug("RhythmAnalysisService", $"开始节拍跟踪: 当前BPM={currentBPM:F1}");

                var energyEnvelope = ComputeEnergyEnvelope(audioSamples, sampleRate, options);
                int hopSize = (int)(0.1 * sampleRate) / 2;
                double timePerFrame = (double)hopSize / sampleRate;

                var result = new BeatTrackingResult
                {
                    CurrentBPM = currentBPM,
                    DetectedBeats = new List<Beat>()
                };

                // 预测下一个节拍位置
                double beatInterval = 60.0 / currentBPM;
                double lastBeatTime = audioSamples.Length / (double)sampleRate - beatInterval;

                // 在能量包络中寻找最近的峰值作为节拍
                for (int i = Math.Max(0, energyEnvelope.Length - 10); i < energyEnvelope.Length; i++)
                {
                    if (i > 0 && i < energyEnvelope.Length - 1 &&
                        energyEnvelope[i] > energyEnvelope[i - 1] && energyEnvelope[i] > energyEnvelope[i + 1])
                    {
                        double beatTime = i * timePerFrame;
                        if (beatTime > lastBeatTime)
                        {
                            result.DetectedBeats.Add(new Beat
                            {
                                Time = beatTime,
                                Strength = energyEnvelope[i],
                                Confidence = 0.8 // 实时检测的置信度较低
                            });
                        }
                    }
                }

                // 更新BPM估计
                if (result.DetectedBeats.Count >= 2)
                {
                    var newIntervals = new List<double>();
                    for (int i = 1; i < result.DetectedBeats.Count; i++)
                    {
                        newIntervals.Add(result.DetectedBeats[i].Time - result.DetectedBeats[i - 1].Time);
                    }

                    double newAverageInterval = newIntervals.Average();
                    double newBPM = 60.0 / newAverageInterval;

                    // 平滑BPM变化
                    result.CurrentBPM = 0.7 * currentBPM + 0.3 * newBPM;
                }

                result.Confidence = CalculateRhythmConfidence(result.DetectedBeats, result.CurrentBPM);
                _logger.Info("RhythmAnalysisService", $"节拍跟踪完成: 新BPM={result.CurrentBPM:F1}, 检测到{result.DetectedBeats.Count}个节拍");

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.Error("RhythmAnalysisService", $"节拍跟踪失败: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// 节奏分析服务接口
    /// </summary>
    public interface IRhythmAnalysisService
    {
        /// <summary>
        /// 分析节奏和节拍
        /// </summary>
        Task<RhythmAnalysis> AnalyzeRhythmAsync(float[] audioSamples, int sampleRate, RhythmAnalysisOptions options);

        /// <summary>
        /// 实时节拍跟踪
        /// </summary>
        Task<BeatTrackingResult> TrackBeatsAsync(float[] audioSamples, int sampleRate, double currentBPM, RhythmAnalysisOptions options);
    }
}