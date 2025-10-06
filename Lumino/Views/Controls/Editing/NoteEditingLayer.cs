using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Lumino.ViewModels.Editor;
using Lumino.ViewModels.Editor.Commands;
using Lumino.Views.Rendering.Notes;
using Lumino.Views.Rendering.Utils;

namespace Lumino.Views.Controls.Editing
{
    /// <summary>
    /// 音符编辑层 - 支持1000万+音符的高性能编辑
    /// </summary>
    public class NoteEditingLayer : Control, IDisposable
    {
        #region 私有字段

        // 核心组件
        private NoteRenderer? _noteRenderer;
        private CreatingNoteRenderer? _creatingNoteRenderer;
        private NoteSelectionManager? _selectionManager;
        private NoteDragManager? _dragManager;
        private NoteResizeManager? _resizeManager;

        // 缓存系统
        private readonly Dictionary<NoteViewModel, Rect> _noteRectCache = new();
        private readonly Dictionary<NoteViewModel, CachedNoteData> _noteCache = new();
        private readonly Dictionary<string, IBrush> _brushCache = new();
        private readonly Dictionary<string, IPen> _penCache = new();

        // 性能优化组件
        private QuadTreeSpatialIndex? _spatialIndex;
        private MultiLevelCacheSystem? _cacheSystem;
        private GpuComputeAcceleration? _gpuCompute;
        private PerformanceMonitor _performanceMonitor;

        // 状态跟踪
        private PianoRollViewModel? _viewModel;
        private bool _isDragging;
        private bool _isResizing;
        private Point _lastMousePosition;
        private DateTime _lastRenderTime = DateTime.MinValue;

        // 性能统计
        private int _cacheHitCount = 0;
        private int _cacheMissCount = 0;
        private int _visibleNotesCount = 0;
        private double _averageRenderTime = 0;

        // 生命周期状态
        private bool _isDisposed = false;

        #endregion

        #region 构造函数和初始化

        public NoteEditingLayer()
        {
            _performanceMonitor = new PerformanceMonitor();
            InitializeComponents();
            InitializeAdvancedOptimizations();
        }

        /// <summary>
        /// 初始化基础组件
        /// </summary>
        private void InitializeComponents()
        {
            try
            {
                // 初始化渲染器
                _noteRenderer = new NoteRenderer();
                _creatingNoteRenderer = new CreatingNoteRenderer();
                _selectionManager = new NoteSelectionManager();
                _dragManager = new NoteDragManager();
                _resizeManager = new NoteResizeManager();

                // 初始化画笔缓存
                InitializeBrushCache();

                Debug.WriteLine("[NoteEditingLayer] 基础组件初始化成功");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 基础组件初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化高级优化组件
        /// </summary>
        private void InitializeAdvancedOptimizations()
        {
            try
            {
                // TODO: 需要提供正确的 bounds 参数
                _spatialIndex = new QuadTreeSpatialIndex(new Rect(0, 0, 10000, 10000));
                _cacheSystem = new MultiLevelCacheSystem();
                // TODO: GpuComputeAcceleration 需要 Vulkan 参数，暂时注释
                // _gpuCompute = new GpuComputeAcceleration();

                Debug.WriteLine("[NoteEditingLayer] 高级优化组件初始化成功");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 优化组件初始化失败: {ex.Message}");
                _spatialIndex = null;
                _cacheSystem = null;
                _gpuCompute = null;
            }
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

                Debug.WriteLine("[NoteEditingLayer] 画笔缓存初始化成功");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 画笔缓存初始化失败: {ex.Message}");
            }
        }

        #endregion

        #region 依赖属性

        /// <summary>
        /// 视图模型依赖属性
        /// </summary>
        public static readonly DirectProperty<NoteEditingLayer, PianoRollViewModel?> ViewModelProperty =
            AvaloniaProperty.RegisterDirect<NoteEditingLayer, PianoRollViewModel?>(
                nameof(ViewModel),
                layer => layer.ViewModel,
                (layer, vm) => layer.ViewModel = vm);

        /// <summary>
        /// 视图模型
        /// </summary>
        public PianoRollViewModel? ViewModel
        {
            get => _viewModel;
            set
            {
                if (SetAndRaise(ViewModelProperty, ref _viewModel, value))
                {
                    OnViewModelChanged();
                }
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 视图模型变更处理
        /// </summary>
        private void OnViewModelChanged()
        {
            try
            {
                if (_viewModel != null)
                {
                    // 订阅视图模型事件
                    _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                    _viewModel.CurrentTrackNotes.CollectionChanged += OnNotesCollectionChanged;

                    // 初始化空间索引
                    UpdateSpatialIndex();

                    // 强制重绘
                    InvalidateVisual();
                }

                Debug.WriteLine("[NoteEditingLayer] 视图模型已变更");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 视图模型变更处理错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 视图模型属性变更处理
        /// </summary>
        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName == nameof(PianoRollViewModel.ViewportWidth) ||
                    e.PropertyName == nameof(PianoRollViewModel.ViewportHeight) ||
                    e.PropertyName == nameof(PianoRollViewModel.HorizontalOffset) ||
                    e.PropertyName == nameof(PianoRollViewModel.VerticalOffset))
                {
                    // 视口变化时更新可见音符
                    Dispatcher.UIThread.Post(() =>
                    {
                        UpdateVisibleNotesCacheOptimized();
                        InvalidateVisual();
                    }, DispatcherPriority.Render);
                }
                else if (e.PropertyName == nameof(PianoRollViewModel.CurrentTrackIndex))
                {
                    // ✅ 轨道切换时异步更新空间索引和可见音符 - 避免UI线程阻塞
                    Debug.WriteLine("[NoteEditingLayer] CurrentTrackIndex 属性变化,异步更新空间索引和音符显示");
                    
                    // 使用Task.Run在后台线程执行索引重建,避免卡死UI
                    Task.Run(async () =>
                    {
                        try
                        {
                            var startTime = DateTime.UtcNow;
                            
                            // 在后台线程重建索引
                            if (_viewModel != null && _spatialIndex != null)
                            {
                                await Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    _spatialIndex.RebuildIndex(_viewModel.CurrentTrackNotes, _viewModel.Coordinates);
                                }, DispatcherPriority.Background); // 使用Background优先级
                                
                                Debug.WriteLine($"[NoteEditingLayer] 异步空间索引更新完成 - 耗时: {(DateTime.UtcNow - startTime).TotalMilliseconds:F2}ms");
                            }
                            
                            // 索引重建完成后,在UI线程更新显示
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                UpdateVisibleNotesCacheOptimized();
                                InvalidateVisual();
                            }, DispatcherPriority.Render);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[NoteEditingLayer] 异步索引重建错误: {ex.Message}");
                        }
                    });
                }
                else if (e.PropertyName == nameof(PianoRollViewModel.PreviewNote))
                {
                    // ✅ 预览音符变化时立即重新渲染
                    Dispatcher.UIThread.Post(() =>
                    {
                        InvalidateVisual();
                    }, DispatcherPriority.Render);
                }
                else if (e.PropertyName == nameof(PianoRollViewModel.CreatingNote))
                {
                    // ✅ 正在创建音符变化时立即重新渲染
                    Debug.WriteLine("[NoteEditingLayer] CreatingNote 属性变化,触发重新渲染");
                    Dispatcher.UIThread.Post(() =>
                    {
                        InvalidateVisual();
                    }, DispatcherPriority.Render);
                }
                else if (e.PropertyName == nameof(PianoRollViewModel.IsSelecting) ||
                         e.PropertyName == nameof(PianoRollViewModel.SelectionStart) ||
                         e.PropertyName == nameof(PianoRollViewModel.SelectionEnd))
                {
                    // ✅ 选择状态变化时立即重新渲染框选框
                    Debug.WriteLine($"[NoteEditingLayer] 选择状态变化: {e.PropertyName}, 触发重新渲染");
                    Dispatcher.UIThread.Post(() =>
                    {
                        InvalidateVisual();
                    }, DispatcherPriority.Render);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 视图模型属性变更处理错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 音符集合变更处理
        /// </summary>
        private void OnNotesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            try
            {
                // 音符集合变化时更新缓存
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateSpatialIndex();
                    UpdateVisibleNotesCacheOptimized();
                    InvalidateVisual();
                }, DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 音符集合变更处理错误: {ex.Message}");
            }
        }

