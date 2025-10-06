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
using EnderDebugger;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel核心字段和属性定义
    /// 包含所有核心组件、模块、状态和基本属性的定义
    /// </summary>
    public partial class PianoRollViewModel : ViewModelBase
    {
        #region 服务依赖
        /// <summary>
        /// 坐标服务 - 用于坐标转换和计算
        /// </summary>
        private readonly ICoordinateService _coordinateService;

        /// <summary>
        /// 事件曲线计算服务 - 用于事件曲线值的计算
        /// </summary>
        private readonly IEventCurveCalculationService _eventCurveCalculationService;

        /// <summary>
        /// MIDI转换服务 - 用于MIDI数据转换
        /// </summary>
        private readonly IMidiConversionService _midiConversionService;

        /// <summary>
        /// 撤销重做服务 - 用于操作历史管理
        /// </summary>
        private readonly IUndoRedoService _undoRedoService;

        /// <summary>
        /// 日志记录器 - 用于调试和错误记录
        /// </summary>
        private readonly EnderLogger _logger;
        #endregion

        #region 核心组件 - 组件化架构
        /// <summary>
        /// 配置组件 - 管理钢琴卷帘的所有配置项
        /// </summary>
        private PianoRollConfiguration _configuration;
        public PianoRollConfiguration Configuration => _configuration;

        /// <summary>
        /// 视口组件 - 管理可视区域和滚动
        /// </summary>
        private PianoRollViewport _viewport;
        public PianoRollViewport Viewport => _viewport;

        /// <summary>
        /// 计算组件 - 处理所有数学计算和转换
        /// </summary>
        private PianoRollCalculations _calculations;
        public PianoRollCalculations Calculations => _calculations;

        /// <summary>
        /// 坐标组件 - 处理坐标转换逻辑
        /// </summary>
        private PianoRollCoordinates _coordinates;
        public PianoRollCoordinates Coordinates => _coordinates;

        /// <summary>
        /// 命令组件 - 管理钢琴卷帘的命令
        /// </summary>
        private PianoRollCommands _commands;
        public PianoRollCommands Commands => _commands;

        /// <summary>
        /// 独立的缩放管理器 - 处理水平和垂直缩放
        /// </summary>
        private PianoRollZoomManager _zoomManager;
        public PianoRollZoomManager ZoomManager => _zoomManager;

        /// <summary>
        /// 自定义滚动条管理器 - 处理滚动条的交互和状态
        /// </summary>
        private PianoRollScrollBarManager _scrollBarManager;
        public PianoRollScrollBarManager ScrollBarManager => _scrollBarManager;

        /// <summary>
        /// 工具栏ViewModel - 独立的工具栏管理
        /// </summary>
        private ToolbarViewModel _toolbar;
        public ToolbarViewModel Toolbar => _toolbar;
        #endregion

        #region 核心模块
        /// <summary>
        /// 音符拖拽模块 - 处理音符的拖拽操作
        /// </summary>
        private NoteDragModule _dragModule;
        public NoteDragModule DragModule => _dragModule;

        /// <summary>
        /// 音符调整大小模块 - 处理音符的调整大小操作
        /// </summary>
        private NoteResizeModule _resizeModule;
        public NoteResizeModule ResizeModule => _resizeModule;

        /// <summary>
        /// 音符创建模块 - 处理新音符的创建
        /// </summary>
        private NoteCreationModule _creationModule;
        public NoteCreationModule CreationModule => _creationModule;

        /// <summary>
        /// 音符选择模块 - 处理音符的选择逻辑
        /// </summary>
        private NoteSelectionModule _selectionModule;
        public NoteSelectionModule SelectionModule => _selectionModule;

        /// <summary>
        /// 音符预览模块 - 处理音符的预览显示
        /// </summary>
        private NotePreviewModule _previewModule;
        public NotePreviewModule PreviewModule => _previewModule;

        /// <summary>
        /// 力度编辑模块 - 处理音符力度的编辑
        /// </summary>
        private VelocityEditingModule _velocityEditingModule;
        public VelocityEditingModule VelocityEditingModule => _velocityEditingModule;

        /// <summary>
        /// 事件曲线绘制模块 - 处理事件曲线的绘制
        /// </summary>
        private EventCurveDrawingModule _eventCurveDrawingModule;
        public EventCurveDrawingModule EventCurveDrawingModule => _eventCurveDrawingModule;
        #endregion

        #region 状态管理
        /// <summary>
        /// 拖拽状态 - 跟踪当前的拖拽操作状态
        /// </summary>
        private DragState _dragState;
        public DragState DragState => _dragState;

        /// <summary>
        /// 调整大小状态 - 跟踪当前的调整大小操作状态
        /// </summary>
        private ResizeState _resizeState;
        public ResizeState ResizeState => _resizeState;

        /// <summary>
        /// 选择状态 - 跟踪当前的选择操作状态
        /// </summary>
        private SelectionState _selectionState;
        public SelectionState SelectionState => _selectionState;
        #endregion

        #region 音轨相关属性
        /// <summary>
        /// 当前轨道的索引号（从0开始）
        /// </summary>
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

        /// <summary>
        /// 是否启用洋葱皮模式 - 半透明显示所有轨道的音符
        /// </summary>
        [ObservableProperty]
        private bool _isOnionSkinEnabled = false;

        /// <summary>
        /// 洋葱皮透明度 (0.0-1.0) - 非当前轨道音符的透明度
        /// </summary>
        [ObservableProperty]
        private double _onionSkinOpacity = 0.3;

        /// <summary>
        /// 演奏指示线位置（以四分音符为单位）
        /// </summary>
        [ObservableProperty]
        private double _playbackPosition = 0.0;
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

        #region 集合
        /// <summary>
        /// 所有音符的集合 - 主集合，包含所有轨道的音符
        /// </summary>
        public ObservableCollection<NoteViewModel> Notes { get; } = new();

        /// <summary>
        /// 当前音轨的音符集合（只读，自动过滤）
        /// </summary>
        public ObservableCollection<NoteViewModel> CurrentTrackNotes { get; } = new();

        /// <summary>
        /// 音符时长选项集合 - 委托给工具栏
        /// </summary>
        public ObservableCollection<NoteDurationOption> NoteDurationOptions => Toolbar.NoteDurationOptions;
        #endregion

        #region 剪贴板数据
        /// <summary>
        /// 剪贴板中的音符数据
        /// </summary>
        private List<NoteClipboardData>? _clipboardNotes;
        #endregion

        #region 命令
        /// <summary>
        /// 编辑器命令ViewModel - 包含所有编辑器级别的命令
        /// </summary>
        [ObservableProperty]
        private EditorCommandsViewModel? _editorCommands;
        #endregion
    }

    /// <summary>
    /// 音符剪贴板数据类
    /// </summary>
    public class NoteClipboardData
    {
        /// <summary>
        /// 开始时间位置
        /// </summary>
        public MusicalFraction StartTime { get; set; }

        /// <summary>
        /// 持续时间
        /// </summary>
        public MusicalFraction Duration { get; set; }

        /// <summary>
        /// 音高
        /// </summary>
        public int Pitch { get; set; }

        /// <summary>
        /// 力度
        /// </summary>
        public int Velocity { get; set; }
    }
}