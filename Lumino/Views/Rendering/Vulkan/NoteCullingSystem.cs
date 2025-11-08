using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Utils;

namespace Lumino.Views.Rendering.Vulkan
{
    /// <summary>
    /// 音符剔除系统 - 高效剔除不可见音符，减少GPU负载
    /// </summary>
    public class NoteCullingSystem
    {
        public NoteCullingSystem()
        {
            RenderingUtils.BrushCacheCleared += OnGlobalBrushCacheCleared;
        }

        private void OnGlobalBrushCacheCleared()
        {
            try { ClearCache(); } catch { }
        }
        // 视口信息
        private Rect _currentViewport;
        private double _zoomLevel = 1.0;
        
        // 剔除统计
        private int _totalNotes;
        private int _visibleNotes;
        private int _culledNotes;
        
        // LOD系统参数
        private const int MAX_LOD_LEVELS = 4;
        private readonly double[] _lodThresholds = { 0.5, 1.0, 2.0, 4.0 }; // 缩放阈值
        
        // 空间分割 - 使用网格加速剔除
        private readonly Dictionary<(int, int), List<NoteViewModel>> _spatialGrid = new();
        private const int GRID_CELL_SIZE = 100; // 像素
        
        // 缓存机制
        private readonly Dictionary<NoteViewModel, CachedNoteInfo> _noteCache = new();
        private readonly Queue<NoteViewModel> _cacheOrder = new();
        private const int MAX_CACHE_SIZE = 10000;
        
        // 性能监控控
        private DateTime _lastCullTime = DateTime.MinValue;
        private const int CULL_INTERVAL_MS = 16; // 60 FPS
        
        /// <summary>
        /// 缓存的音符信息
        /// </summary>
        private class CachedNoteInfo
        {
            public Rect Bounds { get; set; }
            public int LodLevel { get; set; }
            public DateTime LastUsed { get; set; }
            public bool IsDirty { get; set; }
            
            public CachedNoteInfo(Rect bounds, int lodLevel)
            {
                Bounds = bounds;
                LodLevel = lodLevel;
                LastUsed = DateTime.Now;
                IsDirty = false;
            }
        }

        /// <summary>
        /// 设置当前视口
        /// </summary>
        public void SetViewport(Rect viewport, double zoomLevel)
        {
            if (_currentViewport != viewport || Math.Abs(_zoomLevel - zoomLevel) > 0.01)
            {
                _currentViewport = viewport;
                _zoomLevel = zoomLevel;
                InvalidateCache();
            }
        }

        /// <summary>
        /// 执行高效剔除 - 返回可见音符
        /// </summary>
        public List<(NoteViewModel note, Rect bounds, int lodLevel)> CullNotes(IEnumerable<NoteViewModel> notes)
        {
            var currentTime = DateTime.Now;
            if ((currentTime - _lastCullTime).TotalMilliseconds < CULL_INTERVAL_MS)
            {
                return GetCachedVisibleNotes();
            }
            
            _lastCullTime = currentTime;
            
            _totalNotes = notes.Count();
            _visibleNotes = 0;
            _culledNotes = 0;
            
            var visibleNotes = new List<(NoteViewModel, Rect, int)>();
            
            // 构建空间网格
            BuildSpatialGrid(notes);
            
            // 获取影响的网格单元
            var affectedCells = GetAffectedGridCells();
            
            // 检查可见音符
            foreach (var cell in affectedCells)
            {
                if (!_spatialGrid.TryGetValue(cell, out var cellNotes)) continue;
                
                foreach (var note in cellNotes)
                {
                    var result = CheckNoteVisibility(note);
                    if (result.isVisible)
                    {
                        visibleNotes.Add((note, result.bounds, result.lodLevel));
                        _visibleNotes++;
                    }
                    else
                    {
                        _culledNotes++;
                    }
                }
            }
            
            // 更新缓存
            UpdateCache(visibleNotes);
            
            return visibleNotes;
        }

