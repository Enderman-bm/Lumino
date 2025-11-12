using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EnderDebugger;

namespace Lumino.Rendering.Vulkan
{
    /// <summary>
    /// 渲染性能监测器
    /// 实时监测渲染性能指标，支持性能分析和瓶颈诊断
    /// </summary>
    public class RenderPerformanceMonitor : IDisposable
    {
        private readonly List<FrameMetrics> _frameHistory = new();
        private readonly int _historySize;
        private FrameMetrics _currentFrame = new();
        private Stopwatch _frameTimer = new();
        private bool _isRecording = false;

        public int FrameCount => _frameHistory.Count;
        public bool IsRecording => _isRecording;

        public RenderPerformanceMonitor(int historySize = 300)
        {
            _historySize = historySize;
        }

        /// <summary>
        /// 开始记录帧性能
        /// </summary>
        public void BeginFrame()
        {
            _frameTimer.Restart();
            _currentFrame = new FrameMetrics();
            _isRecording = true;
        }

        /// <summary>
        /// 记录阶段时间
        /// </summary>
        public void RecordStageTime(string stageName, double timeMs)
        {
            if (!_isRecording) return;

            if (!_currentFrame.StageTimes.ContainsKey(stageName))
            {
                _currentFrame.StageTimes[stageName] = 0;
            }
            _currentFrame.StageTimes[stageName] += timeMs;
        }

        /// <summary>
        /// 记录提交的渲染批次数
        /// </summary>
        public void RecordBatchCount(int count)
        {
            if (!_isRecording) return;
            _currentFrame.BatchCount = count;
        }

        /// <summary>
        /// 记录渲染的音符数
        /// </summary>
        public void RecordNoteCount(int count)
        {
            if (!_isRecording) return;
            _currentFrame.NoteCount = count;
        }

        /// <summary>
        /// 记录GPU内存使用
        /// </summary>
        public void RecordGPUMemoryUsage(long bytesUsed, long bytesAllocated)
        {
            if (!_isRecording) return;
            _currentFrame.GPUMemoryUsed = bytesUsed;
            _currentFrame.GPUMemoryAllocated = bytesAllocated;
        }

        /// <summary>
        /// 记录CPU内存使用
        /// </summary>
        public void RecordCPUMemoryUsage(long bytesUsed)
        {
            if (!_isRecording) return;
            _currentFrame.CPUMemoryUsed = bytesUsed;
        }

        /// <summary>
        /// 结束帧记录
        /// </summary>
        public void EndFrame()
        {
            if (!_isRecording) return;

            _frameTimer.Stop();
            _currentFrame.TotalTimeMs = _frameTimer.Elapsed.TotalMilliseconds;
            _currentFrame.Timestamp = DateTime.UtcNow;

            _frameHistory.Add(_currentFrame);

            // 维持历史大小
            if (_frameHistory.Count > _historySize)
            {
                _frameHistory.RemoveAt(0);
            }

            _isRecording = false;
        }

        /// <summary>
        /// 获取平均帧时间 (ms)
        /// </summary>
        public double GetAverageFrameTime()
        {
            if (_frameHistory.Count == 0) return 0;
            return _frameHistory.Average(f => f.TotalTimeMs);
        }

        /// <summary>
        /// 获取平均FPS
        /// </summary>
        public double GetAverageFPS()
        {
            var avgTime = GetAverageFrameTime();
            return avgTime > 0 ? 1000.0 / avgTime : 0;
        }

        /// <summary>
        /// 获取最大帧时间
        /// </summary>
        public double GetMaxFrameTime()
        {
            if (_frameHistory.Count == 0) return 0;
            return _frameHistory.Max(f => f.TotalTimeMs);
        }

        /// <summary>
        /// 获取最小帧时间
        /// </summary>
        public double GetMinFrameTime()
        {
            if (_frameHistory.Count == 0) return 0;
            return _frameHistory.Min(f => f.TotalTimeMs);
        }

        /// <summary>
        /// 获取特定百分位的帧时间
        /// </summary>
        public double GetPercentileFrameTime(double percentile)
        {
            if (_frameHistory.Count == 0) return 0;
            var sorted = _frameHistory.OrderBy(f => f.TotalTimeMs).ToList();
            int index = (int)((percentile / 100.0) * sorted.Count);
            return sorted[Math.Min(index, sorted.Count - 1)].TotalTimeMs;
        }

        /// <summary>
        /// 获取平均批次数
        /// </summary>
        public double GetAverageBatchCount()
        {
            if (_frameHistory.Count == 0) return 0;
            return _frameHistory.Average(f => f.BatchCount);
        }

        /// <summary>
        /// 获取平均音符数
        /// </summary>
        public double GetAverageNoteCount()
        {
            if (_frameHistory.Count == 0) return 0;
            return _frameHistory.Average(f => f.NoteCount);
        }

