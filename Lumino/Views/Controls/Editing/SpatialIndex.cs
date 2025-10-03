/*using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Lumino.ViewModels.Editor;

namespace Lumino.Views.Controls.Editing
{
    /// <summary>
    /// 空间索引类 - 用于快速查询指定区域内的音符
    /// 基于网格的空间划分，避免遍历所有音符
    /// </summary>
    public class SpatialIndex<T> where T : class
    {
        #region 常量和字段
        private const double DEFAULT_BUCKET_SIZE = 200.0; // 默认桶大小（像素）
        private const double MIN_BUCKET_SIZE = 50.0;      // 最小桶大小
        private const double MAX_BUCKET_SIZE = 1000.0;    // 最大桶大小
        
        private readonly Dictionary<long, List<SpatialItem>> _buckets = new();
        private readonly HashSet<T> _allItems = new();
        private double _bucketSize;
        private Rect _bounds = new Rect(); // 索引覆盖的边界
        #endregion

        #region 内部数据结构
        private class SpatialItem
        {
            public T Item { get; set; }
            public Rect Bounds { get; set; }
            public long BucketId { get; set; }

            public SpatialItem(T item, Rect bounds, long bucketId)
            {
                Item = item;
                Bounds = bounds;
                BucketId = bucketId;
            }
        }
        #endregion

        #region 构造函数
        public SpatialIndex(double bucketSize = DEFAULT_BUCKET_SIZE)
        {
            _bucketSize = Math.Max(MIN_BUCKET_SIZE, Math.Min(MAX_BUCKET_SIZE, bucketSize));
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 插入项目到空间索引
        /// </summary>
        /// <param name="item">要插入的项目</param>
        /// <param name="bounds">项目的边界矩形</param>
        public void Insert(T item, Rect bounds)
        {
            if (item == null || bounds.Width == 0 || bounds.Height == 0) return;

            // 如果项目已存在，先移除
            Remove(item);

            var bucketIds = GetRelevantBuckets(bounds);
            var spatialItem = new SpatialItem(item, bounds, bucketIds.First());

            foreach (var bucketId in bucketIds)
            {
                if (!_buckets.ContainsKey(bucketId))
                    _buckets[bucketId] = new List<SpatialItem>();
                
                _buckets[bucketId].Add(spatialItem);
            }

            _allItems.Add(item);
            UpdateBounds(bounds);
        }

        /// <summary>
        /// 从空间索引中移除项目
        /// </summary>
        /// <param name="item">要移除的项目</param>
        /// <returns>是否成功移除</returns>
        public bool Remove(T item)
        {
            if (item == null || !_allItems.Contains(item)) return false;

            var bucketsToClean = new List<long>();

            foreach (var kvp in _buckets)
            {
                var bucket = kvp.Value;
                bucket.RemoveAll(spatialItem => ReferenceEquals(spatialItem.Item, item));
                
                if (bucket.Count == 0)
                    bucketsToClean.Add(kvp.Key);
            }

            // 清理空桶
            foreach (var bucketId in bucketsToClean)
            {
                _buckets.Remove(bucketId);
            }

            _allItems.Remove(item);
            return true;
        }

        /// <summary>
        /// 查询指定区域内的所有项目
        /// </summary>
        /// <param name="queryRect">查询区域</param>
        /// <returns>区域内的项目集合</returns>
        public IEnumerable<T> Query(Rect queryRect)
        {
            if (queryRect.Width == 0 || queryRect.Height == 0) return Enumerable.Empty<T>();

            var bucketIds = GetRelevantBuckets(queryRect);
            var results = new HashSet<T>();

            foreach (var bucketId in bucketIds)
            {
                if (_buckets.TryGetValue(bucketId, out var bucket))
                {
                    foreach (var spatialItem in bucket)
                    {
                        if (spatialItem.Bounds.Intersects(queryRect))
                        {
                            results.Add(spatialItem.Item);
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// 更新项目的位置
        /// </summary>
        /// <param name="item">要更新的项目</param>
        /// <param name="newBounds">新的边界矩形</param>
        public void Update(T item, Rect newBounds)
        {
            if (item == null || newBounds.Width == 0 || newBounds.Height == 0) return;

            // 重新插入（会自动移除旧的）
            Insert(item, newBounds);
        }

        /// <summary>
        /// 清空索引
        /// </summary>
        public void Clear()
        {
            _buckets.Clear();
            _allItems.Clear();
            _bounds = new Rect();
        }

        /// <summary>
        /// 获取索引中的项目总数
        /// </summary>
        public int Count => _allItems.Count;

        /// <summary>
        /// 获取索引覆盖的边界
        /// </summary>
        public Rect Bounds => _bounds;

        /// <summary>
        /// 优化索引 - 根据当前数据分布调整桶大小
        /// </summary>
        public void Optimize()
        {
            if (_allItems.Count == 0) return;

            // 根据数据密度调整桶大小
            var totalArea = _bounds.Width * _bounds.Height;
            var density = _allItems.Count / Math.Max(1, totalArea);
            
            // 动态调整桶大小
            var optimalBucketSize = Math.Sqrt(totalArea / Math.Max(1, _allItems.Count)) * 2;
            var newBucketSize = Math.Max(MIN_BUCKET_SIZE, Math.Min(MAX_BUCKET_SIZE, optimalBucketSize));

            if (Math.Abs(newBucketSize - _bucketSize) > 10) // 只有显著变化时才重建
            {
                var allItems = _allItems.ToList();
                var allBounds = new Dictionary<T, Rect>();
                
                // 保存当前边界信息
                foreach (var kvp in _buckets)
                {
                    foreach (var spatialItem in kvp.Value)
                    {
                        if (!allBounds.ContainsKey(spatialItem.Item))
                        {
                            allBounds[spatialItem.Item] = spatialItem.Bounds;
                        }
                    }
                }

                // 重建索引
                _bucketSize = newBucketSize;
                Clear();
                
                foreach (var item in allItems)
                {
                    if (allBounds.TryGetValue(item, out var bounds))
                    {
                        Insert(item, bounds);
                    }
                }
            }
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 获取与指定矩形相关的所有桶ID
        /// </summary>
        private IEnumerable<long> GetRelevantBuckets(Rect rect)
        {
            var minX = (long)Math.Floor(rect.Left / _bucketSize);
            var maxX = (long)Math.Floor(rect.Right / _bucketSize);
            var minY = (long)Math.Floor(rect.Top / _bucketSize);
            var maxY = (long)Math.Floor(rect.Bottom / _bucketSize);

            for (long x = minX; x <= maxX; x++)
            {
                for (long y = minY; y <= maxY; y++)
                {
                    yield return GetBucketId(x, y);
                }
            }
        }

        /// <summary>
        /// 根据网格坐标计算桶ID
        /// </summary>
        private static long GetBucketId(long x, long y)
        {
            // 使用64位整数避免冲突
            return (x << 32) | (uint)y;
        }

        /// <summary>
        /// 更新索引边界
        /// </summary>
        private void UpdateBounds(Rect itemBounds)
        {
            if (_bounds.Width == 0 || _bounds.Height == 0)
            {
                _bounds = itemBounds;
            }
            else
            {
                _bounds = _bounds.Union(itemBounds);
            }
        }
        #endregion

        #region 调试和统计
        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            var bucketCount = _buckets.Count;
            var itemCount = _allItems.Count;
            var avgItemsPerBucket = bucketCount > 0 ? (double)itemCount / bucketCount : 0;
            
            return $"SpatialIndex: {itemCount} items, {bucketCount} buckets, " +
                   $"avg {avgItemsPerBucket:F1} items/bucket, bucket size: {_bucketSize:F0}px";
        }
        #endregion
    }
}*/