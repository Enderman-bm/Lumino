using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Adapters;
using Lumino.Views.Rendering.Utils;
using EnderDebugger;

namespace Lumino.Views.Rendering.Notes
{
    /// <summary>
    /// Vulkan优化的音符渲染器 - 仅支持Vulkan渲染，删除Skia回退逻辑
    /// </summary>
    public class NoteRenderer
    {
        private readonly Color _noteColor = Color.Parse("#4CAF50");
        private readonly Color _noteBorderColor = Color.Parse("#2E7D32");
        private readonly Color _selectedNoteColor = Color.Parse("#FF9800");
        private readonly Color _selectedNoteBorderColor = Color.Parse("#F57C00");
        private readonly Color _previewNoteColor = Color.Parse("#81C784");
        private readonly Color _previewNoteBorderColor = Color.Parse("#66BB6A");

        // 圆角半径
        private const double CORNER_RADIUS = 3.0;

        // 缓存 - 用于透明度缓存，减少重复创建
        // 优化：使用int作为键（量化后的透明度值），避免double的精度和哈希问题
        private readonly Dictionary<int, IBrush> _normalBrushCache = new();
        private readonly Dictionary<int, IBrush> _selectedBrushCache = new();
        
        // 透明度量化精度：100表示精度为0.01
        private const int OPACITY_QUANTIZATION = 100;
        
        // 画笔缓存 - 避免重复创建Pen对象
        private readonly Dictionary<Color, IPen> _penCache = new();
        
        // 阴影画笔 - 用于实现
        private readonly IBrush _shadowBrush = new SolidColorBrush(Colors.Black, 0.2);
        
        // 性能优化选项
        private bool _enableShadowEffect = true; // 控制是否启用阴影效果
        private int _shadowThreshold = 2000; // 当音符数量超过此值时禁用阴影（优化性能）

        // 强制使用Vulkan渲染
        private const bool _useVulkanRendering = true;

        /// <summary>
        /// 设置是否启用阴影效果，用于性能优化
        /// </summary>
        public void SetShadowEnabled(bool enabled)
        {
            _enableShadowEffect = enabled;
        }

        /// <summary>
        /// 设置阴影效果启用的阈值
        /// </summary>
        public void SetShadowThreshold(int threshold)
        {
            _shadowThreshold = threshold;
        }

        /// <summary>
        /// 设置是否使用Vulkan渲染（已废弃，始终使用Vulkan）
        /// </summary>
        [Obsolete("Vulkan渲染已强制启用，此方法不再有效")]
        public void SetVulkanRendering(bool enabled)
        {
            // 始终使用Vulkan渲染，忽略参数
        }