        /// <summary>
        /// 检查单个音符的可见性
        /// </summary>
        private (bool isVisible, Rect bounds, int lodLevel) CheckNoteVisibility(NoteViewModel note)
        {
            if (!_noteCache.TryGetValue(note, out var cachedInfo))
            {
                cachedInfo = new CachedNoteInfo(new Rect(), 0);
                _noteCache[note] = cachedInfo;
                _cacheOrder.Enqueue(note);
                
                // 清理旧缓存
                if (_noteCache.Count > MAX_CACHE_SIZE)
                {
                    if (_cacheOrder.TryDequeue(out var oldNote))
                    {
                        _noteCache.Remove(oldNote);
                    }
                }
            }
            
            if (cachedInfo.IsDirty || cachedInfo.Bounds == new Rect())
            {
                // 计算音符边界
                var bounds = CalculateNoteBounds(note);
                cachedInfo.Bounds = bounds;
                cachedInfo.IsDirty = false;
            }
            
            // 计算LOD级别
            var lodLevel = CalculateLodLevel(cachedInfo.Bounds);
            cachedInfo.LodLevel = lodLevel;
            cachedInfo.LastUsed = DateTime.Now;
            
            // 视口剔除
            var isVisible = IsRectVisible(cachedInfo.Bounds);
            
            return (isVisible, cachedInfo.Bounds, lodLevel);
        }

        /// <summary>
        /// 计算音符边界
        /// </summary>
        private Rect CalculateNoteBounds(NoteViewModel note)
        {
            // 简化的边界计算 - 实际应根据PianoRoll坐标转换
            return new Rect(
                note.StartPosition.ToDouble() * 10, // 时间到像素转换
                (127 - note.Pitch) * 5, // 音高到像素转换
                10.0, // 持续时间到像素转换
                20 // 固定高度
            );
        }

        /// <summary>
        /// 计算LOD级别
        /// </summary>
        private int CalculateLodLevel(Rect bounds)
        {
            var area = bounds.Width * bounds.Height;
            var normalizedArea = area * _zoomLevel;
            
            for (int i = 0; i < _lodThresholds.Length; i++)
            {
                if (normalizedArea < _lodThresholds[i])
                {
                    return i;
                }
            }
            
            return MAX_LOD_LEVELS - 1;
        }

        /// <summary>
        /// 检查矩形是否在视口中
        /// </summary>
        private bool IsRectVisible(Rect rect)
        {
            if (_currentViewport.Width <= 0 || _currentViewport.Height <= 0)
                return true;
            
            return _currentViewport.Intersects(rect);
        }

        /// <summary>
        /// 构建空间网格
        /// </summary>
        private void BuildSpatialGrid(IEnumerable<NoteViewModel> notes)
        {
            _spatialGrid.Clear();
            
            foreach (var note in notes)
            {
                if (!_noteCache.TryGetValue(note, out var cachedInfo))
                {
                    cachedInfo = new CachedNoteInfo(new Rect(), 0);
                    _noteCache[note] = cachedInfo;
                }
                
                if (cachedInfo.IsDirty || cachedInfo.Bounds == new Rect())
                {
                    cachedInfo.Bounds = CalculateNoteBounds(note);
                    cachedInfo.IsDirty = false;
                }
                
                var bounds = cachedInfo.Bounds;
                var cells = GetGridCells(bounds);
                
                foreach (var cell in cells)
                {
                    if (!_spatialGrid.TryGetValue(cell, out var cellNotes))
                    {
                        cellNotes = new List<NoteViewModel>();
                        _spatialGrid[cell] = cellNotes;
                    }
                    cellNotes.Add(note);
                }
            }
        }

        /// <summary>
        /// 获取矩形覆盖的网格单元
        /// </summary>
        private List<(int, int)> GetGridCells(Rect rect)
        {
            var cells = new List<(int, int)>();
            
            int minX = (int)(rect.X / GRID_CELL_SIZE);
            int maxX = (int)((rect.X + rect.Width) / GRID_CELL_SIZE);
            int minY = (int)(rect.Y / GRID_CELL_SIZE);
            int maxY = (int)((rect.Y + rect.Height) / GRID_CELL_SIZE);
            
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    cells.Add((x, y));
                }
            }
            
