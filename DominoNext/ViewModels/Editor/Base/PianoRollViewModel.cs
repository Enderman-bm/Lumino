using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DominoNext.Models.Music;
using DominoNext.Services.Interfaces;
using DominoNext.ViewModels.Editor.Commands;
using DominoNext.ViewModels.Editor.Modules;
using DominoNext.ViewModels.Editor.State;
using DominoNext.ViewModels.Editor.Components;
using DominoNext.ViewModels.Editor.Enums;
using EnderDebugger;

namespace DominoNext.ViewModels.Editor
{
    /// <summary>
    /// 重构后的钢琴卷帘ViewModel - 符合MVVM最佳实践和单一职责原则
    /// 主要负责协调各个组件和模块，业务逻辑委托给专门的组件处理
    /// </summary>
    public partial class PianoRollViewModel : ViewModelBase
    {
        #region 服务依赖
        private readonly ICoordinateService _coordinateService;
        private readonly IEventCurveCalculationService _eventCurveCalculationService;
        private readonly EnderLogger _logger;
        #endregion

        #region 核心组件 - 组件化架构
        public PianoRollConfiguration Configuration { get; }
        public PianoRollViewport Viewport { get; }
        public PianoRollCalculations Calculations { get; }
        public PianoRollCoordinates Coordinates { get; }
        public PianoRollCommands Commands { get; }
        
        /// <summary>
        /// 独立的缩放管理器
        /// </summary>
        public PianoRollZoomManager ZoomManager { get; }
        
        /// <summary>
        /// 自定义滚动条管理器
        /// </summary>
        public PianoRollScrollBarManager ScrollBarManager { get; }

        /// <summary>
        /// 工具栏ViewModel - 独立的工具栏管理
        /// </summary>
        public ToolbarViewModel Toolbar { get; }
        #endregion

        #region 核心模块
        public NoteDragModule DragModule { get; }
        public NoteResizeModule ResizeModule { get; }
        public NoteCreationModule CreationModule { get; }
        public NoteSelectionModule SelectionModule { get; }
        public NotePreviewModule PreviewModule { get; }
        public VelocityEditingModule VelocityEditingModule { get; }
        public EventCurveDrawingModule EventCurveDrawingModule { get; }
        #endregion

        #region 状态管理
        public DragState DragState { get; }
        public ResizeState ResizeState { get; }
        public SelectionState SelectionState { get; }
        #endregion

        #region 音轨相关属性
        [ObservableProperty]
        private int _currentTrackIndex = 0;

        /// <summary>
        /// 当前轨道的ViewModel引用
        /// </summary>
        [ObservableProperty]
        private TrackViewModel? _currentTrack;

        /// <summary>
        /// 当前选择的事件类型
        /// </summary>
        [ObservableProperty]
        private EventType _currentEventType = EventType.Velocity;

        /// <summary>
        /// 当前选择的CC控制器号（0-127），仅在事件类型为ControlChange时使用
        /// </summary>
        [ObservableProperty]
        private int _currentCCNumber = 1;

        /// <summary>
        /// 是否显示事件类型选择器
        /// </summary>
        [ObservableProperty]
        private bool _isEventTypeSelectorOpen = false;

        /// <summary>
        /// 当前Tempo值（BPM）
        /// </summary>
        [ObservableProperty]
        private int _currentTempo = 120;

        /// <summary>
        /// 判断当前是否为Conductor轨道
        /// </summary>
        public bool IsCurrentTrackConductor => CurrentTrack?.IsConductorTrack ?? false;
        #endregion

        #region MIDI文件时长相关属性
        /// <summary>
        /// MIDI文件的总时长（以四分音符为单位）
        /// </summary>
        [ObservableProperty]
        private double _midiFileDuration = 0.0;

        /// <summary>
        /// 是否已设置MIDI文件时长
        /// </summary>
        public bool HasMidiFileDuration => _midiFileDuration > 0;
        #endregion

        #region 基本属性（委托给组件）
        public double Zoom => ZoomManager.Zoom;
        public double VerticalZoom => ZoomManager.VerticalZoom;
        public double TimelinePosition => Viewport.TimelinePosition;

        public double ZoomSliderValue
        {
            get => ZoomManager.ZoomSliderValue;
            set => ZoomManager.ZoomSliderValue = value;
        }

        public double VerticalZoomSliderValue
        {
            get => ZoomManager.VerticalZoomSliderValue;
            set => ZoomManager.VerticalZoomSliderValue = value;
        }

        public EditorTool CurrentTool => Toolbar.CurrentTool;
        public MusicalFraction GridQuantization => Toolbar.GridQuantization;
        public MusicalFraction UserDefinedNoteDuration => Toolbar.UserDefinedNoteDuration;
        public bool IsEventViewVisible => Toolbar.IsEventViewVisible;

        // 动态滚动相关属性
        public double CurrentScrollOffset => Viewport.CurrentScrollOffset;
        public double VerticalScrollOffset => Viewport.VerticalScrollOffset;
        public double ViewportWidth => Viewport.ViewportWidth;
        public double ViewportHeight => Viewport.ViewportHeight;
        public double MaxScrollExtent => Viewport.MaxScrollExtent;
        public double VerticalViewportSize => Viewport.VerticalViewportSize;

        // UI相关属性
        public bool IsNoteDurationDropDownOpen => Toolbar.IsNoteDurationDropDownOpen;
        public string CustomFractionInput => Toolbar.CustomFractionInput;

        /// <summary>
        /// 获取当前事件类型的显示名称
        /// </summary>
        public string CurrentEventTypeText => CurrentEventType switch
        {
            EventType.Velocity => "力度",
            EventType.PitchBend => "弯音",
            EventType.ControlChange => $"CC{CurrentCCNumber}",
            EventType.Tempo => "速度",
            _ => "未知"
        };

        /// <summary>
        /// 获取当前事件类型的数值范围描述
        /// </summary>
        public string CurrentEventValueRange => _eventCurveCalculationService?.GetValueRangeDescription(CurrentEventType, CurrentCCNumber) ?? "0-127";

        /// <summary>
        /// 获取当前事件类型的完整描述
        /// </summary>
        public string CurrentEventDescription => $"{CurrentEventTypeText} ({CurrentEventValueRange})";

        // 曲线绘制相关代理属性
        public bool IsDrawingCurve => EventCurveDrawingModule?.IsDrawing ?? false;
        public List<CurvePoint> CurrentCurvePoints => EventCurveDrawingModule?.CurrentCurvePoints ?? new List<CurvePoint>();

