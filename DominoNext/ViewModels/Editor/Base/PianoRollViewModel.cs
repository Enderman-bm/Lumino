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
using DominoNext.ViewModels.Editor.Models;
using DominoNext.ViewModels.Editor.Components;

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
        #endregion

        #region 核心组件 - 组件化架构
        public PianoRollConfiguration Configuration { get; }
        public PianoRollViewport Viewport { get; }
        public PianoRollCalculations Calculations { get; }
        public PianoRollCoordinates Coordinates { get; }
        public PianoRollCommands Commands { get; }
        #endregion

        #region 核心模块
        public NoteDragModule DragModule { get; }
        public NoteResizeModule ResizeModule { get; }
        public NoteCreationModule CreationModule { get; }
        public NoteSelectionModule SelectionModule { get; }
        public NotePreviewModule PreviewModule { get; }
        public VelocityEditingModule VelocityEditingModule { get; }
        #endregion

        #region 状态管理
        public DragState DragState { get; }
        public ResizeState ResizeState { get; }
        public SelectionState SelectionState { get; }
        #endregion

        #region 音轨相关属性
        [ObservableProperty]
        private int _currentTrackIndex = 0;
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
        public double Zoom => Configuration.Zoom;
        public double VerticalZoom => Configuration.VerticalZoom;
        public double TimelinePosition => Viewport.TimelinePosition;
        
        public double ZoomSliderValue 
        {
            get => Configuration.ZoomSliderValue;
            set => Configuration.ZoomSliderValue = value;
        }
        
        public double VerticalZoomSliderValue 
        {
            get => Configuration.VerticalZoomSliderValue;
            set => Configuration.VerticalZoomSliderValue = value;
        }
        
        public EditorTool CurrentTool => Configuration.CurrentTool;
        public MusicalFraction GridQuantization => Configuration.GridQuantization;
        public MusicalFraction UserDefinedNoteDuration => Configuration.UserDefinedNoteDuration;
        public bool IsEventViewVisible => Configuration.IsEventViewVisible;

        // 动态滚动相关属性
        public double CurrentScrollOffset => Viewport.CurrentScrollOffset;
        public double VerticalScrollOffset => Viewport.VerticalScrollOffset;
        public double ViewportWidth => Viewport.ViewportWidth;
        public double ViewportHeight => Viewport.ViewportHeight;
        public double MaxScrollExtent => Viewport.MaxScrollExtent;
        public double VerticalViewportSize => Viewport.VerticalViewportSize;

        // UI相关属性
        public bool IsNoteDurationDropDownOpen => Configuration.IsNoteDurationDropDownOpen;
        public string CustomFractionInput => Configuration.CustomFractionInput;

        [ObservableProperty] private EditorCommandsViewModel _editorCommands;
        #endregion

        #region 集合
        public ObservableCollection<NoteViewModel> Notes { get; } = new();
        
        /// <summary>
        /// 当前音轨的音符集合（只读，自动过滤）
        /// </summary>
        public ObservableCollection<NoteViewModel> CurrentTrackNotes { get; } = new();
        
        public ObservableCollection<NoteDurationOption> NoteDurationOptions => Configuration.NoteDurationOptions;
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
        public string CurrentNoteDurationText => Configuration.CurrentNoteDurationText;
        public string CurrentNoteTimeValueText => Configuration.CurrentNoteTimeValueText;
        public double TotalHeight => Calculations.TotalHeight;
        
        // 有效滚动范围
        public double EffectiveScrollableHeight => Viewport.GetEffectiveScrollableHeight(TotalHeight, Configuration.IsEventViewVisible);
        
        // 实际渲染高度
        public double ActualRenderHeight => Viewport.GetActualRenderHeight(Configuration.IsEventViewVisible);
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
        #endregion

        #region 构造函数
        /// <summary>
        /// 设计时构造函数 - 仅用于XAML设计器
        /// 注意：这个构造函数仅用于设计时，生产环境应使用依赖注入
        /// </summary>
        public PianoRollViewModel() : this(CreateDesignTimeCoordinateService()) { }

        /// <summary>
        /// 创建设计时使用的坐标服务
        /// </summary>
        private static ICoordinateService CreateDesignTimeCoordinateService()
        {
            // 仅用于设计时，避免在生产环境中调用
            return new DominoNext.Services.Implementation.CoordinateService();
        }

        public PianoRollViewModel(ICoordinateService? coordinateService)
        {
            // 使用依赖注入原则，避免直接new具体实现类
            if (coordinateService == null)
            {
                throw new ArgumentNullException(nameof(coordinateService), 
                    "PianoRollViewModel需要通过依赖注入容器创建，坐标服务不能为null。请使用IViewModelFactory创建实例。");
            }
            
            _coordinateService = coordinateService;

            // 初始化组件 - 组件化架构
            Configuration = new PianoRollConfiguration();
            Viewport = new PianoRollViewport();
            Calculations = new PianoRollCalculations(Configuration);
            Coordinates = new PianoRollCoordinates(_coordinateService, Calculations, Viewport);
            Commands = new PianoRollCommands(Configuration, Viewport);

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

            // 设置模块引用
            DragModule.SetPianoRollViewModel(this);
            ResizeModule.SetPianoRollViewModel(this);
            CreationModule.SetPianoRollViewModel(this);
            SelectionModule.SetPianoRollViewModel(this);
            PreviewModule.SetPianoRollViewModel(this);
            VelocityEditingModule.SetPianoRollViewModel(this);

            // 简化初始化命令
            _editorCommands = new EditorCommandsViewModel(_coordinateService);
            _editorCommands.SetPianoRollViewModel(this);

            // 订阅事件
            SubscribeToEvents();
            
            // 监听Notes集合变化，自动更新滚动范围
            Notes.CollectionChanged += OnNotesCollectionChanged;
            
            // 监听当前音轨变化，更新当前音轨音符集合
            PropertyChanged += OnCurrentTrackIndexChanged;
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
            
            // 命令组件事件
            Commands.SelectAllRequested += () => SelectionModule.SelectAll(CurrentTrackNotes);
            Commands.ConfigurationChanged += InvalidateVisual;
            Commands.ViewportChanged += InvalidateVisual;
        }

        private void OnConfigurationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 将配置变更传播到主ViewModel的属性通知
            switch (e.PropertyName)
            {
                case nameof(Configuration.Zoom):
                    OnPropertyChanged(nameof(Zoom));
                    OnPropertyChanged(nameof(BaseQuarterNoteWidth));
                    OnPropertyChanged(nameof(TimeToPixelScale));
                    OnPropertyChanged(nameof(MeasureWidth));
                    OnPropertyChanged(nameof(BeatWidth));
                    OnPropertyChanged(nameof(EighthNoteWidth));
                    OnPropertyChanged(nameof(SixteenthNoteWidth));
                    UpdateMaxScrollExtent();
                    InvalidateNoteCache();
                    break;
                case nameof(Configuration.VerticalZoom):
                    OnPropertyChanged(nameof(VerticalZoom));
                    OnPropertyChanged(nameof(KeyHeight));
                    OnPropertyChanged(nameof(TotalHeight));
                    OnPropertyChanged(nameof(EffectiveScrollableHeight));
                    OnPropertyChanged(nameof(ActualRenderHeight));
                    InvalidateNoteCache();
                    break;
                case nameof(Configuration.IsEventViewVisible):
                    OnPropertyChanged(nameof(IsEventViewVisible));
                    OnPropertyChanged(nameof(EffectiveScrollableHeight));
                    OnPropertyChanged(nameof(ActualRenderHeight));
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
                case nameof(Configuration.ZoomSliderValue):
                    OnPropertyChanged(nameof(ZoomSliderValue));
                    break;
                case nameof(Configuration.VerticalZoomSliderValue):
                    OnPropertyChanged(nameof(VerticalZoomSliderValue));
                    break;
                // 其他配置属性的处理...
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
            }
            
            UpdateMaxScrollExtent();
        }

        private void InvalidateNoteCache()
        {
            foreach (var note in Notes)
            {
                note.InvalidateCache();
            }
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
        #endregion

        #region 工具方法
        public MusicalFraction SnapToGrid(MusicalFraction time) => Configuration.SnapToGrid(time);
        public double SnapToGridTime(double timeValue) => Configuration.SnapToGridTime(timeValue);
        public bool IsBlackKey(int midiNote) => Calculations.IsBlackKey(midiNote);
        public string GetNoteName(int midiNote) => Calculations.GetNoteName(midiNote);

        public void AddNote(int pitch, MusicalFraction startPosition, MusicalFraction? duration = null, int velocity = 100)
        {
            var quantizedStartPosition = SnapToGrid(startPosition);
            var noteDuration = duration ?? Configuration.UserDefinedNoteDuration;

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
            var noteDuration = duration < 0 ? Configuration.UserDefinedNoteDuration : MusicalFraction.FromDouble(duration);
            AddNote(pitch, startPosition, noteDuration, velocity);
        }
        #endregion

        #region 命令 - 简化命令实现
        [RelayCommand] private void SelectPencilTool() => Configuration.CurrentTool = EditorTool.Pencil;
        [RelayCommand] private void SelectSelectionTool() => Configuration.CurrentTool = EditorTool.Select;
        [RelayCommand] private void SelectEraserTool() => Configuration.CurrentTool = EditorTool.Eraser;
        [RelayCommand] private void SelectCutTool() => Configuration.CurrentTool = EditorTool.Cut;
        
        [RelayCommand] 
        private void ToggleNoteDurationDropDown() 
        {
            Configuration.IsNoteDurationDropDownOpen = !Configuration.IsNoteDurationDropDownOpen;
        }
        
        [RelayCommand] 
        private void SelectNoteDuration(NoteDurationOption option)
        {
            if (option == null) return;
            Configuration.GridQuantization = option.Duration;
            Configuration.IsNoteDurationDropDownOpen = false;
        }
        
        [RelayCommand] 
        private void ApplyCustomFraction()
        {
            if (Configuration.TryParseCustomFraction(Configuration.CustomFractionInput, out var fraction))
            {
                Configuration.GridQuantization = fraction;
                Configuration.IsNoteDurationDropDownOpen = false;
            }
        }
        
        [RelayCommand] private void SelectAll() => SelectionModule.SelectAll(CurrentTrackNotes);
        
        [RelayCommand] 
        private void ToggleEventView()
        {
            Configuration.IsEventViewVisible = !Configuration.IsEventViewVisible;
            Viewport.UpdateViewportForEventView(Configuration.IsEventViewVisible);
        }
        #endregion

        #region MIDI文件时长管理
        /// <summary>
        /// 设置MIDI文件的总时长（以四分音符为单位）
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
        }
        #endregion

        #region 视口管理方法
        public void SetViewportSize(double width, double height)
        {
            Viewport.SetViewportSize(width, height);
            UpdateMaxScrollExtent();
        }

        public void UpdateMaxScrollExtent()
        {
            var noteEndPositions = Notes.Select(n => n.StartPosition + n.Duration);
            
            // 传递MIDI文件时长信息给计算组件
            var contentWidth = Calculations.CalculateContentWidth(noteEndPositions, HasMidiFileDuration ? MidiFileDuration : null);
            Viewport.UpdateMaxScrollExtent(contentWidth);
        }

        public void ValidateAndClampScrollOffsets()
        {
            Viewport.ValidateAndClampScrollOffsets();
        }

        public double GetEffectiveVerticalScrollMax()
        {
            return Viewport.GetEffectiveVerticalScrollMax(TotalHeight);
        }
        #endregion

        #region 清理
        public void Cleanup()
        {
            DragModule.EndDrag();
            ResizeModule.EndResize();
            CreationModule.CancelCreating();
            SelectionModule.ClearSelection(CurrentTrackNotes);
            PreviewModule.ClearPreview();
            VelocityEditingModule.EndEditing();
            Notes.Clear();
        }
        #endregion

        #region 公共设置方法 - 用于外部组件更新状态
        /// <summary>
        /// 设置当前工具
        /// </summary>
        public void SetCurrentTool(EditorTool tool)
        {
            Configuration.CurrentTool = tool;
        }

        /// <summary>
        /// 设置用户定义的音符时长
        /// </summary>
        public void SetUserDefinedNoteDuration(MusicalFraction duration)
        {
            Configuration.UserDefinedNoteDuration = duration;
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
            Configuration.ZoomSliderValue = value;
        }

        /// <summary>
        /// 设置垂直缩放滑块值
        /// </summary>
        public void SetVerticalZoomSliderValue(double value)
        {
            Configuration.VerticalZoomSliderValue = value;
        }

        /// <summary>
        /// 获取有效的垂直滚动最大值（带参数重载）
        /// </summary>
        public double GetEffectiveVerticalScrollMax(double actualRenderHeight)
        {
            return Viewport.GetEffectiveScrollableHeight(TotalHeight, Configuration.IsEventViewVisible);
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
    }
}