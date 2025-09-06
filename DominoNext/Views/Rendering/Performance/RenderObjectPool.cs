/*using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace DominoNext.Views.Rendering.Performance
{
    /// <summary>
    /// 渲染对象池 - 避免频繁的对象创建和GC压力
    /// </summary>
    public class RenderObjectPool
    {
        private static readonly Lazy<RenderObjectPool> _instance = new(() => new RenderObjectPool());
        public static RenderObjectPool Instance => _instance.Value;

        // 画刷池
        private readonly ConcurrentDictionary<Color, SolidColorBrush> _solidBrushPool = new();
        private readonly ConcurrentDictionary<(Color color, double opacity), SolidColorBrush> _solidBrushWithOpacityPool = new();
        
        // 画笔池
        private readonly ConcurrentDictionary<(Color color, double thickness), Pen> _penPool = new();
        
        // 几何对象池
        private readonly ConcurrentQueue<List<Rect>> _rectListPool = new();
        private readonly ConcurrentQueue<List<(Rect rect, Color color)>> _batchRenderDataPool = new();

        private RenderObjectPool() { }

        /// <summary>
        /// 获取纯色画刷
        /// </summary>
        public SolidColorBrush GetSolidBrush(Color color)
        {
            return _solidBrushPool.GetOrAdd(color, c => new SolidColorBrush(c));
        }

        /// <summary>
        /// 获取带透明度的纯色画刷
        /// </summary>
        public SolidColorBrush GetSolidBrush(Color color, double opacity)
        {
            var key = (color, opacity);
            return _solidBrushWithOpacityPool.GetOrAdd(key, k => new SolidColorBrush(k.color, k.opacity));
        }

        /// <summary>
        /// 获取画笔
        /// </summary>
        public Pen GetPen(Color color, double thickness)
        {
            var key = (color, thickness);
            return _penPool.GetOrAdd(key, k => new Pen(GetSolidBrush(k.color), k.thickness));
        }

        /// <summary>
        /// 获取矩形列表（用于批处理）
        /// </summary>
        public List<Rect> GetRectList()
        {
            if (_rectListPool.TryDequeue(out var list))
            {
                list.Clear();
                return list;
            }
            return new List<Rect>();
        }

        /// <summary>
        /// 归还矩形列表
        /// </summary>
        public void ReturnRectList(List<Rect> list)
        {
            if (list.Count < 1000) // 避免缓存过大的列表
            {
                _rectListPool.Enqueue(list);
            }
        }

        /// <summary>
        /// 获取批渲染数据列表
        /// </summary>
        public List<(Rect rect, Color color)> GetBatchRenderDataList()
        {
            if (_batchRenderDataPool.TryDequeue(out var list))
            {
                list.Clear();
                return list;
            }
            return new List<(Rect rect, Color color)>();
        }

        /// <summary>
        /// 归还批渲染数据列表
        /// </summary>
        public void ReturnBatchRenderDataList(List<(Rect rect, Color color)> list)
        {
            if (list.Count < 1000)
            {
                _batchRenderDataPool.Enqueue(list);
            }
        }

        /// <summary>
        /// 清理池（释放内存）
        /// </summary>
        public void ClearPools()
        {
            _solidBrushPool.Clear();
            _solidBrushWithOpacityPool.Clear();
            _penPool.Clear();
            
            while (_rectListPool.TryDequeue(out _)) { }
            while (_batchRenderDataPool.TryDequeue(out _)) { }
        }
    }
}*/