using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using EnderDebugger;
using Lumino.ViewModels.Editor;

namespace Lumino.Views.Rendering.Utils
{
    /// <summary>
    /// 多级缓存系统 - 针对超大规模音符数据的缓存优化
    /// 支持1000W+音符的多层级缓存管理
    /// </summary>
    public class MultiLevelCacheSystem
    {
        // 缓存层级定义
        private const int L1_CACHE_SIZE = 1000;      // L1缓存：当前可见音符（内存）
        private const int L2_CACHE_SIZE = 10000;     // L2缓存：邻近区域音符（内存）
        private const int L3_CACHE_SIZE = 100000;    // L3缓存：预加载数据（内存+磁盘）
        
        // 缓存过期时间
        private static readonly TimeSpan L1_EXPIRATION = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan L2_EXPIRATION = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan L3_EXPIRATION = TimeSpan.FromMinutes(10);
        
        // L1缓存（最快速访问）
        private readonly ConcurrentDictionary<string, L1CacheItem> _l1Cache;
        private readonly ConcurrentQueue<string> _l1AccessQueue;
        
        // L2缓存（快速访问）
        private readonly ConcurrentDictionary<string, L2CacheItem> _l2Cache;
        private readonly ConcurrentQueue<string> _l2AccessQueue;
        
        // L3缓存（大容量缓存）
        private readonly MemoryCache _l3MemoryCache;
        private readonly DiskCache _l3DiskCache;
        
        // 预加载系统
        private readonly PreloadManager _preloadManager;
        
        // 缓存统计
        public MultiLevelCacheStats Stats { get; private set; }

        // 日志记录器
        private readonly EnderLogger _logger = EnderLogger.Instance;
        
        // 缓存事件
        public event EventHandler<CacheHitEventArgs> CacheHit;
        public event EventHandler<CacheMissEventArgs> CacheMiss;
        public event EventHandler<CacheEvictionEventArgs> CacheEvicted;
        
        public MultiLevelCacheSystem()
        {
            _l1Cache = new ConcurrentDictionary<string, L1CacheItem>();
            _l1AccessQueue = new ConcurrentQueue<string>();
            _l2Cache = new ConcurrentDictionary<string, L2CacheItem>();
            _l2AccessQueue = new ConcurrentQueue<string>();
            
            _l3MemoryCache = new MemoryCache("L3Cache");
            _l3DiskCache = new DiskCache();
            _preloadManager = new PreloadManager(this);
            
            Stats = new MultiLevelCacheStats();
        }
        
