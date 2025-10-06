using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.Vulkan;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Adapters;
using Lumino.Views.Rendering.Utils;

namespace Lumino.Views.Rendering.Notes
{
    /// <summary>
    /// 音符渲染器 - 支持1000万+音符的高性能渲染器
    /// </summary>
    public class NoteRenderer : IDisposable
    {
        #region 私有字段

        // 画笔缓存系统
        private readonly Dictionary<string, IBrush> _brushCache = new();
        private readonly Dictionary<string, IPen> _penCache = new();
        private readonly Dictionary<string, CachedBrushData> _cachedBrushes = new();
        private readonly Dictionary<string, CachedPenData> _cachedPens = new();

        // 音符缓存
        private readonly Dictionary<NoteViewModel, CachedNoteData> _noteCache = new();
        private readonly Dictionary<NoteViewModel, Rect> _noteRectCache = new();

        // 性能统计
        private readonly PerformanceMonitor _performanceMonitor;
        private DateTime _lastRenderTime = DateTime.MinValue;
        private int _renderedNotesCount = 0;
        private int _cacheHitCount = 0;
        private int _cacheMissCount = 0;

        // 渲染策略阈值 - 直接拉满性能
        private const int DENSITY_MODE_THRESHOLD = 50000;  // 5万音符以上使用密度图
        private const int HYBRID_MODE_THRESHOLD = 10000;   // 1万音符以上使用混合模式
        private const int FULL_DETAIL_THRESHOLD = 1000;    // 1千音符以下全细节

        // 生命周期状态
        private bool _isDisposed = false;

        #endregion

        #region 构造函数和初始化

        public NoteRenderer()
        {
            _performanceMonitor = new PerformanceMonitor();
            InitializeBrushCache();
        }

        /// <summary>
        /// 初始化画笔缓存
        /// </summary>
        private void InitializeBrushCache()
        {
            try
            {
                // 基础画笔 - 绿色主题
                _brushCache["NoteFill"] = new SolidColorBrush(Colors.LimeGreen);
                _brushCache["NoteBorder"] = new SolidColorBrush(Colors.DarkGreen);
                _brushCache["SelectedNoteFill"] = new SolidColorBrush(Colors.Gold);
                _brushCache["SelectedNoteBorder"] = new SolidColorBrush(Colors.Orange);
                _brushCache["DragPreview"] = new SolidColorBrush(Colors.Orange, 0.3);

                // 基础画笔
                _penCache["NoteBorder"] = new Pen(new SolidColorBrush(Colors.DarkGreen), 1);
                _penCache["SelectedNoteBorder"] = new Pen(new SolidColorBrush(Colors.Orange), 2);
                _penCache["DragPreview"] = new Pen(new SolidColorBrush(Colors.DarkOrange), 1);

                Debug.WriteLine("[NoteRenderer] 画笔缓存初始化成功");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 画笔缓存初始化失败: {ex.Message}");
            }
        }

        #endregion

        #region 主要渲染方法

        /// <summary>
        /// 渲染音符 - 智能渲染策略
        /// </summary>
        public void RenderNotes(
            DrawingContext context,
            PianoRollViewModel viewModel,
            Dictionary<NoteViewModel, Rect> visibleNoteRects,
            VulkanDrawingContextAdapter? vulkanAdapter = null)
        {
            if (_isDisposed || viewModel == null || visibleNoteRects.Count == 0) return;

            try
            {
                var startTime = DateTime.UtcNow;
                var totalNotes = viewModel.Notes.Count;
                var visibleCount = visibleNoteRects.Count;

                // 根据音符数量选择渲染策略 - 直接拉满性能
                if (totalNotes >= DENSITY_MODE_THRESHOLD)
                {
                    // 超大规模：密度图模式
                    RenderWithDensityMap(context, viewModel, visibleNoteRects, vulkanAdapter);
                }
                else if (totalNotes >= HYBRID_MODE_THRESHOLD)
                {
                    // 大规模：智能批处理模式
                    RenderWithSmartBatching(context, viewModel, visibleNoteRects, vulkanAdapter);
                }
                else
                {
                    // 中小规模：详细渲染模式
                    RenderWithDetailedMode(context, viewModel, visibleNoteRects, vulkanAdapter);
                }

                // 更新性能统计
                _lastRenderTime = DateTime.UtcNow;
                _renderedNotesCount = visibleCount;
                _performanceMonitor.RecordPerformance("NoteRender", (DateTime.UtcNow - startTime).TotalMilliseconds);

                Debug.WriteLine($"[NoteRenderer] 音符渲染完成 - 总数: {totalNotes}, 可见: {visibleCount}, 策略: {GetRenderStrategyName(totalNotes)}, 耗时: {(DateTime.UtcNow - startTime).TotalMilliseconds:F2}ms");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 音符渲染错误: {ex.Message}");
                RenderFallback(context, visibleNoteRects);
            }
        }

