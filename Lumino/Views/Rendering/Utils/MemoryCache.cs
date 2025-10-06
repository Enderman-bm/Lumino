using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Lumino.Views.Rendering.Utils
{
    /// <summary>
    /// 简单的内存缓存实现
    /// </summary>
    public class MemoryCache : IDisposable
    {
        private readonly ConcurrentDictionary<string, CacheItem> _cache;
        private readonly Timer _cleanupTimer;
        private readonly string _name;
        private bool _disposed;

        public MemoryCache(string name)
        {
            _name = name;
            _cache = new ConcurrentDictionary<string, CacheItem>();
            _cleanupTimer = new Timer(CleanupExpiredItems, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// 获取缓存项
        /// </summary>
        public object Get(string key)
        {
            if (_cache.TryGetValue(key, out var item))
            {
                if (item.ExpirationTime > DateTime.Now)
                {
                    item.LastAccessTime = DateTime.Now;
                    return item.Value;
                }
                else
                {
                    _cache.TryRemove(key, out _);
                }
            }
            return null;
        }

        /// <summary>
        /// 设置缓存项
        /// </summary>
        public void Set(string key, object value, TimeSpan expiration)
        {
            var item = new CacheItem
            {
                Key = key,
                Value = value,
                ExpirationTime = DateTime.Now.Add(expiration),
                CreatedTime = DateTime.Now,
                LastAccessTime = DateTime.Now
            };
            _cache[key] = item;
        }

        /// <summary>
        /// 移除缓存项
        /// </summary>
        public bool Remove(string key)
        {
            return _cache.TryRemove(key, out _);
        }

        /// <summary>
        /// 获取缓存项数量
        /// </summary>
        public long GetCount()
        {
            return _cache.Count;
        }

        /// <summary>
        /// 转换为数组（用于遍历）
        /// </summary>
        public KeyValuePair<string, object>[] ToArray()
        {
            return _cache.Values
                .Where(item => item.ExpirationTime > DateTime.Now)
                .ToDictionary(item => item.Key, item => item.Value)
                .ToArray();
        }

        /// <summary>
        /// 清空缓存
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// 清理过期项
        /// </summary>
        private void CleanupExpiredItems(object state)
        {
            var expiredKeys = _cache.Values
                .Where(item => item.ExpirationTime <= DateTime.Now)
                .Select(item => item.Key)
                .ToArray();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
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
                _cache.Clear();
                _disposed = true;
            }
        }

        /// <summary>
        /// 缓存项
        /// </summary>
        private class CacheItem
        {
            public string Key { get; set; }
            public object Value { get; set; }
            public DateTime ExpirationTime { get; set; }
            public DateTime CreatedTime { get; set; }
            public DateTime LastAccessTime { get; set; }
        }
    }
}