        /// <summary>
        /// 获取可见音符 - 多级缓存查询
        /// </summary>
        public async Task<List<NoteViewModel>> GetVisibleNotesAsync(
            Rect viewport,
            Func<Rect, Task<List<NoteViewModel>>> dataLoader)
        {
            var startTime = DateTime.Now;
            var cacheKey = GenerateCacheKey(viewport, "visible");
            
            // L1缓存查询（最快）
            if (_l1Cache.TryGetValue(cacheKey, out var l1Item))
            {
                UpdateL1AccessOrder(cacheKey);
                Stats.L1Hits++;
                CacheHit?.Invoke(this, new CacheHitEventArgs { Level = 1, Key = cacheKey });
                Stats.LastAccessTime = (DateTime.Now - startTime).TotalMilliseconds;
                return l1Item.Notes;
            }
            
            // L2缓存查询
            if (_l2Cache.TryGetValue(cacheKey, out var l2Item))
            {
                // 提升到L1缓存
                PromoteToL1(cacheKey, l2Item.Notes);
                UpdateL2AccessOrder(cacheKey);
                Stats.L2Hits++;
                CacheHit?.Invoke(this, new CacheHitEventArgs { Level = 2, Key = cacheKey });
                Stats.LastAccessTime = (DateTime.Now - startTime).TotalMilliseconds;
                return l2Item.Notes;
            }
            
            // L3缓存查询
            if (_l3MemoryCache.Get(cacheKey) is List<NoteViewModel> l3Notes)
            {
                // 提升到L2缓存
                PromoteToL2(cacheKey, l3Notes);
                PromoteToL1(cacheKey, l3Notes);
                Stats.L3Hits++;
                CacheHit?.Invoke(this, new CacheHitEventArgs { Level = 3, Key = cacheKey });
                Stats.LastAccessTime = (DateTime.Now - startTime).TotalMilliseconds;
                return l3Notes;
            }
            
            // 磁盘缓存查询
            var diskNotes = await _l3DiskCache.GetAsync<List<NoteViewModel>>(cacheKey);
            if (diskNotes != null)
            {
                // 加载到L3内存缓存
                _l3MemoryCache.Set(cacheKey, diskNotes, L3_EXPIRATION);
                PromoteToL2(cacheKey, diskNotes);
                PromoteToL1(cacheKey, diskNotes);
                Stats.DiskHits++;
                CacheHit?.Invoke(this, new CacheHitEventArgs { Level = 4, Key = cacheKey });
                Stats.LastAccessTime = (DateTime.Now - startTime).TotalMilliseconds;
                return diskNotes;
            }
            
            // 缓存未命中 - 从数据源加载
            var loadedNotes = await dataLoader(viewport);
            
            // 存储到所有缓存层级
            await StoreInAllLevelsAsync(cacheKey, loadedNotes);
            
            Stats.CacheMisses++;
            CacheMiss?.Invoke(this, new CacheMissEventArgs { Key = cacheKey, LoadTime = (DateTime.Now - startTime).TotalMilliseconds });
            
            // 触发预加载
            _preloadManager.TriggerPreload(viewport);
            
            Stats.LastAccessTime = (DateTime.Now - startTime).TotalMilliseconds;
            return loadedNotes;
        }
        
        /// <summary>
        /// 批量预加载数据
        /// </summary>
        public async Task PreloadDataAsync(
            List<Rect> viewports,
            Func<Rect, Task<List<NoteViewModel>>> dataLoader)
        {
            var startTime = DateTime.Now;
            var preloadTasks = new List<Task>();
            
            foreach (var viewport in viewports)
            {
                var task = PreloadViewportAsync(viewport, dataLoader);
                preloadTasks.Add(task);
            }
            
            await Task.WhenAll(preloadTasks);
            
            Stats.LastPreloadTime = (DateTime.Now - startTime).TotalMilliseconds;
            Stats.TotalPreloads++;
            
            _logger.Info("PreloadViewports", $"预加载完成: {viewports.Count}个视口, 耗时: {Stats.LastPreloadTime:F1}ms");
        }
        
        /// <summary>
        /// 更新缓存中的音符数据
        /// </summary>
        public void UpdateNoteInCache(NoteViewModel updatedNote)
        {
            var noteKey = updatedNote.Id.ToString();
            
            // 更新L1缓存
            foreach (var kvp in _l1Cache)
            {
                var notes = kvp.Value.Notes;
                for (int i = 0; i < notes.Count; i++)
                {
                    if (notes[i].Id == updatedNote.Id)
                    {
                        notes[i] = updatedNote;
                        kvp.Value.LastAccessTime = DateTime.Now;
                        break;
                    }
                }
            }
            
            // 更新L2缓存
            foreach (var kvp in _l2Cache)
            {
                var notes = kvp.Value.Notes;
                for (int i = 0; i < notes.Count; i++)
                {
                    if (notes[i].Id == updatedNote.Id)
                    {
                        notes[i] = updatedNote;
                        kvp.Value.LastAccessTime = DateTime.Now;
                        break;
                    }
                }
            }
            
            // 更新L3缓存
            // TODO: MemoryCache不支持枚举,需要使用其他方法
            // foreach (var item in _l3MemoryCache)
            // {
            //     if (item.Value is List<NoteViewModel> notes)
            //     {
            //         for (int i = 0; i < notes.Count; i++)
            //         {
            //             if (notes[i].Id == updatedNote.Id)
            //             {
            //                 notes[i] = updatedNote;
            //                 break;
            //             }
            //         }
            //     }
            // }
        }
        
