using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using LuminoWaveTable.Interfaces;
using EnderDebugger;

namespace LuminoWaveTable.Core
{
    /// <summary>
    /// 性能监控器
    /// </summary>
    public class PerformanceMonitor
    {
        private readonly EnderLogger _logger;
        private readonly PerformanceCounter? _cpuCounter;
        private readonly Process _currentProcess;
        
        // 性能统计
        private long _totalMessagesSent;
        private long _totalNotesPlayed;
        private long _totalErrors;
        private DateTime _startTime;
        private readonly Queue<LatencySample> _latencySamples;
        private readonly Queue<CpuSample> _cpuSamples;
        private readonly object _statsLock = new object();
        
        // 配置
        private const int MaxLatencySamples = 100;
        private const int MaxCpuSamples = 60;
        private const double HighLatencyThresholdMs = 50.0;
        private const double HighCpuThreshold = 80.0;
        
        // 当前状态
        private int _activeVoices;
        private int _maxVoices = 128;
        private double _currentLatencyMs;
        private double _currentCpuUsage;
        private long _currentMemoryUsage;
        private bool _isOptimized = true;

        public PerformanceMonitor()
        {
            _logger = EnderLogger.Instance;
            _currentProcess = Process.GetCurrentProcess();
            _latencySamples = new Queue<LatencySample>();
            _cpuSamples = new Queue<CpuSample>();
            _startTime = DateTime.Now;
            _cpuCounter = null!;
            
            try
            {
                // 初始化CPU性能计数器
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _logger.Info("PerformanceMonitor", "性能监控器初始化完成");
            }
            catch (Exception ex)
            {
                _logger.Error("PerformanceMonitor", $"初始化CPU性能计数器失败: {ex.Message}");
                _cpuCounter = null;
            }
            
            ResetStats();
        }

        /// <summary>
        /// 记录消息发送
        /// </summary>
        public void RecordMessageSent()
        {
            lock (_statsLock)
            {
                _totalMessagesSent++;
            }
        }

        /// <summary>
        /// 记录音符播放
        /// </summary>
        public void RecordNotePlayed()
        {
            lock (_statsLock)
            {
                _totalNotesPlayed++;
                _activeVoices = Math.Min(_activeVoices + 1, _maxVoices);
            }
        }

