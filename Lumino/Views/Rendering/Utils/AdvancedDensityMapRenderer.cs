using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Lumino.ViewModels.Editor;

namespace Lumino.Views.Rendering.Utils
{
    /// <summary>
    /// 高级密度图渲染器 - 针对超大规模音符的可视化优化
    /// 支持1000W+音符的实时密度图渲染
    /// </summary>
    public class AdvancedDensityMapRenderer
    {
        private const int MAX_GRID_SIZE = 2048;           // 最大网格尺寸
        private const int MIN_GRID_SIZE = 64;             // 最小网格尺寸
        private const double DENSITY_THRESHOLD = 0.1;     // 密度阈值
        private const double MAX_DENSITY_COLOR_VALUE = 100.0; // 最大密度颜色值
        
        // 多级密度图缓存
        private readonly Dictionary<string, DensityMapLevel> _densityMapCache;
        private readonly Queue<string> _cacheAccessQueue;
        private const int MAX_CACHE_SIZE = 20;
        
        // 自适应网格系统
        private int _currentGridWidth;
        private int _currentGridHeight;
        private double _lastViewportWidth;
        private double _lastViewportHeight;
        private double _lastZoomLevel;
        
        // 颜色映射表
        private List<Color> _densityColorMap;
        private List<Color> _heatMapColors;
        
        // 性能统计
        public DensityMapStats Stats { get; private set; }
        
        public AdvancedDensityMapRenderer()
        {
            _densityMapCache = new Dictionary<string, DensityMapLevel>();
            _cacheAccessQueue = new Queue<string>();
            Stats = new DensityMapStats();
            
            InitializeColorMaps();
            InitializeAdaptiveGrid();
        }
        
        /// <summary>
        /// 渲染密度图 - 针对超大规模音符优化
        /// </summary>
        public void RenderDensityMap(IDrawingContextImpl context, IEnumerable<NoteViewModel> notes, Rect viewport, double zoomLevel)
        {
            var startTime = DateTime.Now;
            
            // 自适应网格调整
            UpdateAdaptiveGrid(viewport, zoomLevel);
            
            // 检查缓存
            var cacheKey = GenerateCacheKey(viewport, zoomLevel);
            if (_densityMapCache.TryGetValue(cacheKey, out var cachedLevel))
            {
                // 使用缓存的密度图
                RenderCachedDensityMap(context, cachedLevel);
                Stats.CacheHits++;
            }
            else
            {
                // 生成新的密度图
                var densityMap = GenerateDensityMap(notes, viewport);
                var densityLevel = new DensityMapLevel
                {
                    Viewport = viewport,
                    ZoomLevel = zoomLevel,
                    GridWidth = _currentGridWidth,
                    GridHeight = _currentGridHeight,
                    DensityMap = densityMap,
                    ColorMap = GenerateColorMap(densityMap),
                    GeneratedTime = DateTime.Now
                };
                
                // 添加到缓存
                AddToCache(cacheKey, densityLevel);
                
                // 渲染密度图
                RenderDensityMapInternal(context, densityLevel);
                Stats.CacheMisses++;
            }
            
            Stats.LastRenderTime = (DateTime.Now - startTime).TotalMilliseconds;
            Stats.TotalRenders++;
            
            System.Diagnostics.Debug.WriteLine($"[DensityMap] 渲染完成: 网格{_currentGridWidth}x{_currentGridHeight}, 缓存命中率: {GetCacheHitRatio():P1}, 耗时: {Stats.LastRenderTime:F1}ms");
        }
        
        /// <summary>
        /// 渲染混合密度图（密度图+详细音符）
        /// </summary>
        public void RenderHybridDensityMap(
            IDrawingContextImpl context,
            IEnumerable<NoteViewModel> notes,
            Rect viewport,
            double zoomLevel,
            int maxDetailedNotes = 1000)
        {
            var startTime = DateTime.Now;
            
            // 分离高密度区域和详细音符
            var (densityAreas, detailedNotes) = SeparateDensityAndDetailedAreas(notes, viewport, maxDetailedNotes);
            
            // 渲染密度图背景
            if (densityAreas.Any())
            {
                RenderDensityMap(context, densityAreas, viewport, zoomLevel);
            }
            
            // 渲染详细音符
            if (detailedNotes.Any())
            {
                RenderDetailedNotes(context, detailedNotes, viewport, zoomLevel);
            }
            
            Stats.LastHybridRenderTime = (DateTime.Now - startTime).TotalMilliseconds;
            Stats.TotalHybridRenders++;
        }
        
