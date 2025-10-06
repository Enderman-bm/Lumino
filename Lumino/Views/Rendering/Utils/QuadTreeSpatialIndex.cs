using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Lumino.ViewModels.Editor;

namespace Lumino.Views.Rendering.Utils
{
    /// <summary>
    /// 四叉树空间索引 - 用于超大规模音符的快速空间查询
    /// 支持1000W+音符的实时可见性检测
    /// </summary>
    public class QuadTreeSpatialIndex
    {
        private const int MAX_OBJECTS_PER_NODE = 50;  // 每个节点最大对象数
        private const int MAX_DEPTH = 12;              // 最大树深度（增加深度支持更密集数据）
        private const double MIN_NODE_SIZE = 10.0;     // 最小节点尺寸（像素）
        
        private QuadTreeNode _root;
        private readonly Dictionary<NoteViewModel, QuadTreeObject> _objectMap;
        private readonly Stack<QuadTreeNode> _nodePool;
        private const int MAX_NODE_POOL_SIZE = 5000; // 增加对象池大小以支持更多音符
        
        /// <summary>
        /// 性能统计
        /// </summary>
        public SpatialIndexStats Stats { get; private set; }
        
        public QuadTreeSpatialIndex(Rect bounds)
        {
            _objectMap = new Dictionary<NoteViewModel, QuadTreeObject>();
            _nodePool = new Stack<QuadTreeNode>();
            Stats = new SpatialIndexStats();
            
            // 创建根节点，确保边界足够大以容纳所有音符
            var expandedBounds = ExpandBounds(bounds, 0.2); // 扩展20%避免频繁重建
            _root = CreateNode(expandedBounds, 0);
        }
        
        /// <summary>
        /// 批量构建索引 - 针对超大规模数据优化
        /// </summary>
        public void BuildIndex(IEnumerable<NoteViewModel> notes, Func<NoteViewModel, Rect> rectCalculator)
        {
            var startTime = DateTime.Now;
            
            // 清空现有索引
            Clear();
            
            // 并行预计算所有音符的边界 - 利用多核CPU
            var notesList = notes.ToList();
            var noteBounds = new System.Collections.Concurrent.ConcurrentBag<(NoteViewModel note, Rect rect)>();
            
            System.Threading.Tasks.Parallel.ForEach(
                notesList,
                new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 128 },
                note =>
                {
                    var rect = rectCalculator(note);
                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        noteBounds.Add((note, rect));
                    }
                });
            
            var noteBoundsList = noteBounds.ToList();
            
            // 计算总体边界
            var totalBounds = CalculateTotalBounds(noteBoundsList.Select(nb => nb.rect));
            
            // 重建根节点（如果现有边界不够大）
            if (!IsBoundsSufficient(_root.Bounds, totalBounds))
            {
                ReturnNodeToPool(_root);
                _root = CreateNode(ExpandBounds(totalBounds, 0.3), 0);
            }
            
            // 批量插入（使用并行处理）
            var objectList = new List<QuadTreeObject>(noteBoundsList.Count);
            foreach (var (note, rect) in noteBoundsList)
            {
                var obj = new QuadTreeObject(note, rect);
                objectList.Add(obj);
                _objectMap[note] = obj;
            }
            
            // 批量插入到四叉树
            BulkInsert(_root, objectList);
            
            Stats.BuildTime = (DateTime.Now - startTime).TotalMilliseconds;
            Stats.TotalObjects = _objectMap.Count;
            Stats.NodeCount = CountNodes(_root);
            
