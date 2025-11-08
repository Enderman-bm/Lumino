using System;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Linq;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// 轻量级性能监控工具 - 用于测量和记录关键渲染路径的耗时
    /// </summary>
    public static class PerformanceMonitor
    {
        private static readonly ConcurrentDictionary<string, PerformanceMetrics> _metrics = new();
        private static readonly Stopwatch _globalStopwatch = Stopwatch.StartNew();
        
        public static bool Enabled { get; set; } = false;

        public class PerformanceMetrics
        {
            public string Name { get; set; } = "";
            public long TotalCalls { get; set; }
            public double TotalMilliseconds { get; set; }
            public double MinMilliseconds { get; set; } = double.MaxValue;
            public double MaxMilliseconds { get; set; }
            public double AverageMilliseconds => TotalCalls > 0 ? TotalMilliseconds / TotalCalls : 0;
        }

        public static IDisposable Measure(string category)
        {
            if (!Enabled) return new NoOpDisposable();
            return new MeasurementScope(category);
        }

        private class MeasurementScope : IDisposable
        {
            private readonly string _category;
            private readonly Stopwatch _sw;

            public MeasurementScope(string category)
            {
                _category = category;
                _sw = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _sw.Stop();
                var elapsed = _sw.Elapsed.TotalMilliseconds;
                
                _metrics.AddOrUpdate(_category,
                    _ => new PerformanceMetrics
                    {
                        Name = _category,
                        TotalCalls = 1,
                        TotalMilliseconds = elapsed,
                        MinMilliseconds = elapsed,
                        MaxMilliseconds = elapsed
                    },
                    (_, existing) =>
                    {
                        existing.TotalCalls++;
                        existing.TotalMilliseconds += elapsed;
                        existing.MinMilliseconds = Math.Min(existing.MinMilliseconds, elapsed);
                        existing.MaxMilliseconds = Math.Max(existing.MaxMilliseconds, elapsed);
                        return existing;
                    });
            }
        }

        private class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }

        public static void LogSummary()
        {
            if (!Enabled || _metrics.IsEmpty) return;

            Debug.WriteLine("=== Performance Summary ===");
            foreach (var kvp in _metrics.OrderByDescending(x => x.Value.TotalMilliseconds))
            {
                var m = kvp.Value;
                Debug.WriteLine($"[PERF] {m.Name}: Calls={m.TotalCalls}, Avg={m.AverageMilliseconds:F2}ms, Min={m.MinMilliseconds:F2}ms, Max={m.MaxMilliseconds:F2}ms, Total={m.TotalMilliseconds:F0}ms");
            }
            Debug.WriteLine($"[PERF] GC Gen0={GC.CollectionCount(0)}, Gen1={GC.CollectionCount(1)}, Gen2={GC.CollectionCount(2)}");
        }

        public static void Reset()
        {
            _metrics.Clear();
            GC.Collect(2, GCCollectionMode.Forced, false, true);
            GC.WaitForPendingFinalizers();
            Debug.WriteLine("[PERF] Reset complete - GC forced");
        }
    }
}