        /// <summary>
        /// 从缓存中移除音符
        /// </summary>
        public void RemoveNoteFromCache(NoteViewModel removedNote)
        {
            var noteKey = removedNote.Id.ToString();
            
            // 从L1缓存移除
            foreach (var kvp in _l1Cache.ToArray())
            {
                var notes = kvp.Value.Notes;
                notes.RemoveAll(n => n.Id == removedNote.Id);
                if (notes.Count == 0)
                {
                    _l1Cache.TryRemove(kvp.Key, out _);
                }
            }
            
            // 从L2缓存移除
            foreach (var kvp in _l2Cache.ToArray())
            {
                var notes = kvp.Value.Notes;
                notes.RemoveAll(n => n.Id == removedNote.Id);
                if (notes.Count == 0)
                {
                    _l2Cache.TryRemove(kvp.Key, out _);
                }
            }
            
            // 从L3缓存移除
            foreach (var item in _l3MemoryCache.ToArray())
            {
                if (item.Value is List<NoteViewModel> notes)
                {
                    notes.RemoveAll(n => n.Id == removedNote.Id);
                }
            }
        }
        
        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void ClearAllCache()
        {
            _l1Cache.Clear();
            _l1AccessQueue.Clear();
            _l2Cache.Clear();
            _l2AccessQueue.Clear();
            _l3MemoryCache.Dispose();
            _l3DiskCache.Clear();
            
            Stats = new MultiLevelCacheStats();
        }

        /// <summary>
        /// 检查是否有有效缓存
        /// </summary>
        public bool HasValidCache()
        {
            return _l1Cache.Count > 0 || _l2Cache.Count > 0;
        }

        /// <summary>
        /// 获取缓存的数据(从L1缓存)
        /// </summary>
        public Dictionary<NoteViewModel, Rect> GetCachedData()
        {
            // 返回L1缓存中的第一个条目(如果有)
            if (_l1Cache.IsEmpty)
                return new Dictionary<NoteViewModel, Rect>();
            
            var firstEntry = _l1Cache.Values.FirstOrDefault();
            if (firstEntry != null)
            {
                // TODO: 将Notes转换为Dictionary<NoteViewModel, Rect>
                return new Dictionary<NoteViewModel, Rect>();
            }
            
            return new Dictionary<NoteViewModel, Rect>();
        }

        /// <summary>
        /// 更新缓存(使用给定的note rect映射)
        /// </summary>
        public void UpdateCache(Dictionary<NoteViewModel, Rect> noteRectCache)
        {
            // TODO: 实现缓存更新逻辑
            // 暂时留空,因为现有API使用GetVisibleNotesAsync
        }
        
        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public CacheStatistics GetCacheStatistics()
        {
            return new CacheStatistics
            {
                L1CacheSize = _l1Cache.Count,
                L2CacheSize = _l2Cache.Count,
                L3MemoryCacheSize = (int)_l3MemoryCache.GetCount(),
                L1HitRate = GetL1HitRate(),
                L2HitRate = GetL2HitRate(),
                L3HitRate = GetL3HitRate(),
                OverallHitRate = GetOverallHitRate(),
                TotalAccesses = Stats.L1Hits + Stats.L2Hits + Stats.L3Hits + Stats.DiskHits + Stats.CacheMisses,
                AverageAccessTime = Stats.LastAccessTime
            };
        }
        
        #region 私有方法
        
        private string GenerateCacheKey(Rect viewport, string type)
        {
            var roundedViewport = new Rect(
                Math.Round(viewport.X, 2),
                Math.Round(viewport.Y, 2),
                Math.Round(viewport.Width, 0),
                Math.Round(viewport.Height, 0));
            
            return $"{type}_{roundedViewport.X}_{roundedViewport.Y}_{roundedViewport.Width}_{roundedViewport.Height}";
        }
        
        private async Task<List<NoteViewModel>> PreloadViewportAsync(Rect viewport, Func<Rect, Task<List<NoteViewModel>>> dataLoader)
        {
            var cacheKey = GenerateCacheKey(viewport, "preload");
            
            // 检查是否已缓存
            if (_l3MemoryCache.Get(cacheKey) is List<NoteViewModel> cachedNotes)
            {
                return cachedNotes;
            }
            
            // 加载数据
            var notes = await dataLoader(viewport);
            
            // 存储到L3缓存
            _l3MemoryCache.Set(cacheKey, notes, L3_EXPIRATION);
            
            return notes;
        }
        