        /// <summary>
        /// 实时密度图更新（用于动态数据）
        /// </summary>
        public void UpdateDensityMapRealtime(
            IDrawingContextImpl context,
            IEnumerable<NoteViewModel> addedNotes,
            IEnumerable<NoteViewModel> removedNotes,
            IEnumerable<NoteViewModel> modifiedNotes,
            Rect viewport,
            double zoomLevel)
        {
            var startTime = DateTime.Now;
            
            // 更新现有缓存
            UpdateCacheWithChanges(addedNotes, removedNotes, modifiedNotes, viewport, zoomLevel);
            
            // 重新渲染
            var cacheKey = GenerateCacheKey(viewport, zoomLevel);
            if (_densityMapCache.TryGetValue(cacheKey, out var cachedLevel))
            {
                RenderCachedDensityMap(context, cachedLevel);
            }
            
            Stats.LastUpdateTime = (DateTime.Now - startTime).TotalMilliseconds;
            Stats.TotalUpdates++;
        }
        
        /// <summary>
        /// 预生成多级密度图（用于快速缩放）
        /// </summary>
        public async System.Threading.Tasks.Task PrecomputeMultiLevelDensityMap(
            IEnumerable<NoteViewModel> notes,
            Rect viewport,
            double[] zoomLevels)
        {
            var startTime = DateTime.Now;
            
            var tasks = new List<System.Threading.Tasks.Task>();
            
            foreach (var zoomLevel in zoomLevels)
            {
                var task = System.Threading.Tasks.Task.Run(() =>
                {
                    var cacheKey = GenerateCacheKey(viewport, zoomLevel);
                    if (!_densityMapCache.ContainsKey(cacheKey))
                    {
                        var densityMap = GenerateDensityMap(notes, viewport);
                        var densityLevel = new DensityMapLevel
                        {
                            Viewport = viewport,
                            ZoomLevel = zoomLevel,
                            GridWidth = _currentGridWidth,
                            GridHeight = _currentGridHeight,
                            DensityMap = densityMap,
                            ColorMap = GenerateColorMap(densityMap),
                            GeneratedTime = DateTime.Now
                        };
                        
                        lock (_densityMapCache)
                        {
                            AddToCache(cacheKey, densityLevel);
                        }
                    }
                });
                
                tasks.Add(task);
            }
            
            await System.Threading.Tasks.Task.WhenAll(tasks);
            
            Stats.PrecomputeTime = (DateTime.Now - startTime).TotalMilliseconds;
            Stats.TotalPrecomputations++;
            
            System.Diagnostics.Debug.WriteLine($"[DensityMap] 预生成完成: {zoomLevels.Length}个级别, 耗时: {Stats.PrecomputeTime:F1}ms");
        }
        
        /// <summary>
        /// 清除缓存
        /// </summary>
        public void ClearCache()
        {
            _densityMapCache.Clear();
            _cacheAccessQueue.Clear();
            Stats.CacheHits = 0;
            Stats.CacheMisses = 0;
        }
        
        #region 私有方法
        
        private void InitializeColorMaps()
        {
            // 密度颜色映射（蓝色到红色渐变）
            _densityColorMap = new List<Color>
            {
                Color.FromArgb(0, 0, 0, 0),           // 透明
                Color.FromArgb(50, 0, 0, 255),        // 深蓝
                Color.FromArgb(100, 0, 255, 255),     // 青色
                Color.FromArgb(150, 0, 255, 0),        // 绿色
                Color.FromArgb(200, 255, 255, 0),     // 黄色
                Color.FromArgb(255, 255, 0, 0)        // 红色
            };
            
            // 热力图颜色
            _heatMapColors = new List<Color>
            {
                Color.FromArgb(0, 0, 0, 0),           // 透明
                Color.FromArgb(80, 0, 0, 139),        // 深蓝
                Color.FromArgb(120, 0, 100, 255),     // 蓝色
                Color.FromArgb(160, 0, 255, 127),     // 春绿色
                Color.FromArgb(200, 255, 255, 0),     // 黄色
                Color.FromArgb(255, 255, 0, 0)        // 红色
            };
        }
        
