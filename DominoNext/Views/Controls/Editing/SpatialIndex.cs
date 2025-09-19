/*using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Lumino.ViewModels.Editor;

namespace Lumino.Views.Controls.Editing
{
    /// <summary>
    /// �ռ������� - ���ڿ��ٲ�ѯָ�������ڵ�����
    /// ��������Ŀռ仮�֣����������������
    /// </summary>
    public class SpatialIndex<T> where T : class
    {
        #region �������ֶ�
        private const double DEFAULT_BUCKET_SIZE = 200.0; // Ĭ��Ͱ��С�����أ�
        private const double MIN_BUCKET_SIZE = 50.0;      // ��СͰ��С
        private const double MAX_BUCKET_SIZE = 1000.0;    // ���Ͱ��С
        
        private readonly Dictionary<long, List<SpatialItem>> _buckets = new();
        private readonly HashSet<T> _allItems = new();
        private double _bucketSize;
        private Rect _bounds = new Rect(); // �������ǵı߽�
        #endregion

        #region �ڲ����ݽṹ
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

        #region ���캯��
        public SpatialIndex(double bucketSize = DEFAULT_BUCKET_SIZE)
        {
            _bucketSize = Math.Max(MIN_BUCKET_SIZE, Math.Min(MAX_BUCKET_SIZE, bucketSize));
        }
        #endregion

        #region ��������
        /// <summary>
        /// ������Ŀ���ռ�����
        /// </summary>
        /// <param name="item">Ҫ�������Ŀ</param>
        /// <param name="bounds">��Ŀ�ı߽����</param>
        public void Insert(T item, Rect bounds)
        {
            if (item == null || bounds.Width == 0 || bounds.Height == 0) return;

            // �����Ŀ�Ѵ��ڣ����Ƴ�
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
        /// �ӿռ��������Ƴ���Ŀ
        /// </summary>
        /// <param name="item">Ҫ�Ƴ�����Ŀ</param>
        /// <returns>�Ƿ�ɹ��Ƴ�</returns>
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

            // ������Ͱ
            foreach (var bucketId in bucketsToClean)
            {
                _buckets.Remove(bucketId);
            }

            _allItems.Remove(item);
            return true;
        }

        /// <summary>
        /// ��ѯָ�������ڵ�������Ŀ
        /// </summary>
        /// <param name="queryRect">��ѯ����</param>
        /// <returns>�����ڵ���Ŀ����</returns>
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
        /// ������Ŀ��λ��
        /// </summary>
        /// <param name="item">Ҫ���µ���Ŀ</param>
        /// <param name="newBounds">�µı߽����</param>
        public void Update(T item, Rect newBounds)
        {
            if (item == null || newBounds.Width == 0 || newBounds.Height == 0) return;

            // ���²��루���Զ��Ƴ��ɵģ�
            Insert(item, newBounds);
        }

        /// <summary>
        /// �������
        /// </summary>
        public void Clear()
        {
            _buckets.Clear();
            _allItems.Clear();
            _bounds = new Rect();
        }

        /// <summary>
        /// ��ȡ�����е���Ŀ����
        /// </summary>
        public int Count => _allItems.Count;

        /// <summary>
        /// ��ȡ�������ǵı߽�
        /// </summary>
        public Rect Bounds => _bounds;

        /// <summary>
        /// �Ż����� - ���ݵ�ǰ���ݷֲ�����Ͱ��С
        /// </summary>
        public void Optimize()
        {
            if (_allItems.Count == 0) return;

            // ���������ܶȵ���Ͱ��С
            var totalArea = _bounds.Width * _bounds.Height;
            var density = _allItems.Count / Math.Max(1, totalArea);
            
            // ��̬����Ͱ��С
            var optimalBucketSize = Math.Sqrt(totalArea / Math.Max(1, _allItems.Count)) * 2;
            var newBucketSize = Math.Max(MIN_BUCKET_SIZE, Math.Min(MAX_BUCKET_SIZE, optimalBucketSize));

            if (Math.Abs(newBucketSize - _bucketSize) > 10) // ֻ�������仯ʱ���ؽ�
            {
                var allItems = _allItems.ToList();
                var allBounds = new Dictionary<T, Rect>();
                
                // ���浱ǰ�߽���Ϣ
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

                // �ؽ�����
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

        #region ˽�з���
        /// <summary>
        /// ��ȡ��ָ��������ص�����ͰID
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
        /// ���������������ͰID
        /// </summary>
        private static long GetBucketId(long x, long y)
        {
            // ʹ��64λ���������ͻ
            return (x << 32) | (uint)y;
        }

        /// <summary>
        /// ���������߽�
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

        #region ���Ժ�ͳ��
        /// <summary>
        /// ��ȡ������Ϣ
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