        /// <summary>
        /// 获取特定阶段的平均时间
        /// </summary>
        public double GetAverageStageTime(string stageName)
        {
            var framesWithStage = _frameHistory.Where(f => f.StageTimes.ContainsKey(stageName)).ToList();
            if (framesWithStage.Count == 0) return 0;
            return framesWithStage.Average(f => f.StageTimes[stageName]);
        }

        /// <summary>
        /// 获取性能报告
        /// </summary>
        public PerformanceReport GetReport()
        {
            var report = new PerformanceReport
            {
                FrameCount = _frameHistory.Count,
                AverageFrameTime = GetAverageFrameTime(),
                AverageFPS = GetAverageFPS(),
                MinFrameTime = GetMinFrameTime(),
                MaxFrameTime = GetMaxFrameTime(),
                P95FrameTime = GetPercentileFrameTime(95),
                P99FrameTime = GetPercentileFrameTime(99),
                AverageBatchCount = GetAverageBatchCount(),
                AverageNoteCount = GetAverageNoteCount(),
            };

            // 添加所有阶段时间
            var allStages = _frameHistory
                .SelectMany(f => f.StageTimes.Keys)
                .Distinct()
                .ToList();

            foreach (var stage in allStages)
            {
                report.StageAverageTimes[stage] = GetAverageStageTime(stage);
            }

            if (_frameHistory.Count > 0)
            {
                var lastFrame = _frameHistory.Last();
                report.LastFrameGPUMemoryUsed = lastFrame.GPUMemoryUsed;
                report.LastFrameGPUMemoryAllocated = lastFrame.GPUMemoryAllocated;
                report.LastFrameCPUMemoryUsed = lastFrame.CPUMemoryUsed;
            }

            return report;
        }

        /// <summary>
        /// 清空历史记录
        /// </summary>
        public void Clear()
        {
            _frameHistory.Clear();
        }

        /// <summary>
        /// 生成详细的性能分析日志
        /// </summary>
        public void LogDetailedAnalysis(string title = "渲染性能分析")
        {
            var report = GetReport();

            EnderLogger.Instance.Info("PerformanceAnalysis", $"=== {title} ===");
            EnderLogger.Instance.Info("PerformanceAnalysis", $"记录帧数: {report.FrameCount}");
            EnderLogger.Instance.Info("PerformanceAnalysis", $"平均帧时间: {report.AverageFrameTime:F2}ms");
            EnderLogger.Instance.Info("PerformanceAnalysis", $"平均FPS: {report.AverageFPS:F1}");
            EnderLogger.Instance.Info("PerformanceAnalysis", $"最小/最大帧时间: {report.MinFrameTime:F2}ms / {report.MaxFrameTime:F2}ms");
            EnderLogger.Instance.Info("PerformanceAnalysis", $"P95/P99帧时间: {report.P95FrameTime:F2}ms / {report.P99FrameTime:F2}ms");
            EnderLogger.Instance.Info("PerformanceAnalysis", $"平均批次数: {report.AverageBatchCount:F0}");
            EnderLogger.Instance.Info("PerformanceAnalysis", $"平均音符数: {report.AverageNoteCount:F0}");

            foreach (var kvp in report.StageAverageTimes)
            {
                EnderLogger.Instance.Info("PerformanceAnalysis", $"  {kvp.Key}: {kvp.Value:F3}ms");
            }

            EnderLogger.Instance.Info("PerformanceAnalysis", $"GPU内存: {report.LastFrameGPUMemoryUsed / 1024.0 / 1024.0:F2}MB / {report.LastFrameGPUMemoryAllocated / 1024.0 / 1024.0:F2}MB");
            EnderLogger.Instance.Info("PerformanceAnalysis", $"CPU内存: {report.LastFrameCPUMemoryUsed / 1024.0 / 1024.0:F2}MB");
        }

        public void Dispose()
        {
            _frameHistory.Clear();
            if (_frameTimer != null)
            {
                _frameTimer.Stop();
            }
        }
    }

    /// <summary>
    /// 单帧性能指标
    /// </summary>
    public class FrameMetrics
    {
        public DateTime Timestamp { get; set; }
        public double TotalTimeMs { get; set; }
        public Dictionary<string, double> StageTimes { get; set; } = new();
        public int BatchCount { get; set; }
        public int NoteCount { get; set; }
        public long GPUMemoryUsed { get; set; }
        public long GPUMemoryAllocated { get; set; }
        public long CPUMemoryUsed { get; set; }
    }

    /// <summary>
    /// 性能报告
    /// </summary>
    public class PerformanceReport
    {
        public int FrameCount { get; set; }
        public double AverageFrameTime { get; set; }
        public double AverageFPS { get; set; }
        public double MinFrameTime { get; set; }
        public double MaxFrameTime { get; set; }
        public double P95FrameTime { get; set; }
        public double P99FrameTime { get; set; }
        public double AverageBatchCount { get; set; }
        public double AverageNoteCount { get; set; }
        public Dictionary<string, double> StageAverageTimes { get; set; } = new();
        public long LastFrameGPUMemoryUsed { get; set; }
        public long LastFrameGPUMemoryAllocated { get; set; }
        public long LastFrameCPUMemoryUsed { get; set; }