        [ObservableProperty] private EditorCommandsViewModel? _editorCommands;

        #region 歌曲长度和滚动条相关属性
        /// <summary>
        /// 获取当前歌曲的有效长度（四分音符单位）
        /// </summary>
        public double EffectiveSongLength
        {
            get
            {
                var noteEndPositions = GetAllNotes().Select(n => n.StartPosition + n.Duration);
                return Calculations.CalculateEffectiveSongLength(
                    noteEndPositions,
                    HasMidiFileDuration ? MidiFileDuration : null
                );
            }
        }

        /// <summary>
        /// 获取滚动条总长度（像素单位）
        /// </summary>
        public double ScrollbarTotalLength
        {
            get
            {
                return Calculations.CalculateScrollbarTotalLengthInPixels(EffectiveSongLength);
            }
        }

        /// <summary>
        /// 获取当前视口相对于总歌曲长度的比例
        /// </summary>
        public double CurrentViewportRatio
        {
            get
            {
                var noteEndPositions = GetAllNotes().Select(n => n.StartPosition + n.Duration);
                return Calculations.CalculateViewportRatio(
                    ViewportWidth,
                    noteEndPositions,
                    HasMidiFileDuration ? MidiFileDuration : null
                );
            }
        }

        /// <summary>
        /// 获取当前滚动位置相对于总长度的比例
        /// </summary>
        public double CurrentScrollPositionRatio
        {
            get
            {
                var noteEndPositions = GetAllNotes().Select(n => n.StartPosition + n.Duration);
                return Calculations.CalculateScrollPositionRatio(
                    CurrentScrollOffset,
                    ViewportWidth,
                    noteEndPositions,
                    HasMidiFileDuration ? MidiFileDuration : null
                );
            }
        }

        /// <summary>
        /// 获取滚动条状态的诊断信息
        /// </summary>
        public string ScrollBarDiagnostics => ScrollBarManager?.GetScrollBarDiagnostics() ?? "滚动条管理器未初始化";
        #endregion

        #region 集合
        public ObservableCollection<NoteViewModel> Notes { get; } = new();

        /// <summary>
        /// 当前音轨的音符集合（只读，自动过滤）
        /// </summary>
        public ObservableCollection<NoteViewModel> CurrentTrackNotes { get; } = new();

        public ObservableCollection<NoteDurationOption> NoteDurationOptions => Toolbar.NoteDurationOptions;
        #endregion

        #region 计算属性 - 委托给计算组件
        public double BaseQuarterNoteWidth => Calculations.BaseQuarterNoteWidth;
        public double TimeToPixelScale => Calculations.TimeToPixelScale;
        public double KeyHeight => Calculations.KeyHeight;
        public double MeasureWidth => Calculations.MeasureWidth;
        public double BeatWidth => Calculations.BeatWidth;
        public double EighthNoteWidth => Calculations.EighthNoteWidth;
        public double SixteenthNoteWidth => Calculations.SixteenthNoteWidth;
        public int BeatsPerMeasure => Calculations.BeatsPerMeasure;

        // UI相关计算属性
        public string CurrentNoteDurationText => Toolbar.CurrentNoteDurationText;
        public string CurrentNoteTimeValueText => Toolbar.CurrentNoteTimeValueText;
        public double TotalHeight => Calculations.TotalHeight;

        // 有效滚动范围
        public double EffectiveScrollableHeight => Viewport.GetEffectiveScrollableHeight(TotalHeight, Toolbar.IsEventViewVisible);

        // 实际渲染高度
        public double ActualRenderHeight => Viewport.GetActualRenderHeight(Toolbar.IsEventViewVisible);
        #endregion

        #region 代理属性 - 简化访问
        // 拖拽相关
        public bool IsDragging => DragState.IsDragging;
        public NoteViewModel? DraggingNote => DragState.DraggingNote;
        public List<NoteViewModel> DraggingNotes => DragState.DraggingNotes;

        // 调整大小相关
        public bool IsResizing => ResizeState.IsResizing;
        public ResizeHandle CurrentResizeHandle => ResizeState.CurrentResizeHandle;
        public NoteViewModel? ResizingNote => ResizeState.ResizingNote;
        public List<NoteViewModel> ResizingNotes => ResizeState.ResizingNotes;

        // 创建音符
        public bool IsCreatingNote => CreationModule.IsCreatingNote;
        public NoteViewModel? CreatingNote => CreationModule.CreatingNote;

        // 选择框
        public bool IsSelecting => SelectionState.IsSelecting;
        public Point? SelectionStart => SelectionState.SelectionStart;
        public Point? SelectionEnd => SelectionState.SelectionEnd;

        // 预览音符
        public NoteViewModel? PreviewNote => PreviewModule.PreviewNote;

        // 曲线绘制
        public bool IsDrawingEventCurve => EventCurveDrawingModule?.IsDrawing ?? false;
        public List<CurvePoint> CurrentEventCurvePoints => EventCurveDrawingModule?.CurrentCurvePoints ?? new List<CurvePoint>();
        #endregion

        #region 构造函数
        /// <summary>
        /// 设计时构造函数 - 仅用于XAML设计器
        /// 注意：这个构造函数仅用于设计时，生产环境应使用依赖注入
        /// </summary>
        public PianoRollViewModel() : this(CreateDesignTimeCoordinateService(), CreateDesignTimeEventCurveCalculationService()) { }

        /// <summary>
        /// 创建设计时使用的坐标服务
        /// </summary>
        private static ICoordinateService CreateDesignTimeCoordinateService()
        {
            // 仅用于设计时，避免在生产环境中调用
            return new DominoNext.Services.Implementation.CoordinateService();
        }

        /// <summary>
        /// 创建设计时使用的事件曲线计算服务
        /// </summary>
        private static IEventCurveCalculationService CreateDesignTimeEventCurveCalculationService()
        {
            return new DominoNext.Services.Implementation.EventCurveCalculationService();
        }

