using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using EnderDebugger;
using Lumino.Services.Interfaces;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// 内存池服务 - 减少GC压力，提高性能
    /// </summary>
    public class MemoryPoolService : IMemoryPoolService
    {
        private readonly EnderLogger _logger;
        private readonly ConcurrentDictionary<Type, object> _pools = new();
        private bool _disposed;

        public MemoryPoolService()
        {
            _logger = EnderLogger.Instance;
            _logger.Info("MemoryPoolService", "[内存池] 内存池服务已初始化");
        }

        /// <summary>
        /// 获取或创建对象池
        /// </summary>
        public IObjectPool<T> GetPool<T>() where T : class, new()
        {
            var type = typeof(T);
            if (!_pools.TryGetValue(type, out var pool))
            {
                pool = new ObjectPool<T>();
                _pools[type] = pool;
                _logger.Info("MemoryPoolService", $"[内存池] 创建了 {type.Name} 的对象池");
            }
            return (IObjectPool<T>)pool;
        }

        /// <summary>
        /// 获取数组池
        /// </summary>
        public IArrayPool<T> GetArrayPool<T>()
        {
            var type = typeof(T[]);
            if (!_pools.TryGetValue(type, out var pool))
            {
                pool = new ArrayPool<T>();
                _pools[type] = pool;
                _logger.Info("MemoryPoolService", $"[内存池] 创建了 {typeof(T).Name}[] 的数组池");
            }
            return (IArrayPool<T>)pool;
        }

        /// <summary>
        /// 清理所有池
        /// </summary>
        public void ClearAllPools()
        {
            foreach (var pool in _pools.Values)
            {
                if (pool is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _pools.Clear();
            _logger.Info("MemoryPoolService", "[内存池] 已清理所有对象池");
        }

        /// <summary>
        /// 获取性能统计
        /// </summary>
        public MemoryPoolStats GetStats()
        {
            var stats = new MemoryPoolStats();
            foreach (var kvp in _pools)
            {
                if (kvp.Value is IObjectPool pool)
                {
                    stats.TotalPools++;
                    stats.TotalObjects += pool.Count;
                }
            }
            return stats;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                ClearAllPools();
                _disposed = true;
                _logger.Info("MemoryPoolService", "[内存池] 内存池服务已释放");
            }
        }
    }

    /// <summary>
    /// 对象池接口
    /// </summary>
    public interface IObjectPool
    {
        int Count { get; }
    }

    /// <summary>
    /// 通用对象池
    /// </summary>
    public class ObjectPool<T> : IObjectPool<T> where T : class, new()
    {
        private readonly ConcurrentQueue<T> _pool = new();
        private readonly EnderLogger _logger = EnderLogger.Instance;
        private int _createdCount;
        private int _rentedCount;
        private int _returnedCount;

        public int Count => _pool.Count;

        /// <summary>
        /// 租用对象
        /// </summary>
        public T Rent()
        {
            if (_pool.TryDequeue(out var item))
            {
                _rentedCount++;
                return item;
            }

            _createdCount++;
            return new T();
        }

        /// <summary>
        /// 归还对象
        /// </summary>
        public void Return(T item)
        {
            if (item == null) return;

            _returnedCount++;
            _pool.Enqueue(item);
        }

        /// <summary>
        /// 清空池
        /// </summary>
        public void Clear()
        {
            _pool.Clear();
            _logger.Info("ObjectPool", $"[内存池] 清空了 {typeof(T).Name} 池，创建:{_createdCount}, 租用:{_rentedCount}, 归还:{_returnedCount}");
        }

        public void Dispose()
        {
            Clear();
        }
    }

    /// <summary>
    /// 数组池 - 专门用于数组的内存池
    /// </summary>
    public class ArrayPool<T> : IArrayPool<T>
    {
        private readonly ConcurrentDictionary<int, ConcurrentQueue<T[]>> _pools = new();
        private readonly EnderLogger _logger = EnderLogger.Instance;

        /// <summary>
        /// 租用数组
        /// </summary>
        public T[] Rent(int minimumLength)
        {
            var size = GetSize(minimumLength);
            if (!_pools.TryGetValue(size, out var pool))
            {
                pool = new ConcurrentQueue<T[]>();
                _pools[size] = pool;
            }

            if (pool.TryDequeue(out var array) && array.Length >= minimumLength)
            {
                return array;
            }

            return new T[size];
        }

        /// <summary>
        /// 归还数组
        /// </summary>
        public void Return(T[] array)
        {
            if (array == null) return;

            var size = array.Length;
            if (!_pools.TryGetValue(size, out var pool))
            {
                pool = new ConcurrentQueue<T[]>();
                _pools[size] = pool;
            }

            // 清空数组内容
            Array.Clear(array, 0, array.Length);
            pool.Enqueue(array);
        }

        /// <summary>
        /// 获取合适的数组大小
        /// </summary>
        private static int GetSize(int minimumLength)
        {
            var size = 16;
            while (size < minimumLength)
            {
                size *= 2;
            }
            return size;
        }

        /// <summary>
        /// 清空池
        /// </summary>
        public void Clear()
        {
            _pools.Clear();
            _logger.Info("ArrayPool", $"[内存池] 清空了 {typeof(T).Name}[] 数组池");
        }

        public void Dispose()
        {
            Clear();
        }
    }
}