        public override string ToString()
        {
            return $"FPS: {AverageFPS:F1}, 平均帧时间: {AverageFrameTime:F2}ms, 音符: {AverageNoteCount:F0}, 批次: {AverageBatchCount:F0}";
        }
    }

    /// <summary>
    /// 渲染优化建议引擎
    /// </summary>
    public class RenderOptimizationAdvisor
    {
        private readonly RenderPerformanceMonitor _monitor;
        private readonly double _targetFPS;

        public RenderOptimizationAdvisor(RenderPerformanceMonitor monitor, double targetFPS = 60.0)
        {
            _monitor = monitor;
            _targetFPS = targetFPS;
        }

        /// <summary>
        /// 获取优化建议
        /// </summary>
        public List<OptimizationSuggestion> GetOptimizationSuggestions()
        {
            var suggestions = new List<OptimizationSuggestion>();
            var report = _monitor.GetReport();
            var targetFrameTime = 1000.0 / _targetFPS;

            // 检查平均帧时间
            if (report.AverageFrameTime > targetFrameTime * 1.2)
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    Severity = SeverityLevel.Warning,
                    Title = "平均帧时间过长",
                    Description = $"平均帧时间 {report.AverageFrameTime:F2}ms 超过目标 {targetFrameTime:F2}ms",
                    Recommendations = new[]
                    {
                        "减少每帧渲染的音符数量",
                        "增加批处理大小以提高效率",
                        "检查是否有内存泄漏",
                        "优化着色器性能"
                    }
                });
            }

            // 检查帧时间方差
            var variance = report.MaxFrameTime - report.MinFrameTime;
            if (variance > targetFrameTime * 0.5)
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    Severity = SeverityLevel.Caution,
                    Title = "帧时间波动较大",
                    Description = $"帧时间从 {report.MinFrameTime:F2}ms 到 {report.MaxFrameTime:F2}ms",
                    Recommendations = new[]
                    {
                        "检查是否存在峰值负载",
                        "使用时间切片分散计算任务",
                        "预加载资源以避免运行时加载"
                    }
                });
            }

            // 检查P99性能
            if (report.P99FrameTime > targetFrameTime * 2)
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    Severity = SeverityLevel.Warning,
                    Title = "P99帧时间过长（卡顿风险）",
                    Description = $"99%的帧超过 {report.P99FrameTime:F2}ms",
                    Recommendations = new[]
                    {
                        "使用帧率上限避免GPU过载",
                        "减少复杂渲染操作",
                        "考虑使用更简化的渲染路径"
                    }
                });
            }

            // 检查批次数
            if (report.AverageBatchCount > 1000)
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    Severity = SeverityLevel.Warning,
                    Title = "批次数过多",
                    Description = $"平均批次数 {report.AverageBatchCount:F0}",
                    Recommendations = new[]
                    {
                        "合并相似的渲染对象",
                        "增加单个批次的对象数量上限",
                        "使用更高效的数据结构"
                    }
                });
            }

            // 检查内存使用
            var gpuMemoryMB = report.LastFrameGPUMemoryUsed / 1024.0 / 1024.0;
            if (gpuMemoryMB > 1000)
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    Severity = SeverityLevel.Caution,
                    Title = "GPU内存使用过高",
                    Description = $"当前使用 {gpuMemoryMB:F2}MB",
                    Recommendations = new[]
                    {
                        "使用纹理压缩",
                        "实现LOD系统",
                        "清理未使用的资源"
                    }
                });
            }

            return suggestions;
        }

        /// <summary>
        /// 生成优化报告
        /// </summary>
        public void GenerateOptimizationReport()
        {
            var suggestions = GetOptimizationSuggestions();

            EnderLogger.Instance.Info("RenderOptimization", "=== 渲染优化建议 ===");

            if (suggestions.Count == 0)
            {
                EnderLogger.Instance.Info("RenderOptimization", "✓ 性能良好，无优化建议");
                return;
            }

            foreach (var suggestion in suggestions)
            {
                var severityLabel = suggestion.Severity switch
                {
                    SeverityLevel.Info => "ℹ",
                    SeverityLevel.Caution => "⚠",
                    SeverityLevel.Warning => "❌",
                    _ => "?"
                };

                EnderLogger.Instance.Info("RenderOptimization", $"{severityLabel} {suggestion.Title}");
                EnderLogger.Instance.Info("RenderOptimization", $"  说明: {suggestion.Description}");
                foreach (var rec in suggestion.Recommendations)
                {
                    EnderLogger.Instance.Info("RenderOptimization", $"  - {rec}");
                }
            }
        }
    }

    /// <summary>
    /// 优化建议
    /// </summary>
    public class OptimizationSuggestion
    {
        public SeverityLevel Severity { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string[] Recommendations { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// 严重级别
    /// </summary>
    public enum SeverityLevel
    {
        Info,
        Caution,
        Warning,
        Critical
    }
}