            return cells;
        }

        /// <summary>
        /// 获取受影响的网格单元
        /// </summary>
        private List<(int, int)> GetAffectedGridCells()
        {
            var cells = new List<(int, int)>();
            
            int minX = (int)(_currentViewport.X / GRID_CELL_SIZE);
            int maxX = (int)((_currentViewport.X + _currentViewport.Width) / GRID_CELL_SIZE);
            int minY = (int)(_currentViewport.Y / GRID_CELL_SIZE);
            int maxY = (int)((_currentViewport.Y + _currentViewport.Height) / GRID_CELL_SIZE);
            
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    cells.Add((x, y));
                }
            }
            
            return cells;
        }

        /// <summary>
        /// 从缓存获取可见音符
        /// </summary>
        private List<(NoteViewModel, Rect, int)> GetCachedVisibleNotes()
        {
            var visibleNotes = new List<(NoteViewModel, Rect, int)>();
            
            foreach (var kvp in _noteCache)
            {
                if (IsRectVisible(kvp.Value.Bounds))
                {
                    visibleNotes.Add((kvp.Key, kvp.Value.Bounds, kvp.Value.LodLevel));
                }
            }
            
            return visibleNotes;
        }

        /// <summary>
        /// 更新缓存
        /// </summary>
        private void UpdateCache(List<(NoteViewModel note, Rect bounds, int lodLevel)> visibleNotes)
        {
            foreach (var note in visibleNotes)
            {
                if (_noteCache.TryGetValue(note.note, out var cachedInfo))
                {
                    cachedInfo.Bounds = note.bounds;
                    cachedInfo.LodLevel = note.lodLevel;
                }
            }
        }

        /// <summary>
        /// 使缓存失效
        /// </summary>
        private void InvalidateCache()
        {
            foreach (var cachedInfo in _noteCache.Values)
            {
                cachedInfo.IsDirty = true;
            }
        }

        /// <summary>
        /// 获取剔除统计
        /// </summary>
        public (int total, int visible, int culled, double efficiency) GetCullingStats()
        {
            var efficiency = _totalNotes > 0 ? (double)_culledNotes / _totalNotes : 0;
            return (_totalNotes, _visibleNotes, _culledNotes, efficiency);
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public void ClearCache()
        {
            _noteCache.Clear();
            _cacheOrder.Clear();
            _spatialGrid.Clear();
        }
    }

    /// <summary>
    /// 音符LOD渲染器 - 根据距离和缩放级别调整渲染质量
    /// </summary>
    public class NoteLodRenderer
    {
        private readonly Dictionary<int, LodConfig> _lodConfigs = new();
        
        public NoteLodRenderer()
        {
            // 配置不同LOD级别的渲染参数
            _lodConfigs[0] = new LodConfig { DetailLevel = 1.0f, EnableEffects = true, UseText = true };
            _lodConfigs[1] = new LodConfig { DetailLevel = 0.8f, EnableEffects = true, UseText = false };
            _lodConfigs[2] = new LodConfig { DetailLevel = 0.5f, EnableEffects = false, UseText = false };
            _lodConfigs[3] = new LodConfig { DetailLevel = 0.3f, EnableEffects = false, UseText = false };
        }

        /// <summary>
        /// 根据LOD级别获取渲染配置
        /// </summary>
        public LodConfig GetLodConfig(int lodLevel)
        {
            return _lodConfigs.GetValueOrDefault(lodLevel, _lodConfigs[0]);
        }
    }

    /// <summary>
    /// LOD配置
    /// </summary>
    public class LodConfig
    {
        public float DetailLevel { get; set; }
        public bool EnableEffects { get; set; }
        public bool UseText { get; set; }
        public bool UseRoundedCorners { get; set; } = true;
    }
}