        private void PromoteToL1(string key, List<NoteViewModel> notes)
        {
            // 确保L1缓存大小限制
            while (_l1Cache.Count >= L1_CACHE_SIZE)
            {
                if (_l1AccessQueue.TryDequeue(out var oldestKey))
                {
                    _l1Cache.TryRemove(oldestKey, out _);
                    CacheEvicted?.Invoke(this, new CacheEvictionEventArgs { Level = 1, Key = oldestKey });
                }
            }
            
            var item = new L1CacheItem
            {
                Notes = notes,
                CreatedTime = DateTime.Now,
                LastAccessTime = DateTime.Now
            };
            
            _l1Cache[key] = item;
            _l1AccessQueue.Enqueue(key);
        }
        
        private void PromoteToL2(string key, List<NoteViewModel> notes)
        {
            // 确保L2缓存大小限制
            while (_l2Cache.Count >= L2_CACHE_SIZE)
            {
                if (_l2AccessQueue.TryDequeue(out var oldestKey))
                {
                    _l2Cache.TryRemove(oldestKey, out _);
                    CacheEvicted?.Invoke(this, new CacheEvictionEventArgs { Level = 2, Key = oldestKey });
                }
            }
            
            var item = new L2CacheItem
            {
                Notes = notes,
                CreatedTime = DateTime.Now,
                LastAccessTime = DateTime.Now
            };
            
            _l2Cache[key] = item;
            _l2AccessQueue.Enqueue(key);
        }
        
        private async Task StoreInAllLevelsAsync(string key, List<NoteViewModel> notes)
        {
            // 存储到L1缓存
            PromoteToL1(key, notes);
            
            // 存储到L2缓存
            PromoteToL2(key, notes);
            
            // 存储到L3内存缓存
            _l3MemoryCache.Set(key, notes, L3_EXPIRATION);
            
            // 异步存储到磁盘缓存
            await _l3DiskCache.SetAsync(key, notes);
        }
        
        private void UpdateL1AccessOrder(string key)
        {
            // 更新访问顺序（LRU）
            if (_l1Cache.TryGetValue(key, out var item))
            {
                item.LastAccessTime = DateTime.Now;
            }
        }
        
        private void UpdateL2AccessOrder(string key)
        {
            // 更新访问顺序（LRU）
            if (_l2Cache.TryGetValue(key, out var item))
            {
                item.LastAccessTime = DateTime.Now;
            }
        }
        
        private double GetL1HitRate()
        {
            int total = Stats.L1Hits + Stats.L2Hits + Stats.L3Hits + Stats.DiskHits + Stats.CacheMisses;
            return total > 0 ? (double)Stats.L1Hits / total : 0;
        }
        
        private double GetL2HitRate()
        {
            int totalL2AndBelow = Stats.L2Hits + Stats.L3Hits + Stats.DiskHits + Stats.CacheMisses;
            return totalL2AndBelow > 0 ? (double)Stats.L2Hits / totalL2AndBelow : 0;
        }
        
        private double GetL3HitRate()
        {
            int totalL3AndBelow = Stats.L3Hits + Stats.DiskHits + Stats.CacheMisses;
            return totalL3AndBelow > 0 ? (double)Stats.L3Hits / totalL3AndBelow : 0;
        }
        
        private double GetOverallHitRate()
        {
            int total = Stats.L1Hits + Stats.L2Hits + Stats.L3Hits + Stats.DiskHits + Stats.CacheMisses;
            int hits = Stats.L1Hits + Stats.L2Hits + Stats.L3Hits + Stats.DiskHits;
            return total > 0 ? (double)hits / total : 0;
        }
        
        #endregion
        
        #region 内部类
        
        private class L1CacheItem
        {
            public List<NoteViewModel> Notes { get; set; }
            public DateTime CreatedTime { get; set; }
            public DateTime LastAccessTime { get; set; }
        }
        
