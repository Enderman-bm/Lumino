using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DominoNext.ViewModels.Editor;
using DominoNext.Models.Music;
using System.Collections.Concurrent;
using System.Threading;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// 性能优化服务 - 提供高性能的批量操作和缓存管理
    /// </summary>
    public class PerformanceOptimizationService
    {
        // 缓存相关
        private static readonly ConcurrentDictionary<string, object> _globalCache = new();
        private static Timer? _cacheCleanupTimer;
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);
        
        static PerformanceOptimizationService()
        {
            // 定期清理缓存
            _cacheCleanupTimer = new Timer(CleanupCache, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// 高性能批量量化操作
        /// </summary>
        public static async Task<List<(NoteViewModel note, MusicalFraction newPosition)>> QuantizeNotesAsync(
            IEnumerable<NoteViewModel> notes, 
            MusicalFraction gridUnit, 
            int ticksPerBeat)
        {
            var notesList = notes.ToList();
            if (notesList.Count == 0) return new List<(NoteViewModel, MusicalFraction)>();

            return await Task.Run(() =>
            {
                var results = new List<(NoteViewModel note, MusicalFraction newPosition)>();
                
                // 批量提取位置
                var positions = new double[notesList.Count];
                for (int i = 0; i < notesList.Count; i++)
                {
                    positions[i] = notesList[i].StartPosition.ToTicks(ticksPerBeat);
                }

                // 使用高性能批量量化
                MusicalFraction.QuantizeToGridBatch(positions, gridUnit, ticksPerBeat);

                // 转换回 MusicalFraction
                for (int i = 0; i < notesList.Count; i++)
                {
                    try
                    {
                        var newPosition = MusicalFraction.FromTicks(positions[i], ticksPerBeat);
                        results.Add((notesList[i], newPosition));
                    }
                    catch
                    {
                        // 跳过错误的音符
                    }
                }

                return results;
            });
        }

        /// <summary>
        /// 高性能批量碰撞检测
        /// </summary>
        public static async Task<List<(NoteViewModel note, bool intersects)>> DetectCollisionsAsync(
            IEnumerable<NoteViewModel> notes,
            Avalonia.Rect area,
            Func<NoteViewModel, Avalonia.Rect> getRectFunc)
        {
            var notesList = notes.ToList();
            if (notesList.Count == 0) return new List<(NoteViewModel, bool)>();

            return await Task.Run(() =>
            {
                var results = new List<(NoteViewModel note, bool intersects)>();
                
                // 使用并行处理提升大量音符的碰撞检测性能
                var partitioner = Partitioner.Create(notesList, true);
                var parallelResults = new ConcurrentBag<(NoteViewModel, bool)>();

                Parallel.ForEach(partitioner, note =>
                {
                    try
                    {
                        var noteRect = getRectFunc(note);
                        var intersects = area.Intersects(noteRect);
                        parallelResults.Add((note, intersects));
                    }
                    catch
                    {
                        // 跳过错误的音符
                        parallelResults.Add((note, false));
                    }
                });

                results.AddRange(parallelResults);
                return results;
            });
        }

        /// <summary>
        /// 高性能批量音符创建
        /// </summary>
        public static async Task<List<NoteViewModel>> CreateNotesAsync(IEnumerable<(int pitch, MusicalFraction start, MusicalFraction duration, int velocity)> noteData)
        {
            var dataList = noteData.ToList();
            if (dataList.Count == 0) return new List<NoteViewModel>();

            return await Task.Run(() =>
            {
                var notes = new List<NoteViewModel>(dataList.Count);
                
                foreach (var (pitch, start, duration, velocity) in dataList)
                {
                    try
                    {
                        var note = new NoteViewModel
                        {
                            Pitch = pitch,
                            StartPosition = start,
                            Duration = duration,
                            Velocity = velocity
                        };
                        notes.Add(note);
                    }
                    catch
                    {
                        // 跳过错误的音符数据
                    }
                }

                return notes;
            });
        }

        /// <summary>
        /// 批量更新音符属性
        /// </summary>
        public static void BatchUpdateNotes(IEnumerable<NoteViewModel> notes, Action<NoteViewModel> updateAction)
        {
            var notesList = notes.ToList();
            if (notesList.Count == 0) return;

            // 对于大量音符使用并行处理
            if (notesList.Count > 100)
            {
                Parallel.ForEach(notesList, updateAction);
            }
            else
            {
                foreach (var note in notesList)
                {
                    updateAction(note);
                }
            }
        }

        /// <summary>
        /// 缓存获取或计算值
        /// </summary>
        public static T GetOrCompute<T>(string key, Func<T> computeFunc, TimeSpan? expiry = null)
        {
            var cacheKey = $"{key}_{typeof(T).Name}";
            
            if (_globalCache.TryGetValue(cacheKey, out var cached) && cached is CacheItem<T> item && !item.IsExpired)
            {
                return item.Value;
            }

            var value = computeFunc();
            var cacheItem = new CacheItem<T>(value, expiry ?? CacheExpiry);
            _globalCache.AddOrUpdate(cacheKey, cacheItem, (k, v) => cacheItem);
            
            return value;
        }

        /// <summary>
        /// 清除特定前缀的缓存
        /// </summary>
        public static void ClearCache(string? prefix = null)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                _globalCache.Clear();
                return;
            }

            var keysToRemove = _globalCache.Keys.Where(k => k.StartsWith(prefix)).ToList();
            foreach (var key in keysToRemove)
            {
                _globalCache.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// 定期清理过期缓存
        /// </summary>
        private static void CleanupCache(object? state)
        {
            var keysToRemove = new List<string>();
            
            foreach (var kvp in _globalCache)
            {
                if (kvp.Value is ICacheItem item && item.IsExpired)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _globalCache.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// 高性能范围查询 - 查找指定时间范围内的音符
        /// </summary>
        public static IEnumerable<NoteViewModel> FindNotesInTimeRange(
            IEnumerable<NoteViewModel> notes,
            double startTicks,
            double endTicks,
            int ticksPerBeat)
        {
            return notes.Where(note =>
            {
                var noteStart = note.StartPosition.ToTicks(ticksPerBeat);
                var noteEnd = noteStart + note.Duration.ToTicks(ticksPerBeat);
                return noteEnd >= startTicks && noteStart <= endTicks;
            });
        }

        /// <summary>
        /// 高性能范围查询 - 查找指定音高范围内的音符
        /// </summary>
        public static IEnumerable<NoteViewModel> FindNotesInPitchRange(
            IEnumerable<NoteViewModel> notes,
            int minPitch,
            int maxPitch)
        {
            return notes.Where(note => note.Pitch >= minPitch && note.Pitch <= maxPitch);
        }

        #region 缓存项类型定义
        
        private interface ICacheItem
        {
            bool IsExpired { get; }
        }

        private class CacheItem<T> : ICacheItem
        {
            public T Value { get; }
            public DateTime ExpiryTime { get; }
            public bool IsExpired => DateTime.UtcNow > ExpiryTime;

            public CacheItem(T value, TimeSpan expiry)
            {
                Value = value;
                ExpiryTime = DateTime.UtcNow.Add(expiry);
            }
        }

        #endregion

        /// <summary>
        /// 清理资源
        /// </summary>
        public static void Cleanup()
        {
            _cacheCleanupTimer?.Dispose();
            _cacheCleanupTimer = null;
            _globalCache.Clear();
            MusicalFraction.ClearCache();
        }
    }
}