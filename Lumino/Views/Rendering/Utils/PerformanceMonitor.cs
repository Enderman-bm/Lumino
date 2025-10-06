using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Lumino.Views.Rendering.Utils
{
    /// <summary>
    /// 性能监控器 - 用于跟踪渲染和计算性能
    /// </summary>
    public class PerformanceMonitor : IDisposable
    {
        private readonly ConcurrentDictionary<string, PerformanceCounter> _counters;
        private readonly ConcurrentQueue<PerformanceRecord> _recentRecords;
    private readonly Timer _cleanupTimer;
        private bool _disposed;

        public PerformanceMonitor()
        {
            _counters = new ConcurrentDictionary<string, PerformanceCounter>();
            _recentRecords = new ConcurrentQueue<PerformanceRecord>();
            _cleanupTimer = new Timer(state => CleanupOldRecords(state!), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// 开始性能测量
        /// </summary>
        public IDisposable BeginMeasure(string operationName)
        {
            return new PerformanceMeasure(this, operationName);
        }

        /// <summary>
        /// 记录性能数据
        /// </summary>
        public void RecordPerformance(string operationName, double elapsedMilliseconds)
        {
            var counter = _counters.GetOrAdd(operationName, _ => new PerformanceCounter());
            counter.Record(elapsedMilliseconds);

            var record = new PerformanceRecord
            {
                OperationName = operationName,
                ElapsedMilliseconds = elapsedMilliseconds,
                Timestamp = DateTime.Now
            };

            _recentRecords.Enqueue(record);
        }

        /// <summary>
        /// 获取性能统计信息
        /// </summary>
        public PerformanceStatistics GetStatistics(string operationName)
        {
            if (_counters.TryGetValue(operationName, out var counter))
            {
                return counter.GetStatistics();
            }
            return new PerformanceStatistics();
        }

        /// <summary>
        /// 获取平均渲染时间
        /// </summary>
        public double GetAverageRenderTime()
        {
            const string RENDER_OPERATION = "NoteRender";
            if (_counters.TryGetValue(RENDER_OPERATION, out var counter))
            {
                var stats = counter.GetStatistics();
                return stats.AverageTime;
            }
            return 0.0;
        }

        /// <summary>
        /// 获取所有操作的统计信息
        /// </summary>
        public ConcurrentDictionary<string, PerformanceStatistics> GetAllStatistics()
        {
            var result = new ConcurrentDictionary<string, PerformanceStatistics>();
            foreach (var kvp in _counters)
            {
                result[kvp.Key] = kvp.Value.GetStatistics();
            }
            return result;
        }

        /// <summary>
        /// 获取最近的操作记录
        /// </summary>
        public PerformanceRecord[] GetRecentRecords(int count = 100)
        {
            return _recentRecords
                .OrderByDescending(r => r.Timestamp)
                .Take(count)
                .ToArray();
        }

        /// <summary>
        /// 清理旧记录
        /// </summary>
        private void CleanupOldRecords(object state)
        {
            var cutoffTime = DateTime.Now.AddHours(-1);
            var oldRecords = _recentRecords.Where(r => r.Timestamp < cutoffTime).ToArray();
            
            foreach (var record in oldRecords)
            {
                _recentRecords.TryDequeue(out _);
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();
                _counters.Clear();
                _recentRecords.Clear();
                _disposed = true;
            }
        }

        /// <summary>
        /// 性能测量实现
        /// </summary>
        private class PerformanceMeasure : IDisposable
        {
            private readonly PerformanceMonitor _monitor;
            private readonly string _operationName;
            private readonly Stopwatch _stopwatch;

            public PerformanceMeasure(PerformanceMonitor monitor, string operationName)
            {
                _monitor = monitor;
                _operationName = operationName;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                _monitor.RecordPerformance(_operationName, _stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// 性能计数器
        /// </summary>
        private class PerformanceCounter
        {
            private double _totalTime;
            private long _count;
            private double _minTime = double.MaxValue;
            private double _maxTime = double.MinValue;
            private readonly ConcurrentQueue<double> _recentTimes;

            public PerformanceCounter()
            {
                _recentTimes = new ConcurrentQueue<double>();
            }

            public void Record(double elapsedMilliseconds)
            {
                _totalTime += elapsedMilliseconds;
                _count++;
                
                if (elapsedMilliseconds < _minTime) _minTime = elapsedMilliseconds;
                if (elapsedMilliseconds > _maxTime) _maxTime = elapsedMilliseconds;

                _recentTimes.Enqueue(elapsedMilliseconds);
                
                // 保持最近1000次记录
                if (_recentTimes.Count > 1000)
                {
                    _recentTimes.TryDequeue(out _);
                }
            }

            public PerformanceStatistics GetStatistics()
            {
                if (_count == 0)
                {
                    return new PerformanceStatistics();
                }

                var recentArray = _recentTimes.ToArray();
                var recentAverage = recentArray.Length > 0 ? recentArray.Average() : 0;

                return new PerformanceStatistics
                {
                    TotalOperations = _count,
                    TotalTime = _totalTime,
                    AverageTime = _totalTime / _count,
                    MinTime = _minTime == double.MaxValue ? 0 : _minTime,
                    MaxTime = _maxTime == double.MinValue ? 0 : _maxTime,
                    RecentAverageTime = recentAverage
                };
            }
        }
    }

    /// <summary>
    /// 性能记录
    /// </summary>
    public class PerformanceRecord
    {
    public required string OperationName { get; set; }
        public double ElapsedMilliseconds { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 性能统计信息
    /// </summary>
    public class PerformanceStatistics
    {
        public long TotalOperations { get; set; }
        public double TotalTime { get; set; }
        public double AverageTime { get; set; }
        public double MinTime { get; set; }
        public double MaxTime { get; set; }
        public double RecentAverageTime { get; set; }
    }
}