using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace DominoNext.ViewModels.Editor
{
    /// <summary>
    /// 重构后的钢琴卷帘ViewModel - 符合MVVM最佳实践
    /// 主要负责协调各个模块，业务逻辑委托给专门的模块处理
    /// </summary>
    public partial class PianoRollViewModel : ViewModelBase
    {
        #region 服务依赖
        private readonly ICoordinateService _coordinateService;
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

        #region 基本属性
        [ObservableProperty] private double _zoom = 1.0;
        [ObservableProperty] private double _verticalZoom = 1.0;
        [ObservableProperty] private double _timelinePosition;
        [ObservableProperty] private double _zoomSliderValue = 50.0;
        [ObservableProperty] private double _verticalZoomSliderValue = 50.0;
        [ObservableProperty] private EditorTool _currentTool = EditorTool.Pencil;
        [ObservableProperty] private MusicalFraction _gridQuantization = MusicalFraction.SixteenthNote;
        [ObservableProperty] private MusicalFraction _userDefinedNoteDuration = MusicalFraction.QuarterNote;
        [ObservableProperty] private EditorCommandsViewModel _editorCommands;
        [ObservableProperty] private bool _isEventViewVisible = true; // 控制事件视图的可见性

        // 动态滚动相关属性
        [ObservableProperty] private double _currentScrollOffset = 0.0; // 当前水平滚动偏移量
        [ObservableProperty] private double _verticalScrollOffset = 0.0; // 当前垂直滚动偏移量
        [ObservableProperty] private double _viewportWidth = 800.0; // 视口宽度，默认800px
        [ObservableProperty] private double _viewportHeight = 400.0; // 视口高度，默认400px
        [ObservableProperty] private double _maxScrollExtent = 5000.0; // 最大滚动范围
        [ObservableProperty] private double _verticalViewportSize = 400.0; // 垂直视口大小

        // UI相关属性
        [ObservableProperty] private bool _isNoteDurationDropDownOpen = false;
        [ObservableProperty] private string _customFractionInput = "1/4";
        #endregion

        #region 集合
        public ObservableCollection<NoteViewModel> Notes { get; } = new();
        public ObservableCollection<NoteDurationOption> NoteDurationOptions { get; } = new(); // 网格量化选项
        #endregion

        #region 计算属性 - 性能优化版本
        public int TicksPerBeat => MusicalFraction.QUARTER_NOTE_TICKS;
        
        // 直接计算时间到像素的缩放比例，避免重复的 PixelsPerTick * Zoom 计算
        public double TimeToPixelScale => Zoom * 100.0 / TicksPerBeat;
        
        public double KeyHeight => 12.0 * VerticalZoom;
        
        // 使用TimeToPixelScale简化计算
        public double MeasureWidth => (4 * TicksPerBeat) * TimeToPixelScale;
        public double BeatWidth => TicksPerBeat * TimeToPixelScale;

        // 音符宽度计算 - 简化版本
        public double EighthNoteWidth => (TicksPerBeat / 2) * TimeToPixelScale;
        public double SixteenthNoteWidth => (TicksPerBeat / 4) * TimeToPixelScale;

        // 新增：小节相关
        public int BeatsPerMeasure => 4; // 标准4/4拍

        // UI相关计算属性
        public string CurrentNoteDurationText => GridQuantization.ToString(); // 显示当前网格量化而不是音符时值
        public string CurrentNoteTimeValueText => UserDefinedNoteDuration.ToString(); // 显示当前音符时值
        public double TotalHeight => 128 * KeyHeight; // 128个MIDI音符
        
        // 有效滚动范围 - 限制在合理的MIDI音符范围内
        public double EffectiveScrollableHeight => Math.Max(0, TotalHeight - VerticalViewportSize);
        
        // 实际渲染高度 - 考虑事件视图占用的空间
        public double ActualRenderHeight => IsEventViewVisible ? ViewportHeight * 0.75 : ViewportHeight; // 事件视图打开时钢琴卷帘占75%
        #endregion

        #region 代理属性 - 简化访问
        #region 便捷属性 - 简化访问
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
        #endregion

        #region 构造函数
        public PianoRollViewModel() : this(null) { }

        public PianoRollViewModel(ICoordinateService? coordinateService)
        {
            _coordinateService = coordinateService ?? new DominoNext.Services.Implementation.CoordinateService();

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

            // 订阅模块事件
            SubscribeToModuleEvents();

            // 初始化选项
            InitializeNoteDurationOptions();
        }
        #endregion

        #region 模块事件订阅
        private void SubscribeToModuleEvents()
        {
            // 拖拽模块事件（避免nameof冲突）
            DragModule.OnDragUpdated += InvalidateVisual;
            DragModule.OnDragEnded += InvalidateVisual;

            ResizeModule.OnResizeUpdated += InvalidateVisual;
            ResizeModule.OnResizeEnded += InvalidateVisual;

            CreationModule.OnCreationUpdated += InvalidateVisual;
            CreationModule.OnCreationCompleted += OnNoteCreated; // 订阅音符创建完成事件

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
                    // 当选择框状态变化时，通知UI更新
                    OnPropertyChanged(nameof(SelectionStart));
                    OnPropertyChanged(nameof(SelectionEnd));
                    OnPropertyChanged(nameof(IsSelecting));
                    InvalidateVisual();
                }
            };
        }

        private void InvalidateVisual()
        {
            // 触发UI更新的方法，由View层实现
        }

        /// <summary>
        /// 音符创建完成后，同步更新用户定义的音符时值
        /// </summary>
        private void OnNoteCreated()
        {
            InvalidateVisual();
            
            // 同步最新创建音符的时值到UI显示
            if (Notes.Count > 0)
            {
                var lastNote = Notes.Last();
                if (!lastNote.Duration.Equals(UserDefinedNoteDuration))
                {
                    UserDefinedNoteDuration = lastNote.Duration;
                    OnPropertyChanged(nameof(CurrentNoteTimeValueText));
                }
            }
            
            // 重新计算滚动范围以支持自动延长小节功能
            UpdateMaxScrollExtent();
        }
        #endregion

        #region 初始化方法
        private void InitializeNoteDurationOptions()
        {
            // 网格量化选项 - 控制音符可以放置在多细的网格上
            NoteDurationOptions.Add(new NoteDurationOption("全音符网格 (1/1)", MusicalFraction.WholeNote, "𝅝"));
            NoteDurationOptions.Add(new NoteDurationOption("二分音符网格 (1/2)", MusicalFraction.HalfNote, "𝅗𝅥"));
            NoteDurationOptions.Add(new NoteDurationOption("三连二分音符网格 (1/3)", MusicalFraction.TripletHalf, "𝅗𝅥"));
            NoteDurationOptions.Add(new NoteDurationOption("四分音符网格 (1/4)", MusicalFraction.QuarterNote, "𝅘𝅥"));
            NoteDurationOptions.Add(new NoteDurationOption("三连四分音符网格 (1/6)", MusicalFraction.TripletQuarter, "𝅘𝅥"));
            NoteDurationOptions.Add(new NoteDurationOption("八分音符网格 (1/8)", MusicalFraction.EighthNote, "𝅘𝅥𝅮"));
            NoteDurationOptions.Add(new NoteDurationOption("三连八分音符网格 (1/12)", MusicalFraction.TripletEighth, "𝅘𝅥𝅮"));
            NoteDurationOptions.Add(new NoteDurationOption("十六分音符网格 (1/16)", MusicalFraction.SixteenthNote, "𝅘𝅥𝅯"));
            NoteDurationOptions.Add(new NoteDurationOption("三连十六分音符网格 (1/24)", MusicalFraction.TripletSixteenth, "𝅘𝅥𝅯"));
            NoteDurationOptions.Add(new NoteDurationOption("三十二分音符网格 (1/32)", MusicalFraction.ThirtySecondNote, "𝅘𝅥𝅰"));
            NoteDurationOptions.Add(new NoteDurationOption("三连三十二分音符网格 (1/48)", new MusicalFraction(1, 48), "𝅘𝅥𝅰"));
            NoteDurationOptions.Add(new NoteDurationOption("六十四分音符网格 (1/64)", new MusicalFraction(1, 64), "𝅘𝅥𝅱"));
        }
        #endregion

        #region 坐标转换委托 - 优化版本
        public int GetPitchFromY(double y) => _coordinateService.GetPitchFromY(y, KeyHeight);
        public double GetTimeFromX(double x) => _coordinateService.GetTimeFromX(x, TimeToPixelScale);
        public Point GetPositionFromNote(NoteViewModel note) => _coordinateService.GetPositionFromNote(note, TimeToPixelScale, KeyHeight);
        public Rect GetNoteRect(NoteViewModel note) => _coordinateService.GetNoteRect(note, TimeToPixelScale, KeyHeight);
        
        // 添加支持滚动偏移量的坐标转换方法 - 简化版本
        public int GetPitchFromScreenY(double screenY) => _coordinateService.GetPitchFromY(screenY, KeyHeight, VerticalScrollOffset);
        public double GetTimeFromScreenX(double screenX) => _coordinateService.GetTimeFromX(screenX, TimeToPixelScale, CurrentScrollOffset);
        public Point GetScreenPositionFromNote(NoteViewModel note) => _coordinateService.GetPositionFromNote(note, TimeToPixelScale, KeyHeight, CurrentScrollOffset, VerticalScrollOffset);
        public Rect GetScreenNoteRect(NoteViewModel note) => _coordinateService.GetNoteRect(note, TimeToPixelScale, KeyHeight, CurrentScrollOffset, VerticalScrollOffset);
        #endregion

        #region 公共方法委托给模块
        public void StartCreatingNote(Point position) => CreationModule.StartCreating(position);
        public void UpdateCreatingNote(Point position) => CreationModule.UpdateCreating(position);
        public void FinishCreatingNote() => CreationModule.FinishCreating();
        public void CancelCreatingNote() => CreationModule.CancelCreating();

        public void StartNoteDrag(NoteViewModel note, Point startPoint) => DragModule.StartDrag(note, startPoint);
        public void UpdateNoteDrag(Point currentPoint, Point startPoint) => DragModule.UpdateDrag(currentPoint);
        public void EndNoteDrag() => DragModule.EndDrag();

        public void StartNoteResize(Point position, NoteViewModel note, ResizeHandle handle) => ResizeModule.StartResize(position, note, handle);
        public void UpdateNoteResize(Point currentPosition) => ResizeModule.UpdateResize(currentPosition);
        public void EndNoteResize() => ResizeModule.EndResize();

        public ResizeHandle GetResizeHandleAtPosition(Point position, NoteViewModel note) => ResizeModule.GetResizeHandleAtPosition(position, note);

        public NoteViewModel? GetNoteAtPosition(Point position) => SelectionModule.GetNoteAtPosition(position, Notes, TimeToPixelScale, KeyHeight);
        #endregion

        #region 工具方法
        public double SnapToGridTime(double time) => MusicalFraction.QuantizeToGrid(time, GridQuantization, TicksPerBeat);

        // 新增：音符名称和键盘相关方法
        public bool IsBlackKey(int midiNote)
        {
            var noteInOctave = midiNote % 12;
            return noteInOctave == 1 || noteInOctave == 3 || noteInOctave == 6 || noteInOctave == 8 || noteInOctave == 10;
        }

        public string GetNoteName(int midiNote)
        {
            var noteNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            var octave = midiNote / 12 - 1;
            var noteIndex = midiNote % 12;
            return $"{noteNames[noteIndex]}{octave}";
        }

        public void AddNote(int pitch, double startTime, double duration = -1, int velocity = 100)
        {
            var quantizedStartTime = SnapToGridTime(startTime);
            var quantizedPosition = MusicalFraction.FromTicks(quantizedStartTime, TicksPerBeat);
            var noteDuration = duration < 0 ? UserDefinedNoteDuration : MusicalFraction.FromTicks(duration, TicksPerBeat);

            var note = new NoteViewModel
            {
                Pitch = pitch,
                StartPosition = quantizedPosition,
                Duration = noteDuration,
                Velocity = velocity
            };
            Notes.Add(note);
            
            // 添加音符后重新计算滚动范围以支持自动延长小节功能
            UpdateMaxScrollExtent();
        }
        #endregion

        #region 命令
        [RelayCommand]
        private void SelectPencilTool() => CurrentTool = EditorTool.Pencil;

        [RelayCommand]
        private void SelectSelectionTool() => CurrentTool = EditorTool.Select;

        [RelayCommand]
        private void SelectEraserTool() => CurrentTool = EditorTool.Eraser;

        [RelayCommand]
        private void SelectCutTool() => CurrentTool = EditorTool.Cut;

        [RelayCommand]
        private void ToggleNoteDurationDropDown() => IsNoteDurationDropDownOpen = !IsNoteDurationDropDownOpen;

        [RelayCommand]
        private void SelectNoteDuration(NoteDurationOption option)
        {
            // 这里应该更改网格量化，而不是用户定义的音符时值
            GridQuantization = option.Duration;
            IsNoteDurationDropDownOpen = false;
            
            // 手动触发UI更新
            OnPropertyChanged(nameof(CurrentNoteDurationText));
        }

        [RelayCommand]
        private void ApplyCustomFraction()
        {
            try
            {
                // 简单的分数解析
                var parts = CustomFractionInput.Split('/');
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out int numerator) &&
                    int.TryParse(parts[1], out int denominator) &&
                    numerator > 0 && denominator > 0)
                {
                    // 这里应该更改网格量化，而不是用户定义的音符时值
                    GridQuantization = new MusicalFraction(numerator, denominator);
                    IsNoteDurationDropDownOpen = false;
                    OnPropertyChanged(nameof(CurrentNoteDurationText));
                }
            }
            catch
            {
                // 解析失败，保持原值
            }
        }

        [RelayCommand]
        private void SelectAll() => SelectionModule.SelectAll(Notes);

        [RelayCommand]
        private void ToggleEventView()
        {
            IsEventViewVisible = !IsEventViewVisible;
            
            // 当事件视图可见性改变时，重新计算视口尺寸
            OnPropertyChanged(nameof(EffectiveScrollableHeight));
            OnPropertyChanged(nameof(ActualRenderHeight));
            
            // 重新设置视口尺寸以适应新的布局
            VerticalViewportSize = IsEventViewVisible ? ViewportHeight * 0.75 : ViewportHeight;
            
            // 验证并限制滚动位置
            ValidateAndClampScrollOffsets();
        }
        #endregion

        #region 清理
        public void Cleanup()
        {
            DragModule.EndDrag();
            ResizeModule.EndResize();
            CreationModule.CancelCreating();
            SelectionModule.ClearSelection(Notes);
            PreviewModule.ClearPreview();
            VelocityEditingModule.EndEditing();
            Notes.Clear();
        }
        #endregion

        #region 属性变更处理
        partial void OnZoomSliderValueChanged(double value)
        {
            // 将0-100的滑块值转换为0.1-5.0的缩放值
            // 50对应1.0倍缩放，0对应0.1倍，100对应5.0倍
            Zoom = ConvertSliderValueToZoom(value);
        }

        partial void OnVerticalZoomSliderValueChanged(double value)
        {
            // 将0-100的滑块值转换为0.5-3.0的垂直缩放值
            // 50对应1.0倍缩放，0对应0.5倍，100对应3.0倍
            VerticalZoom = ConvertSliderValueToVerticalZoom(value);
        }

        partial void OnZoomChanged(double value)
        {
            // 当Zoom发生变化时，通知所有相关的计算属性
            OnPropertyChanged(nameof(TimeToPixelScale)); // 新增：通知TimeToPixelScale变化
            OnPropertyChanged(nameof(MeasureWidth));
            OnPropertyChanged(nameof(BeatWidth));
            OnPropertyChanged(nameof(EighthNoteWidth));
            OnPropertyChanged(nameof(SixteenthNoteWidth));
            
            // 重新计算最大滚动范围
            UpdateMaxScrollExtent();
            
            // 使所有音符的缓存失效
            foreach (var note in Notes)
            {
                note.InvalidateCache();
            }
        }

        partial void OnVerticalZoomChanged(double value)
        {
            // 当VerticalZoom发生变化时，通知所有相关的计算属性
            OnPropertyChanged(nameof(KeyHeight));
            OnPropertyChanged(nameof(TotalHeight));
            OnPropertyChanged(nameof(EffectiveScrollableHeight));
            OnPropertyChanged(nameof(ActualRenderHeight));
            
            // 使所有音符的缓存失效
            foreach (var note in Notes)
            {
                note.InvalidateCache();
            }
        }

        partial void OnCurrentScrollOffsetChanged(double value)
        {
            // 当滚动偏移量变化时的处理
            // 触发重新渲染等操作
        }

        partial void OnVerticalScrollOffsetChanged(double value)
        {
            // 当垂直滚动偏移量变化时的处理
            // 触发重新渲染等操作
        }

        partial void OnViewportHeightChanged(double value)
        {
            // 当视口高度变化时，更新相关计算属性
            OnPropertyChanged(nameof(EffectiveScrollableHeight));
            OnPropertyChanged(nameof(ActualRenderHeight));
        }

        partial void OnVerticalViewportSizeChanged(double value)
        {
            // 当垂直视口大小变化时，更新相关计算属性
            OnPropertyChanged(nameof(EffectiveScrollableHeight));
        }

        partial void OnIsEventViewVisibleChanged(bool value)
        {
            // 当事件视图可见性变化时，更新相关计算属性
            OnPropertyChanged(nameof(EffectiveScrollableHeight));
            OnPropertyChanged(nameof(ActualRenderHeight));
        }

        private double ConvertSliderValueToZoom(double sliderValue)
        {
            // 确保滑块值在有效范围内
            sliderValue = Math.Max(0, Math.Min(100, sliderValue));
            
            // 水平缩放：0-100 -> 0.1-5.0
            // 使用指数函数实现更好的缩放体验
            if (sliderValue <= 50)
            {
                // 0-50对应0.1-1.0
                return 0.1 + (sliderValue / 50.0) * 0.9;
            }
            else
            {
                // 50-100对应1.0-5.0
                return 1.0 + ((sliderValue - 50) / 50.0) * 4.0;
            }
        }

        private double ConvertSliderValueToVerticalZoom(double sliderValue)
        {
            // 确保滑块值在有效范围内
            sliderValue = Math.Max(0, Math.Min(100, sliderValue));
            
            // 垂直缩放：0-100 -> 0.5-3.0
            if (sliderValue <= 50)
            {
                // 0-50对应0.5-1.0
                return 0.5 + (sliderValue / 50.0) * 0.5;
            }
            else
            {
                // 50-100对应1.0-3.0
                return 1.0 + ((sliderValue - 50) / 50.0) * 2.0;
            }
        }

        /// <summary>
        /// 更新最大滚动范围
        /// 根据音符内容和缩放级别动态计算 - 优化版本
        /// </summary>
        public void UpdateMaxScrollExtent()
        {
            // 计算所有音符的最大结束位置
            double maxNoteEndTime = 0;
            foreach (var note in Notes)
            {
                var endTime = note.StartPosition.ToTicks(TicksPerBeat) + note.Duration.ToTicks(TicksPerBeat);
                maxNoteEndTime = Math.Max(maxNoteEndTime, endTime);
            }

            // 直接使用TimeToPixelScale转换为像素位置
            var maxNoteEndPixels = maxNoteEndTime * TimeToPixelScale;

            // 至少显示8个小节，或者到最后一个音符后2个小节
            var minExtent = 8 * MeasureWidth;
            var noteBasedExtent = maxNoteEndPixels + 2 * MeasureWidth;

            MaxScrollExtent = Math.Max(minExtent, noteBasedExtent);
            
            // 确保当前滚动偏移量不超过最大范围
            if (CurrentScrollOffset > MaxScrollExtent - ViewportWidth)
            {
                CurrentScrollOffset = Math.Max(0, MaxScrollExtent - ViewportWidth);
            }
        }

        /// <summary>
        /// 设置视口尺寸
        /// </summary>
        public void SetViewportSize(double width, double height)
        {
            ViewportWidth = width;
            ViewportHeight = height;
            VerticalViewportSize = IsEventViewVisible ? height * 0.75 : height; // 考虑事件视图占用的空间
            UpdateMaxScrollExtent();
            
            // 确保当前滚动位置在有效范围内
            ValidateAndClampScrollOffsets();
            
            // 通知相关属性变化
            OnPropertyChanged(nameof(EffectiveScrollableHeight));
            OnPropertyChanged(nameof(ActualRenderHeight));
        }

        /// <summary>
        /// 验证并限制滚动偏移量在有效范围内
        /// </summary>
        public void ValidateAndClampScrollOffsets()
        {
            // 垂直滚动范围：0 到 (TotalHeight - VerticalViewportSize)
            var maxVerticalScroll = Math.Max(0, TotalHeight - VerticalViewportSize);
            if (VerticalScrollOffset > maxVerticalScroll)
            {
                VerticalScrollOffset = maxVerticalScroll;
            }
            else if (VerticalScrollOffset < 0)
            {
                VerticalScrollOffset = 0;
            }

            // 水平滚动范围：0 到 MaxScrollExtent - ViewportWidth
            var maxHorizontalScroll = Math.Max(0, MaxScrollExtent - ViewportWidth);
            if (CurrentScrollOffset > maxHorizontalScroll)
            {
                CurrentScrollOffset = maxHorizontalScroll;
            }
            else if (CurrentScrollOffset < 0)
            {
                CurrentScrollOffset = 0;
            }
        }

        /// <summary>
        /// 获取有效的垂直滚动最大值
        /// </summary>
        public double GetEffectiveVerticalScrollMax()
        {
            return Math.Max(0, TotalHeight - VerticalViewportSize);
        }
        
        /// <summary>
        /// 基于实际渲染高度获取有效的垂直滚动最大值
        /// </summary>
        public double GetEffectiveVerticalScrollMax(double actualRenderHeight)
        {
            return Math.Max(0, TotalHeight - actualRenderHeight);
        }
        #endregion
    }
}