        public PianoRollViewModel(ICoordinateService? coordinateService, IEventCurveCalculationService? eventCurveCalculationService = null)
        {
            // 使用依赖注入原则，避免直接new具体实现类
            if (coordinateService == null)
            {
                throw new ArgumentNullException(nameof(coordinateService),
                    "PianoRollViewModel需要通过依赖注入容器创建，坐标服务不能为null。请使用IViewModelFactory创建实例。");
            }

            _coordinateService = coordinateService;
            _eventCurveCalculationService = eventCurveCalculationService ?? CreateDesignTimeEventCurveCalculationService();
            _logger = EnderLogger.Instance;

            // 初始化组件 - 组件化架构
            Configuration = new PianoRollConfiguration();
            Viewport = new PianoRollViewport();
            ZoomManager = new PianoRollZoomManager();
            Calculations = new PianoRollCalculations(ZoomManager);
            Coordinates = new PianoRollCoordinates(_coordinateService, Calculations, Viewport);
            Commands = new PianoRollCommands(Configuration, Viewport);
            
            // 初始化滚动条管理器
            ScrollBarManager = new PianoRollScrollBarManager();

            // 初始化工具栏ViewModel
            Toolbar = new ToolbarViewModel(Configuration);

            // 初始化状态
            DragState = new DragState();
            ResizeState = new ResizeState();
            SelectionState = new SelectionState();

            // 初始化模块
            DragModule = new NoteDragModule(DragState, _coordinateService);
            ResizeModule = new NoteResizeModule(ResizeState, _coordinateService);
            CreationModule = new NoteCreationModule(_coordinateService);
            SelectionModule = new NoteSelectionModule(SelectionState, _coordinateService);
            PreviewModule = new NotePreviewModule(_coordinateService);
            VelocityEditingModule = new VelocityEditingModule(_coordinateService);
            EventCurveDrawingModule = new EventCurveDrawingModule(_eventCurveCalculationService);

            // 设置模块引用
            DragModule.SetPianoRollViewModel(this);
            ResizeModule.SetPianoRollViewModel(this);
            CreationModule.SetPianoRollViewModel(this);
            SelectionModule.SetPianoRollViewModel(this);
            PreviewModule.SetPianoRollViewModel(this);
            VelocityEditingModule.SetPianoRollViewModel(this);
            EventCurveDrawingModule.SetPianoRollViewModel(this);

            // 设置滚动条管理器引用
            ScrollBarManager.SetPianoRollViewModel(this);

            // 简化初始化命令
            _editorCommands = new EditorCommandsViewModel(_coordinateService);
            _editorCommands.SetPianoRollViewModel(this);

            // 订阅事件
            SubscribeToEvents();

            // 监听Notes集合变化，自动更新滚动范围
            Notes.CollectionChanged += OnNotesCollectionChanged;

            // 监听当前音轨变化，更新当前音轨音符集合
            PropertyChanged += OnCurrentTrackIndexChanged;

            // 监听事件类型变化
            PropertyChanged += OnEventTypePropertyChanged;
        }
        #endregion

        #region 事件订阅
        private void SubscribeToEvents()
        {
            // 订阅模块事件
            SubscribeToModuleEvents();

            // 订阅组件事件
            SubscribeToComponentEvents();
        }

        /// <summary>
        /// 处理事件类型相关属性变化
        /// </summary>
        private void OnEventTypePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if ( e.PropertyName == nameof(CurrentEventType))
            {
                OnPropertyChanged(nameof(CurrentEventTypeText));
                OnPropertyChanged(nameof(CurrentEventValueRange));
                OnPropertyChanged(nameof(CurrentEventDescription));
            }
            else if (e.PropertyName == nameof(CurrentCCNumber))
            {
                OnPropertyChanged(nameof(CurrentEventTypeText));
                OnPropertyChanged(nameof(CurrentEventDescription));
            }
        }
        #endregion

