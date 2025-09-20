using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models.Music;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor.Commands;
using Lumino.ViewModels.Editor.Modules;
using Lumino.ViewModels.Editor.State;
using Lumino.ViewModels.Editor.Components;
using Lumino.ViewModels.Editor.Enums;

namespace Lumino.ViewModels.Editor
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
        private readonly INoteEditingService _noteEditingService;
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
        /// 自定义滚动条管理器 - 已废弃，使用Viewport替代
        /// </summary>
        // public PianoRollScrollBarManager ScrollBarManager { get; }

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
        public bool HasMidiFileDuration => MidiFileDuration > 0;
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

        public MusicalFraction GridQuantization => Toolbar.GridQuantization;
        public bool IsEventViewVisible => Toolbar.IsEventViewVisible;

        // 动态滚动相关属性
        // UI相关属性
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
        
        /// <summary>
        /// 批量操作标志 - 用于优化批量操作期间的UI更新
        /// </summary>
        private bool _isBatchOperationInProgress = false;
        #endregion

        #region 集合
        public ObservableCollection<NoteDurationOption> NoteDurationOptions => Toolbar.NoteDurationOptions;
        #endregion

        #region 计算属性 - 委托给计算组件
        public double BaseQuarterNoteWidth => Calculations.BaseQuarterNoteWidth;
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

        /// <summary>
        /// 有效歌曲长度（用于滚动条计算）
        /// </summary>
        public double EffectiveSongLength => Calculations.GetEffectiveSongLength(Notes.Select(n => n.StartPosition + n.Duration), MidiFileDuration);

        /// <summary>
        /// 滚动条总长度（包含额外空间）
        /// </summary>
        public double ScrollbarTotalLength => EffectiveSongLength + ViewportWidth * 0.5; // 添加50%视口宽度的额外空间

        /// <summary>
        /// 当前视口比例（0-1）
        /// </summary>
        public double CurrentViewportRatio => Calculations.CalculateViewportRatio(ViewportWidth, Notes.Select(n => n.StartPosition + n.Duration), MidiFileDuration);

        /// <summary>
        /// 当前滚动位置比例（0-1）
        /// </summary>
        public double CurrentScrollPositionRatio => Calculations.CalculateScrollPositionRatio(CurrentScrollOffset, ViewportWidth, Notes.Select(n => n.StartPosition + n.Duration), MidiFileDuration);
        public string GetScrollDiagnostics()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== 滚动系统诊断信息 ===");
            sb.AppendLine($"有效歌曲长度: {EffectiveSongLength:F2}");
            sb.AppendLine($"滚动条总长度: {ScrollbarTotalLength:F2}");
            sb.AppendLine($"当前滚动偏移: {CurrentScrollOffset:F2}");
            sb.AppendLine($"视口宽度: {ViewportWidth:F2}");
            sb.AppendLine($"MIDI文件时长: {MidiFileDuration:F2}");
            sb.AppendLine($"音符数量: {Notes.Count}");
            return sb.ToString();
        }

        /// <summary>
        /// 批量添加音符到钢琴卷帘
        /// </summary>
        public void AddNotesInBatch(IEnumerable<NoteViewModel> noteViewModels)
        {
            if (noteViewModels == null) return;
            
            foreach (var note in noteViewModels)
            {
                if (note != null)
                {
                    Notes.Add(note);
                }
            }
            
            // 通知属性变化，更新UI
            OnPropertyChanged(nameof(Notes));
            OnPropertyChanged(nameof(EffectiveSongLength));
            OnPropertyChanged(nameof(ScrollbarTotalLength));
        }

        /// <summary>
        /// 设置缩放滑块值
        /// </summary>
        public void SetZoomSliderValue(double value)
        {
            ZoomSliderValue = value;
            OnPropertyChanged(nameof(ZoomSliderValue));
        }

        /// <summary>
        /// 获取所有音符
        /// </summary>
        public IEnumerable<NoteViewModel> GetAllNotes()
        {
            return Notes;
        }

        /// <summary>
        /// 设置垂直缩放滑块值
        /// </summary>
        public void SetVerticalZoomSliderValue(double value)
        {
            VerticalZoomSliderValue = value;
            OnPropertyChanged(nameof(VerticalZoomSliderValue));
        }
        #endregion

        #region 代理属性 - 简化访问
        // 拖拽相关
        public NoteViewModel? DraggingNote => DragState.DraggingNote;
        public List<NoteViewModel> DraggingNotes => DragState.DraggingNotes;

        // 调整大小相关
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
        public List<CurvePoint> CurrentEventCurvePoints => EventCurveDrawingModule?.CurrentCurvePoints ?? new List<CurvePoint>();
        #endregion

        #region 构造函数
        /// <summary>
        /// 设计时构造函数 - 仅用于XAML设计器
        /// 注意：这个构造函数仅用于设计时，生产环境应使用依赖注入
        /// </summary>
        public PianoRollViewModel() : this(CreateDesignTimeCoordinateService(), CreateDesignTimeEventCurveCalculationService(), CreateDesignTimeNoteEditingService()) { }

        /// <summary>
        /// 创建设计时使用的坐标服务
        /// </summary>
        private static ICoordinateService CreateDesignTimeCoordinateService()
        {
            // 仅用于设计时，避免在生产环境中调用
            return new Lumino.Services.Implementation.CoordinateService();
        }

        /// <summary>
        /// 创建设计时使用的事件曲线计算服务
        /// </summary>
        private static IEventCurveCalculationService CreateDesignTimeEventCurveCalculationService()
        {
            return new Lumino.Services.Implementation.EventCurveCalculationService();
        }

        /// <summary>
        /// 创建设计时使用的音符编辑服务
        /// </summary>
        private static INoteEditingService CreateDesignTimeNoteEditingService()
        {
            return new Lumino.Services.Implementation.NoteEditingService(null, null);
        }

        public PianoRollViewModel(ICoordinateService? coordinateService, IEventCurveCalculationService? eventCurveCalculationService = null, INoteEditingService? noteEditingService = null)
        {
            // 使用依赖注入原则，避免直接new具体实现类
            if (coordinateService == null)
            {
                throw new ArgumentNullException(nameof(coordinateService),
                    "PianoRollViewModel需要通过依赖注入容器创建，坐标服务不能为null。请使用IViewModelFactory创建实例。");
            }

            _coordinateService = coordinateService;
            _eventCurveCalculationService = eventCurveCalculationService ?? CreateDesignTimeEventCurveCalculationService();
            _noteEditingService = noteEditingService ?? throw new ArgumentNullException(nameof(noteEditingService), "音符编辑服务不能为null");

            // 初始化组件 - 组件化架构
            Configuration = new PianoRollConfiguration();
            Viewport = new PianoRollViewport();
            ZoomManager = new PianoRollZoomManager();
            Calculations = new PianoRollCalculations(ZoomManager);
            Coordinates = new PianoRollCoordinates(_coordinateService, Calculations, Viewport);
            Commands = new PianoRollCommands(Configuration, Viewport);
            
            // 初始化滚动条管理器 - 移除，使用Viewport替代

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

            // 设置滚动条管理器引用 - 移除，使用Viewport替代

            // 简化初始化命令
            _editorCommands = new EditorCommandsViewModel(_coordinateService);
            _editorCommands.SetPianoRollViewModel(this);

            // 初始化事件处理
            InitializeEventHandlers();

            // 监听Notes集合变化，自动更新滚动范围
            Notes.CollectionChanged += OnNotesCollectionChanged;

            // 监听当前音轨变化，更新当前音轨音符集合
            PropertyChanged += OnCurrentTrackIndexChanged;

            // 监听事件类型变化
            PropertyChanged += OnEventTypePropertyChanged;

            // 初始化撤销重做管理器（已在UndoRedo部分文件中定义）
            // InitializeUndoRedoManager();
        }
        #endregion

        #region 事件处理初始化
        partial void InitializeEventHandlers();
        #endregion

        #region 撤销重做管理器初始化
        // 撤销重做管理器的初始化已在UndoRedo部分文件中处理
        // private void InitializeUndoRedoManager() 方法已存在
        #endregion
    }
}