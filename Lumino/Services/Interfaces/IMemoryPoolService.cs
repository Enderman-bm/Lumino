using System;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// 内存池服务接口
    /// </summary>
    public interface IMemoryPoolService : IDisposable
    {
        /// <summary>
        /// 获取对象池
        /// </summary>
        IObjectPool<T> GetPool<T>() where T : class, new();

        /// <summary>
        /// 获取数组池
        /// </summary>
        IArrayPool<T> GetArrayPool<T>();

        /// <summary>
        /// 清理所有池
        /// </summary>
        void ClearAllPools();

        /// <summary>
        /// 获取性能统计
        /// </summary>
        MemoryPoolStats GetStats();
    }

    /// <summary>
    /// 对象池接口
    /// </summary>
    public interface IObjectPool<T> where T : class
    {
        T Rent();
        void Return(T item);
    }

    /// <summary>
    /// 数组池接口
    /// </summary>
    public interface IArrayPool<T>
    {
        T[] Rent(int minimumLength);
        void Return(T[] array);
    }

    /// <summary>
    /// 对象池类
    /// </summary>
    public class ObjectPool<T> : IObjectPool<T> where T : class, new()
    {
        public virtual T Rent() => new T();
        public virtual void Return(T item) { }
    }

    /// <summary>
    /// 数组池类
    /// </summary>
    public class ArrayPool<T> : IArrayPool<T>
    {
        public virtual T[] Rent(int minimumLength) => new T[minimumLength];
        public virtual void Return(T[] array) { }
    }

    /// <summary>
    /// 内存池统计信息
    /// </summary>
    public struct MemoryPoolStats
    {
        public int TotalPools;
        public int TotalObjects;
        public long MemoryUsageMB;
    }
}