        #region 计算模块
        private void SubscribeToModuleEvents()
        {
            // 拖拽模块事件
            DragModule.OnDragUpdated += InvalidateVisual;
            DragModule.OnDragEnded += InvalidateVisual;

            ResizeModule.OnResizeUpdated += InvalidateVisual;
            ResizeModule.OnResizeEnded += InvalidateVisual;

            CreationModule.OnCreationUpdated += InvalidateVisual;
            CreationModule.OnCreationCompleted += OnNoteCreated;

            // 选择模块事件
            SelectionModule.OnSelectionUpdated += InvalidateVisual;

            // 力度编辑模块事件
            VelocityEditingModule.OnVelocityUpdated += InvalidateVisual;

            // 事件曲线绘制模块事件
            EventCurveDrawingModule.OnCurveUpdated += InvalidateVisual;
            EventCurveDrawingModule.OnCurveCompleted += OnCurveDrawingCompleted;
            EventCurveDrawingModule.OnCurveCancelled += InvalidateVisual;

            // 订阅选择状态变更事件
            SelectionState.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(SelectionState.SelectionStart) ||
                    e.PropertyName == nameof(SelectionState.SelectionEnd) ||
                    e.PropertyName == nameof(SelectionState.IsSelecting))
                {
                    OnPropertyChanged(nameof(SelectionStart));
                    OnPropertyChanged(nameof(SelectionEnd));
                    OnPropertyChanged(nameof(IsSelecting));
                    InvalidateVisual();
                }
            };
        }

        private void SubscribeToComponentEvents()
        {
            // 配置变更事件
            Configuration.PropertyChanged += OnConfigurationPropertyChanged;

            // 视口变更事件
            Viewport.PropertyChanged += OnViewportPropertyChanged;
            
            // 缩放管理器变更事件
            ZoomManager.PropertyChanged += OnZoomManagerPropertyChanged;

            // 命令组件事件
            Commands.SelectAllRequested += () => SelectionModule.SelectAll(CurrentTrackNotes);
            Commands.ConfigurationChanged += InvalidateVisual;
            Commands.ViewportChanged += InvalidateVisual;

            // 工具栏事件 - 先暂时注释掉，我们通过Configuration属性变化来处理
            // Toolbar.EventViewToggleRequested += OnEventViewToggleRequested;
            // Toolbar.ToolChanged += OnToolChanged;
            // Toolbar.NoteDurationChanged += OnNoteDurationChanged;
            // Toolbar.GridQuantizationChanged += OnGridQuantizationChanged;
        }

        private void OnConfigurationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 将配置变更传播到主ViewModel的属性通知
            switch (e.PropertyName)
            {
                case nameof(Configuration.IsEventViewVisible):
                    OnPropertyChanged(nameof(IsEventViewVisible));
                    OnPropertyChanged(nameof(EffectiveScrollableHeight));
                    OnPropertyChanged(nameof(ActualRenderHeight));
                    // 事件视图可见性变化时，更新视口设置
                    Viewport.UpdateViewportForEventView(Configuration.IsEventViewVisible);
                    break;
                case nameof(Configuration.CurrentTool):
                    OnPropertyChanged(nameof(CurrentTool));
                    break;
                case nameof(Configuration.GridQuantization):
                    OnPropertyChanged(nameof(GridQuantization));
                    OnPropertyChanged(nameof(CurrentNoteDurationText));
                    break;
                case nameof(Configuration.UserDefinedNoteDuration):
                    OnPropertyChanged(nameof(UserDefinedNoteDuration));
                    OnPropertyChanged(nameof(CurrentNoteTimeValueText));
                    break;
                case nameof(Configuration.IsNoteDurationDropDownOpen):
                    OnPropertyChanged(nameof(IsNoteDurationDropDownOpen));
                    break;
                case nameof(Configuration.CustomFractionInput):
                    OnPropertyChanged(nameof(CustomFractionInput));
                    break;
                    // 其他配置属性的处理...
            }

            InvalidateVisual();
        }

        private void OnZoomManagerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 将缩放管理器变更传播到主ViewModel的属性通知
            switch (e.PropertyName)
            {
                case nameof(ZoomManager.Zoom):
                    // 在缩放变化前保存相对位置
                    var oldRelativePosition = GetRelativeScrollPosition();
                    
                    OnPropertyChanged(nameof(Zoom));
                    OnPropertyChanged(nameof(BaseQuarterNoteWidth));
                    OnPropertyChanged(nameof(TimeToPixelScale));
                    OnPropertyChanged(nameof(MeasureWidth));
                    OnPropertyChanged(nameof(BeatWidth));
                    OnPropertyChanged(nameof(EighthNoteWidth));
                    OnPropertyChanged(nameof(SixteenthNoteWidth));
                    // 缩放变化时必须更新滚动范围
                    UpdateMaxScrollExtent();
                    // 同时更新最大滚动范围的属性通知
                    OnPropertyChanged(nameof(MaxScrollExtent));
                    // 更新歌曲长度相关属性
                    OnPropertyChanged(nameof(EffectiveSongLength));
                    OnPropertyChanged(nameof(ScrollbarTotalLength));
                    OnPropertyChanged(nameof(CurrentViewportRatio));
                    OnPropertyChanged(nameof(CurrentScrollPositionRatio));
                    InvalidateNoteCache();
                    
                    // 在缩放变化后恢复相对位置
                    SetRelativeScrollPosition(oldRelativePosition);
                    break;
                case nameof(ZoomManager.VerticalZoom):
                    // 在垂直缩放变化前保存相对位置
                    var oldVerticalRelativePosition = GetVerticalRelativeScrollPosition();
                    
                    OnPropertyChanged(nameof(VerticalZoom));
                    OnPropertyChanged(nameof(KeyHeight));
                    OnPropertyChanged(nameof(TotalHeight));
                    OnPropertyChanged(nameof(EffectiveScrollableHeight));
                    OnPropertyChanged(nameof(ActualRenderHeight));
                    InvalidateNoteCache();
                    
                    // 在垂直缩放变化后恢复相对位置
                    SetVerticalRelativeScrollPosition(oldVerticalRelativePosition);
                    break;
                case nameof(ZoomManager.ZoomSliderValue):
                    OnPropertyChanged(nameof(ZoomSliderValue));
                    // 滑块值变化也要更新滚动范围
                    UpdateMaxScrollExtent();
                    OnPropertyChanged(nameof(MaxScrollExtent));
                    // 更新歌曲长度相关属性
                    OnPropertyChanged(nameof(EffectiveSongLength));
                    OnPropertyChanged(nameof(ScrollbarTotalLength));
                    OnPropertyChanged(nameof(CurrentViewportRatio));
                    OnPropertyChanged(nameof(CurrentScrollPositionRatio));
                    break;
                case nameof(ZoomManager.VerticalZoomSliderValue):
                    OnPropertyChanged(nameof(VerticalZoomSliderValue));
                    break;
            }

            InvalidateVisual();
        }

        private void OnViewportPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 将视口变更传播到主ViewModel的属性通知
            switch (e.PropertyName)
            {
                case nameof(Viewport.CurrentScrollOffset):
                    OnPropertyChanged(nameof(CurrentScrollOffset));
                    OnPropertyChanged(nameof(CurrentScrollPositionRatio));
                    break;
                case nameof(Viewport.VerticalScrollOffset):
                    OnPropertyChanged(nameof(VerticalScrollOffset));
                    break;
                case nameof(Viewport.ViewportWidth):
                case nameof(Viewport.ViewportHeight):
                    OnPropertyChanged(nameof(ViewportWidth));
                    OnPropertyChanged(nameof(ViewportHeight));
                    OnPropertyChanged(nameof(EffectiveScrollableHeight));
                    OnPropertyChanged(nameof(ActualRenderHeight));
                    // 视口大小变化影响比例计算
                    OnPropertyChanged(nameof(CurrentViewportRatio));
                    OnPropertyChanged(nameof(CurrentScrollPositionRatio));
                    break;
            }

            InvalidateVisual();
        }

        private void InvalidateVisual()
        {
            // 触发UI更新的方法，由View层实现
        }

        private void OnNoteCreated()
        {
            InvalidateVisual();

            // 同步最新创建音符的时值到UI显示
            if (Notes.Count > 0)
            {
                var lastNote = Notes.Last();
                if (!lastNote.Duration.Equals(Configuration.UserDefinedNoteDuration))
                {
                    // 这里需要通过Configuration组件来更新
                    // Configuration.UserDefinedNoteDuration = lastNote.Duration;
                    OnPropertyChanged(nameof(CurrentNoteTimeValueText));
                }
            };

            UpdateMaxScrollExtent();
        }

        private void InvalidateNoteCache()
        {
            foreach (var note in Notes)
            {
                note.InvalidateCache();
            }
        }

        /// <summary>
        /// 处理曲线绘制完成事件
        /// </summary>
        private void OnCurveDrawingCompleted(List<CurvePoint> curvePoints)
        {
            // TODO: 将曲线点转换为MIDI事件并保存到项目中
            System.Diagnostics.Debug.WriteLine($"曲线绘制完成，包含 {curvePoints.Count} 个点，事件类型：{CurrentEventType}");
            
            foreach (var point in curvePoints)
            {
                System.Diagnostics.Debug.WriteLine($"  时间: {point.Time:F1}, 数值: {point.Value}");
            }
            
            InvalidateVisual();
        }
        #endregion

        #region Notes集合变化处理
        /// <summary>
        /// 处理Notes集合变化，自动更新滚动范围
        /// </summary>
        private void OnNotesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // 批量操作期间跳过频繁的UI更新
            if (_isBatchOperationInProgress)
                return;

            // 音符集合发生变化时，自动更新滚动范围以支持自动延长小节功能
            UpdateMaxScrollExtent();

            // 更新当前音轨的音符集合
            UpdateCurrentTrackNotes();

            // 更新歌曲长度相关属性
            OnPropertyChanged(nameof(EffectiveSongLength));
            OnPropertyChanged(nameof(ScrollbarTotalLength));
            OnPropertyChanged(nameof(CurrentViewportRatio));
            OnPropertyChanged(nameof(CurrentScrollPositionRatio));

            // 触发UI更新
            InvalidateVisual();

            // 如果是添加音符且接近当前可 visible区域的末尾，考虑自动滚动
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (NoteViewModel newNote in e.NewItems)
                {
                    CheckAutoScrollForNewNote(newNote);
                }
            }
        }

        /// <summary>
        /// 处理当前音轨索引变化
        /// </summary>
        private void OnCurrentTrackIndexChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CurrentTrackIndex))
            {
                UpdateCurrentTrackNotes();
                
                // 如果切换到Conductor轨道，自动切换到Tempo事件类型
                if (IsCurrentTrackConductor && CurrentEventType != EventType.Tempo)
                {
                    CurrentEventType = EventType.Tempo;
                }
                // 如果从Conductor轨道切换到普通轨道，切换到Velocity事件类型
                else if (!IsCurrentTrackConductor && CurrentEventType == EventType.Tempo)
                {
                    CurrentEventType = EventType.Velocity;
                }
                
                OnPropertyChanged(nameof(IsCurrentTrackConductor));
                
                // 确保在切换音轨后滚动条连接正常
                EnsureScrollBarManagerConnection();
            }
        }

        /// <summary>
        /// 更新当前音轨的音符集合
        /// </summary>
        private void UpdateCurrentTrackNotes()
        {
            CurrentTrackNotes.Clear();

            var currentTrackNotes = Notes.Where(note => note.TrackIndex == CurrentTrackIndex);
            foreach (var note in currentTrackNotes)
            {
                CurrentTrackNotes.Add(note);
            }
        }

        /// <summary>
        /// 设置当前音轨索引
        /// </summary>
        public void SetCurrentTrackIndex(int trackIndex)
        {
            if (CurrentTrackIndex != trackIndex)
            {
                CurrentTrackIndex = trackIndex;
                
                // 确保在切换音轨后滚动条连接正常
                EnsureScrollBarManagerConnection();
            }
        }

        /// <summary>
        /// 检查新添加的音符是否需要自动滚动
        /// </summary>
        private void CheckAutoScrollForNewNote(NoteViewModel note)
        {
            // 计算音符结束位置的像素坐标
            var noteEndTime = note.StartPosition + note.Duration;
            var noteEndPixels = noteEndTime.ToDouble() * BaseQuarterNoteWidth;

            // 获取当前可见区域的右边界
            var visibleEndPixels = CurrentScrollOffset + ViewportWidth;

            // 如果音符超出当前可见区域右边界，且距离不太远，则自动滚动
            var scrollThreshold = ViewportWidth * 0.1; // 10%的视口宽度作为阈值
            if (noteEndPixels > visibleEndPixels && (noteEndPixels - visibleEndPixels) <= scrollThreshold)
            {
                // 计算需要滚动的距离，让音符完全可见
                var targetScrollOffset = noteEndPixels - ViewportWidth * 0.8; // 留20%边距
                targetScrollOffset = Math.Max(0, Math.Min(targetScrollOffset, MaxScrollExtent - ViewportWidth));

                // 平滑滚动到目标位置
                Viewport.SetHorizontalScrollOffset(targetScrollOffset);
            }
        }

        /// <summary>
        /// 设置当前音轨的ViewModel
        /// </summary>
        public void SetCurrentTrack(TrackViewModel? track)
        {
            if (CurrentTrack != track)
            {
                CurrentTrack = track;
                OnPropertyChanged(nameof(IsCurrentTrackConductor));
                
                // 根据轨道类型自动设置事件类型
                if (IsCurrentTrackConductor && CurrentEventType != EventType.Tempo)
                {
                    CurrentEventType = EventType.Tempo;
                }
                else if (!IsCurrentTrackConductor && CurrentEventType == EventType.Tempo)
                {
                    CurrentEventType = EventType.Velocity;
                }
            }
        }
        #endregion

        #region 坐标转换委托方法
        public int GetPitchFromY(double y) => Coordinates.GetPitchFromY(y);
        public double GetTimeFromX(double x) => Coordinates.GetTimeFromX(x);
        public Point GetPositionFromNote(NoteViewModel note) => Coordinates.GetPositionFromNote(note);
        public Rect GetNoteRect(NoteViewModel note) => Coordinates.GetNoteRect(note);

        public int GetPitchFromScreenY(double screenY) => Coordinates.GetPitchFromScreenY(screenY);
        public double GetTimeFromScreenX(double screenX) => Coordinates.GetTimeFromScreenX(screenX);
        public Point GetScreenPositionFromNote(NoteViewModel note) => Coordinates.GetScreenPositionFromNote(note);
        public Rect GetScreenNoteRect(NoteViewModel note) => Coordinates.GetScreenNoteRect(note);
        #endregion

        #region 公共方法委托给模块
        public void StartCreatingNote(Point position = default) => CreationModule.StartCreating(position);
        public void UpdateCreatingNote(Point position = default) => CreationModule.UpdateCreating(position);
        public void FinishCreatingNote() => CreationModule.FinishCreating();
        public void CancelCreatingNote() => CreationModule.CancelCreating();

        public void StartNoteDrag(NoteViewModel note, Point startPoint) => DragModule.StartDrag(note, startPoint);
        public void UpdateNoteDrag(Point currentPoint, Point startPoint) => DragModule.UpdateDrag(currentPoint);
        public void EndNoteDrag() => DragModule.EndDrag();

        public void StartNoteResize(Point position, NoteViewModel note, ResizeHandle handle) => ResizeModule.StartResize(position, note, handle);
        public void UpdateNoteResize(Point currentPosition) => ResizeModule.UpdateResize(currentPosition);
        public void EndNoteResize() => ResizeModule.EndResize();

        public ResizeHandle GetResizeHandleAtPosition(Point position, NoteViewModel note) => ResizeModule.GetResizeHandleAtPosition(position, note);

        public NoteViewModel? GetNoteAtPosition(Point position) => SelectionModule.GetNoteAtPosition(position, CurrentTrackNotes, TimeToPixelScale, KeyHeight);

        // 事件曲线绘制方法
        public void StartDrawingEventCurve(Point startPoint, double canvasHeight)
        {
            EventCurveDrawingModule.StartDrawing(startPoint, CurrentEventType, CurrentCCNumber, canvasHeight);
        }

        public void UpdateDrawingEventCurve(Point currentPoint)
        {
            EventCurveDrawingModule.UpdateDrawing(currentPoint);
        }

        public void FinishDrawingEventCurve()
        {
            EventCurveDrawingModule.FinishDrawing();
        }

        public void CancelDrawingEventCurve()
        {
            EventCurveDrawingModule.CancelDrawing();
        }

        public int GetEventValueAtPosition(Point position, double canvasHeight)
        {
            return _eventCurveCalculationService.YToValue(position.Y, canvasHeight, CurrentEventType, CurrentCCNumber);
        }

        public double GetYPositionForEventValue(int value, double canvasHeight)
        {
            return _eventCurveCalculationService.ValueToY(value, canvasHeight, CurrentEventType, CurrentCCNumber);
        }
        #endregion

        #region 工具方法
        public MusicalFraction SnapToGrid(MusicalFraction time) => Toolbar.SnapToGrid(time);
        public double SnapToGridTime(double timeValue) => Toolbar.SnapToGridTime(timeValue);
        public bool IsBlackKey(int midiNote) => Calculations.IsBlackKey(midiNote);
        public string GetNoteName(int midiNote) => Calculations.GetNoteName(midiNote);

        public void AddNote(int pitch, MusicalFraction startPosition, MusicalFraction? duration = null, int velocity = 100)
        {
            var quantizedStartPosition = SnapToGrid(startPosition);
            var noteDuration = duration ?? Toolbar.UserDefinedNoteDuration;

            var note = new NoteViewModel
            {
                Pitch = pitch,
                StartPosition = quantizedStartPosition,
                Duration = noteDuration,
                Velocity = velocity,
                TrackIndex = CurrentTrackIndex // 设置为当前音轨
            };
            Notes.Add(note);

            UpdateMaxScrollExtent();
        }

        public void AddNote(int pitch, double startTime, double duration = -1, int velocity = 100)
        {
            var startPosition = MusicalFraction.FromDouble(startTime);
            var noteDuration = duration < 0 ? Toolbar.UserDefinedNoteDuration : MusicalFraction.FromDouble(duration);
            AddNote(pitch, startPosition, noteDuration, velocity);
        }
        #endregion

        #region 命令 - 简化命令实现
        [RelayCommand] private void SelectPencilTool() => Toolbar.SelectPencilTool();
        [RelayCommand] private void SelectSelectionTool() => Toolbar.SelectSelectionTool();
        [RelayCommand] private void SelectEraserTool() => Toolbar.SelectEraserTool();
        [RelayCommand] private void SelectCutTool() => Toolbar.SelectCutTool();

        [RelayCommand]
        private void ToggleNoteDurationDropDown() => Toolbar.ToggleNoteDurationDropDown();

        [RelayCommand]
        private void SelectNoteDuration(NoteDurationOption option) => Toolbar.SelectNoteDuration(option);

        [RelayCommand]
        private void ApplyCustomFraction() => Toolbar.ApplyCustomFraction();

        [RelayCommand] private void SelectAll() => SelectionModule.SelectAll(CurrentTrackNotes);

        [RelayCommand]
        private void ToggleEventView() => Toolbar.ToggleEventView();
        // 事件类型选择相关命令
        [RelayCommand]
        private void ToggleEventTypeSelector()
        {
            IsEventTypeSelectorOpen = !IsEventTypeSelectorOpen;
        }

        [RelayCommand]
        private void SelectEventType(EventType eventType)
        {
            CurrentEventType = eventType;
            IsEventTypeSelectorOpen = false;
        }

        [RelayCommand]
        private void SetCCNumber(int ccNumber)
        {
            if (ccNumber >= 0 && ccNumber <= 127)
            {
                CurrentCCNumber = ccNumber;
            }
        }

        /// <summary>
        /// 验证并设置CC号（支持字符串输入）
        /// </summary>
        [RelayCommand]
        private void ValidateAndSetCCNumber(string ccNumberText)
        {
            if (int.TryParse(ccNumberText, out int ccNumber))
            {
                ccNumber = Math.Max(0, Math.Min(127, ccNumber)); // 限制在0-127范围内
                CurrentCCNumber = ccNumber;
            }
        }
        #endregion

        #region MIDI文件时长管理
        /// <summary>
        /// 设置MIDI文件的总时长（被四分音符数表示）
        /// </summary>
        /// <param name="durationInQuarterNotes">时长（四分音符单位）</param>
        public void SetMidiFileDuration(double durationInQuarterNotes)
        {
            if (durationInQuarterNotes < 0)
            {
                throw new ArgumentException("MIDI文件时长不能为负数", nameof(durationInQuarterNotes));
            }

            MidiFileDuration = durationInQuarterNotes;

            // 设置时长后立即更新滚动范围
            UpdateMaxScrollExtent();

            OnPropertyChanged(nameof(HasMidiFileDuration));
            
            // 更新歌曲长度相关属性
            OnPropertyChanged(nameof(EffectiveSongLength));
            OnPropertyChanged(nameof(ScrollbarTotalLength));
            OnPropertyChanged(nameof(CurrentViewportRatio));
            OnPropertyChanged(nameof(CurrentScrollPositionRatio));
        }

        /// <summary>
        /// 设置MIDI文件的总时长（以秒为单位）
        /// </summary>
        /// <param name="durationInSeconds">时长（秒）</param>
        /// <param name="microsecondsPerQuarterNote">每四分音符的微秒数（用于转换）</param>
        public void SetMidiFileDurationFromSeconds(double durationInSeconds, int microsecondsPerQuarterNote = 500000)
        {
            if (durationInSeconds < 0)
            {
                throw new ArgumentException("MIDI文件时长不能为负数", nameof(durationInSeconds));
            }

            // 将秒转换为四分音符单位
            // 每四分音符的秒数 = 微秒数 / 1,000,000
            double secondsPerQuarterNote = microsecondsPerQuarterNote / 1_000_000.0;
            double durationInQuarterNotes = durationInSeconds / secondsPerQuarterNote;

            SetMidiFileDuration(durationInQuarterNotes);
        }

        /// <summary>
        /// 清除MIDI文件时长设置
        /// </summary>
        public void ClearMidiFileDuration()
        {
            MidiFileDuration = 0.0;
            UpdateMaxScrollExtent();
            OnPropertyChanged(nameof(HasMidiFileDuration));
            
            // 更新歌曲长度相关属性
            OnPropertyChanged(nameof(EffectiveSongLength));
            OnPropertyChanged(nameof(ScrollbarTotalLength));
            OnPropertyChanged(nameof(CurrentViewportRatio));
            OnPropertyChanged(nameof(CurrentScrollPositionRatio));
        }
        #endregion

        #region 视口管理方法
        public void SetViewportSize(double width, double height)
        {
            Viewport.SetViewportSize(width, height);
            UpdateMaxScrollExtent();
            
            // 更新滚动条轨道长度
            ScrollBarManager.SetScrollBarTrackLengths(width, height);
        }

        public void UpdateMaxScrollExtent()
        {
            var noteEndPositions = Notes.Select(n => n.StartPosition + n.Duration);

            // 传递MIDI文件时长信息给计算组件
            var contentWidth = Calculations.CalculateContentWidth(noteEndPositions, HasMidiFileDuration ? MidiFileDuration : null);
            Viewport.UpdateMaxScrollExtent(contentWidth);

            // 添加调试信息
            System.Diagnostics.Debug.WriteLine($"[PianoRoll] 更新滚动范围: 内容宽度={contentWidth:F1}, 最大滚动={MaxScrollExtent:F1}, 恢复到上次滚动={Zoom:F2}");
        }

        public void ValidateAndClampScrollOffsets()
        {
            Viewport.ValidateAndClampScrollOffsets();
        }

        public double GetEffectiveVerticalScrollMax()
        {
            return Viewport.GetEffectiveVerticalScrollMax(TotalHeight);
        }

        /// <summary>
        /// 获取滚动范围的诊断信息
        /// </summary>
        public string GetScrollDiagnostics()
        {
            var noteCount = Notes.Count;
            var maxNoteEnd = Notes.Any() ? Notes.Max(n => (n.StartPosition + n.Duration).ToDouble()) : 0;
            var contentWidth = Calculations.CalculateContentWidth(Notes.Select(n => n.StartPosition + n.Duration), HasMidiFileDuration ? MidiFileDuration : null);
            var scrollableRange = Viewport.GetHorizontalScrollableRange();
            var scrollPercentage = Viewport.GetScrollPercentage();

            return $"音符数量: {noteCount}\n" +
                   $"最远音符位置: {maxNoteEnd:F2} 四分音符\n" +
                   $"MIDI文件时长: {(HasMidiFileDuration ? MidiFileDuration.ToString("F2") : "未设置")}\n" +
                   $"内容宽度: {contentWidth:F1} 像素\n" +
                   $"最大滚动范围: {MaxScrollExtent:F1} 像素\n" +
                   $"可滚动范围: {scrollableRange:F1} 像素\n" +
                   $"当前滚动位置: {CurrentScrollOffset:F1} 像素 ({scrollPercentage:P1})\n" +
                   $"视口宽度: {ViewportWidth:F1} 像素\n" +
                   $"当前缩放: {Zoom:F2}x\n" +
                   $"基础四分音符宽度: {BaseQuarterNoteWidth:F1} 像素";
        }

        /// <summary>
        /// 强制重新计算并更新所有滚动相关的属性
        /// </summary>
        public void ForceRefreshScrollSystem()
        {
            // 强制重新计算内容宽度
            UpdateMaxScrollExtent();

            // 验证滚动位置
            ValidateAndClampScrollOffsets();
            
            // 强制更新滚动条
            ScrollBarManager.ForceUpdateScrollBars();

            // 通知所有相关属性变化
            OnPropertyChanged(nameof(MaxScrollExtent));
            OnPropertyChanged(nameof(CurrentScrollOffset));
            OnPropertyChanged(nameof(ViewportWidth));

            System.Diagnostics.Debug.WriteLine($"[PianoRoll] 强制刷新滚动系统完成");
            System.Diagnostics.Debug.WriteLine(GetScrollDiagnostics());
        }
        #endregion

        #region 清理
        public void Cleanup()
        {
            // 保存ScrollBarManager的连接状态，因为在MIDI导入后需要保持连接
            var scrollBarManagerWasConnected = ScrollBarManager != null;
            
            DragModule.EndDrag();
            ResizeModule.EndResize();
            CreationModule.CancelCreating();
            SelectionModule.ClearSelection(CurrentTrackNotes);
            PreviewModule.ClearPreview();
            VelocityEditingModule.EndEditing();
            EventCurveDrawingModule.CancelDrawing();
            
            // 不要完全清理ScrollBarManager，因为这会断开与UI的连接
            // ScrollBarManager.Cleanup();
            
            Toolbar.Cleanup();
            Notes.Clear();
            
            // 如果ScrollBarManager之前是连接的，重新建立连接
            if (scrollBarManagerWasConnected)
            {
                EnsureScrollBarManagerConnection();
            }
        }
        
        /// <summary>
        /// 确保ScrollBarManager与PianoRollViewModel的连接
        /// </summary>
        private void EnsureScrollBarManagerConnection()
        {
            if (ScrollBarManager != null)
            {
                // 重新建立连接，确保滚动条功能正常
                ScrollBarManager.SetPianoRollViewModel(this);
                
                // 强制更新滚动条状态
                ScrollBarManager.ForceUpdateScrollBars();
                
                System.Diagnostics.Debug.WriteLine("[PianoRoll] 重新建立ScrollBarManager连接");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[PianoRoll] 警告：ScrollBarManager为null，无法建立连接");
            }
        }
        
        /// <summary>
        /// 轻量级清理方法，用于MIDI导入等场景，不断开ScrollBarManager连接
        /// </summary>
        public void ClearContent()
        {
            DragModule.EndDrag();
            ResizeModule.EndResize();
            CreationModule.CancelCreating();
            SelectionModule.ClearSelection(CurrentTrackNotes);
            PreviewModule.ClearPreview();
            VelocityEditingModule.EndEditing();
            EventCurveDrawingModule.CancelDrawing();
            
            // 清空音符但保持ScrollBarManager连接
            Notes.Clear();
            
            // 重置MIDI文件时长
            ClearMidiFileDuration();
        }
        #endregion

        #region 公共设置方法 - 用于外部组件更新状态
        /// <summary>
        /// 设置当前工具
        /// </summary>
        public void SetCurrentTool(EditorTool tool)
        {
            Toolbar.SetCurrentTool(tool);
        }

        /// <summary>
        /// 设置用户定义的音符时长
        /// </summary>
        public void SetUserDefinedNoteDuration(MusicalFraction duration)
        {
            Toolbar.SetUserDefinedNoteDuration(duration);
        }

        /// <summary>
        /// 设置水平滚动偏移量
        /// </summary>
        public void SetCurrentScrollOffset(double offset)
        {
            Viewport.SetHorizontalScrollOffset(offset);
        }

        /// <summary>
        /// 设置垂直滚动偏移量
        /// </summary>
        public void SetVerticalScrollOffset(double offset)
        {
            Viewport.SetVerticalScrollOffset(offset, TotalHeight);
        }

        /// <summary>
        /// 设置缩放滑块值
        /// </summary>
        public void SetZoomSliderValue(double value)
        {
            ZoomManager.SetZoomSliderValue(value);
        }

        /// <summary>
        /// 设置垂直缩放滑块值
        /// </summary>
        public void SetVerticalZoomSliderValue(double value)
        {
            ZoomManager.SetVerticalZoomSliderValue(value);
        }

        /// <summary>
        /// 获取有效的垂直滚动最大值（带参数重载）
        /// </summary>
        public double GetEffectiveVerticalScrollMax(double actualRenderHeight)
        {
            return Viewport.GetEffectiveScrollableHeight(TotalHeight, Toolbar.IsEventViewVisible);
        }
        #endregion

        #region 相对滚动位置管理
        /// <summary>
        /// 获取当前水平滚动相对位置（0.0-1.0）
        /// </summary>
        public double GetRelativeScrollPosition()
        {
            var maxScroll = MaxScrollExtent;
            if (maxScroll <= 0)
                return 0.0;
            return Math.Max(0.0, Math.Min(1.0, CurrentScrollOffset / maxScroll));
        }

        /// <summary>
        /// 设置水平滚动相对位置（0.0-1.0）
        /// </summary>
        public void SetRelativeScrollPosition(double relativePosition)
        {
            var maxScroll = MaxScrollExtent;
            if (maxScroll > 0)
            {
                var newOffset = Math.Max(0.0, Math.Min(maxScroll, relativePosition * maxScroll));
                SetCurrentScrollOffset(newOffset);
            }
        }

        /// <summary>
        /// 获取当前垂直滚动相对位置（0.0-1.0）
        /// </summary>
        public double GetVerticalRelativeScrollPosition()
        {
            var maxVerticalScroll = GetEffectiveVerticalScrollMax();
            if (maxVerticalScroll <= 0)
                return 0.0;
            return Math.Max(0.0, Math.Min(1.0, VerticalScrollOffset / maxVerticalScroll));
        }

        /// <summary>
        /// 设置垂直滚动相对位置（0.0-1.0）
        /// </summary>
        public void SetVerticalRelativeScrollPosition(double relativePosition)
        {
            var maxVerticalScroll = GetEffectiveVerticalScrollMax();
            if (maxVerticalScroll > 0)
            {
                var newOffset = Math.Max(0.0, Math.Min(maxVerticalScroll, relativePosition * maxVerticalScroll));
                SetVerticalScrollOffset(newOffset);
            }
        }
        #endregion

        #region 批量操作优化
        private bool _isBatchOperationInProgress = false;

        /// <summary>
        /// 开始批量操作，暂停集合变更通知以提升性能
        /// </summary>
        public void BeginBatchOperation()
        {
            _isBatchOperationInProgress = true;
        }

        /// <summary>
        /// 结束批量操作，恢复集合变更通知并手动触发更新
        /// </summary>
        public void EndBatchOperation()
        {
            _isBatchOperationInProgress = false;

            // 批量操作结束后，手动触发一次更新
            UpdateMaxScrollExtent();
            UpdateCurrentTrackNotes();
            InvalidateVisual();
        }

        /// <summary>
        /// 批量添加音符，避免频繁的UI更新
        /// </summary>
        /// <param name="noteViewModels">要添加的音符ViewModel集合</param>
        public void AddNotesInBatch(IEnumerable<NoteViewModel> noteViewModels)
        {
            BeginBatchOperation();

            try
            {
                foreach (var noteViewModel in noteViewModels)
                {
                    Notes.Add(noteViewModel);
                }
            }
            finally
            {
                EndBatchOperation();
            }
        }

        /// <summary>
        /// 获取所有音符
        /// </summary>
        /// <returns>所有音符的集合</returns>
        public IEnumerable<NoteViewModel> GetAllNotes()
        {
            return Notes;
        }
        #endregion

        #region 项目初始化方法
        /// <summary>
        /// 初始化新项目，添加默认的Tempo事件
        /// </summary>
        public void InitializeNewProject()
        {
            // 在时值0位置添加默认的BPM120事件
            AddDefaultTempoEvent();
        }

        /// <summary>
        /// 添加默认的Tempo事件（BPM120在时值0位置）
        /// </summary>
        private void AddDefaultTempoEvent()
        {
            // TODO: 实际应该将Tempo事件添加到项目的事件列表中
            // 这里先设置当前Tempo值作为显示
            CurrentTempo = 120;
            
            _logger.Info("PianoRollViewModel", "已初始化默认Tempo事件：BPM120 在时值0位置");
        }

        /// <summary>
        /// 设置当前Tempo值（用于显示和编辑）
        /// </summary>
        public void SetCurrentTempo(int bpm)
        {
            if (bpm >= 20 && bpm <= 300)
            {
                CurrentTempo = bpm;
                Toolbar.SetCurrentTempo(bpm);
            }
        }
        #endregion
    }
    #endregion
}