        /// <summary>
        /// Vulkan优化的音符渲染 - 仅使用Vulkan，删除Skia回退逻辑
        /// </summary>
        public void RenderNotes(DrawingContext context, PianoRollViewModel viewModel, System.Collections.Generic.Dictionary<NoteViewModel, Rect> visibleNoteCache, VulkanDrawingContextAdapter? vulkanAdapter = null)
        {
            // 安全性检查
            if (context == null || viewModel == null || visibleNoteCache == null)
            {
                EnderLogger.Instance.Warn("NoteRenderer", "RenderNotes收到null参数，跳过渲染");
                return;
            }

            // 始终使用Vulkan适配器
            bool createdAdapter = vulkanAdapter == null;
            vulkanAdapter ??= new VulkanDrawingContextAdapter(context);

            try
            {
                int totalNotes = visibleNoteCache.Count;
                
                // 动态判断是否启用阴影效果
                bool shouldRenderShadow = _enableShadowEffect && totalNotes <= _shadowThreshold;
                
                // 阶段1：准备音符分组数据
                var noteGroups = PrepareNoteGroups(viewModel, visibleNoteCache, shouldRenderShadow);
                
                // 阶段2：批量渲染音符
                int drawnNotes = DrawGroupedNotes(vulkanAdapter, noteGroups);

                // 调试信息
                if (drawnNotes > 0)
                {
                    EnderLogger.Instance.Debug("NoteRenderer", $"绘制 {drawnNotes} 个音符 (总数: {totalNotes})");
                }
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.Error("NoteRenderer", $"渲染错误: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                // 如果是我们创建的适配器，确保正确释放资源
                if (createdAdapter && vulkanAdapter != null)
                {
                    vulkanAdapter.Dispose();
                }
            }
        }

        /// <summary>
        /// 音符渲染数据结构
        /// </summary>
        private struct NoteRenderData
        {
            public Rect Rect;
            public IBrush Brush;
            public IPen Pen;
            public bool RenderShadow;
        }

        /// <summary>
        /// 音符分组结果
        /// </summary>
        private struct NoteGroupsResult
        {
            public List<NoteRenderData> NormalNotes;
            public List<NoteRenderData> SpecialNotes;
        }

        /// <summary>
        /// 准备音符分组 - 预计算所有音符的状态和属性
        /// </summary>
        private NoteGroupsResult PrepareNoteGroups(PianoRollViewModel viewModel, Dictionary<NoteViewModel, Rect> visibleNoteCache, bool shouldRenderShadow)
        {
            var normalNotes = new List<NoteRenderData>(visibleNoteCache.Count);
            var specialNotes = new List<NoteRenderData>();

            // 预创建常用画笔，避免循环中重复创建
            var normalPen = GetCachedPen(_noteBorderColor, 2);
            var selectedPen = GetCachedPen(_selectedNoteBorderColor, 2);

            foreach (var kvp in visibleNoteCache)
            {
                var note = kvp.Key;
                var rect = kvp.Value;

                // 安全性检查：跳过无效矩形
                if (rect.Width <= 0 || rect.Height <= 0)
                    continue;

                // 计算音符状态
                bool isBeingDragged = viewModel.DragState?.IsDragging == true && 
                                     viewModel.DragState.DraggingNotes?.Contains(note) == true;
                bool isBeingResized = viewModel.ResizeState?.IsResizing == true && 
                                     viewModel.ResizeState.ResizingNotes?.Contains(note) == true;
                bool isBeingManipulated = isBeingDragged || isBeingResized;

                // 预计算透明度
                var opacity = CalculateNoteOpacity(note, isBeingManipulated);

                // 分配到不同的列表
                if (note.IsSelected || isBeingManipulated)
                {
                    var brush = GetCachedSelectedBrush(opacity);
                    specialNotes.Add(new NoteRenderData
                    {
                        Rect = rect,
                        Brush = brush,
                        Pen = selectedPen,
                        RenderShadow = shouldRenderShadow && isBeingManipulated
                    });
                }
                else
                {
                    var brush = GetCachedNormalBrush(opacity);
                    normalNotes.Add(new NoteRenderData
                    {
                        Rect = rect,
                        Brush = brush,
                        Pen = normalPen,
                        RenderShadow = shouldRenderShadow
                    });
                }
            }

            return new NoteGroupsResult
            {
                NormalNotes = normalNotes,
                SpecialNotes = specialNotes
            };
        }

        /// <summary>
        /// 计算音符透明度
        /// </summary>
        private double CalculateNoteOpacity(NoteViewModel note, bool isBeingManipulated)
        {
            var opacity = Math.Max(0.7, note.Velocity / 127.0);
            if (isBeingManipulated)
            {
                opacity = Math.Min(1.0, opacity * 1.1);
            }
            return opacity;
        }

        /// <summary>
        /// 绘制分组的音符 - GPU批量渲染
        /// </summary>
        private int DrawGroupedNotes(VulkanDrawingContextAdapter vulkanAdapter, NoteGroupsResult noteGroups)
        {
            int drawnNotes = 0;

            // 渲染普通音符
            if (noteGroups.NormalNotes.Count > 0)
            {
                drawnNotes += DrawNoteGroup(vulkanAdapter, noteGroups.NormalNotes, "普通音符", 50);
            }

            // 中间刷新批处理
            vulkanAdapter.FlushBatches();

            // 渲染特殊音符（选中或操作中的）
            if (noteGroups.SpecialNotes.Count > 0)
            {
                drawnNotes += DrawNoteGroup(vulkanAdapter, noteGroups.SpecialNotes, "特殊音符", 30);
            }

            // 最后刷新所有批处理
            vulkanAdapter.FlushBatches();

            return drawnNotes;
        }

        /// <summary>
        /// 绘制单个音符组 - 按画笔和阴影属性进行批处理
        /// </summary>
        private int DrawNoteGroup(VulkanDrawingContextAdapter vulkanAdapter, List<NoteRenderData> notes, string groupName, int batchFlushThreshold)
        {
            int drawnCount = 0;

            // 按画笔、笔刷和阴影属性分组，最大化GPU批处理效率
            var groupedNotes = notes.GroupBy(n => (n.Brush, n.Pen, n.RenderShadow));

            foreach (var group in groupedNotes)
            {
                var (brush, pen, renderShadow) = group.Key;
                var groupList = group.ToList();

                // 批量绘制同属性的音符
                foreach (var noteData in groupList)
                {
                    DrawSingleNote(vulkanAdapter, noteData);
                    drawnCount++;
                }

                // 定期刷新批次，避免批次过大
                if (groupList.Count >= batchFlushThreshold)
                {
                    vulkanAdapter.FlushBatches();
                }
            }

            return drawnCount;
        }

        /// <summary>
        /// 绘制单个音符
        /// </summary>
        private void DrawSingleNote(VulkanDrawingContextAdapter vulkanAdapter, NoteRenderData noteData)
        {
            try
            {
                // 绘制阴影
                if (noteData.RenderShadow)
                {
                    var shadowOffset = new Vector(1.0, 1.0);
                    var shadowRect = new RoundedRect(noteData.Rect.Translate(shadowOffset), CORNER_RADIUS);
                    vulkanAdapter.DrawRectangle(_shadowBrush, null, shadowRect);
                }

                // 绘制音符主体
                var roundedRect = new RoundedRect(noteData.Rect, CORNER_RADIUS);
                vulkanAdapter.DrawRectangle(noteData.Brush, noteData.Pen, roundedRect);
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.Warn("NoteRenderer", $"单个音符绘制失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 渲染预览音符
        /// </summary>
        public void RenderPreviewNote(DrawingContext context, Rect rect, bool useVulkan = true)
        {
            try
            {
                if (useVulkan)
                {
                    using var vulkanAdapter = new VulkanDrawingContextAdapter(context);
                    
                    // 预览音符使用特殊的颜色和透明度
                    var previewBrush = new SolidColorBrush(_previewNoteColor, 0.8);
                    var previewPen = new Pen(new SolidColorBrush(_previewNoteBorderColor, 1.0), 2);
                    
                    var roundedRect = new RoundedRect(rect, CORNER_RADIUS);
                    vulkanAdapter.DrawRectangle(previewBrush, previewPen, roundedRect);
                    vulkanAdapter.FlushBatches();
                }
                else
                {
                    // 回退到标准渲染
                    var previewBrush = new SolidColorBrush(_previewNoteColor, 0.8);
                    var previewPen = new Pen(new SolidColorBrush(_previewNoteBorderColor, 1.0), 2);
                    
                    var roundedRect = new RoundedRect(rect, CORNER_RADIUS);
                    context.DrawRectangle(previewBrush, previewPen, roundedRect);
                }
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.Error("NoteRenderer", $"预览音符渲染错误: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 渲染预览音符（兼容旧接口）
        /// </summary>
        public void RenderPreviewNote(DrawingContext context, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect, VulkanDrawingContextAdapter? vulkanAdapter = null)
        {
            if (viewModel.PreviewNote == null) return;

            // 始终使用Vulkan适配器
            bool createdAdapter = vulkanAdapter == null;
            vulkanAdapter ??= new VulkanDrawingContextAdapter(context);

            try
            {
                var previewRect = calculateNoteRect(viewModel.PreviewNote);
                if (previewRect.Width > 0 && previewRect.Height > 0)
                {
                    // 使用新的简化接口
                    RenderPreviewNote(context, previewRect, true);
                }
            }
            finally
            {
                // 如果是我们创建的适配器，确保正确释放资源
                if (createdAdapter && vulkanAdapter != null)
                {
                    vulkanAdapter.Dispose();
                }
            }
        }

        /// <summary>
        /// 渲染动画音符 - 为拖动动画提供特殊渲染支持
        /// </summary>
        public void RenderAnimatedNotes(DrawingContext context, PianoRollViewModel viewModel, IEnumerable<NoteViewModel> animatedNotes, Func<NoteViewModel, Rect> calculateNoteRect, VulkanDrawingContextAdapter? vulkanAdapter = null)
        {
            if (animatedNotes == null || !animatedNotes.Any()) return;

            // 始终使用Vulkan适配器
            bool createdAdapter = vulkanAdapter == null;
            vulkanAdapter ??= new VulkanDrawingContextAdapter(context);

            try
            {
                var animatedNoteRects = new List<RoundedRect>();
                var animatedNoteData = new List<(NoteViewModel note, Rect rect)>();

                // 预创建动画音符的画笔 - 使用特殊颜色和高透明度
                var animatedBrush = new SolidColorBrush(Colors.DeepSkyBlue, 0.85);
                var animatedPen = new Pen(new SolidColorBrush(Colors.DodgerBlue, 0.95), 2);

                foreach (var note in animatedNotes)
                {
                    var rect = calculateNoteRect(note);
                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        animatedNoteRects.Add(new RoundedRect(rect, CORNER_RADIUS));
                        animatedNoteData.Add((note, rect));
                    }
                }

                // 批量渲染动画音符
                if (animatedNoteRects.Count > 0)
                {
                    vulkanAdapter.DrawRoundedRectsInstanced(animatedNoteRects, animatedBrush, animatedPen);
                }

                // 绘制音符文本（如果音符足够大）
                foreach (var (note, rect) in animatedNoteData)
                {
                    if (rect.Width > 25 && rect.Height > 8)
                    {
                        NoteTextRenderer.DrawNotePitchText(context, note.Pitch, rect);
                    }
                }
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.Error("NoteRenderer", $"动画音符渲染错误: {ex.Message}");
            }
            finally
            {
                // 如果是我们创建的适配器，确保正确释放资源
                if (createdAdapter && vulkanAdapter != null)
                {
                    vulkanAdapter.Dispose();
                }
            }
        }

        /// <summary>
        /// 使用Vulkan渲染单个音符（私有方法）
        /// </summary>
        private void DrawNoteVulkan(VulkanDrawingContextAdapter vulkanAdapter, Rect rect, IBrush brush, IPen pen, bool renderShadow = false)
        {
            try
            {
                if (renderShadow)
                {
                    var shadowOffset = new Vector(1.0, 1.0);
                    var shadowRect = new RoundedRect(rect.Translate(shadowOffset), CORNER_RADIUS);
                    vulkanAdapter.DrawRectangle(_shadowBrush, null, shadowRect);
                }
                
                var roundedRect = new RoundedRect(rect, CORNER_RADIUS);
                vulkanAdapter.DrawRectangle(brush, pen, roundedRect);
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.Error("NoteRenderer", $"Vulkan音符绘制错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取缓存的普通画笔（带透明度）
        /// 优化：使用量化的int键避免double精度问题
        /// </summary>
        private IBrush GetCachedNormalBrush(double opacity)
        {
            // 量化透明度值到指定精度，避免浮点数精度问题
            int quantizedOpacity = QuantizeOpacity(opacity);
            
            if (!_normalBrushCache.TryGetValue(quantizedOpacity, out var brush))
            {
                // 使用量化后的值重新计算实际透明度
                double actualOpacity = quantizedOpacity / (double)OPACITY_QUANTIZATION;
                brush = new SolidColorBrush(_noteColor, actualOpacity);
                _normalBrushCache[quantizedOpacity] = brush;
            }
            return brush;
        }

        /// <summary>
        /// 获取缓存的选中画笔（带透明度）
        /// 优化：使用量化的int键避免double精度问题
        /// </summary>
        private IBrush GetCachedSelectedBrush(double opacity)
        {
            // 量化透明度值到指定精度，避免浮点数精度问题
            int quantizedOpacity = QuantizeOpacity(opacity);
            
            if (!_selectedBrushCache.TryGetValue(quantizedOpacity, out var brush))
            {
                // 使用量化后的值重新计算实际透明度
                double actualOpacity = quantizedOpacity / (double)OPACITY_QUANTIZATION;
                brush = new SolidColorBrush(_selectedNoteColor, actualOpacity);
                _selectedBrushCache[quantizedOpacity] = brush;
            }
            return brush;
        }

        /// <summary>
        /// 量化透明度值到整数，避免浮点数精度问题
        /// </summary>
        /// <param name="opacity">原始透明度值 (0.0-1.0)</param>
        /// <returns>量化后的整数值 (0-100)</returns>
        private static int QuantizeOpacity(double opacity)
        {
            // 确保opacity在有效范围内
            opacity = Math.Clamp(opacity, 0.0, 1.0);
            
            // 量化到指定精度并四舍五入
            return (int)Math.Round(opacity * OPACITY_QUANTIZATION);
        }

        /// <summary>
        /// 获取缓存的画笔（Pen对象），避免重复创建
        /// </summary>
        private IPen GetCachedPen(Color color, double thickness)
        {
            if (!_penCache.TryGetValue(color, out var pen))
            {
                pen = new Pen(new SolidColorBrush(color, 1.0), thickness);
                _penCache[color] = pen;
            }
            return pen;
        }

        /// <summary>
        /// 清除画笔缓存
        /// </summary>
        public void ClearBrushCache()
        {
            _normalBrushCache.Clear();
            _selectedBrushCache.Clear();
            _penCache.Clear();
        }

        /// <summary>
        /// 全局优化的音符渲染 - 针对10W+音符的极高性能优化
        /// </summary>
        public void RenderNotesOptimized(DrawingContext context, PianoRollViewModel viewModel, System.Collections.Generic.Dictionary<NoteViewModel, Rect> visibleNoteCache, VulkanDrawingContextAdapter? vulkanAdapter = null)
        {
            // 安全性检查
            if (context == null || viewModel == null || visibleNoteCache == null)
            {
                EnderLogger.Instance.Warn("NoteRenderer", "RenderNotesOptimized收到null参数，跳过渲染");
                return;
            }

            // 始终使用Vulkan适配器
            bool createdAdapter = vulkanAdapter == null;
            vulkanAdapter ??= new VulkanDrawingContextAdapter(context);
    
            try
            {
                int totalNotes = visibleNoteCache.Count;
                int drawnNotes = 0;
    
                // 快速路径：少量音符直接渲染
                if (totalNotes < 1000)
                {
                    RenderNotes(context, viewModel, visibleNoteCache, vulkanAdapter);
                    return;
                }
    
                // 动态判断是否启用阴影效果
                bool shouldRenderShadow = _enableShadowEffect && totalNotes <= _shadowThreshold;
    
                // 阶段1：预收集所有渲染数据
                var normalRects = new List<RoundedRect>(totalNotes);
                var selectedRects = new List<RoundedRect>();
                var shadowRects = new List<RoundedRect>();
    
                // 阶段2：批量处理
                foreach (var kvp in visibleNoteCache)
                {
                    var note = kvp.Key;
                    var rect = kvp.Value;
    
                    if (rect.Width <= 0 || rect.Height <= 0) continue;
    
                    // 快速视口剔除
                    if (!vulkanAdapter.IsRectVisible(rect)) continue;
    
                    // 状态检查（带空值保护）
                    bool isSelected = note.IsSelected ||
                        (viewModel.DragState?.IsDragging == true && viewModel.DragState.DraggingNotes?.Contains(note) == true) ||
                        (viewModel.ResizeState?.IsResizing == true && viewModel.ResizeState.ResizingNotes?.Contains(note) == true);
    
                    var roundedRect = new RoundedRect(rect, CORNER_RADIUS);
    
                    if (isSelected)
                    {
                        selectedRects.Add(roundedRect);
                        if (shouldRenderShadow)
                        {
                            shadowRects.Add(new RoundedRect(rect.Translate(new Vector(1.0, 1.0)), CORNER_RADIUS));
                        }
                    }
                    else
                    {
                        normalRects.Add(roundedRect);
                        if (shouldRenderShadow)
                        {
                            shadowRects.Add(new RoundedRect(rect.Translate(new Vector(1.0, 1.0)), CORNER_RADIUS));
                        }
                    }
    
                    drawnNotes++;
                }
    
                // 阶段3：高效批量渲染（使用缓存的画笔）
                if (shadowRects.Count > 0)
                {
                    vulkanAdapter.DrawRoundedRectsInstanced(shadowRects, _shadowBrush, null);
                }
    
                if (normalRects.Count > 0)
                {
                    var normalBrush = GetCachedNormalBrush(1.0);
                    var normalPen = GetCachedPen(_noteBorderColor, 2);
                    vulkanAdapter.DrawRoundedRectsInstanced(normalRects, normalBrush, normalPen);
                }
    
                if (selectedRects.Count > 0)
                {
                    var selectedBrush = GetCachedSelectedBrush(1.0);
                    var selectedPen = GetCachedPen(_selectedNoteBorderColor, 2);
                    vulkanAdapter.DrawRoundedRectsInstanced(selectedRects, selectedBrush, selectedPen);
                }
    
                // 阶段4：同步渲染
                vulkanAdapter.FlushBatches();
    
                // 性能监控
                if (totalNotes > 50000)
                {
                    EnderLogger.Instance.Info("NoteRenderer-UltraOptimized", $"渲染{totalNotes}个音符, 可见{drawnNotes}个, 批次:{3}个");
                }
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.Error("NoteRenderer-UltraOptimized", $"错误:{ex.Message}\n{ex.StackTrace}, 回退到传统渲染");
                // 安全的回退
                try
                {
                    RenderNotes(context, viewModel, visibleNoteCache, vulkanAdapter);
                }
                catch (Exception fallbackEx)
                {
                    EnderLogger.Instance.Error("NoteRenderer-UltraOptimized", $"回退渲染也失败: {fallbackEx.Message}");
                }
            }
            finally
            {
                // 如果是我们创建的适配器，确保正确释放资源
                if (createdAdapter && vulkanAdapter != null)
                {
                    vulkanAdapter.Dispose();
                }
            }
        }

        /// <summary>
        /// 多线程优化的音符渲染 - 使用并行处理准备数据
        /// </summary>
        public void RenderNotesParallel(DrawingContext context, PianoRollViewModel viewModel, System.Collections.Generic.Dictionary<NoteViewModel, Rect> visibleNoteCache, VulkanDrawingContextAdapter? vulkanAdapter = null)
        {
            // 安全性检查
            if (context == null || viewModel == null || visibleNoteCache == null)
            {
                EnderLogger.Instance.Warn("NoteRenderer", "RenderNotesParallel收到null参数，跳过渲染");
                return;
            }

            // 始终使用Vulkan适配器
            bool createdAdapter = vulkanAdapter == null;
            vulkanAdapter ??= new VulkanDrawingContextAdapter(context);

            try
            {
                int totalNotes = visibleNoteCache.Count;

                // 对于小量音符，使用单线程优化版本
                if (totalNotes < 1000)
                {
                    RenderNotesOptimized(context, viewModel, visibleNoteCache, vulkanAdapter);
                    return;
                }

                // 多线程数据准备
                var normalRects = new System.Collections.Concurrent.ConcurrentBag<RoundedRect>();
                var selectedRects = new System.Collections.Concurrent.ConcurrentBag<RoundedRect>();
                var shadowRects = new System.Collections.Concurrent.ConcurrentBag<RoundedRect>();

                // 动态判断是否启用阴影效果
                bool shouldRenderShadow = _enableShadowEffect && totalNotes <= _shadowThreshold;

                // 预计算画笔（线程安全）
                var normalBrush = GetCachedNormalBrush(1.0);
                var selectedBrush = GetCachedSelectedBrush(1.0);
                var normalPen = GetCachedPen(_noteBorderColor, 2);
                var selectedPen = GetCachedPen(_selectedNoteBorderColor, 2);

                // 并行处理音符数据准备
                System.Threading.Tasks.Parallel.ForEach(visibleNoteCache, kvp =>
                {
                    var note = kvp.Key;
                    var rect = kvp.Value;

                    if (rect.Width <= 0 || rect.Height <= 0) return;

                    // 视口剔除
                    if (!vulkanAdapter.IsRectVisible(rect)) return;

                    // 安全的状态检查
                    bool isBeingDragged = viewModel.DragState?.IsDragging == true && 
                                         viewModel.DragState.DraggingNotes?.Contains(note) == true;
                    bool isBeingResized = viewModel.ResizeState?.IsResizing == true && 
                                         viewModel.ResizeState.ResizingNotes?.Contains(note) == true;
                    bool isBeingManipulated = isBeingDragged || isBeingResized;

                    var roundedRect = new RoundedRect(rect, CORNER_RADIUS);

                    if (note.IsSelected || isBeingManipulated)
                    {
                        selectedRects.Add(roundedRect);
                        if (shouldRenderShadow && isBeingManipulated)
                        {
                            var shadowOffset = new Vector(1.0, 1.0);
                            shadowRects.Add(new RoundedRect(rect.Translate(shadowOffset), CORNER_RADIUS));
                        }
                    }
                    else
                    {
                        normalRects.Add(roundedRect);
                        if (shouldRenderShadow)
                        {
                            var shadowOffset = new Vector(1.0, 1.0);
                            shadowRects.Add(new RoundedRect(rect.Translate(shadowOffset), CORNER_RADIUS));
                        }
                    }
                });

                // 转换为List进行渲染
                var shadowList = shadowRects.ToList();
                var normalList = normalRects.ToList();
                var selectedList = selectedRects.ToList();

                // 全局GPU批量渲染
                if (shadowList.Count > 0)
                {
                    vulkanAdapter.DrawRoundedRectsInstanced(shadowList, _shadowBrush, null);
                }

                if (normalList.Count > 0)
                {
                    vulkanAdapter.DrawRoundedRectsInstanced(normalList, normalBrush, normalPen);
                }

                if (selectedList.Count > 0)
                {
                    vulkanAdapter.DrawRoundedRectsInstanced(selectedList, selectedBrush, selectedPen);
                }

                vulkanAdapter.FlushBatches();

                // 性能监控
                if (totalNotes > 10000)
                {
                    EnderLogger.Instance.Debug("NoteRenderer-MultiThread", $"多线程优化音符渲染: {totalNotes}个音符, 可见{normalList.Count + selectedList.Count}个, 阴影:{shadowList.Count}, 普通:{normalList.Count}, 选中:{selectedList.Count}");
                }
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.Error("NoteRenderer-MultiThread", $"多线程优化音符渲染错误: {ex.Message}\n{ex.StackTrace}");
                // 错误时回退到单线程优化版本
                try
                {
                    RenderNotesOptimized(context, viewModel, visibleNoteCache, vulkanAdapter);
                }
                catch (Exception fallbackEx)
                {
                    EnderLogger.Instance.Error("NoteRenderer-MultiThread", $"回退渲染也失败: {fallbackEx.Message}");
                }
            }
            finally
            {
                // 如果是我们创建的适配器，确保正确释放资源
                if (createdAdapter && vulkanAdapter != null)
                {
                    vulkanAdapter.Dispose();
                }
            }
        }
    }
}