        #endregion

        #region 渲染逻辑

        /// <summary>
        /// 重写渲染方法
        /// </summary>
        public override void Render(DrawingContext context)
        {
            if (_isDisposed || _viewModel == null || _noteRenderer == null) return;

            // ✅ 关键修复:绘制透明背景以接收鼠标事件
            // Control 必须绘制内容才能参与命中测试(hit testing)
            context.FillRectangle(Brushes.Transparent, new Rect(0, 0, Bounds.Width, Bounds.Height));

            try
            {
                var startTime = DateTime.UtcNow;

                // ✅ 洋葱皮模式: 先渲染所有轨道的音符(半透明)
                if (_viewModel.IsOnionSkinEnabled)
                {
                    RenderAllTracksWithOnionSkin(context);
                }

                // 渲染当前轨道的音符(不透明)
                var visibleNotes = GetVisibleNotesOptimized();

                if (visibleNotes.Count > 0)
                {
                    // 更新渲染器缓存
                    _noteRenderer.UpdateNoteCache(visibleNotes);

                    // 渲染音符
                    _noteRenderer.RenderNotes(context, _viewModel, visibleNotes);

                    // 渲染拖拽预览 - 已禁用，避免显示多余的透明框
                    // if (_isDragging && _dragManager != null)
                    // {
                    //     RenderDragPreview(context);
                    // }

                    // 渲染调整大小预览 - 已禁用，避免显示多余的透明框
                    // if (_isResizing && _resizeManager != null)
                    // {
                    //     RenderResizePreview(context);
                    // }
                }

                // ✅ 渲染预览音符 - 铅笔工具的音符放置预览框
                if (_viewModel.PreviewNote != null)
                {
                    RenderPreviewNote(context, _viewModel.PreviewNote);
                }

                // ✅ 渲染正在创建的音符 - 铅笔工具拖拽延长动画
                if (_viewModel.CreatingNote != null && _viewModel.IsCreatingNote && _creatingNoteRenderer != null)
                {
                    Debug.WriteLine($"[NoteEditingLayer] 渲染正在创建的音符: Duration={_viewModel.CreatingNote.Duration}");
                    _creatingNoteRenderer.Render(context, null, _viewModel, CalculateNoteRect);
                }

                // ✅ 渲染演奏指示线
                RenderPlaybackIndicator(context);

                // ✅ 渲染框选矩形
                RenderSelectionBox(context);

                // 更新性能统计
                _lastRenderTime = DateTime.UtcNow;
                _visibleNotesCount = visibleNotes.Count;
                _averageRenderTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _performanceMonitor.RecordPerformance("NoteEditing", _averageRenderTime);

                Debug.WriteLine($"[NoteEditingLayer] 渲染完成 - 可见音符: {visibleNotes.Count}, 耗时: {_averageRenderTime:F2}ms");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 渲染错误: {ex.Message}");
                RenderErrorFallback(context);
            }
        }

