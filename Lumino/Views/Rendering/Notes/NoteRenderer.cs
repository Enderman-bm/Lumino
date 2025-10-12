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
        private readonly Dictionary<double, IBrush> _normalBrushCache = new();
        private readonly Dictionary<double, IBrush> _selectedBrushCache = new();
        
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
            // 始终使用Vulkan适配器
            vulkanAdapter ??= new VulkanDrawingContextAdapter(context);

            try
            {
                int drawnNotes = 0;
                int totalNotes = visibleNoteCache.Count;
                
                // 动态判断是否启用阴影效果
                bool shouldRenderShadow = _enableShadowEffect && totalNotes <= _shadowThreshold;
                
                // GPU加速优化：预计算和批处理策略
                // 1. 预计算所有音符的状态和属性，减少循环中的计算
                var normalNotes = new List<(NoteViewModel note, Rect rect, IBrush brush, IPen pen, bool renderShadow)>();
                var specialNotes = new List<(NoteViewModel note, Rect rect, IBrush brush, IPen pen, bool isManipulated, bool renderShadow)>();
                
                foreach (var kvp in visibleNoteCache)
                {
                    var note = kvp.Key;
                    var rect = kvp.Value;

                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        bool isBeingDragged = viewModel.DragState.IsDragging && viewModel.DragState.DraggingNotes.Contains(note);
                        bool isBeingResized = viewModel.ResizeState.IsResizing && viewModel.ResizeState.ResizingNotes.Contains(note);
                        bool isBeingManipulated = isBeingDragged || isBeingResized;
                        
                        // 预计算画笔和属性
                        var opacity = Math.Max(0.7, note.Velocity / 127.0);
                        if (isBeingManipulated)
                        {
                            opacity = Math.Min(1.0, opacity * 1.1);
                        }
                        
                        IBrush brush;
                        IPen pen;
                        
                        if (note.IsSelected || isBeingManipulated)
                        {
                            brush = GetCachedSelectedBrush(opacity);
                            pen = new Pen(new SolidColorBrush(_selectedNoteBorderColor, 1.0), 2);
                            specialNotes.Add((note, rect, brush, pen, isBeingManipulated, shouldRenderShadow && isBeingManipulated));
                        }
                        else
                        {
                            brush = GetCachedNormalBrush(opacity);
                            pen = new Pen(new SolidColorBrush(_noteBorderColor, 1.0), 2);
                            normalNotes.Add((note, rect, brush, pen, shouldRenderShadow));
                        }
                    }
                }
                
                // 2. GPU批处理渲染：普通音符（大量）
                if (normalNotes.Count > 0)
                {
                    // 按画笔分组进行批处理，最大化GPU利用率
                    var groupedNormalNotes = normalNotes.GroupBy(n => (n.brush, n.pen, n.renderShadow));
                    
                    foreach (var group in groupedNormalNotes)
                    {
                        var (brush, pen, renderShadow) = group.Key;
                        
                        // 批量处理同属性的音符
                        foreach (var item in group)
                        {
                            var note = item.note;
                            var rect = item.rect;
                            
                            if (renderShadow)
                            {
                                var shadowOffset = new Vector(1.0, 1.0);
                                var shadowRect = new RoundedRect(rect.Translate(shadowOffset), CORNER_RADIUS);
                                vulkanAdapter.DrawRectangle(_shadowBrush, null, shadowRect);
                            }
                            
                            var roundedRect = new RoundedRect(rect, CORNER_RADIUS);
                            vulkanAdapter.DrawRectangle(brush, pen, roundedRect);
                            drawnNotes++;
                        }
                        
                        // 每批次后刷新，避免批次过大
                        if (group.Count() >= 50)
                        {
                            vulkanAdapter.FlushBatches();
                        }
                    }
                }
                
                // 刷新第二批批处理
                vulkanAdapter.FlushBatches();

                // 4. GPU批处理渲染：特殊音符（选中或操作中的）
                if (specialNotes.Count > 0)
                {
                    // 按操作状态分组进行批处理
                    var groupedSpecialNotes = specialNotes.GroupBy(n => (n.brush, n.pen, n.isManipulated, n.renderShadow));
                    
                    foreach (var group in groupedSpecialNotes)
                    {
                        var (brush, pen, isManipulated, renderShadow) = group.Key;
                        
                        // 批量处理同属性的特殊音符
                        foreach (var item in group)
                        {
                            var note = item.note;
                            var rect = item.rect;
                            
                            if (renderShadow)
                            {
                                var shadowOffset = new Vector(1.0, 1.0);
                                var shadowRect = new RoundedRect(rect.Translate(shadowOffset), CORNER_RADIUS);
                                vulkanAdapter.DrawRectangle(_shadowBrush, null, shadowRect);
                            }
                            
                            var roundedRect = new RoundedRect(rect, CORNER_RADIUS);
                            vulkanAdapter.DrawRectangle(brush, pen, roundedRect);
                            drawnNotes++;
                        }
                        
                        // 每批次后刷新
                        if (group.Count() >= 30)
                        {
                            vulkanAdapter.FlushBatches();
                        }
                    }
                }

                // 5. 最后刷新所有批处理
                vulkanAdapter.FlushBatches();

                // 调试信息
                if (drawnNotes > 0)
                {
                    EnderLogger.Instance.Debug("NoteRenderer", $"绘制 {drawnNotes} 个音符 (总数: {totalNotes})");
                }
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.Error("NoteRenderer", $"渲染错误: {ex.Message}");
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
                    var vulkanAdapter = new VulkanDrawingContextAdapter(context);
                    
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
            vulkanAdapter ??= new VulkanDrawingContextAdapter(context);

            var previewRect = calculateNoteRect(viewModel.PreviewNote);
            if (previewRect.Width > 0 && previewRect.Height > 0)
            {
                // 使用新的简化接口
                RenderPreviewNote(context, previewRect, true);
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
        /// </summary>
        private IBrush GetCachedNormalBrush(double opacity)
        {
            if (!_normalBrushCache.TryGetValue(opacity, out var brush))
            {
                brush = new SolidColorBrush(_noteColor, opacity);
                _normalBrushCache[opacity] = brush;
            }
            return brush;
        }

        /// <summary>
        /// 获取缓存的选中画笔（带透明度）
        /// </summary>
        private IBrush GetCachedSelectedBrush(double opacity)
        {
            if (!_selectedBrushCache.TryGetValue(opacity, out var brush))
            {
                brush = new SolidColorBrush(_selectedNoteColor, opacity);
                _selectedBrushCache[opacity] = brush;
            }
            return brush;
        }

        /// <summary>
        /// 清除画笔缓存
        /// </summary>
        public void ClearBrushCache()
        {
            _normalBrushCache.Clear();
            _selectedBrushCache.Clear();
        }

        /// <summary>
        /// 全局优化的音符渲染 - 针对10W+音符的极高性能优化
        /// </summary>
        public void RenderNotesOptimized(DrawingContext context, PianoRollViewModel viewModel, System.Collections.Generic.Dictionary<NoteViewModel, Rect> visibleNoteCache, VulkanDrawingContextAdapter? vulkanAdapter = null)
        {
            // 始终使用Vulkan适配器
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
                var normalRects = new List<RoundedRect>();
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
    
                    // 状态检查
                    bool isSelected = note.IsSelected ||
                        (viewModel.DragState.IsDragging && viewModel.DragState.DraggingNotes.Contains(note)) ||
                        (viewModel.ResizeState.IsResizing && viewModel.ResizeState.ResizingNotes.Contains(note));
    
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
    
                // 阶段3：高效批量渲染
                if (shadowRects.Count > 0)
                {
                    vulkanAdapter.DrawRoundedRectsInstanced(shadowRects, _shadowBrush, null);
                }
    
                if (normalRects.Count > 0)
                {
                    var normalBrush = GetCachedNormalBrush(1.0);
                    var normalPen = new Pen(new SolidColorBrush(_noteBorderColor, 1.0), 2);
                    vulkanAdapter.DrawRoundedRectsInstanced(normalRects, normalBrush, normalPen);
                }
    
                if (selectedRects.Count > 0)
                {
                    var selectedBrush = GetCachedSelectedBrush(1.0);
                    var selectedPen = new Pen(new SolidColorBrush(_selectedNoteBorderColor, 1.0), 2);
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
                EnderLogger.Instance.Error("NoteRenderer-UltraOptimized", $"错误:{ex.Message}, 回退到传统渲染");
                RenderNotes(context, viewModel, visibleNoteCache, vulkanAdapter);
            }
        }

        /// <summary>
        /// 多线程优化的音符渲染 - 使用并行处理准备数据
        /// </summary>
        public void RenderNotesParallel(DrawingContext context, PianoRollViewModel viewModel, System.Collections.Generic.Dictionary<NoteViewModel, Rect> visibleNoteCache, VulkanDrawingContextAdapter? vulkanAdapter = null)
        {
            // 始终使用Vulkan适配器
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

                // 预计算画笔
                var normalBrush = GetCachedNormalBrush(1.0);
                var selectedBrush = GetCachedSelectedBrush(1.0);
                var normalPen = new Pen(new SolidColorBrush(_noteBorderColor, 1.0), 2);
                var selectedPen = new Pen(new SolidColorBrush(_selectedNoteBorderColor, 1.0), 2);

                // 并行处理音符数据准备
                System.Threading.Tasks.Parallel.ForEach(visibleNoteCache, kvp =>
                {
                    var note = kvp.Key;
                    var rect = kvp.Value;

                    if (rect.Width <= 0 || rect.Height <= 0) return;

                    // 视口剔除
                    if (!vulkanAdapter.IsRectVisible(rect)) return;

                    bool isBeingDragged = viewModel.DragState.IsDragging && viewModel.DragState.DraggingNotes.Contains(note);
                    bool isBeingResized = viewModel.ResizeState.IsResizing && viewModel.ResizeState.ResizingNotes.Contains(note);
                    bool isBeingManipulated = isBeingDragged || isBeingResized;

                    // 计算透明度
                    var opacity = Math.Max(0.7, note.Velocity / 127.0);
                    if (isBeingManipulated)
                    {
                        opacity = Math.Min(1.0, opacity * 1.1);
                    }

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
                EnderLogger.Instance.Error("NoteRenderer-MultiThread", $"多线程优化音符渲染错误: {ex.Message}");
                // 错误时回退到单线程优化版本
                RenderNotesOptimized(context, viewModel, visibleNoteCache, vulkanAdapter);
            }
        }
    }
}