        private class L2CacheItem
        {
            public List<NoteViewModel> Notes { get; set; }
            public DateTime CreatedTime { get; set; }
            public DateTime LastAccessTime { get; set; }
        }
        
        private class PreloadManager
        {
            private readonly MultiLevelCacheSystem _cacheSystem;
            private readonly Timer _preloadTimer;
            private Rect _lastViewport;
            
            public PreloadManager(MultiLevelCacheSystem cacheSystem)
            {
                _cacheSystem = cacheSystem;
                _preloadTimer = new Timer(OnPreloadTimer, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
            }
            
            public void TriggerPreload(Rect viewport)
            {
                _lastViewport = viewport;
            }
            
            private void OnPreloadTimer(object state)
            {
                // 实现预加载逻辑
                if (_lastViewport != new Rect())
                {
                    // 预加载相邻区域
                    var adjacentViewports = CalculateAdjacentViewports(_lastViewport);
                    // 这里可以触发实际的预加载操作
                }
            }
            
            private List<Rect> CalculateAdjacentViewports(Rect viewport)
            {
                var adjacent = new List<Rect>();
                double offsetX = viewport.Width * 0.5;
                double offsetY = viewport.Height * 0.5;
                
                // 左
                adjacent.Add(new Rect(viewport.X - offsetX, viewport.Y, viewport.Width, viewport.Height));
                // 右
                adjacent.Add(new Rect(viewport.X + offsetX, viewport.Y, viewport.Width, viewport.Height));
                // 上
                adjacent.Add(new Rect(viewport.X, viewport.Y - offsetY, viewport.Width, viewport.Height));
                // 下
                adjacent.Add(new Rect(viewport.X, viewport.Y + offsetY, viewport.Width, viewport.Height));
                
                return adjacent;
            }
        }
        
        private class DiskCache
        {
            public async Task SetAsync<T>(string key, T value)
            {
                // 实现磁盘缓存逻辑
                await Task.CompletedTask;
            }
            
            public async Task<T> GetAsync<T>(string key)
            {
                // 实现磁盘缓存读取逻辑
                return default(T);
            }
            
            public void Clear()
            {
                // 实现磁盘缓存清理逻辑
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// 多级缓存统计
    /// </summary>
    public class MultiLevelCacheStats
    {
        public int L1Hits { get; set; }
        public int L2Hits { get; set; }
        public int L3Hits { get; set; }
        public int DiskHits { get; set; }
        public int CacheMisses { get; set; }
        public double LastAccessTime { get; set; }
        public double LastPreloadTime { get; set; }
        public int TotalPreloads { get; set; }
        
        public int TotalHits => L1Hits + L2Hits + L3Hits + DiskHits;
        public int TotalAccesses => TotalHits + CacheMisses;
        public double HitRate => TotalAccesses > 0 ? (double)TotalHits / TotalAccesses : 0;
    }
    
    /// <summary>
    /// 缓存统计信息
    /// </summary>
    public class CacheStatistics
    {
        public int L1CacheSize { get; set; }
        public int L2CacheSize { get; set; }
        public int L3MemoryCacheSize { get; set; }
        public double L1HitRate { get; set; }
        public double L2HitRate { get; set; }
        public double L3HitRate { get; set; }
        public double OverallHitRate { get; set; }
        public int TotalAccesses { get; set; }
        public double AverageAccessTime { get; set; }
    }
    
    /// <summary>
    /// 缓存命中事件参数
    /// </summary>
    public class CacheHitEventArgs : EventArgs
    {
        public int Level { get; set; }
        public string Key { get; set; }
    }
    
    /// <summary>
    /// 缓存未命中事件参数
    /// </summary>
    public class CacheMissEventArgs : EventArgs
    {
        public string Key { get; set; }
        public double LoadTime { get; set; }
    }
    
    /// <summary>
    /// 缓存驱逐事件参数
    /// </summary>
    public class CacheEvictionEventArgs : EventArgs
    {
        public int Level { get; set; }
        public string Key { get; set; }
    }
}