        /// <summary>
        /// 渲染拖拽预览
        /// </summary>
        private void RenderDragPreview(DrawingContext context)
        {
            try
            {
                if (_dragManager?.SelectedNotes == null) return;

                var dragBrush = _brushCache["DragPreview"];
                var dragPen = _penCache["DragPreview"];

                foreach (var note in _dragManager.SelectedNotes)
                {
                    if (_noteRectCache.TryGetValue(note, out var originalRect))
                    {
                        var previewRect = new Rect(
                            originalRect.X + _dragManager.DragOffset.X,
                            originalRect.Y + _dragManager.DragOffset.Y,
                            originalRect.Width,
                            originalRect.Height);

                        context.DrawRectangle(dragBrush, dragPen, previewRect);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 渲染拖拽预览错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 渲染调整大小预览
        /// </summary>
        private void RenderResizePreview(DrawingContext context)
        {
            try
            {
                if (_resizeManager?.SelectedNotes == null) return;

                var resizeBrush = _brushCache["DragPreview"];
                var resizePen = _penCache["DragPreview"];

                foreach (var note in _resizeManager.SelectedNotes)
                {
                    if (_noteRectCache.TryGetValue(note, out var originalRect))
                    {
                        var previewRect = new Rect(
                            originalRect.X,
                            originalRect.Y,
                            originalRect.Width + _resizeManager.ResizeDelta,
                            originalRect.Height);

                        context.DrawRectangle(resizeBrush, resizePen, previewRect);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 渲染调整大小预览错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 渲染预览音符 - 铅笔工具的音符放置预览框
        /// </summary>
        private void RenderPreviewNote(DrawingContext context, NoteViewModel previewNote)
        {
            try
            {
                if (_viewModel == null) return;

                Debug.WriteLine($"[NoteEditingLayer] 渲染预览音符: Pitch={previewNote.Pitch}, Position={previewNote.StartPosition}");

                // ✅ 修复: 使用 Coordinates 的 GetScreenNoteRect 来获取考虑滚动偏移的屏幕坐标
                var previewRect = _viewModel.Coordinates.GetScreenNoteRect(previewNote);

                // 绘制半透明预览音符 - 绿色主题带圆角
                var previewBrush = new SolidColorBrush(Color.FromArgb(128, 50, 205, 50)); // 半透明绿色 (LimeGreen)
                var previewPen = new Pen(new SolidColorBrush(Colors.DarkGreen), 2);

                var roundedRect = new RoundedRect(previewRect, 4);
                context.DrawRectangle(previewBrush, previewPen, roundedRect);

                Debug.WriteLine($"[NoteEditingLayer] 预览框渲染: X={previewRect.X:F2}, Y={previewRect.Y:F2}, W={previewRect.Width:F2}, H={previewRect.Height:F2}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 渲染预览音符错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 渲染洋葱皮模式 - 半透明显示所有轨道的音符
        /// </summary>
        private void RenderAllTracksWithOnionSkin(DrawingContext context)
        {
            if (_viewModel == null || _noteRenderer == null) return;

            try
            {
                // 获取所有轨道的音符(排除当前轨道)
                var allNotes = _viewModel.Notes.Where(n => n.TrackIndex != _viewModel.CurrentTrackIndex).ToList();
                
                if (allNotes.Count == 0) return;

                var startTime = DateTime.UtcNow;

                // 计算可见区域的所有轨道音符
                var visibleOtherTrackNotes = GetVisibleNotesForAllTracks(allNotes);

                if (visibleOtherTrackNotes.Count > 0)
                {
                    Debug.WriteLine($"[NoteEditingLayer] 洋葱皮渲染 - 其他轨道可见音符: {visibleOtherTrackNotes.Count}");

                    // 使用半透明渲染其他轨道的音符
                    RenderNotesWithOpacity(context, visibleOtherTrackNotes, _viewModel.OnionSkinOpacity);
                }

                Debug.WriteLine($"[NoteEditingLayer] 洋葱皮渲染完成 - 耗时: {(DateTime.UtcNow - startTime).TotalMilliseconds:F2}ms");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 洋葱皮渲染错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取所有轨道的可见音符
        /// </summary>
        private Dictionary<NoteViewModel, Rect> GetVisibleNotesForAllTracks(List<NoteViewModel> notes)
        {
            var visibleNotes = new Dictionary<NoteViewModel, Rect>();

            if (_viewModel == null || notes.Count == 0) return visibleNotes;

            try
            {
                // 遍历所有音符,使用坐标服务检查可见性
                foreach (var note in notes)
                {
                    // 使用坐标服务检查音符是否可见
                    if (_viewModel.Coordinates.IsNoteVisible(note))
                    {
                        // 获取屏幕坐标矩形
                        var screenRect = _viewModel.Coordinates.GetScreenNoteRect(note);
                        visibleNotes[note] = screenRect;
                    }
                }

                Debug.WriteLine($"[NoteEditingLayer] 找到 {visibleNotes.Count} 个其他轨道的可见音符");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 获取所有轨道可见音符错误: {ex.Message}");
            }

            return visibleNotes;
        }

        /// <summary>
        /// 使用指定透明度渲染音符
        /// </summary>
        private void RenderNotesWithOpacity(DrawingContext context, Dictionary<NoteViewModel, Rect> noteRects, double opacity)
        {
            if (noteRects.Count == 0) return;

            try
            {
                // 为不同轨道的音符分组
                var notesByTrack = noteRects.GroupBy(kvp => kvp.Key.TrackIndex);

                foreach (var trackGroup in notesByTrack)
                {
                    var trackIndex = trackGroup.Key;
                    
                    // 为每个轨道生成高饱和度的纯色
                    var trackColor = GetTrackColor(trackIndex);
                    var opacityBrush = new SolidColorBrush(trackColor, opacity);
                    var opacityPen = new Pen(new SolidColorBrush(DarkenColor(trackColor), opacity), 1);

                    // 渲染该轨道的所有音符
                    foreach (var kvp in trackGroup)
                    {
                        var rect = kvp.Value;
                        
                        // 确保矩形有效
                        if (rect.Width > 0 && rect.Height > 0)
                        {
                            context.DrawRectangle(opacityBrush, opacityPen, rect);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 半透明渲染音符错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据轨道索引生成高饱和度纯色
        /// </summary>
        private Color GetTrackColor(int trackIndex)
        {
            // 使用HSV色彩空间生成高饱和度纯色
            // 每个轨道在色环上均匀分布
            var hue = (trackIndex * 137.5) % 360; // 使用黄金角度分布，避免相邻轨道颜色相似
            return HSVToRGB(hue, 0.85, 0.95); // 高饱和度(85%)，高亮度(95%)
        }

        /// <summary>
        /// 使颜色变暗用于边框
        /// </summary>
        private Color DarkenColor(Color color)
        {
            return Color.FromRgb(
                (byte)(color.R * 0.7),
                (byte)(color.G * 0.7),
                (byte)(color.B * 0.7)
            );
        }

        /// <summary>
        /// HSV转RGB - 生成高饱和度纯色
        /// </summary>
        private Color HSVToRGB(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            byte v = Convert.ToByte(value);
            byte p = Convert.ToByte(value * (1 - saturation));
            byte q = Convert.ToByte(value * (1 - f * saturation));
            byte t = Convert.ToByte(value * (1 - (1 - f) * saturation));

            switch (hi)
            {
                case 0: return Color.FromRgb(v, t, p);
                case 1: return Color.FromRgb(q, v, p);
                case 2: return Color.FromRgb(p, v, t);
                case 3: return Color.FromRgb(p, q, v);
                case 4: return Color.FromRgb(t, p, v);
                default: return Color.FromRgb(v, p, q);
            }
        }

        /// <summary>
        /// 渲染演奏指示线
        /// </summary>
        private void RenderPlaybackIndicator(DrawingContext context)
        {
            if (_viewModel == null) return;

            try
            {
                // 计算演奏指示线的X坐标（基于PlaybackPosition和当前滚动偏移）
                var x = _viewModel.PlaybackPosition * _viewModel.BaseQuarterNoteWidth - _viewModel.CurrentScrollOffset;

                // 只在可见范围内绘制
                if (x >= 0 && x <= Bounds.Width)
                {
                    // 创建黄色画笔（较粗，3.5像素）
                    var playbackPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 200, 0)), 3.5);

                    // 绘制从顶部到底部的垂直线
                    context.DrawLine(playbackPen, new Point(x, 0), new Point(x, Bounds.Height));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 渲染演奏指示线错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 渲染框选矩形
        /// </summary>
        /// <summary>
        /// 渲染框选框
        /// </summary>
        private void RenderSelectionBox(DrawingContext context)
        {
            if (_viewModel == null || _viewModel.SelectionState == null) return;

            try
            {
                // 检查是否正在进行框选
                bool isSelecting = _viewModel.SelectionState.IsSelecting;
                bool hasStart = _viewModel.SelectionState.SelectionStart.HasValue;
                bool hasEnd = _viewModel.SelectionState.SelectionEnd.HasValue;

                Debug.WriteLine($"[NoteEditingLayer] 检查框选状态: IsSelecting={isSelecting}, HasStart={hasStart}, HasEnd={hasEnd}");

                if (isSelecting && hasStart && hasEnd)
                {
                    var start = _viewModel.SelectionState.SelectionStart!.Value;
                    var end = _viewModel.SelectionState.SelectionEnd!.Value;

                    Debug.WriteLine($"[NoteEditingLayer] 框选坐标: Start={start}, End={end}");

                    // 计算框选矩形
                    var x = Math.Min(start.X, end.X);
                    var y = Math.Min(start.Y, end.Y);
                    var width = Math.Abs(end.X - start.X);
                    var height = Math.Abs(end.Y - start.Y);

                    // 确保矩形有最小尺寸
                    if (width < 1) width = 1;
                    if (height < 1) height = 1;

                    var selectionRect = new Rect(x, y, width, height);

                    // 创建半透明的蓝色填充和边框
                    var fillBrush = new SolidColorBrush(Color.FromArgb(60, 100, 150, 255)); // 半透明蓝色填充
                    var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 100, 150, 255)), 2); // 蓝色边框

                    // 绘制框选矩形
                    context.DrawRectangle(fillBrush, borderPen, selectionRect);

                    Debug.WriteLine($"[NoteEditingLayer] 成功渲染框选矩形: {selectionRect}");
                }
                else
                {
                    Debug.WriteLine($"[NoteEditingLayer] 跳过框选渲染: 条件不满足");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 渲染框选矩形错误: {ex.Message}");
                Debug.WriteLine($"[NoteEditingLayer] 异常详情: {ex.StackTrace}");
            }
        }        /// <summary>
        /// 错误回退渲染
        /// </summary>
        private void RenderErrorFallback(DrawingContext context)
        {
            try
            {
                var errorBrush = new SolidColorBrush(Colors.Red, 0.3);
                var errorPen = new Pen(new SolidColorBrush(Colors.DarkRed), 1);
                var errorRect = new Rect(0, 0, Bounds.Width, Bounds.Height);

                context.DrawRectangle(errorBrush, errorPen, errorRect);

                // TODO: Avalonia 文本绘制 API 需要确认正确的 FormattedText/TextLayout 用法
                // var errorText = new FormattedText(
                //     "渲染错误",
                //     new Typeface("Arial"),
                //     16,
                //     TextAlignment.Center,
                //     TextWrapping.NoWrap,
                //     Size.Infinity);
                // context.DrawText(Brushes.White, new Point(Bounds.Width / 2 - 40, Bounds.Height / 2 - 10), errorText);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 错误回退渲染失败: {ex.Message}");
            }
        }

        #endregion

        #region 优化方法

        /// <summary>
        /// 优化的可见音符缓存更新
        /// </summary>
        private void UpdateVisibleNotesCacheOptimized()
        {
            if (_isDisposed || _viewModel == null) return;

            try
            {
                var startTime = DateTime.UtcNow;

                // ✅ 修复: 使用屏幕坐标定义viewport (起点为0,0)
                // CalculateNoteRect返回的是屏幕坐标,所以viewport也应该是屏幕坐标
                var viewport = new Rect(
                    0,  // 屏幕坐标起点X
                    0,  // 屏幕坐标起点Y
                    _viewModel.ViewportWidth,
                    _viewModel.ViewportHeight);

                List<NoteViewModel> visibleNotes;

                if (_spatialIndex != null && _spatialIndex.IsInitialized)
                {
                    // 使用四叉树空间索引
                    visibleNotes = _spatialIndex.QueryVisibleNotes(viewport);
                }
                else
                {
                    // 回退到传统方法 - 只显示当前轨道的音符
                    visibleNotes = _viewModel.CurrentTrackNotes
                        .Where(note => IsNoteVisible(note, viewport))
                        .ToList();
                }

                // 更新音符矩形缓存
                _noteRectCache.Clear();
                foreach (var note in visibleNotes)
                {
                    var rect = CalculateNoteRect(note);
                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        _noteRectCache[note] = rect;
                    }
                }

                // 更新缓存系统
                if (_cacheSystem != null)
                {
                    _cacheSystem.UpdateCache(_noteRectCache);
                }

                Debug.WriteLine($"[NoteEditingLayer] 可见音符缓存更新完成 - 数量: {_noteRectCache.Count}, 耗时: {(DateTime.UtcNow - startTime).TotalMilliseconds:F2}ms");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 可见音符缓存更新错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新空间索引
        /// </summary>
        /// <summary>
        /// 更新空间索引 - 同步方法,仅用于初始化
        /// 警告: 轨道切换时使用异步方法,避免UI线程阻塞
        /// </summary>
        private void UpdateSpatialIndex()
        {
            if (_isDisposed || _viewModel == null || _spatialIndex == null) return;

            try
            {
                var startTime = DateTime.UtcNow;

                // 重建空间索引 - 只索引当前轨道的音符,传入坐标服务用于正确计算矩形
                _spatialIndex.RebuildIndex(_viewModel.CurrentTrackNotes, _viewModel.Coordinates);

                Debug.WriteLine($"[NoteEditingLayer] 空间索引更新完成 - 耗时: {(DateTime.UtcNow - startTime).TotalMilliseconds:F2}ms");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 空间索引更新错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取优化的可见音符
        /// </summary>
        private Dictionary<NoteViewModel, Rect> GetVisibleNotesOptimized()
        {
            var result = new Dictionary<NoteViewModel, Rect>();

            try
            {
                // 使用缓存系统
                if (_cacheSystem != null && _cacheSystem.HasValidCache())
                {
                    return _cacheSystem.GetCachedData();
                }

                // 更新缓存
                UpdateVisibleNotesCacheOptimized();
                return _noteRectCache;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 获取可见音符错误: {ex.Message}");
                return _noteRectCache;
            }
        }

        /// <summary>
        /// 计算音符矩形 - 使用屏幕坐标(考虑滚动偏移)
        /// </summary>
        private Rect CalculateNoteRect(NoteViewModel note)
        {
            try
            {
                if (_viewModel == null) return new Rect();

                // ✅ 修复: 使用 Coordinates.GetScreenNoteRect 来获取考虑滚动偏移的屏幕坐标
                return _viewModel.Coordinates.GetScreenNoteRect(note);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 计算音符矩形错误: {ex.Message}");
                return new Rect();
            }
        }

        /// <summary>
        /// 判断音符是否可见
        /// </summary>
        private bool IsNoteVisible(NoteViewModel note, Rect viewport)
        {
            try
            {
                var rect = CalculateNoteRect(note);
                return rect.Intersects(viewport);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 判断音符可见性错误: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 输入处理

        /// <summary>
        /// 鼠标按下事件
        /// </summary>
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (_isDisposed || _viewModel == null) return;

            Debug.WriteLine("[NoteEditingLayer] OnPointerPressed 被调用!");

            // 调用 EditorCommandsViewModel 处理事件
            if (_viewModel?.EditorCommands != null)
            {
                var point = e.GetPosition(this);
                var currentTool = _viewModel.CurrentTool;
                var modifiers = e.KeyModifiers;
                
                Debug.WriteLine($"[NoteEditingLayer] 准备调用 EditorCommands, Tool={currentTool}, Position={point}");

                var args = new EditorInteractionArgs
                {
                    Position = point,
                    Tool = currentTool,
                    Modifiers = modifiers,
                    InteractionType = EditorInteractionType.Press
                };

                _viewModel.EditorCommands.HandleInteractionCommand.Execute(args);
            }

            try
            {
                var position = e.GetPosition(this);
                var visibleNotes = GetVisibleNotesOptimized();

                // 查找点击的音符
                var clickedNote = FindNoteAtPosition(position, visibleNotes);

                if (clickedNote != null)
                {
                    // 处理音符点击
                    HandleNoteClick(clickedNote, e);
                }
                else
                {
                    // 处理空白区域点击
                    HandleEmptyClick(e);
                }

                _lastMousePosition = position;
                base.OnPointerPressed(e);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 鼠标按下事件错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 鼠标移动事件
        /// </summary>
        protected override void OnPointerMoved(PointerEventArgs e)
        {
            if (_isDisposed || _viewModel == null) return;

            // 调用 EditorCommandsViewModel 处理事件
            if (_viewModel?.EditorCommands != null)
            {
                var point = e.GetPosition(this);
                var currentTool = _viewModel.CurrentTool;
                var modifiers = e.KeyModifiers;
                
                // 降低日志频率:每10次输出一次
                if (DateTime.UtcNow.Millisecond % 100 < 10)
                    Debug.WriteLine($"[NoteEditingLayer] OnPointerMoved, Tool={currentTool}");

                var args = new EditorInteractionArgs
                {
                    Position = point,
                    Tool = currentTool,
                    Modifiers = modifiers,
                    InteractionType = EditorInteractionType.Move
                };

                _viewModel.EditorCommands.HandleInteractionCommand.Execute(args);
            }

            try
            {
                var position = e.GetPosition(this);

                if (_isDragging && _dragManager != null)
                {
                    // 处理拖拽
                    var delta = position - _lastMousePosition;
                    _dragManager.UpdateDrag(delta);
                    InvalidateVisual();
                }
                else if (_isResizing && _resizeManager != null)
                {
                    // 处理调整大小
                    var delta = position.X - _lastMousePosition.X;
                    _resizeManager.UpdateResize(delta);
                    InvalidateVisual();
                }
                else
                {
                    // 处理悬停
                    HandleHover(position);
                }

                _lastMousePosition = position;
                base.OnPointerMoved(e);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 鼠标移动事件错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 鼠标释放事件
        /// </summary>
        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (_isDisposed || _viewModel == null) return;

            Debug.WriteLine("[NoteEditingLayer] OnPointerReleased 被调用!");

            // 调用 EditorCommandsViewModel 处理事件
            if (_viewModel?.EditorCommands != null)
            {
                var point = e.GetPosition(this);
                var currentTool = _viewModel.CurrentTool;
                var modifiers = e.KeyModifiers;
                
                Debug.WriteLine($"[NoteEditingLayer] 准备调用 EditorCommands Release, Tool={currentTool}");

                var args = new EditorInteractionArgs
                {
                    Position = point,
                    Tool = currentTool,
                    Modifiers = modifiers,
                    InteractionType = EditorInteractionType.Release
                };

                _viewModel.EditorCommands.HandleInteractionCommand.Execute(args);
            }

            try
            {
                if (_isDragging)
                {
                    EndDrag();
                }
                else if (_isResizing)
                {
                    EndResize();
                }

                base.OnPointerReleased(e);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 鼠标释放事件错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 查找位置处的音符
        /// </summary>
        private NoteViewModel? FindNoteAtPosition(Point position, Dictionary<NoteViewModel, Rect> visibleNotes)
        {
            try
            {
                foreach (var kvp in visibleNotes)
                {
                    if (kvp.Value.Contains(position))
                    {
                        return kvp.Key;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 查找音符错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 处理音符点击
        /// </summary>
        private void HandleNoteClick(NoteViewModel note, PointerPressedEventArgs e)
        {
            try
            {
                var properties = e.GetCurrentPoint(this).Properties;

                if (properties.IsLeftButtonPressed)
                {
                    // TODO: 需要从事件参数获取修饰键状态，而不是 Keyboard.Modifiers
                    // if (keyModifiers.HasFlag(KeyModifiers.Control))
                    // {
                    //     // Ctrl+点击：切换选择
                    //     _selectionManager?.ToggleNoteSelection(note);
                    // }
                    // else if (keyModifiers.HasFlag(KeyModifiers.Shift))
                    // {
                    //     // Shift+点击：添加到选择
                    //     _selectionManager?.AddNoteToSelection(note);
                    // }
                    // else
                    {
                        // 普通点击：开始拖拽或调整大小
                        if (IsNearNoteEdge(e.GetPosition(this), note))
                        {
                            StartResize(note);
                        }
                        else
                        {
                            StartDrag(note);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 处理音符点击错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理空白区域点击
        /// </summary>
        private void HandleEmptyClick(PointerPressedEventArgs e)
        {
            try
            {
                // 不要在这里处理任何逻辑！
                // 框选逻辑已经在 EditorCommandsViewModel 中处理
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 处理空白点击错误: {ex.Message}");
            }
        }

        /// <summary>
       
        /// <summary>
        /// 处理悬停
        /// </summary>
        private void HandleHover(Point position)
        {
            try
            {
                var visibleNotes = GetVisibleNotesOptimized();
                var hoveredNote = FindNoteAtPosition(position, visibleNotes);

                // 更新悬停状态
                if (_viewModel != null)
                {
                    // TODO: NoteViewModel 需要添加 IsHovered 属性
                    // foreach (var note in _viewModel.Notes)
                    // {
                    //     note.IsHovered = note == hoveredNote;
                    // }
                }

                // 更新光标
                if (hoveredNote != null)
                {
                    if (IsNearNoteEdge(position, hoveredNote))
                    {
                        Cursor = new Cursor(StandardCursorType.SizeWestEast);
                    }
                    else
                    {
                        Cursor = new Cursor(StandardCursorType.Hand);
                    }
                }
                else
                {
                    Cursor = new Cursor(StandardCursorType.Arrow);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 处理悬停错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 开始拖拽
        /// </summary>
        private void StartDrag(NoteViewModel note)
        {
            try
            {
                // 记录原始选中状态
                bool wasOriginallySelected = _selectionManager?.IsNoteSelected(note) ?? false;

                if (_selectionManager != null && !wasOriginallySelected)
                {
                    _selectionManager.SelectNote(note);
                }

                _dragManager?.StartDrag((_selectionManager?.SelectedNotes ?? new List<NoteViewModel>()).ToList(), wasOriginallySelected);
                _isDragging = true;

                Debug.WriteLine($"[NoteEditingLayer] 开始拖拽音符: {note.Id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 开始拖拽错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 结束拖拽
        /// </summary>
        private void EndDrag()
        {
            try
            {
                if (_dragManager != null)
                {
                    _dragManager.EndDrag();
                }

                _isDragging = false;
                InvalidateVisual();

                Debug.WriteLine("[NoteEditingLayer] 拖拽结束");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 结束拖拽错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 开始调整大小
        /// </summary>
        private void StartResize(NoteViewModel note)
        {
            try
            {
                if (_selectionManager != null && !_selectionManager.IsNoteSelected(note))
                {
                    _selectionManager.SelectNote(note);
                }

                _resizeManager?.StartResize((_selectionManager?.SelectedNotes ?? new List<NoteViewModel>()).ToList());
                _isResizing = true;

                Debug.WriteLine($"[NoteEditingLayer] 开始调整大小: {note.Id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 开始调整大小错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 结束调整大小
        /// </summary>
        private void EndResize()
        {
            try
            {
                if (_resizeManager != null)
                {
                    _resizeManager.EndResize();
                }

                _isResizing = false;
                InvalidateVisual();

                Debug.WriteLine("[NoteEditingLayer] 调整大小结束");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 结束调整大小错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 判断是否在音符边缘
        /// </summary>
        private bool IsNearNoteEdge(Point position, NoteViewModel note)
        {
            try
            {
                if (_noteRectCache.TryGetValue(note, out var rect))
                {
                    const double edgeThreshold = 5; // 5像素阈值
                    return Math.Abs(position.X - rect.Right) < edgeThreshold;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 判断音符边缘错误: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 刷新渲染
        /// </summary>
        public void RefreshRender()
        {
            if (_isDisposed) return;

            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateVisibleNotesCacheOptimized();
                    InvalidateVisual();
                }, DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 刷新渲染错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取性能统计信息
        /// </summary>
        public Dictionary<string, object> GetPerformanceStats()
        {
            var stats = new Dictionary<string, object>
            {
                ["VisibleNotesCount"] = _visibleNotesCount,
                ["CacheHitCount"] = _cacheHitCount,
                ["CacheMissCount"] = _cacheMissCount,
                ["CacheHitRate"] = GetCacheHitRate(),
                ["AverageRenderTime"] = _averageRenderTime,
                ["LastRenderTime"] = _lastRenderTime,
                ["IsDragging"] = _isDragging,
                ["IsResizing"] = _isResizing,
                ["SpatialIndexEnabled"] = _spatialIndex != null,
                ["CacheSystemEnabled"] = _cacheSystem != null,
                ["GpuComputeEnabled"] = _gpuCompute != null
            };

            // 添加渲染器统计
            if (_noteRenderer != null)
            {
                var rendererStats = _noteRenderer.GetPerformanceStats();
                foreach (var stat in rendererStats)
                {
                    stats[$"Renderer_{stat.Key}"] = stat.Value;
                }
            }

            // 添加性能监控器统计
            var monitorStats = _performanceMonitor.GetAllStatistics();
            foreach (var stat in monitorStats)
            {
                stats[$"Monitor_{stat.Key}"] = stat.Value;
            }

            return stats;
        }

        /// <summary>
        /// 获取缓存命中率
        /// </summary>
        private double GetCacheHitRate()
        {
            var total = _cacheHitCount + _cacheMissCount;
            return total > 0 ? (double)_cacheHitCount / total : 0.0;
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void ClearAllCaches()
        {
            if (_isDisposed) return;

            try
            {
                _noteRectCache.Clear();
                _noteCache.Clear();
                _brushCache.Clear();
                _penCache.Clear();

                if (_cacheSystem != null)
                {
                    // TODO: MultiLevelCacheSystem 需要实现 ClearAll 方法
                    // _cacheSystem.ClearAll();
                }

                if (_noteRenderer != null)
                {
                    _noteRenderer.ClearAllCaches();
                }

                // 重新初始化画笔缓存
                InitializeBrushCache();

                // 重置统计
                _cacheHitCount = 0;
                _cacheMissCount = 0;

                Debug.WriteLine("[NoteEditingLayer] 所有缓存已清除");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 清除缓存错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 清除可见音符缓存
        /// 在缩放变化时调用，确保音符状态正确刷新
        /// </summary>
        public void ClearVisibleNotesCache()
        {
            if (_isDisposed) return;

            try
            {
                _noteRectCache.Clear();
                
                if (_cacheSystem != null)
                {
                    _cacheSystem.ClearAllCache();
                }

                Debug.WriteLine("[NoteEditingLayer] 可见音符缓存已清除");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NoteEditingLayer] 清除可见音符缓存错误: {ex.Message}");
            }
        }

        #endregion

        #region 资源释放

        /// <summary>
        /// 析构函数
        /// </summary>
        ~NoteEditingLayer()
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
                    // 取消事件订阅
                    if (_viewModel != null)
                    {
                        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                        _viewModel.CurrentTrackNotes.CollectionChanged -= OnNotesCollectionChanged;
                    }

                    // 释放管理器
                    _selectionManager?.Dispose();
                    // TODO: 需要为 NoteDragManager 和 NoteResizeManager 实现 IDisposable
                    // _dragManager?.Dispose();
                    // _resizeManager?.Dispose();

                    // 释放渲染器
                    _noteRenderer?.Dispose();

                    // 释放优化组件
                    // TODO: 需要为 QuadTreeSpatialIndex 和 MultiLevelCacheSystem 实现 IDisposable
                    // _spatialIndex?.Dispose();
                    // _cacheSystem?.Dispose();
                    _gpuCompute?.Dispose();
                    _performanceMonitor?.Dispose();

                    // 清除缓存
                    ClearAllCaches();

                    _isDisposed = true;

                    Debug.WriteLine("[NoteEditingLayer] 资源已释放");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[NoteEditingLayer] 资源释放错误: {ex.Message}");
                }
            }
        }

        #endregion
    }

    #region 管理器类

    /// <summary>
    /// 音符选择管理器
    /// </summary>
    public class NoteSelectionManager : IDisposable
    {
        private readonly List<NoteViewModel> _selectedNotes = new();

        public IReadOnlyList<NoteViewModel> SelectedNotes => _selectedNotes;

        public void SelectNote(NoteViewModel note)
        {
            ClearSelection();
            AddNoteToSelection(note);
        }

        public void AddNoteToSelection(NoteViewModel note)
        {
            if (!_selectedNotes.Contains(note))
            {
                _selectedNotes.Add(note);
                note.IsSelected = true;
            }
        }

        public void ToggleNoteSelection(NoteViewModel note)
        {
            if (_selectedNotes.Contains(note))
            {
                RemoveNoteFromSelection(note);
            }
            else
            {
                AddNoteToSelection(note);
            }
        }

        public void RemoveNoteFromSelection(NoteViewModel note)
        {
            _selectedNotes.Remove(note);
            note.IsSelected = false;
        }

        public void ClearSelection()
        {
            foreach (var note in _selectedNotes)
            {
                note.IsSelected = false;
            }
            _selectedNotes.Clear();
        }

        public bool IsNoteSelected(NoteViewModel note)
        {
            return _selectedNotes.Contains(note);
        }

        public void Dispose()
        {
            ClearSelection();
        }
    }

    /// <summary>
    /// 音符拖拽管理器
    /// </summary>
    public class NoteDragManager
    {
        private List<NoteViewModel> _selectedNotes = new();
        private Dictionary<NoteViewModel, bool> _originalSelectionStates = new();
        private Point _dragStartPoint;
        private Vector _dragOffset;

        public IReadOnlyList<NoteViewModel> SelectedNotes => _selectedNotes;
        public Vector DragOffset => _dragOffset;

        public void StartDrag(List<NoteViewModel> selectedNotes, bool? singleNoteOriginalState = null)
        {
            _selectedNotes = new List<NoteViewModel>(selectedNotes);
            _originalSelectionStates.Clear();
            
            // 记录每个音符的原始选中状态
            foreach (var note in _selectedNotes)
            {
                if (singleNoteOriginalState.HasValue && _selectedNotes.Count == 1)
                {
                    // 对于单个音符，使用传入的原始状态
                    _originalSelectionStates[note] = singleNoteOriginalState.Value;
                }
                else
                {
                    // 对于多个音符，使用当前状态
                    _originalSelectionStates[note] = note.IsSelected;
                }
            }
            
            _dragStartPoint = new Point();
            _dragOffset = new Vector();
        }

        public void UpdateDrag(Vector delta)
        {
            _dragOffset += delta;
        }

        public void EndDrag()
        {
            // 恢复每个音符的原始选中状态
            foreach (var kvp in _originalSelectionStates)
            {
                kvp.Key.IsSelected = kvp.Value;
            }

            _selectedNotes.Clear();
            _originalSelectionStates.Clear();
            _dragOffset = new Vector();
        }
    }

    /// <summary>
    /// 音符调整大小管理器
    /// </summary>
    public class NoteResizeManager
    {
        private List<NoteViewModel> _selectedNotes = new();
        private double _resizeDelta;

        public IReadOnlyList<NoteViewModel> SelectedNotes => _selectedNotes;
        public double ResizeDelta => _resizeDelta;

        public void StartResize(List<NoteViewModel> selectedNotes)
        {
            _selectedNotes = new List<NoteViewModel>(selectedNotes);
            _resizeDelta = 0;
        }

        public void UpdateResize(double delta)
        {
            _resizeDelta += delta;
        }

        public void EndResize()
        {
            // 应用调整大小结果
            foreach (var note in _selectedNotes)
            {
                // 这里应该更新音符的实际持续时间
                // note.Duration += ...
            }

            _selectedNotes.Clear();
            _resizeDelta = 0;
        }
    }

    #endregion
}