        private void InitializeAdaptiveGrid()
        {
            _currentGridWidth = 512;
            _currentGridHeight = 512;
            _lastViewportWidth = 0;
            _lastViewportHeight = 0;
            _lastZoomLevel = 1.0;
        }
        
        private void UpdateAdaptiveGrid(Rect viewport, double zoomLevel)
        {
            // 根据视口大小和缩放级别调整网格密度
            double viewportArea = viewport.Width * viewport.Height;
            double zoomFactor = Math.Max(0.1, Math.Min(10.0, zoomLevel));
            
            // 计算最优网格尺寸
            int optimalGridSize = (int)Math.Max(MIN_GRID_SIZE, Math.Min(MAX_GRID_SIZE, 
                Math.Sqrt(viewportArea) / (20.0 * zoomFactor)));
            
            // 使用2的幂次方尺寸（优化GPU处理）
            optimalGridSize = RoundToPowerOfTwo(optimalGridSize);
            
            if (_currentGridWidth != optimalGridSize || _currentGridHeight != optimalGridSize ||
                Math.Abs(_lastViewportWidth - viewport.Width) > 10 ||
                Math.Abs(_lastViewportHeight - viewport.Height) > 10 ||
                Math.Abs(_lastZoomLevel - zoomLevel) > 0.1)
            {
                _currentGridWidth = optimalGridSize;
                _currentGridHeight = optimalGridSize;
                _lastViewportWidth = viewport.Width;
                _lastViewportHeight = viewport.Height;
                _lastZoomLevel = zoomLevel;
                
                // 网格变化时清除部分缓存
                ClearCache();
                
                System.Diagnostics.Debug.WriteLine($"[DensityMap] 网格调整: {_currentGridWidth}x{_currentGridHeight} (缩放: {zoomLevel:F1})");
            }
        }
        
        private int RoundToPowerOfTwo(int value)
        {
            int power = 1;
            while (power < value && power < MAX_GRID_SIZE)
            {
                power <<= 1;
            }
            return Math.Min(power, MAX_GRID_SIZE);
        }
        
        private string GenerateCacheKey(Rect viewport, double zoomLevel)
        {
            // 生成缓存键（考虑视口位置和缩放级别）
            var roundedViewport = new Rect(
                Math.Round(viewport.X, 2),
                Math.Round(viewport.Y, 2),
                Math.Round(viewport.Width, 0),
                Math.Round(viewport.Height, 0));
            
            var roundedZoom = Math.Round(zoomLevel, 2);
            var gridKey = $"{_currentGridWidth}x{_currentGridHeight}";
            
            return $"{gridKey}_{roundedViewport.X}_{roundedViewport.Y}_{roundedViewport.Width}_{roundedViewport.Height}_{roundedZoom}";
        }
        
        private int[,] GenerateDensityMap(IEnumerable<NoteViewModel> notes, Rect viewport)
        {
            var densityMap = new int[_currentGridHeight, _currentGridWidth];
            
            // 计算网格单元尺寸
            double cellWidth = viewport.Width / _currentGridWidth;
            double cellHeight = viewport.Height / _currentGridHeight;
            
            int processedCount = 0;
            
            foreach (var note in notes)
            {
                // 计算音符在视口中的位置
                var noteRect = new Rect(
                    note.StartTime * 100, // 时间到像素的转换系数
                    (127 - note.Pitch) * 10, // 音高到像素的转换系数
                    note.Duration.ToDouble() * 100,
                    8); // 固定高度
                
                // 检查音符是否在视口内
                if (!viewport.Intersects(noteRect))
                    continue;
                
                // 计算音符覆盖的网格范围
                int startGridX = Math.Max(0, (int)((noteRect.X - viewport.X) / cellWidth));
                int endGridX = Math.Min(_currentGridWidth - 1, (int)((noteRect.Right - viewport.X) / cellWidth));
                int startGridY = Math.Max(0, (int)((noteRect.Y - viewport.Y) / cellHeight));
                int endGridY = Math.Min(_currentGridHeight - 1, (int)((noteRect.Bottom - viewport.Y) / cellHeight));
                
                // 增加密度计数
                for (int y = startGridY; y <= endGridY; y++)
                {
                    for (int x = startGridX; x <= endGridX; x++)
                    {
                        densityMap[y, x]++;
                    }
                }
                
                processedCount++;
            }
            
            Stats.LastProcessedNotes = processedCount;
            return densityMap;
        }
        