            System.Diagnostics.Debug.WriteLine($"[QuadTree] 构建完成: {Stats.TotalObjects}个对象, {Stats.NodeCount}个节点, 耗时: {Stats.BuildTime:F1}ms");
        }
        
        /// <summary>
        /// 查询可见音符 - 超高速查询优化
        /// </summary>
        public IEnumerable<NoteViewModel> QueryVisible(Rect viewport)
        {
            var startTime = DateTime.Now;
            var results = new List<NoteViewModel>(1000); // 预分配合理容量
            
            QueryRecursive(_root, viewport, results);
            
            Stats.LastQueryTime = (DateTime.Now - startTime).TotalMilliseconds;
            Stats.LastQueryResults = results.Count;
            Stats.TotalQueries++;
            
            return results;
        }
        
        /// <summary>
        /// 更新单个音符位置
        /// </summary>
        public void UpdateObject(NoteViewModel note, Rect newRect)
        {
            if (_objectMap.TryGetValue(note, out var obj))
            {
                // 移除旧位置
                RemoveFromNode(_root, obj);
                
                // 更新边界
                obj.Rect = newRect;
                
                // 插入新位置
                InsertRecursive(_root, obj);
            }
        }
        
        /// <summary>
        /// 移除对象
        /// </summary>
        public void RemoveObject(NoteViewModel note)
        {
            if (_objectMap.TryGetValue(note, out var obj))
            {
                RemoveFromNode(_root, obj);
                _objectMap.Remove(note);
            }
        }
        
        /// <summary>
        /// 清空索引
        /// </summary>
        public void Clear()
        {
            _objectMap.Clear();
            ReturnNodeRecursive(_root);
            _root = CreateNode(_root.Bounds, 0);
        }

        /// <summary>
        /// 索引是否已初始化(是否包含对象)
        /// </summary>
        public bool IsInitialized => _objectMap.Count > 0;

        /// <summary>
        /// 查询可见音符(别名方法,内部调用QueryVisible)
        /// </summary>
        public List<NoteViewModel> QueryVisibleNotes(Rect viewport)
        {
            return QueryVisible(viewport).ToList();
        }

        /// <summary>
        /// 重建索引(使用给定的音符集合和坐标服务)
        /// </summary>
        public void RebuildIndex(IEnumerable<NoteViewModel> notes, ViewModels.Editor.Components.PianoRollCoordinates? coordinates = null)
        {
            if (coordinates == null)
            {
                // 如果没有提供坐标服务,清空索引
                Clear();
                return;
            }
            
            // 使用坐标服务计算正确的矩形
            BuildIndex(notes, note => coordinates.GetScreenNoteRect(note));
        }
        
        #region 私有方法
        
        private QuadTreeNode CreateNode(Rect bounds, int depth)
        {
            if (_nodePool.Count > 0)
            {
                var node = _nodePool.Pop();
                node.Initialize(bounds, depth);
                return node;
            }
            return new QuadTreeNode(bounds, depth);
        }
        
        private void ReturnNodeToPool(QuadTreeNode node)
        {
            if (_nodePool.Count < MAX_NODE_POOL_SIZE)
            {
                node.Clear();
                _nodePool.Push(node);
            }
        }
        
        private void ReturnNodeRecursive(QuadTreeNode node)
        {
            if (node == null) return;
            
            for (int i = 0; i < 4; i++)
            {
                if (node.Children[i] != null)
                {
                    ReturnNodeRecursive(node.Children[i]!);
                }
            }
            
            ReturnNodeToPool(node);
        }
        
        private void BulkInsert(QuadTreeNode node, List<QuadTreeObject> objects)
        {
            // 如果对象数量较少，直接插入到当前节点
            if (objects.Count <= MAX_OBJECTS_PER_NODE || node.Depth >= MAX_DEPTH || 
                Math.Min(node.Bounds.Width, node.Bounds.Height) <= MIN_NODE_SIZE)
            {
                // 预分配容量避免多次扩容
                if (node.Objects.Capacity < node.Objects.Count + objects.Count)
                {
                    node.Objects.Capacity = node.Objects.Count + objects.Count;
                }
                
                foreach (var obj in objects)
                {
                    node.Objects.Add(obj);
                }
                return;
            }
            
            // 创建子节点
            CreateChildren(node);
            
            // 将对象分配到子节点 - 使用数组代替字典提升性能
            var childObjects = new List<QuadTreeObject>[4];
            for (int i = 0; i < 4; i++)
            {
                childObjects[i] = new List<QuadTreeObject>(objects.Count / 4 + 1); // 预估容量
            }
            
            // 批量分配对象到子节点
            foreach (var obj in objects)
            {
                int index = GetChildIndex(node, obj.Rect);
                if (index >= 0 && index < 4)
                {
                    childObjects[index].Add(obj);
                }
                else
                {
                    // 如果对象不完全在任何一个子节点内，保留在当前节点
                    node.Objects.Add(obj);
                }
            }
            
            // 递归插入到子节点
            for (int i = 0; i < 4; i++)
            {
                if (childObjects[i].Count > 0 && node.Children[i] != null)
                {
                    BulkInsert(node.Children[i]!, childObjects[i]);
                }
            }
        }
        
        private void InsertRecursive(QuadTreeNode node, QuadTreeObject obj)
        {
            // 如果对象不在节点边界内，跳过
            if (!node.Bounds.Intersects(obj.Rect))
                return;
            
            // 检查是否应该插入到子节点
            if (node.Children[0] != null)
            {
                int index = GetChildIndex(node, obj.Rect);
                if (index >= 0 && node.Children[index] != null)
                {
                    InsertRecursive(node.Children[index]!, obj);
                    return;
                }
            }
            
            // 插入到当前节点
            node.Objects.Add(obj);
            
            // 检查是否需要分裂
            if (node.Objects.Count > MAX_OBJECTS_PER_NODE && 
                node.Depth < MAX_DEPTH && 
                Math.Min(node.Bounds.Width, node.Bounds.Height) > MIN_NODE_SIZE)
            {
                SplitNode(node);
            }
        }
        
        private void RemoveFromNode(QuadTreeNode node, QuadTreeObject obj)
        {
            // 从当前节点移除
            node.Objects.Remove(obj);
            
            // 从子节点递归移除
            if (node.Children[0] != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (node.Children[i] != null && node.Children[i]!.Bounds.Intersects(obj.Rect))
                    {
                        RemoveFromNode(node.Children[i]!, obj);
                    }
                }
            }
        }
        
        private void QueryRecursive(QuadTreeNode node, Rect viewport, List<NoteViewModel> results)
        {
            // 快速边界检查
            if (!node.Bounds.Intersects(viewport))
                return;
            
            // 添加当前节点中的对象
            foreach (var obj in node.Objects)
            {
                if (viewport.Intersects(obj.Rect))
                {
                    results.Add(obj.Note);
                }
            }
            
            // 查询子节点
            if (node.Children[0] != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (node.Children[i] != null && node.Children[i]!.Bounds.Intersects(viewport))
                    {
                        QueryRecursive(node.Children[i]!, viewport, results);
                    }
                }
            }
        }
        
        private void SplitNode(QuadTreeNode node)
        {
            CreateChildren(node);
            
            // 重新分配对象到子节点
            var objectsToMove = new List<QuadTreeObject>();
            foreach (var obj in node.Objects)
            {
                int index = GetChildIndex(node, obj.Rect);
                if (index >= 0)
                {
                    objectsToMove.Add(obj);
                }
            }
            
            // 移动对象并移除从父节点
            foreach (var obj in objectsToMove)
            {
                node.Objects.Remove(obj);
                int index = GetChildIndex(node, obj.Rect);
                if (index >= 0 && node.Children[index] != null)
                {
                    InsertRecursive(node.Children[index]!, obj);
                }
            }
        }
        
        private void CreateChildren(QuadTreeNode node)
        {
            if (node.Children[0] != null) return; // 已经创建
            
            double halfWidth = node.Bounds.Width / 2;
            double halfHeight = node.Bounds.Height / 2;
            double x = node.Bounds.X;
            double y = node.Bounds.Y;
            
            node.Children[0] = CreateNode(new Rect(x, y, halfWidth, halfHeight), node.Depth + 1);
            node.Children[1] = CreateNode(new Rect(x + halfWidth, y, halfWidth, halfHeight), node.Depth + 1);
            node.Children[2] = CreateNode(new Rect(x, y + halfHeight, halfWidth, halfHeight), node.Depth + 1);
            node.Children[3] = CreateNode(new Rect(x + halfWidth, y + halfHeight, halfWidth, halfHeight), node.Depth + 1);
        }
        
        private int GetChildIndex(QuadTreeNode node, Rect rect)
        {
            int index = 0;
            double centerX = node.Bounds.X + node.Bounds.Width / 2;
            double centerY = node.Bounds.Y + node.Bounds.Height / 2;
            
            bool left = rect.Right < centerX;
            bool right = rect.Left > centerX;
            bool top = rect.Bottom < centerY;
            bool bottom = rect.Top > centerY;
            
            if (left)
            {
                if (top) index = 0;
                else if (bottom) index = 2;
                else return -1; // 跨越中线
            }
            else if (right)
            {
                if (top) index = 1;
                else if (bottom) index = 3;
                else return -1; // 跨越中线
            }
            else
            {
                return -1; // 跨越中线
            }
            
            return index;
        }
        
        private Rect ExpandBounds(Rect bounds, double factor)
        {
            double expansionX = bounds.Width * factor;
            double expansionY = bounds.Height * factor;
            return new Rect(
                bounds.X - expansionX,
                bounds.Y - expansionY,
                bounds.Width + 2 * expansionX,
                bounds.Height + 2 * expansionY);
        }
        
        private bool IsBoundsSufficient(Rect currentBounds, Rect requiredBounds)
        {
            return currentBounds.Contains(requiredBounds);
        }
        
        private Rect CalculateTotalBounds(IEnumerable<Rect> rects)
        {
            bool first = true;
            double minX = 0, minY = 0, maxX = 0, maxY = 0;
            
            foreach (var rect in rects)
            {
                if (first)
                {
                    minX = rect.X;
                    minY = rect.Y;
                    maxX = rect.Right;
                    maxY = rect.Bottom;
                    first = false;
                }
                else
                {
                    minX = Math.Min(minX, rect.X);
                    minY = Math.Min(minY, rect.Y);
                    maxX = Math.Max(maxX, rect.Right);
                    maxY = Math.Max(maxY, rect.Bottom);
                }
            }
            
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
        
        private int CountNodes(QuadTreeNode node)
        {
            if (node == null) return 0;
            
            int count = 1;
            for (int i = 0; i < 4; i++)
            {
                if (node.Children[i] != null)
                {
                    count += CountNodes(node.Children[i]!);
                }
            }
            return count;
        }
        
        #endregion
    }
    
    /// <summary>
    /// 四叉树节点
    /// </summary>
    public class QuadTreeNode
    {
        public Rect Bounds { get; private set; }
        public int Depth { get; private set; }
        public List<QuadTreeObject> Objects { get; private set; } = new List<QuadTreeObject>(50);
        public QuadTreeNode?[] Children { get; private set; } = new QuadTreeNode?[4];
        
        public QuadTreeNode(Rect bounds, int depth)
        {
            Initialize(bounds, depth);
        }
        
        public void Initialize(Rect bounds, int depth)
        {
            Bounds = bounds;
            Depth = depth;
            Objects = new List<QuadTreeObject>(50); // 预分配容量
            Children = new QuadTreeNode[4];
        }
        
        public void Clear()
        {
            Objects?.Clear();
            for (int i = 0; i < 4; i++)
            {
                Children[i] = null;
            }
        }
    }
    
    /// <summary>
    /// 四叉树对象
    /// </summary>
    public class QuadTreeObject
    {
        public NoteViewModel Note { get; }
        public Rect Rect { get; set; }
        
        public QuadTreeObject(NoteViewModel note, Rect rect)
        {
            Note = note;
            Rect = rect;
        }
    }
    
    /// <summary>
    /// 空间索引性能统计
    /// </summary>
    public class SpatialIndexStats
    {
        public double BuildTime { get; set; }
        public double LastQueryTime { get; set; }
        public int LastQueryResults { get; set; }
        public int TotalQueries { get; set; }
        public int TotalObjects { get; set; }
        public int NodeCount { get; set; }
        
        public double AverageQueryTime => TotalQueries > 0 ? LastQueryTime : 0;
    }
}