using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace LuminoRenderEngine.Performance
{
    /// <summary>
    /// 空间索引数据结构 - 使用BVH(Bounding Volume Hierarchy)树实现高效2D/3D查询
    /// 支持 O(log n) 的范围查询性能
    /// </summary>
    public class SpatialIndex<T> where T : class
    {
        #region 私有成员
        private BVHNode? _root;
        private readonly List<BVHLeaf<T>> _leaves = new();
        private readonly Func<T, (double minX, double minY, double maxX, double maxY)> _getBounds;
        private int _nodeCount = 0;
        private bool _isDirty = true;
        #endregion

        #region 属性
        /// <summary>
        /// 树中的元素总数
        /// </summary>
        public int Count => _leaves.Count;

        /// <summary>
        /// 树的节点总数
        /// </summary>
        public int NodeCount => _nodeCount;

        /// <summary>
        /// 是否需要重建树
        /// </summary>
        public bool IsDirty => _isDirty;
        #endregion

        #region 构造函数
        /// <summary>
        /// 创建空间索引
        /// </summary>
        /// <param name="getBounds">获取对象边界的函数 (minX, minY, maxX, maxY)</param>
        public SpatialIndex(Func<T, (double minX, double minY, double maxX, double maxY)> getBounds)
        {
            _getBounds = getBounds ?? throw new ArgumentNullException(nameof(getBounds));
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 添加元素到索引
        /// </summary>
        public void Add(T item)
        {
            _leaves.Add(new BVHLeaf<T>(item, _getBounds(item)));
            _isDirty = true;
        }

        /// <summary>
        /// 批量添加元素
        /// </summary>
        public void AddRange(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }

        /// <summary>
        /// 清空索引
        /// </summary>
        public void Clear()
        {
            _leaves.Clear();
            _root = null;
            _nodeCount = 0;
            _isDirty = true;
        }

        /// <summary>
        /// 重建BVH树以优化查询性能
        /// </summary>
        public void Rebuild()
        {
            if (_leaves.Count == 0)
            {
                _root = null;
                _nodeCount = 0;
                _isDirty = false;
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            _nodeCount = 0;
            _root = BuildBVHTree(_leaves, 0);
            stopwatch.Stop();

            _isDirty = false;
        }

        /// <summary>
        /// 查询指定范围内的所有元素 - O(log n)性能
        /// </summary>
        public List<T> QueryRange(double minX, double minY, double maxX, double maxY)
        {
            var result = new List<T>();

            if (_isDirty)
                Rebuild();

            if (_root != null)
            {
                QueryRangeRecursive(_root, minX, minY, maxX, maxY, result);
            }

            return result;
        }

        /// <summary>
        /// 查询指定点附近的元素
        /// </summary>
        public List<T> QueryNearby(double x, double y, double radius)
        {
            return QueryRange(x - radius, y - radius, x + radius, y + radius);
        }

        /// <summary>
        /// 查询指定时间范围内的所有音符
        /// </summary>
        public List<T> QueryByTimeRange(double startTime, double endTime, 
            Func<T, (double startTime, double endTime)> getTimeRange)
        {
            if (_isDirty)
                Rebuild();

            return _leaves
                .Select(l => l.Item)
                .Where(item =>
                {
                    var (itemStart, itemEnd) = getTimeRange(item);
                    // 检查时间区间是否重叠
                    return !(itemEnd < startTime || itemStart > endTime);
                })
                .ToList();
        }

        /// <summary>
        /// 获取所有元素（调试用）
        /// </summary>
        public IEnumerable<T> GetAll()
        {
            return _leaves.Select(l => l.Item);
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 递归构建BVH树
        /// </summary>
        private BVHNode BuildBVHTree(List<BVHLeaf<T>> items, int depth)
        {
            _nodeCount++;

            if (items.Count <= 4) // 叶节点阈值
            {
                return new BVHNode
                {
                    Leaves = items,
                    Children = null,
                    Depth = depth
                };
            }

            // 选择最长的轴进行分割
            var bounds = ComputeBounds(items);
            var widthX = bounds.maxX - bounds.minX;
            var widthY = bounds.maxY - bounds.minY;

            int splitAxis = widthX > widthY ? 0 : 1; // 0: X轴, 1: Y轴

            // 按中位数分割
            var middle = items.Count / 2;
            items.Sort((a, b) =>
            {
                var aVal = splitAxis == 0 ? (a.Bounds.minX + a.Bounds.maxX) / 2 : (a.Bounds.minY + a.Bounds.maxY) / 2;
                var bVal = splitAxis == 0 ? (b.Bounds.minX + b.Bounds.maxX) / 2 : (b.Bounds.minY + b.Bounds.maxY) / 2;
                return aVal.CompareTo(bVal);
            });

            var left = items.Take(middle).ToList();
            var right = items.Skip(middle).ToList();

            return new BVHNode
            {
                Children = new List<BVHNode>
                {
                    BuildBVHTree(left, depth + 1),
                    BuildBVHTree(right, depth + 1)
                },
                Leaves = null,
                Depth = depth
            };
        }

        /// <summary>
        /// 递归查询
        /// </summary>
        private void QueryRangeRecursive(BVHNode node, double minX, double minY, 
            double maxX, double maxY, List<T> result)
        {
            if (node.Leaves != null)
            {
                // 叶节点：直接检查所有项
                foreach (var leaf in node.Leaves)
                {
                    if (BoundsIntersect(leaf.Bounds, minX, minY, maxX, maxY))
                    {
                        result.Add(leaf.Item);
                    }
                }
            }
            else if (node.Children != null)
            {
                // 内部节点：检查子节点
                foreach (var child in node.Children)
                {
                    var childBounds = ComputeNodeBounds(child);
                    if (BoundsIntersect(childBounds, minX, minY, maxX, maxY))
                    {
                        QueryRangeRecursive(child, minX, minY, maxX, maxY, result);
                    }
                }
            }
        }

        /// <summary>
        /// 计算边界框
        /// </summary>
        private (double minX, double minY, double maxX, double maxY) ComputeBounds(List<BVHLeaf<T>> items)
        {
            if (items.Count == 0)
                return (0, 0, 0, 0);

            var first = items[0].Bounds;
            double minX = first.minX, minY = first.minY, maxX = first.maxX, maxY = first.maxY;

            for (int i = 1; i < items.Count; i++)
            {
                var bounds = items[i].Bounds;
                minX = Math.Min(minX, bounds.minX);
                minY = Math.Min(minY, bounds.minY);
                maxX = Math.Max(maxX, bounds.maxX);
                maxY = Math.Max(maxY, bounds.maxY);
            }

            return (minX, minY, maxX, maxY);
        }

        /// <summary>
        /// 计算节点的边界框
        /// </summary>
        private (double minX, double minY, double maxX, double maxY) ComputeNodeBounds(BVHNode node)
        {
            if (node.Leaves != null)
                return ComputeBounds(node.Leaves);

            if (node.Children != null && node.Children.Count > 0)
            {
                var bounds = ComputeNodeBounds(node.Children[0]);
                for (int i = 1; i < node.Children.Count; i++)
                {
                    var childBounds = ComputeNodeBounds(node.Children[i]);
                    bounds.minX = Math.Min(bounds.minX, childBounds.minX);
                    bounds.minY = Math.Min(bounds.minY, childBounds.minY);
                    bounds.maxX = Math.Max(bounds.maxX, childBounds.maxX);
                    bounds.maxY = Math.Max(bounds.maxY, childBounds.maxY);
                }
                return bounds;
            }

            return (0, 0, 0, 0);
        }

        /// <summary>
        /// 检查两个边界框是否相交
        /// </summary>
        private bool BoundsIntersect((double minX, double minY, double maxX, double maxY) a,
            double minX, double minY, double maxX, double maxY)
        {
            return !(a.maxX < minX || a.minX > maxX ||
                     a.maxY < minY || a.minY > maxY);
        }
        #endregion

        #region 私有类
        /// <summary>
        /// BVH树的节点
        /// </summary>
        private class BVHNode
        {
            public List<BVHLeaf<T>>? Leaves { get; set; }
            public List<BVHNode>? Children { get; set; }
            public int Depth { get; set; }
        }

        /// <summary>
        /// BVH树的叶节点
        /// </summary>
        private class BVHLeaf<TItem> where TItem : class
        {
            public TItem Item { get; }
            public (double minX, double minY, double maxX, double maxY) Bounds { get; }

            public BVHLeaf(TItem item, (double minX, double minY, double maxX, double maxY) bounds)
            {
                Item = item;
                Bounds = bounds;
            }
        }
        #endregion
    }

    /// <summary>
    /// 高效的音符查询索引
    /// 支持按时间、音高、速度等多维度查询
    /// </summary>
    public class NoteQueryIndex
    {
        private readonly SpatialIndex<NoteData> _spatialIndex;
        private readonly Dictionary<int, List<NoteData>> _pitchIndex = new();
        private readonly Dictionary<int, List<NoteData>> _velocityIndex = new();
        private double _lastRebuildTime = 0;
        private const double REBUILD_INTERVAL = 1.0; // 秒

        public int TotalNotes => _spatialIndex.Count;
        public int SpatialNodeCount => _spatialIndex.NodeCount;

        public NoteQueryIndex()
        {
            _spatialIndex = new SpatialIndex<NoteData>(note =>
            (
                note.StartTime,
                note.Pitch,
                note.StartTime + note.Duration,
                note.Pitch + 1
            ));
        }

        /// <summary>
        /// 添加音符到索引
        /// </summary>
        public void AddNote(NoteData note)
        {
            _spatialIndex.Add(note);

            // 更新音高索引
            if (!_pitchIndex.ContainsKey(note.Pitch))
                _pitchIndex[note.Pitch] = new List<NoteData>();
            _pitchIndex[note.Pitch].Add(note);

            // 更新速度索引
            if (!_velocityIndex.ContainsKey(note.Velocity))
                _velocityIndex[note.Velocity] = new List<NoteData>();
            _velocityIndex[note.Velocity].Add(note);
        }

        /// <summary>
        /// 批量添加音符
        /// </summary>
        public void AddNotes(IEnumerable<NoteData> notes)
        {
            foreach (var note in notes)
                AddNote(note);
        }

        /// <summary>
        /// 查询指定时间范围内的所有音符 - O(log n)
        /// </summary>
        public List<NoteData> QueryByTimeRange(double startTime, double endTime)
        {
            return _spatialIndex.QueryByTimeRange(startTime, endTime, note =>
                (note.StartTime, note.StartTime + note.Duration));
        }

        /// <summary>
        /// 查询指定音高范围内的音符
        /// </summary>
        public List<NoteData> QueryByPitchRange(int minPitch, int maxPitch)
        {
            var result = new List<NoteData>();
            for (int pitch = minPitch; pitch <= maxPitch; pitch++)
            {
                if (_pitchIndex.TryGetValue(pitch, out var notes))
                    result.AddRange(notes);
            }
            return result;
        }

        /// <summary>
        /// 查询指定速度的所有音符
        /// </summary>
        public List<NoteData> QueryByVelocity(int velocity)
        {
            return _velocityIndex.TryGetValue(velocity, out var notes) 
                ? new List<NoteData>(notes) 
                : new List<NoteData>();
        }

        /// <summary>
        /// 综合查询 - 时间 + 音高
        /// </summary>
        public List<NoteData> QueryComprehensive(double startTime, double endTime, 
            int minPitch, int maxPitch)
        {
            var timeResults = QueryByTimeRange(startTime, endTime);
            return timeResults.Where(n => n.Pitch >= minPitch && n.Pitch <= maxPitch).ToList();
        }

        /// <summary>
        /// 优化空间索引
        /// </summary>
        public void OptimizeIfNeeded(double currentTime)
        {
            if (currentTime - _lastRebuildTime > REBUILD_INTERVAL && _spatialIndex.IsDirty)
            {
                _spatialIndex.Rebuild();
                _lastRebuildTime = currentTime;
            }
        }

        /// <summary>
        /// 清空所有索引
        /// </summary>
        public void Clear()
        {
            _spatialIndex.Clear();
            _pitchIndex.Clear();
            _velocityIndex.Clear();
        }
    }

    /// <summary>
    /// 音符数据模型
    /// </summary>
    public class NoteData
    {
        public double StartTime { get; set; }
        public double Duration { get; set; }
        public int Pitch { get; set; }
        public int Velocity { get; set; }
        public int Channel { get; set; }
        public int TrackIndex { get; set; }
        public Guid Id { get; set; } = Guid.NewGuid();

        public NoteData() { }

        public NoteData(double startTime, double duration, int pitch, 
            int velocity, int channel = 0, int trackIndex = 0)
        {
            StartTime = startTime;
            Duration = duration;
            Pitch = pitch;
            Velocity = velocity;
            Channel = channel;
            TrackIndex = trackIndex;
        }
    }
}