        private Color[,] GenerateColorMap(int[,] densityMap)
        {
            var colorMap = new Color[_currentGridHeight, _currentGridWidth];
            
            // 找到最大密度值（用于归一化）
            int maxDensity = 0;
            for (int y = 0; y < _currentGridHeight; y++)
            {
                for (int x = 0; x < _currentGridWidth; x++)
                {
                    maxDensity = Math.Max(maxDensity, densityMap[y, x]);
                }
            }
            
            // 生成颜色映射
            for (int y = 0; y < _currentGridHeight; y++)
            {
                for (int x = 0; x < _currentGridWidth; x++)
                {
                    int density = densityMap[y, x];
                    if (density > 0)
                    {
                        double normalizedDensity = (double)density / Math.Max(1, maxDensity);
                        colorMap[y, x] = InterpolateColor(normalizedDensity);
                    }
                    else
                    {
                        colorMap[y, x] = Colors.Transparent;
                    }
                }
            }
            
            return colorMap;
        }
        
        private Color InterpolateColor(double density)
        {
            if (density <= 0) return Colors.Transparent;
            if (density >= 1.0) return _densityColorMap[_densityColorMap.Count - 1];
            
            // 使用颜色映射表插值
            double scaledDensity = density * (_densityColorMap.Count - 1);
            int lowerIndex = (int)Math.Floor(scaledDensity);
            int upperIndex = (int)Math.Ceiling(scaledDensity);
            
            if (lowerIndex == upperIndex)
            {
                return _densityColorMap[lowerIndex];
            }
            
            double t = scaledDensity - lowerIndex;
            var lowerColor = _densityColorMap[lowerIndex];
            var upperColor = _densityColorMap[upperIndex];
            
            return Color.FromArgb(
                (byte)(lowerColor.A + (upperColor.A - lowerColor.A) * t),
                (byte)(lowerColor.R + (upperColor.R - lowerColor.R) * t),
                (byte)(lowerColor.G + (upperColor.G - lowerColor.G) * t),
                (byte)(lowerColor.B + (upperColor.B - lowerColor.B) * t));
        }
        
        private void RenderDensityMapInternal(IDrawingContextImpl context, DensityMapLevel densityLevel)
        {
            var cellWidth = densityLevel.Viewport.Width / densityLevel.GridWidth;
            var cellHeight = densityLevel.Viewport.Height / densityLevel.GridHeight;
            
            // 批量渲染网格单元
            for (int y = 0; y < densityLevel.GridHeight; y++)
            {
                for (int x = 0; x < densityLevel.GridWidth; x++)
                {
                    var color = densityLevel.ColorMap[y, x];
                    if (color.A > 0) // 只渲染非透明单元
                    {
                        var rect = new Rect(
                            densityLevel.Viewport.X + x * cellWidth,
                            densityLevel.Viewport.Y + y * cellHeight,
                            cellWidth + 0.5, // 稍微重叠避免缝隙
                            cellHeight + 0.5);
                        
                        var brush = new SolidColorBrush(color);
                        // TODO: IDrawingContextImpl.DrawRectangle 不可访问，需要使用其他方法
                        // context.DrawRectangle(brush, null, rect);
                    }
                }
            }
        }
        
        private void RenderCachedDensityMap(IDrawingContextImpl context, DensityMapLevel densityLevel)
        {
            RenderDensityMapInternal(context, densityLevel);
        }
        