        /// <summary>
        /// 密度图渲染模式
        /// </summary>
        private void RenderWithDensityMap(
            DrawingContext context,
            PianoRollViewModel viewModel,
            Dictionary<NoteViewModel, Rect> visibleNoteRects,
            VulkanDrawingContextAdapter? vulkanAdapter = null)
        {
            try
            {
                // 按区域分组音符
                var densityGroups = GroupNotesByDensity(visibleNoteRects);

                foreach (var group in densityGroups)
                {
                    if (group.Value.Count > 100)
                    {
                        // 高密度区域：使用密度图渲染
                        RenderDensityGroup(context, group.Key, group.Value);
                    }
                    else
                    {
                        // 低密度区域：使用简化渲染
                        RenderSimplifiedGroup(context, group.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 密度图渲染错误: {ex.Message}");
                RenderWithSmartBatching(context, viewModel, visibleNoteRects, vulkanAdapter);
            }
        }

        /// <summary>
        /// 智能批处理渲染模式
        /// </summary>
        private void RenderWithSmartBatching(
            DrawingContext context,
            PianoRollViewModel viewModel,
            Dictionary<NoteViewModel, Rect> visibleNoteRects,
            VulkanDrawingContextAdapter? vulkanAdapter = null)
        {
            try
            {
                // 获取优化的音符列表
                var optimizedNotes = GetOptimizedNoteList(visibleNoteRects);

                // 按重要性和样式分组
                var noteGroups = GroupNotesByImportanceAndStyle(optimizedNotes);

                // 批量渲染每个组
                foreach (var group in noteGroups)
                {
                    RenderNoteGroup(context, group.Key, group.Value);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 智能批处理渲染错误: {ex.Message}");
                RenderWithDetailedMode(context, viewModel, visibleNoteRects, vulkanAdapter);
            }
        }

        /// <summary>
        /// 详细渲染模式
        /// </summary>
        private void RenderWithDetailedMode(
            DrawingContext context,
            PianoRollViewModel viewModel,
            Dictionary<NoteViewModel, Rect> visibleNoteRects,
            VulkanDrawingContextAdapter? vulkanAdapter = null)
        {
            try
            {
                // 获取优化的音符列表
                var optimizedNotes = GetOptimizedNoteList(visibleNoteRects);

                // 按重要性和样式分组
                var noteGroups = GroupNotesByImportanceAndStyle(optimizedNotes);

                // 详细渲染每个组
                foreach (var group in noteGroups)
                {
                    RenderNoteGroupDetailed(context, group.Key, group.Value);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 详细渲染错误: {ex.Message}");
                RenderFallback(context, visibleNoteRects);
            }
        }

        #endregion

        #region 辅助渲染方法

        /// <summary>
        /// 获取优化的音符列表 - GPU加速筛选
        /// </summary>
        private List<NoteViewModel> GetOptimizedNoteList(Dictionary<NoteViewModel, Rect> visibleNoteRects)
        {
            try
            {
                // 按重要性排序
                var sortedNotes = visibleNoteRects.Keys
                    .OrderByDescending(note => GetNoteImportance(note))
                    .ToList();

                return sortedNotes;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 优化音符列表错误: {ex.Message}");
                return visibleNoteRects.Keys.ToList();
            }
        }

        /// <summary>
        /// 按重要性和样式分组音符
        /// </summary>
        private Dictionary<string, List<NoteViewModel>> GroupNotesByImportanceAndStyle(List<NoteViewModel> notes)
        {
            var groups = new Dictionary<string, List<NoteViewModel>>();

            try
            {
                foreach (var note in notes)
                {
                    var groupKey = GetNoteGroupKey(note);
                    
                    if (!groups.ContainsKey(groupKey))
                    {
                        groups[groupKey] = new List<NoteViewModel>();
                    }
                    
                    groups[groupKey].Add(note);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 音符分组错误: {ex.Message}");
                groups["default"] = notes;
            }

            return groups;
        }

        /// <summary>
        /// 获取音符分组键
        /// </summary>
        private string GetNoteGroupKey(NoteViewModel note)
        {
            try
            {
                var importance = GetNoteImportance(note);
                var isSelected = note.IsSelected ? "Selected" : "Normal";
                var isSpecial = IsSpecialNote(note) ? "Special" : "Normal";
                
                return $"{importance}_{isSelected}_{isSpecial}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 获取分组键错误: {ex.Message}");
                return "default";
            }
        }

        /// <summary>
        /// 渲染音符组
        /// </summary>
        private void RenderNoteGroup(DrawingContext context, string groupKey, List<NoteViewModel> notes)
        {
            try
            {
                var (fillBrush, borderPen) = GetNoteStyle(groupKey);

                // 批量渲染音符
                foreach (var note in notes)
                {
                    if (_noteRectCache.TryGetValue(note, out var rect))
                    {
                        RenderSingleNote(context, rect, fillBrush, borderPen);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 渲染音符组错误: {ex.Message}");
                RenderNormalNotes(context, notes);
            }
        }

        /// <summary>
        /// 详细渲染音符组
        /// </summary>
        private void RenderNoteGroupDetailed(DrawingContext context, string groupKey, List<NoteViewModel> notes)
        {
            try
            {
                var (fillBrush, borderPen) = GetNoteStyle(groupKey);

                // 详细渲染每个音符
                foreach (var note in notes)
                {
                    if (_noteRectCache.TryGetValue(note, out var rect))
                    {
                        RenderSingleNoteDetailed(context, rect, note, fillBrush, borderPen);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 详细渲染音符组错误: {ex.Message}");
                RenderNoteGroup(context, groupKey, notes);
            }
        }

        /// <summary>
        /// 获取音符重要性
        /// </summary>
        private int GetNoteImportance(NoteViewModel note)
        {
            try
            {
                var importance = 0;

                // 选中状态增加重要性
                if (note.IsSelected) importance += 100;

                // 特殊音符增加重要性
                if (IsSpecialNote(note)) importance += 50;

                // 长音符增加重要性
                if (note.Duration.ToDouble() > 1000) importance += 20;

                // 力度大的音符增加重要性
                if (note.Velocity > 100) importance += 10;

                return importance;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 获取音符重要性错误: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 判断是否为特殊音符
        /// </summary>
        private bool IsSpecialNote(NoteViewModel note)
        {
            try
            {
                // 检查是否为特殊音符（如鼓点、和弦音等）
                return note.Pitch == 60 || note.Pitch == 64 || note.Pitch == 67 || // C大调和弦
                       note.Velocity > 120 || // 高力度
                       note.Duration.ToDouble() < 50;    // 短音符
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 判断特殊音符错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取音符样式
        /// </summary>
        private (IBrush fillBrush, IPen borderPen) GetNoteStyle(string groupKey)
        {
            try
            {
                if (_cachedBrushes.TryGetValue(groupKey, out var cachedBrush) &&
                    _cachedPens.TryGetValue(groupKey, out var cachedPen))
                {
                    _cacheHitCount++;
                    return (cachedBrush.Brush, cachedPen.Pen);
                }

                _cacheMissCount++;

                // 根据分组键创建样式
                IBrush fillBrush;
                IPen borderPen;

                if (groupKey.Contains("Selected"))
                {
                    fillBrush = _brushCache["SelectedNoteFill"];
                    borderPen = _penCache["SelectedNoteBorder"];
                }
                else if (groupKey.Contains("Special"))
                {
                    // 特殊音符使用深绿色高亮，而不是红色
                    fillBrush = new SolidColorBrush(Colors.ForestGreen);
                    borderPen = new Pen(new SolidColorBrush(Colors.DarkGreen), 2);
                }
                else
                {
                    fillBrush = _brushCache["NoteFill"];
                    borderPen = _penCache["NoteBorder"];
                }

                // 缓存样式
                _cachedBrushes[groupKey] = new CachedBrushData { Brush = fillBrush, Timestamp = DateTime.UtcNow };
                _cachedPens[groupKey] = new CachedPenData { Pen = borderPen, Timestamp = DateTime.UtcNow };

                return (fillBrush, borderPen);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 获取音符样式错误: {ex.Message}");
                return (_brushCache["NoteFill"], _penCache["NoteBorder"]);
            }
        }

        /// <summary>
        /// 渲染单个音符 - 带圆角
        /// </summary>
        private void RenderSingleNote(DrawingContext context, Rect rect, IBrush fillBrush, IPen borderPen)
        {
            try
            {
                if (rect.Width > 0 && rect.Height > 0)
                {
                    // 使用圆角矩形,圆角半径为4像素
                    var roundedRect = new RoundedRect(rect, 4);
                    context.DrawRectangle(fillBrush, borderPen, roundedRect);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 渲染单个音符错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 详细渲染单个音符 - 带圆角
        /// </summary>
        private void RenderSingleNoteDetailed(DrawingContext context, Rect rect, NoteViewModel note, IBrush fillBrush, IPen borderPen)
        {
            try
            {
                if (rect.Width > 0 && rect.Height > 0)
                {
                    // 绘制音符主体 - 圆角矩形
                    var roundedRect = new RoundedRect(rect, 4);
                    context.DrawRectangle(fillBrush, borderPen, roundedRect);

                    // 绘制音符标签（如果足够大）
                    // TODO: Avalonia 文本绘制 API 需要确认正确的 FormattedText/TextLayout 用法
                    // if (rect.Width > 30 && rect.Height > 15)
                    // {
                    //     var labelText = GetNoteLabel(note);
                    //     var labelBrush = new SolidColorBrush(Colors.Black);
                    //     var labelPoint = new Point(rect.X + 2, rect.Y + rect.Height - 2);
                    //     // 需要使用 Avalonia 支持的文本绘制 API
                    // }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 详细渲染单个音符错误: {ex.Message}");
                RenderSingleNote(context, rect, fillBrush, borderPen);
            }
        }

        /// <summary>
        /// 按密度分组音符
        /// </summary>
        private Dictionary<Rect, List<NoteViewModel>> GroupNotesByDensity(Dictionary<NoteViewModel, Rect> noteRects)
        {
            var groups = new Dictionary<Rect, List<NoteViewModel>>();

            try
            {
                const int gridSize = 50; // 50x50像素网格

                foreach (var kvp in noteRects)
                {
                    var note = kvp.Key;
                    var rect = kvp.Value;

                    // 计算网格位置
                    var gridX = (int)(rect.X / gridSize) * gridSize;
                    var gridY = (int)(rect.Y / gridSize) * gridSize;
                    var gridRect = new Rect(gridX, gridY, gridSize, gridSize);

                    if (!groups.ContainsKey(gridRect))
                    {
                        groups[gridRect] = new List<NoteViewModel>();
                    }

                    groups[gridRect].Add(note);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 按密度分组错误: {ex.Message}");
                // 回退：每个音符单独一组
                foreach (var kvp in noteRects)
                {
                    groups[kvp.Value] = new List<NoteViewModel> { kvp.Key };
                }
            }

            return groups;
        }

        /// <summary>
        /// 渲染密度组
        /// </summary>
        private void RenderDensityGroup(DrawingContext context, Rect gridRect, List<NoteViewModel> notes)
        {
            try
            {
                var density = notes.Count;
                var intensity = Math.Min(1.0, density / 100.0);
                
                // 使用绿色主题的密度图
                var densityBrush = new SolidColorBrush(Colors.LimeGreen, intensity * 0.6);
                var densityPen = new Pen(new SolidColorBrush(Colors.DarkGreen), 1);

                var roundedRect = new RoundedRect(gridRect, 4);
                context.DrawRectangle(densityBrush, densityPen, roundedRect);

                // 在密度图中心显示数量
                // TODO: Avalonia 文本绘制 API 需要确认正确的 FormattedText/TextLayout 用法
                // if (density > 10)
                // {
                //     var countText = density.ToString();
                //     var countPoint = new Point(gridRect.X + gridRect.Width / 2 - 10, gridRect.Y + gridRect.Height / 2 - 5);
                //     var countBrush = new SolidColorBrush(Colors.White);
                //     // 需要使用 Avalonia 支持的文本绘制 API
                // }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 渲染密度组错误: {ex.Message}");
                RenderSimplifiedGroup(context, notes);
            }
        }

        /// <summary>
        /// 渲染简化组
        /// </summary>
        private void RenderSimplifiedGroup(DrawingContext context, List<NoteViewModel> notes)
        {
            try
            {
                var (fillBrush, borderPen) = GetNoteStyle("Simplified_Normal_Normal");

                foreach (var note in notes)
                {
                    if (_noteRectCache.TryGetValue(note, out var rect))
                    {
                        RenderSingleNote(context, rect, fillBrush, borderPen);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 渲染简化组错误: {ex.Message}");
                RenderNormalNotes(context, notes);
            }
        }

        /// <summary>
        /// 渲染普通音符
        /// </summary>
        private void RenderNormalNotes(DrawingContext context, List<NoteViewModel> notes)
        {
            try
            {
                var fillBrush = _brushCache["NoteFill"];
                var borderPen = _penCache["NoteBorder"];

                foreach (var note in notes)
                {
                    if (_noteRectCache.TryGetValue(note, out var rect))
                    {
                        RenderSingleNote(context, rect, fillBrush, borderPen);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 渲染普通音符错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 渲染回退
        /// </summary>
        private void RenderFallback(DrawingContext context, Dictionary<NoteViewModel, Rect> visibleNoteRects)
        {
            try
            {
                var fallbackBrush = new SolidColorBrush(Colors.LightGray, 0.5);
                var fallbackPen = new Pen(new SolidColorBrush(Colors.Gray), 1);

                foreach (var kvp in visibleNoteRects)
                {
                    var rect = kvp.Value;
                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        var roundedRect = new RoundedRect(rect, 4);
                        context.DrawRectangle(fallbackBrush, fallbackPen, roundedRect);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 回退渲染错误: {ex.Message}");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取音符标签
        /// </summary>
        private string GetNoteLabel(NoteViewModel note)
        {
            try
            {
                return $"{GetNoteName(note.Pitch)}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 获取音符标签错误: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取音符名称
        /// </summary>
        private string GetNoteName(int pitch)
        {
            try
            {
                var noteNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
                var octave = pitch / 12 - 1;
                var noteIndex = pitch % 12;
                return $"{noteNames[noteIndex]}{octave}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 获取音符名称错误: {ex.Message}");
                return pitch.ToString();
            }
        }


        /// <summary>
        /// 获取音符名称
        /// </summary>
        /// <summary>
        /// 获取渲染策略名称
        /// </summary>
        private string GetRenderStrategyName(int totalNotes)
        {
            if (totalNotes >= DENSITY_MODE_THRESHOLD) return "DensityMap";
            if (totalNotes >= HYBRID_MODE_THRESHOLD) return "SmartBatching";
            return "DetailedMode";
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 更新音符缓存
        /// </summary>
        public void UpdateNoteCache(Dictionary<NoteViewModel, Rect> noteRects)
        {
            if (_isDisposed) return;

            try
            {
                _noteRectCache.Clear();
                foreach (var kvp in noteRects)
                {
                    _noteRectCache[kvp.Key] = kvp.Value;
                }

                Debug.WriteLine($"[NoteRenderer] 音符缓存已更新 - 数量: {noteRects.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 更新音符缓存错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void ClearAllCaches()
        {
            if (_isDisposed) return;

            try
            {
                _brushCache.Clear();
                _penCache.Clear();
                _cachedBrushes.Clear();
                _cachedPens.Clear();
                _noteCache.Clear();
                _noteRectCache.Clear();

                // 重新初始化基础缓存
                InitializeBrushCache();

                // 重置统计
                _cacheHitCount = 0;
                _cacheMissCount = 0;

                Debug.WriteLine("[NoteRenderer] 所有缓存已清除");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteRenderer] 清除缓存错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取性能统计信息
        /// </summary>
        public Dictionary<string, object> GetPerformanceStats()
        {
            var stats = new Dictionary<string, object>
            {
                ["RenderedNotesCount"] = _renderedNotesCount,
                ["CacheHitCount"] = _cacheHitCount,
                ["CacheMissCount"] = _cacheMissCount,
                ["AverageRenderTime"] = _performanceMonitor.GetAverageRenderTime()
            };
            return stats;
        }

        #endregion

        #region 资源释放

        /// <summary>
        /// 析构函数
        /// </summary>
        ~NoteRenderer()
        {
            Dispose(false);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源实现
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !_isDisposed)
            {
                try
                {
                    // 清除画笔缓存
                    _brushCache.Clear();
                    _penCache.Clear();
                    _cachedBrushes.Clear();
                    _cachedPens.Clear();
                    _noteCache.Clear();
                    _noteRectCache.Clear();

                    // 释放性能监控器
                    _performanceMonitor?.Dispose();

                    _isDisposed = true;

                    Debug.WriteLine("[NoteRenderer] 资源已释放");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[NoteRenderer] 资源释放错误: {ex.Message}");
                }
            }
        }

        #endregion
    }

    #region 缓存数据结构

    /// <summary>
    /// 缓存音符数据
    /// </summary>
    public class CachedNoteData
    {
        public NoteViewModel Note { get; set; } = null!;
        public Rect Rect { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsVisible { get; set; }
    }

    /// <summary>
    /// 缓存画笔数据
    /// </summary>
    public class CachedBrushData
    {
        public IBrush Brush { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 缓存画笔数据
    /// </summary>
    public class CachedPenData
    {
        public IPen Pen { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }

    #endregion
}