        /// <summary>
        /// 记录音符停止
        /// </summary>
        public void RecordNoteStopped()
        {
            lock (_statsLock)
            {
                _activeVoices = Math.Max(_activeVoices - 1, 0);
            }
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        public void RecordError()
        {
            lock (_statsLock)
            {
                _totalErrors++;
            }
        }

        /// <summary>
        /// 记录延迟样本
        /// </summary>
        public void RecordLatencySample(double latencyMs)
        {
            lock (_statsLock)
            {
                var sample = new LatencySample
                {
                    Timestamp = DateTime.Now,
                    LatencyMs = latencyMs
                };
                
                _latencySamples.Enqueue(sample);
                if (_latencySamples.Count > MaxLatencySamples)
                {
                    _latencySamples.Dequeue();
                }
                
                UpdateCurrentLatency();
            }
        }

        /// <summary>
        /// 更新性能统计
        /// </summary>
        public void UpdatePerformanceStats()
        {
            try
            {
                lock (_statsLock)
                {
                    // 更新CPU使用率
                    UpdateCpuUsage();
                    
                    // 更新内存使用
                    UpdateMemoryUsage();
                    
                    // 检查性能状态
                    CheckPerformanceStatus();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("PerformanceMonitor", $"更新性能统计失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前性能信息
        /// </summary>
        public WaveTablePerformanceInfo GetCurrentPerformance()
        {
            lock (_statsLock)
            {
                return new WaveTablePerformanceInfo
                {
                    CpuUsage = _currentCpuUsage,
                    MemoryUsage = _currentMemoryUsage,
                    ActiveVoices = _activeVoices,
                    MaxVoices = _maxVoices,
                    LatencyMs = _currentLatencyMs,
                    IsOptimized = _isOptimized
                };
            }
        }

        /// <summary>
        /// 获取性能统计
        /// </summary>
        public PerformanceStatistics GetPerformanceStatistics()
        {
            lock (_statsLock)
            {
                var now = DateTime.Now;
                var uptime = now - _startTime;
                
                return new PerformanceStatistics
                {
                    TotalMessagesSent = _totalMessagesSent,
                    TotalNotesPlayed = _totalNotesPlayed,
                    TotalErrors = _totalErrors,
                    AverageLatencyMs = _latencySamples.Count > 0 ? _latencySamples.Average(s => s.LatencyMs) : 0,
                    MaxLatencyMs = _latencySamples.Count > 0 ? _latencySamples.Max(s => s.LatencyMs) : 0,
                    MinLatencyMs = _latencySamples.Count > 0 ? _latencySamples.Min(s => s.LatencyMs) : 0,
                    AverageCpuUsage = _cpuSamples.Count > 0 ? _cpuSamples.Average(s => s.CpuUsage) : 0,
                    MaxCpuUsage = _cpuSamples.Count > 0 ? _cpuSamples.Max(s => s.CpuUsage) : 0,
                    CurrentMemoryUsage = _currentMemoryUsage,
                    PeakMemoryUsage = GetPeakMemoryUsage(),
                    Uptime = uptime,
                    MessagesPerSecond = uptime.TotalSeconds > 0 ? _totalMessagesSent / uptime.TotalSeconds : 0,
                    NotesPerSecond = uptime.TotalSeconds > 0 ? _totalNotesPlayed / uptime.TotalSeconds : 0,
                    ErrorRate = _totalMessagesSent > 0 ? (double)_totalErrors / _totalMessagesSent * 100 : 0
                };
            }
        }

        /// <summary>
        /// 优化性能
        /// </summary>
        public void OptimizePerformance()
        {
            try
            {
                _logger.Info("PerformanceMonitor", "开始性能优化...");
                
                // 设置进程优先级
                try
                {
                    _currentProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
                    _logger.Debug("PerformanceMonitor", "进程优先级已设置为 AboveNormal");
                }
                catch (Exception ex)
                {
                    _logger.Warn("PerformanceMonitor", $"设置进程优先级失败: {ex.Message}");
                }
                
                // 优化内存使用
                try
                {
                    _currentProcess.MaxWorkingSet = new IntPtr(256 * 1024 * 1024); // 256MB
                    _currentProcess.MinWorkingSet = new IntPtr(64 * 1024 * 1024);  // 64MB
                    _logger.Debug("PerformanceMonitor", "工作集大小已优化");
                }
                catch (Exception ex)
                {
                    _logger.Warn("PerformanceMonitor", $"优化工作集大小失败: {ex.Message}");
                }
                
                // 强制垃圾回收
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                _isOptimized = true;
                _logger.Info("PerformanceMonitor", "性能优化完成");
            }
            catch (Exception ex)
            {
                _logger.Error("PerformanceMonitor", $"性能优化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置统计信息
        /// </summary>
        public void ResetStats()
        {
            lock (_statsLock)
            {
                _totalMessagesSent = 0;
                _totalNotesPlayed = 0;
                _totalErrors = 0;
                _activeVoices = 0;
                _currentLatencyMs = 0;
                _currentCpuUsage = 0;
                _currentMemoryUsage = 0;
                _latencySamples.Clear();
                _cpuSamples.Clear();
                _startTime = DateTime.Now;
                _isOptimized = true;
                
                _logger.Info("PerformanceMonitor", "性能统计已重置");
            }
        }

        /// <summary>
        /// 设置最大复音数
        /// </summary>
        public void SetMaxVoices(int maxVoices)
        {
            if (maxVoices > 0 && maxVoices <= 256)
            {
                _maxVoices = maxVoices;
                _logger.Debug("PerformanceMonitor", $"最大复音数设置为: {maxVoices}");
            }
        }

        /// <summary>
        /// 检查是否为高性能状态
        /// </summary>
        public bool IsHighPerformance()
        {
            lock (_statsLock)
            {
                return _isOptimized && 
                       _currentCpuUsage < HighCpuThreshold && 
                       _currentLatencyMs < HighLatencyThresholdMs;
            }
        }

        #region 私有方法

        /// <summary>
        /// 更新CPU使用率
        /// </summary>
        private void UpdateCpuUsage()
        {
            try
            {
                double cpuUsage = 0;
                
                if (_cpuCounter != null)
                {
                    cpuUsage = _cpuCounter.NextValue();
                }
                else
                {
                    // 备用方法：使用进程CPU使用率
                    var currentTime = DateTime.Now;
                    var processCpuTime = _currentProcess.TotalProcessorTime;
                    
                    if (_cpuSamples.Count > 0)
                    {
                        var lastSample = _cpuSamples.Last();
                        var timeDiff = (currentTime - lastSample.Timestamp).TotalMilliseconds;
                        var cpuTimeDiff = (processCpuTime - lastSample.ProcessCpuTime).TotalMilliseconds;
                        
                        if (timeDiff > 0)
                        {
                            cpuUsage = (cpuTimeDiff / timeDiff) * 100.0 * Environment.ProcessorCount;
                        }
                    }
                }
                
                // 添加到样本队列
                var sample = new CpuSample
                {
                    Timestamp = DateTime.Now,
                    CpuUsage = cpuUsage,
                    ProcessCpuTime = _currentProcess.TotalProcessorTime
                };
                
                _cpuSamples.Enqueue(sample);
                if (_cpuSamples.Count > MaxCpuSamples)
                {
                    _cpuSamples.Dequeue();
                }
                
                // 计算当前CPU使用率（最近10秒的平均值）
                var recentSamples = _cpuSamples.Where(s => s.Timestamp > DateTime.Now.AddSeconds(-10)).ToList();
                _currentCpuUsage = recentSamples.Count > 0 ? recentSamples.Average(s => s.CpuUsage) : 0;
            }
            catch (Exception ex)
            {
                _logger.Error("PerformanceMonitor", $"更新CPU使用率失败: {ex.Message}");
                _currentCpuUsage = 0;
            }
        }

        /// <summary>
        /// 更新内存使用
        /// </summary>
        private void UpdateMemoryUsage()
        {
            try
            {
                _currentProcess.Refresh();
                _currentMemoryUsage = _currentProcess.WorkingSet64;
            }
            catch (Exception ex)
            {
                _logger.Error("PerformanceMonitor", $"更新内存使用失败: {ex.Message}");
                _currentMemoryUsage = 0;
            }
        }

        /// <summary>
        /// 更新当前延迟
        /// </summary>
        private void UpdateCurrentLatency()
        {
            try
            {
                // 计算最近10秒的平均延迟
                var recentSamples = _latencySamples.Where(s => s.Timestamp > DateTime.Now.AddSeconds(-10)).ToList();
                _currentLatencyMs = recentSamples.Count > 0 ? recentSamples.Average(s => s.LatencyMs) : 0;
            }
            catch (Exception ex)
            {
                _logger.Error("PerformanceMonitor", $"更新当前延迟失败: {ex.Message}");
                _currentLatencyMs = 0;
            }
        }

        /// <summary>
        /// 检查性能状态
        /// </summary>
        private void CheckPerformanceStatus()
        {
            try
            {
                var previousOptimized = _isOptimized;
                
                // 检查高延迟
                if (_currentLatencyMs > HighLatencyThresholdMs)
                {
                    _isOptimized = false;
                    _logger.Warn("PerformanceMonitor", $"检测到高延迟: {_currentLatencyMs:F2}ms");
                }
                
                // 检查高CPU使用率
                if (_currentCpuUsage > HighCpuThreshold)
                {
                    _isOptimized = false;
                    _logger.Warn("PerformanceMonitor", $"检测到高CPU使用率: {_currentCpuUsage:F1}%");
                }
                
                // 检查内存使用
                var memoryMB = _currentMemoryUsage / (1024.0 * 1024.0);
                if (memoryMB > 500) // 500MB
                {
                    _logger.Warn("PerformanceMonitor", $"检测到高内存使用: {memoryMB:F1}MB");
                }
                
                // 如果性能恢复，重新标记为优化状态
                if (_currentLatencyMs < HighLatencyThresholdMs * 0.5 && 
                    _currentCpuUsage < HighCpuThreshold * 0.5)
                {
                    _isOptimized = true;
                }
                
                if (previousOptimized != _isOptimized)
                {
                    _logger.Info("PerformanceMonitor", $"性能状态变更: {( _isOptimized ? "优化" : "未优化" )}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("PerformanceMonitor", $"检查性能状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取峰值内存使用
        /// </summary>
        private long GetPeakMemoryUsage()
        {
            try
            {
                return _currentProcess.PeakWorkingSet64;
            }
            catch
            {
                return _currentMemoryUsage;
            }
        }

        #endregion

        #region 内部类

        private class LatencySample
        {
            public DateTime Timestamp { get; set; }
            public double LatencyMs { get; set; }
        }

        private class CpuSample
        {
            public DateTime Timestamp { get; set; }
            public double CpuUsage { get; set; }
            public TimeSpan ProcessCpuTime { get; set; }
        }

        #endregion
    }

    /// <summary>
    /// 性能统计信息
    /// </summary>
    public class PerformanceStatistics
    {
        public long TotalMessagesSent { get; set; }
        public long TotalNotesPlayed { get; set; }
        public long TotalErrors { get; set; }
        public double AverageLatencyMs { get; set; }
        public double MaxLatencyMs { get; set; }
        public double MinLatencyMs { get; set; }
        public double AverageCpuUsage { get; set; }
        public double MaxCpuUsage { get; set; }
        public long CurrentMemoryUsage { get; set; }
        public long PeakMemoryUsage { get; set; }
        public TimeSpan Uptime { get; set; }
        public double MessagesPerSecond { get; set; }
        public double NotesPerSecond { get; set; }
        public double ErrorRate { get; set; }
    }
}