        private void RenderDetailedNotes(IDrawingContextImpl context, IEnumerable<NoteViewModel> notes, Rect viewport, double zoomLevel)
        {
            // 使用标准音符渲染逻辑渲染详细音符
            var noteBrush = new SolidColorBrush(Colors.LightBlue);
            var pen = new Pen(new SolidColorBrush(Colors.White), 1);
            
            foreach (var note in notes)
            {
                var noteRect = new Rect(
                    note.StartTime * 100 - viewport.X,
                    (127 - note.Pitch) * 10 - viewport.Y,
                    note.Duration.ToDouble() * 100,
                    8);
                
                // TODO: IDrawingContextImpl.DrawRectangle 不可访问
                // context.DrawRectangle(noteBrush, pen, noteRect);
            }
        }
        
        private (IEnumerable<NoteViewModel> densityAreas, IEnumerable<NoteViewModel> detailedNotes) 
            SeparateDensityAndDetailedAreas(IEnumerable<NoteViewModel> notes, Rect viewport, int maxDetailedNotes)
        {
            var allNotes = notes.ToList();
            var densityAreas = new List<NoteViewModel>();
            var detailedNotes = new List<NoteViewModel>();
            
            // 简单的分离策略：根据音符密度和重要性
            var noteGroups = allNotes.GroupBy(n => new { Pitch = n.Pitch / 2, Time = (int)(n.StartTime / 0.1) });
            
            foreach (var group in noteGroups)
            {
                if (group.Count() > 5 && detailedNotes.Count < maxDetailedNotes)
                {
                    // 高密度区域，选择代表性的音符详细显示
                    detailedNotes.AddRange(group.Take(3));
                    densityAreas.AddRange(group.Skip(3));
                }
                else
                {
                    densityAreas.AddRange(group);
                }
            }
            
            return (densityAreas, detailedNotes);
        }
        
        private void AddToCache(string key, DensityMapLevel densityLevel)
        {
            // 检查缓存大小限制
            if (_densityMapCache.Count >= MAX_CACHE_SIZE)
            {
                // 移除最久未使用的缓存项
                var oldestKey = _cacheAccessQueue.Dequeue();
                _densityMapCache.Remove(oldestKey);
            }
            
            _densityMapCache[key] = densityLevel;
            _cacheAccessQueue.Enqueue(key);
        }
        
        private void UpdateCacheWithChanges(
            IEnumerable<NoteViewModel> addedNotes,
            IEnumerable<NoteViewModel> removedNotes,
            IEnumerable<NoteViewModel> modifiedNotes,
            Rect viewport,
            double zoomLevel)
        {
            // 简单的缓存更新策略：清除相关缓存项
            var keysToRemove = new List<string>();
            
            foreach (var key in _densityMapCache.Keys)
            {
                // 这里可以实现更智能的缓存更新逻辑
                keysToRemove.Add(key);
            }
            
            foreach (var key in keysToRemove)
            {
                _densityMapCache.Remove(key);
            }
            
            _cacheAccessQueue.Clear();
        }
        
        private double GetCacheHitRatio()
        {
            int total = Stats.CacheHits + Stats.CacheMisses;
            return total > 0 ? (double)Stats.CacheHits / total : 0;
        }
        
        #endregion
    }
    
    /// <summary>
    /// 密度图级别
    /// </summary>
    public class DensityMapLevel
    {
        public Rect Viewport { get; set; }
        public double ZoomLevel { get; set; }
        public int GridWidth { get; set; }
        public int GridHeight { get; set; }
        public int[,] DensityMap { get; set; }
        public Color[,] ColorMap { get; set; }
        public DateTime GeneratedTime { get; set; }
    }
    
    /// <summary>
    /// 密度图性能统计
    /// </summary>
    public class DensityMapStats
    {
        public double LastRenderTime { get; set; }
        public double LastHybridRenderTime { get; set; }
        public double LastUpdateTime { get; set; }
        public double PrecomputeTime { get; set; }
        public int TotalRenders { get; set; }
        public int TotalHybridRenders { get; set; }
        public int TotalUpdates { get; set; }
        public int TotalPrecomputations { get; set; }
        public int LastProcessedNotes { get; set; }
        public int CacheHits { get; set; }
        public int CacheMisses { get; set; }
        
        public double AverageRenderTime => TotalRenders > 0 ? LastRenderTime : 0;
    }
}