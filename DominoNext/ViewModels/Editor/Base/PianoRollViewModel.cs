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
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace DominoNext.ViewModels.Editor
{
    /// <summary>
    /// 重构后的钢琴卷帘ViewModel - 符合MVVM最佳实践
    /// 主要负责协调各个模块，业务逻辑委托给专门的模块处理
    /// 集成MIDI读取与播放、多音轨洋葱皮功能，以及动态大小调整功能
    /// 高性能优化版本：批量更新、延迟通知、缓存优化
    /// 修复：默认显示设置、PPQ适应、多音轨缩放同步
    /// </summary>
    public partial class PianoRollViewModel : ViewModelBase
    {
        #region 服务依赖
        private readonly ICoordinateService? _coordinateService;
        private readonly IPlaybackService? _playbackService;
        private readonly Services.Implementation.NoteIndexService _noteIndexService = new(); // 新增：音符索引服务
        #endregion

        #region 核心模块
        public NoteDragModule DragModule { get; private set; } = null!;
        public NoteResizeModule ResizeModule { get; private set; } = null!;
        public NoteCreationModule CreationModule { get; private set; } = null!;
        public NoteSelectionModule SelectionModule { get; private set; } = null!;
        public NotePreviewModule PreviewModule { get; private set; } = null!;
        public VelocityEditingModule VelocityEditingModule { get; private set; } = null!;
        #endregion

        #region 状态管理
        public DragState DragState { get; }
        public ResizeState ResizeState { get; }
        public SelectionState SelectionState { get; }
        #endregion

        #region 性能优化字段
        private volatile bool _isUpdatingZoom = false;
        private volatile bool _isUpdatingVerticalZoom = false;
        private readonly object _updateLock = new object();
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
        [ObservableProperty] private IRelayCommand _resetZoomCommand;

        // UI相关属性
        [ObservableProperty] private bool _isNoteDurationDropDownOpen = false;
        [ObservableProperty] private string _customFractionInput = "1/4";
        [ObservableProperty] private bool _showGridLines = true;

        [RelayCommand]
        private async Task ToggleFollowPlayback()
        {
            FollowPlayback = !FollowPlayback;
            
            // 如果刚刚启用跟随播放，立即跳转到当前位置
            if (FollowPlayback)
            {
                TimelinePosition = PlaybackPosition * PixelsPerTick;
            }
        }

        // 新增：MIDI和时间相关属性
        [ObservableProperty] private int _ticksPerBeat = MusicalFraction.QUARTER_NOTE_TICKS;
        [ObservableProperty] private int _beatsPerMeasure = 4; // 标准4/4拍
        
        [ObservableProperty] private int _subdivisionLevel = 4; // 小节分割段数，根据设置动态调整
        [ObservableProperty] private int _totalMeasures = 16; // 修复：初始显示16小节，确保有足够内容显示
        
        // 添加播放指示线相关属性
        [ObservableProperty] private long _playbackPosition = 0;
        [ObservableProperty] private bool _isPlaying = false;
        [ObservableProperty] private bool _followPlayback = false;
        #endregion

        #region 洋葱皮属性
        [ObservableProperty] private bool _isOnionSkinEnabled = true; // 默认启用洋葱皮功能
        [ObservableProperty] private int _onionSkinPreviousFrames = 1;
        [ObservableProperty] private int _onionSkinNextFrames = 1;
        [ObservableProperty] private double _onionSkinOpacity = 0.3;
        #endregion

        #region 集合
        public ObservableCollection<NoteViewModel> Notes { get; } = new();
        public ObservableCollection<NoteDurationOption> NoteDurationOptions { get; } = new(); // 网格量化选项

        // 新增：MIDI事件集合
        public ObservableCollection<MidiEventItem> MidiEvents { get; } = new();

        // 新增：音轨集合
        public ObservableCollection<TrackViewModel> Tracks { get; } = new();
        #endregion

        #region 音轨管理
        private TrackViewModel? _selectedTrack;
        public TrackViewModel? SelectedTrack
        {
            get => _selectedTrack;
            set
            {
                if (_selectedTrack != value)
                {
                    // 保存当前轨道的音符
                    SyncNotesToTrack();

                    _selectedTrack = value;

                    // 加载新轨道的音符
                    Notes.Clear();
                    if (_selectedTrack != null)
                    {
                        foreach (var note in _selectedTrack.Notes)
                            Notes.Add(note);
                        
                        // 同步洋葱皮属性
                        IsOnionSkinEnabled = _selectedTrack.IsOnionSkinEnabled;
                        OnionSkinOpacity = _selectedTrack.OnionSkinOpacity;
                        OnionSkinPreviousFrames = _selectedTrack.OnionSkinPreviousFrames;
                        OnionSkinNextFrames = _selectedTrack.OnionSkinNextFrames;
                    }

                    OnPropertyChanged(nameof(SelectedTrack));
                    InvalidateVisual();
                }
            }
        }
        #endregion

        #region 计算属性 - 高性能缓存版本，修复PPQ适应
        private double _cachedPixelsPerTick = double.NaN;
        private double _cachedKeyHeight = double.NaN;
        private double _cachedMeasureWidth = double.NaN;
        private double _cachedBeatWidth = double.NaN;
        private double _lastZoomForCache = double.NaN;
        private double _lastVerticalZoomForCache = double.NaN;
        private int _lastTicksPerBeatForCache = -1;
        private int _lastBeatsPerMeasureForCache = -1;

        public double PixelsPerTick
        {
            get
            {
                if (double.IsNaN(_cachedPixelsPerTick) || 
                    Math.Abs(_lastZoomForCache - Zoom) > 0.001 ||
                    _lastTicksPerBeatForCache != TicksPerBeat)
                {
                    // 修复：根据PPQ动态调整基础缩放
                    var baseScale = 96.0 / TicksPerBeat; // 使用96作为标准PPQ
                    _cachedPixelsPerTick = Zoom * baseScale;
                    _lastZoomForCache = Zoom;
                    _lastTicksPerBeatForCache = TicksPerBeat;
                }
                return _cachedPixelsPerTick;
            }
        }

        public double KeyHeight
        {
            get
            {
                if (double.IsNaN(_cachedKeyHeight) || Math.Abs(_lastVerticalZoomForCache - VerticalZoom) > 0.001)
                {
                    _cachedKeyHeight = 12.0 * VerticalZoom;
                    _lastVerticalZoomForCache = VerticalZoom;
                }
                return _cachedKeyHeight;
            }
        }

        public double MeasureWidth
        {
            get
            {
                if (double.IsNaN(_cachedMeasureWidth) || 
                    _lastBeatsPerMeasureForCache != BeatsPerMeasure ||
                    _lastTicksPerBeatForCache != TicksPerBeat ||
                    Math.Abs(_lastZoomForCache - Zoom) > 0.001)
                {
                    _cachedMeasureWidth = BeatsPerMeasure * TicksPerBeat * PixelsPerTick;
                    _lastBeatsPerMeasureForCache = BeatsPerMeasure;
                    _lastTicksPerBeatForCache = TicksPerBeat;
                }
                return _cachedMeasureWidth;
            }
        }

        public double BeatWidth
        {
            get
            {
                if (double.IsNaN(_cachedBeatWidth) ||
                    _lastTicksPerBeatForCache != TicksPerBeat ||
                    Math.Abs(_lastZoomForCache - Zoom) > 0.001)
                {
                    _cachedBeatWidth = TicksPerBeat * PixelsPerTick;
                    _lastTicksPerBeatForCache = TicksPerBeat;
                }
                return _cachedBeatWidth;
            }
        }

        // 其他计算属性使用内联计算，避免过多缓存
        public double EighthNoteWidth => TicksPerBeat * 0.5 * PixelsPerTick;
        public double SixteenthNoteWidth => TicksPerBeat * 0.25 * PixelsPerTick;
        public double ContentWidth => MeasureWidth * Math.Max(1, TotalMeasures);
        public double TotalHeight => 128 * KeyHeight;

        public string CurrentNoteDurationText => GridQuantization.ToString();
        public string CurrentNoteTimeValueText => UserDefinedNoteDuration.ToString();
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

        #region 性能优化方法
        
        /// <summary>
        /// 清除所有缓存，强制重新计算
        /// </summary>
        public void InvalidateAllCaches()
        {
            _cachedPixelsPerTick = double.NaN;
            _cachedKeyHeight = double.NaN;
            _cachedMeasureWidth = double.NaN;
            _cachedBeatWidth = double.NaN;
            _lastZoomForCache = double.NaN;
            _lastVerticalZoomForCache = double.NaN;
            _lastTicksPerBeatForCache = -1;
            _lastBeatsPerMeasureForCache = -1;

            // 使所有音符的缓存失效
            foreach (var note in Notes)
            {
                note.InvalidateCache();
            }
        }

        /// <summary>
        /// 批量添加音符，提升性能
        /// </summary>
        public async Task AddNotesAsync(IEnumerable<NoteViewModel> notes)
        {
            var notesList = notes.ToList();
            if (notesList.Count == 0) return;

            if (notesList.Count > 100)
            {
                // 大量音符时使用异步处理
                await Task.Run(() =>
                {
                    // 后台预处理
                    foreach (var note in notesList)
                    {
                        // 预计算位置等
                    }
                });
            }

            foreach (var note in notesList)
            {
                Notes.Add(note);
                // 同时添加到索引
                _noteIndexService.AddNote(note, TicksPerBeat);
            }
        }

        /// <summary>
        /// 批量移除音符，提升性能
        /// </summary>
        public async Task RemoveNotesAsync(IEnumerable<NoteViewModel> notes)
        {
            var notesList = notes.ToList();
            if (notesList.Count == 0) return;

            foreach (var note in notesList)
            {
                Notes.Remove(note);
                // 同时从索引中移除
                _noteIndexService.RemoveNote(note, TicksPerBeat);
            }
        }

        /// <summary>
        /// 高性能视口查询 - 只获取可见区域的音符
        /// </summary>
        public IEnumerable<NoteViewModel> GetNotesInViewport(double startTicks, double endTicks, int minPitch, int maxPitch)
        {
            return _noteIndexService.FindNotesInViewport(startTicks, endTicks, minPitch, maxPitch, TicksPerBeat);
        }

        /// <summary>
        /// 高性能时间范围查询
        /// </summary>
        public IEnumerable<NoteViewModel> GetNotesInTimeRange(double startTicks, double endTicks)
        {
            return _noteIndexService.FindNotesInTimeRange(startTicks, endTicks, TicksPerBeat);
        }

        /// <summary>
        /// 高性能音高范围查询
        /// </summary>
        public IEnumerable<NoteViewModel> GetNotesInPitchRange(int minPitch, int maxPitch)
        {
            return _noteIndexService.FindNotesInPitchRange(minPitch, maxPitch);
        }

        /// <summary>
        /// 查找重叠音符
        /// </summary>
        public IEnumerable<NoteViewModel> FindOverlappingNotes(NoteViewModel targetNote)
        {
            return _noteIndexService.FindOverlappingNotes(targetNote, TicksPerBeat);
        }

        /// <summary>
        /// 重建音符索引（在大量操作后调用）
        /// </summary>
        public void RebuildNoteIndex()
        {
            _noteIndexService.RebuildIndex(Notes, TicksPerBeat);
        }

        /// <summary>
        /// 获取索引统计信息
        /// </summary>
        public Services.Implementation.IndexStatistics GetIndexStatistics()
        {
            return _noteIndexService.GetStatistics();
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
            OnPropertyChanged("Visual");
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

        #region 坐标转换委托
        public int GetPitchFromY(double y) => _coordinateService.GetPitchFromY(y, KeyHeight);
        public double GetTimeFromX(double x) => _coordinateService.GetTimeFromX(x, Zoom, PixelsPerTick);
        public Point GetPositionFromNote(NoteViewModel note) => _coordinateService.GetPositionFromNote(note, Zoom, PixelsPerTick, KeyHeight);
        public Rect GetNoteRect(NoteViewModel note) => _coordinateService.GetNoteRect(note, Zoom, PixelsPerTick, KeyHeight);
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

        public NoteViewModel? GetNoteAtPosition(Point position) => SelectionModule.GetNoteAtPosition(position, Notes, Zoom, PixelsPerTick, KeyHeight);
        #endregion

        #region 工具方法
        public double SnapToGridTime(double time) => MusicalFraction.QuantizeToGrid(time, GridQuantization, TicksPerBeat);
        
        public int SnapToGridPitch(int pitch)
        {
            // FL Studio风格：音高始终对齐到整数音符，不允许半音
            return Math.Clamp(pitch, 0, 127);
        }

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

        public void AddNote(int pitch, MusicalFraction startPosition, MusicalFraction? duration = null, int velocity = 100)
        {
            var noteDuration = duration ?? UserDefinedNoteDuration;
            var note = new NoteViewModel
            {
                Pitch = pitch,
                StartPosition = startPosition,
                Duration = noteDuration,
                Velocity = velocity
            };
            Notes.Add(note);
        }

        // 新增：动态大小调整方法
        public void AutoExtendWhenNearEnd(double positionX)
        {
            // 当接近末尾时自动扩展
            var contentEndX = ContentWidth;
            if (positionX > contentEndX - 100) // 距离末尾100像素时扩展
            {
                TotalMeasures = Math.Max(TotalMeasures, (int)(positionX / MeasureWidth) + 5);
            }
        }

        public int CalculateMeasuresToFillUI(double viewportWidth)
        {
            // 计算需要多少小节来填满视口
            return Math.Max(1, (int)Math.Ceiling(viewportWidth / MeasureWidth));
        }

        public void EnsureCapacityForNote(NoteViewModel note)
        {
            // 计算音符结束位置的分数
            var noteEnd = note.StartPosition + note.Duration;
            // 计算需要多少小节来容纳该音符
            var noteEndTicks = noteEnd.ToTicks(TicksPerBeat);
            var requiredMeasures = Math.Max(1, (int)(noteEndTicks / (TicksPerBeat * BeatsPerMeasure)) + 1);
            TotalMeasures = Math.Max(TotalMeasures, requiredMeasures);
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

        // 洋葱皮相关命令
        [RelayCommand]
        private void ToggleOnionSkin() => IsOnionSkinEnabled = !IsOnionSkinEnabled;
        
        [RelayCommand]
        private void IncreaseOnionSkinOpacity() => OnionSkinOpacity = Math.Min(1.0, OnionSkinOpacity + 0.1);
        
        [RelayCommand]
        private void DecreaseOnionSkinOpacity() => OnionSkinOpacity = Math.Max(0.1, OnionSkinOpacity - 0.1);

        [RelayCommand]
        private void DecreasePreviousOnionSkinFrames()
        {
            OnionSkinPreviousFrames = Math.Max(0, OnionSkinPreviousFrames - 1);
        }

        [RelayCommand]
        private void IncreasePreviousOnionSkinFrames()
        {
            OnionSkinPreviousFrames = Math.Min(10, OnionSkinPreviousFrames + 1);
        }

        [RelayCommand]
        private void DecreaseNextOnionSkinFrames()
        {
            OnionSkinNextFrames = Math.Max(0, OnionSkinNextFrames - 1);
        }

        [RelayCommand]
        private void IncreaseNextOnionSkinFrames()
        {
            OnionSkinNextFrames = Math.Min(10, OnionSkinNextFrames + 1);
        }

        // 播放控制命令
        [RelayCommand]
        private async Task Play()
        {
            if (_playbackService != null)
                await _playbackService.PlayAsync(this);
        }

        [RelayCommand]
        private async Task Pause()
        {
            if (_playbackService != null)
                await _playbackService.PauseAsync();
        }

        [RelayCommand]
        private async Task Stop()
        {
            if (_playbackService != null)
                await _playbackService.StopAsync();
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
            
            // 清除音符索引
            _noteIndexService.Clear();
            
            // 清除 MusicalFraction 缓存
            MusicalFraction.ClearCache();
        }
        #endregion

        #region 修复：为加载的MIDI内容设置最佳缩放
        /// <summary>
        /// 修复：为加载的MIDI内容设置最佳缩放
        /// </summary>
        private void SetOptimalZoomForLoadedContent()
        {
            // 计算一个小节应该占据的像素宽度（目标：在1920px宽屏幕上显示约6-8个小节）
            var targetMeasureWidth = 200.0; // 目标小节宽度
            var optimalZoom = targetMeasureWidth / (BeatsPerMeasure * TicksPerBeat);
            
            // 根据PPQ调整：高PPQ的文件需要更小的缩放值
            if (TicksPerBeat > 480) // 高精度MIDI文件
            {
                optimalZoom *= 0.5;
            }
            else if (TicksPerBeat > 240) // 中等精度
            {
                optimalZoom *= 0.7;
            }
            else if (TicksPerBeat < 96) // 低精度
            {
                optimalZoom *= 1.5;
            }
            
            // 限制在合理范围内
            optimalZoom = Math.Max(0.1, Math.Min(5.0, optimalZoom));
            
            System.Diagnostics.Debug.WriteLine($"MIDI加载后设置缩放: PPQ={TicksPerBeat}, 目标小节宽度={targetMeasureWidth}, 最终缩放={optimalZoom}");
            
            // 更新缩放值
            Zoom = optimalZoom;
            ZoomSliderValue = ConvertZoomToSliderValue(optimalZoom);
            
            // 使所有音轨的音符缓存失效
            InvalidateAllNoteCaches();
        }

        /// <summary>
        /// 修复：为空项目设置合理的默认缩放 - 公共方法
        /// </summary>
        public void SetDefaultZoomForEmptyProject()
        {
            // 空项目时设置合理的默认缩放 - 恢复为250像素/小节的标准比例
            var targetMeasureWidth = 250.0; // 标准目标小节宽度
            var defaultZoom = targetMeasureWidth / (BeatsPerMeasure * TicksPerBeat);
            
            // 确保缩放不会过大或过小
            defaultZoom = Math.Max(0.1, Math.Min(5.0, defaultZoom));
            
            System.Diagnostics.Debug.WriteLine($"恢复默认缩放: 目标小节宽度={targetMeasureWidth}, 计算缩放={defaultZoom}");
            
            Zoom = defaultZoom;
            ZoomSliderValue = ConvertZoomToSliderValue(defaultZoom);
            VerticalZoom = 1.0; // 保持垂直缩放
            VerticalZoomSliderValue = 50.0;
        }

        /// <summary>
        /// 从设置同步钢琴卷帘配置
        /// </summary>
        public void SyncSettingsFromConfig()
        {
            // 使用静态访问获取设置服务
            var settingsService = GetSettingsService();
            if (settingsService != null)
            {
                // 同步小节分割设置
                SubdivisionLevel = settingsService.Settings.SubdivisionLevel;
                
                // 同步网格线设置
                ShowGridLines = settingsService.Settings.ShowGridLines;
                
                System.Diagnostics.Debug.WriteLine($"同步设置完成：小节分割={SubdivisionLevel}, 网格线={ShowGridLines}");
            }
            else
            {
                // 回退到默认值
                SubdivisionLevel = 4;
                ShowGridLines = true;
                System.Diagnostics.Debug.WriteLine("设置服务不可用，使用默认值");
            }
        }
        #endregion

        #region 修复：多音轨缓存同步和初始化
        
        /// <summary>
        /// 同步当前音符到当前轨道
        /// </summary>
        private void SyncNotesToTrack()
        {
            if (_selectedTrack != null)
            {
                _selectedTrack.Notes.Clear();
                foreach (var note in Notes)
                {
                    _selectedTrack.Notes.Add(note);
                }
            }
        }

        /// <summary>
        /// 修复：使所有音轨的音符缓存失效
        /// </summary>
        private void InvalidateAllNoteCaches()
        {
            // 当前音轨的音符
            foreach (var note in Notes)
            {
                note.InvalidateCache();
            }

            // 所有音轨的音符
            foreach (var track in Tracks)
            {
                foreach (var note in track.Notes)
                {
                    note.InvalidateCache();
                }
            }
        }

        /// <summary>
        /// 缩放值转换为滑块值
        /// </summary>
        private double ConvertZoomToSliderValue(double zoom)
        {
            if (zoom <= 1.0)
                return (zoom - 0.1) / 0.9 * 50.0;
            else
                return 50.0 + (zoom - 1.0) / 4.0 * 50.0;
        }

        /// <summary>
        /// 垂直缩放值转换为滑块值
        /// </summary>
        private double ConvertVerticalZoomToSliderValue(double zoom)
        {
            if (zoom <= 1.0)
                return (zoom - 0.5) / 0.5 * 50.0;
            else
                return 50.0 + (zoom - 1.0) / 2.0 * 50.0;
        }
        #endregion

        #region MIDI事件方法
        /// <summary>
        /// 更新MIDI事件集合
        /// </summary>
        /// <param name="midiEvents">新的MIDI事件集合</param>
        public void UpdateMidiEvents(IEnumerable<MidiEventItem> midiEvents)
        {
            // 清空现有MIDI事件
            MidiEvents.Clear();
            
            // 添加新的MIDI事件
            foreach (var midiEvent in midiEvents)
            {
                MidiEvents.Add(midiEvent);
            }
            
            // 使视觉元素失效，触发更新
            InvalidateVisual();
        }
        
        /// <summary>
        /// 添加单个MIDI事件
        /// </summary>
        /// <param name="midiEvent">要添加的MIDI事件</param>
        public void AddMidiEvent(MidiEventItem midiEvent)
        {
            MidiEvents.Add(midiEvent);
            InvalidateVisual();
        }
        
        /// <summary>
        /// 移除单个MIDI事件
        /// </summary>
        /// <param name="midiEvent">要移除的MIDI事件</param>
        public void RemoveMidiEvent(MidiEventItem midiEvent)
        {
            MidiEvents.Remove(midiEvent);
            InvalidateVisual();
        }
        #endregion

        private static ISettingsService? _settingsService;

        /// <summary>
        /// 获取设置服务的静态方法
        /// </summary>
        private static ISettingsService? GetSettingsService()
        {
            if (_settingsService == null)
            {
                _settingsService = new DominoNext.Services.Implementation.SettingsService();
                _ = _settingsService.LoadSettingsAsync();
            }
            return _settingsService;
        }

        #region 构造函数
        public PianoRollViewModel()
        {
            // 初始化核心服务
            _coordinateService = new Services.Implementation.CoordinateService();
            _playbackService = null;

            // 初始化状态对象
            SelectionState = new SelectionState();
            DragState = new DragState();
            ResizeState = new ResizeState();

            // 初始化功能模块
            DragModule = new NoteDragModule(DragState, _coordinateService);
            ResizeModule = new NoteResizeModule(ResizeState, _coordinateService);
            CreationModule = new NoteCreationModule(_coordinateService);
            SelectionModule = new NoteSelectionModule(SelectionState, _coordinateService);
            PreviewModule = new NotePreviewModule(_coordinateService);
            VelocityEditingModule = new VelocityEditingModule(_coordinateService);

            // 初始化EditorCommands
            _editorCommands = new EditorCommandsViewModel(_coordinateService);
            _editorCommands.SetPianoRollViewModel(this);

            // 初始化命令
            ResetZoomCommand = new RelayCommand(() => 
            {
                Zoom = 1.0;
                VerticalZoom = 1.0;
                ZoomSliderValue = 50.0;
                VerticalZoomSliderValue = 50.0;
            });
            
            // 初始化默认值
            InitializeDefaults();
            
            // 从设置同步配置
            SyncSettingsFromConfig();
            
            // 设置事件监听
            SetupEventListeners();

            // 订阅模块事件
            SubscribeToModuleEvents();
        }

        public PianoRollViewModel(ICoordinateService coordinateService, IPlaybackService? playbackService = null) : this()
        {
            _coordinateService = coordinateService;
            _playbackService = playbackService;

            // 初始化功能模块
            DragModule = new NoteDragModule(DragState, _coordinateService);
            ResizeModule = new NoteResizeModule(ResizeState, _coordinateService);
            CreationModule = new NoteCreationModule(_coordinateService);
            SelectionModule = new NoteSelectionModule(SelectionState, _coordinateService);
            PreviewModule = new NotePreviewModule(_coordinateService);
            VelocityEditingModule = new VelocityEditingModule(_coordinateService);

            // 订阅模块事件
            SubscribeToModuleEvents();

            // 设置EditorCommands的PianoRollViewModel引用
            _editorCommands.SetPianoRollViewModel(this);

            // 订阅播放服务事件
            if (_playbackService != null)
            {
                _playbackService.PositionChanged += OnPlaybackPositionChanged;
                _playbackService.StateChanged += OnPlaybackStateChanged;
            }
        }

        /// <summary>
        /// 设置事件监听器
        /// </summary>
        private void SetupEventListeners()
        {
            // 使用静态访问获取设置服务
            var settingsService = GetSettingsService();
            if (settingsService != null)
            {
                // 监听设置变化
                settingsService.Settings.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(settingsService.Settings.SubdivisionLevel))
                    {
                        SubdivisionLevel = settingsService.Settings.SubdivisionLevel;
                        System.Diagnostics.Debug.WriteLine($"小节分割设置变更：{SubdivisionLevel}");
                    }
                    else if (e.PropertyName == nameof(settingsService.Settings.ShowGridLines))
                    {
                        ShowGridLines = settingsService.Settings.ShowGridLines;
                        System.Diagnostics.Debug.WriteLine($"网格线显示设置变更：{ShowGridLines}");
                    }
                };
            }
        }

        #endregion

        #region 私有方法
        private void InitializeDefaults()
        {
            // 初始化默认值
            Zoom = 1.0;
            VerticalZoom = 1.0;
            ZoomSliderValue = 50;
            VerticalZoomSliderValue = 50;
            SubdivisionLevel = 4;
            ShowGridLines = true;
            FollowPlayback = false;
            IsPlaying = false;
            PlaybackPosition = 0;
            TimelinePosition = 0;
        }
        #endregion

        #region 播放服务事件处理
        private void OnPlaybackPositionChanged(object? sender, long position)
        {
            PlaybackPosition = position;
            
            // 如果启用了跟随播放功能，则滚动到播放位置
            if (FollowPlayback)
            {
                TimelinePosition = position * PixelsPerTick;
            }
        }
        
        private void OnPlaybackStateChanged(object? sender, PlaybackState state)
        {
            IsPlaying = state == PlaybackState.Playing;
        }
        #endregion

        private double ConvertSliderValueToZoom(double sliderValue)
        {
            sliderValue = Math.Max(0, Math.Min(100, sliderValue));
            
            if (sliderValue <= 50)
                return 0.1 + (sliderValue / 50.0) * 0.9;
            else
                return 1.0 + ((sliderValue - 50) / 50.0) * 4.0;
        }

        private double ConvertSliderValueToVerticalZoom(double sliderValue)
        {
            sliderValue = Math.Max(0, Math.Min(100, sliderValue));
            
            if (sliderValue <= 50)
                return 0.5 + (sliderValue / 50.0) * 0.5;
            else
                return 1.0 + ((sliderValue - 50) / 50.0) * 2.0;
        }
        
        partial void OnZoomSliderValueChanged(double value)
        {
            if (_isUpdatingZoom) return;
            
            lock (_updateLock)
            {
                if (_isUpdatingZoom) return;
                _isUpdatingZoom = true;
            }

            try
            {
                var newZoom = ConvertSliderValueToZoom(value);
                if (Math.Abs(Zoom - newZoom) > 0.001)
                {
                    Zoom = newZoom;
                }
            }
            finally
            {
                _isUpdatingZoom = false;
            }
        }

        partial void OnVerticalZoomSliderValueChanged(double value)
        {
            if (_isUpdatingVerticalZoom) return;
            
            lock (_updateLock)
            {
                if (_isUpdatingVerticalZoom) return;
                _isUpdatingVerticalZoom = true;
            }

            try
            {
                var newVerticalZoom = ConvertSliderValueToVerticalZoom(value);
                if (Math.Abs(VerticalZoom - newVerticalZoom) > 0.001)
                {
                    VerticalZoom = newVerticalZoom;
                }
            }
            finally
            {
                _isUpdatingVerticalZoom = false;
            }
        }

        partial void OnZoomChanged(double value)
        {
            if (_isUpdatingZoom) return;

            // 批量更新相关属性，减少通知次数
            InvalidateZoomRelatedCaches();
            
            // 修复：使所有音轨的音符缓存失效，确保多音轨同步缩放
            InvalidateAllNoteCaches();
            
            // 修复：同步更新滑块值
            if (!_isUpdatingZoom) // 防止循环更新
            {
                var sliderValue = ConvertZoomToSliderValue(value);
                if (Math.Abs(ZoomSliderValue - sliderValue) > 0.1)
                {
                    ZoomSliderValue = sliderValue;
                }
            }
            
            // 使用单个通知更新所有相关的UI
            OnPropertyChanged(nameof(MeasureWidth));
            OnPropertyChanged(nameof(BeatWidth));
            OnPropertyChanged(nameof(EighthNoteWidth));
            OnPropertyChanged(nameof(SixteenthNoteWidth));
            OnPropertyChanged(nameof(ContentWidth));
            OnPropertyChanged(nameof(PixelsPerTick)); // 确保PixelsPerTick属性也通知更新
            
            // 立即通知视觉更新，不使用延迟
            InvalidateVisual();
        }

        partial void OnVerticalZoomChanged(double value)
        {
            if (_isUpdatingVerticalZoom) return;

            // 批量更新相关属性
            InvalidateVerticalZoomRelatedCaches();
            
            // 修复：使所有音轨的音符缓存失效
            InvalidateAllNoteCaches();
            
            // 修复：同步更新垂直滑块值
            if (!_isUpdatingVerticalZoom) // 防止循环更新
            {
                var sliderValue = ConvertVerticalZoomToSliderValue(value);
                if (Math.Abs(VerticalZoomSliderValue - sliderValue) > 0.1)
                {
                    VerticalZoomSliderValue = sliderValue;
                }
            }
            
            OnPropertyChanged(nameof(KeyHeight));
            OnPropertyChanged(nameof(TotalHeight));
            
            // 通知视觉更新
            InvalidateVisual();
        }

        private void InvalidateZoomRelatedCaches()
        {
            _cachedPixelsPerTick = double.NaN;
            _cachedMeasureWidth = double.NaN;
            _cachedBeatWidth = double.NaN;
            _lastZoomForCache = double.NaN;
        }

        private void InvalidateVerticalZoomRelatedCaches()
        {
            _cachedKeyHeight = double.NaN;
            _lastVerticalZoomForCache = double.NaN;
        }

        partial void OnTicksPerBeatChanged(int value)
        {
            if (value <= 0)
            {
                TicksPerBeat = MusicalFraction.QUARTER_NOTE_TICKS;
                return;
            }

            System.Diagnostics.Debug.WriteLine($"PPQ变更为: {value}");
            
            InvalidateZoomRelatedCaches();
            InvalidateAllNoteCaches(); // 修复：使所有音符缓存失效
            
            OnPropertyChanged(nameof(PixelsPerTick));
            OnPropertyChanged(nameof(MeasureWidth));
            OnPropertyChanged(nameof(BeatWidth));
            OnPropertyChanged(nameof(EighthNoteWidth));
            OnPropertyChanged(nameof(SixteenthNoteWidth));
            OnPropertyChanged(nameof(ContentWidth));
            
            InvalidateVisual();
        }

        partial void OnBeatsPerMeasureChanged(int value)
        {
            if (value <= 0) return;
            InvalidateZoomRelatedCaches();
            OnPropertyChanged(nameof(MeasureWidth));
            OnPropertyChanged(nameof(ContentWidth));
            InvalidateVisual();
        }

        partial void OnTotalMeasuresChanged(int value)
        {
            OnPropertyChanged(nameof(ContentWidth));
            InvalidateVisual();
        }

        // 洋葱皮属性变更处理
        partial void OnIsOnionSkinEnabledChanged(bool value)
        {
            if (_selectedTrack != null)
                _selectedTrack.IsOnionSkinEnabled = value;
        }

        partial void OnOnionSkinPreviousFramesChanged(int value)
        {
            if (_selectedTrack != null)
                _selectedTrack.OnionSkinPreviousFrames = value;
        }

        partial void OnOnionSkinNextFramesChanged(int value)
        {
            if (_selectedTrack != null)
                _selectedTrack.OnionSkinNextFrames = value;
        }

        partial void OnOnionSkinOpacityChanged(double value)
        {
            if (_selectedTrack != null)
                _selectedTrack.OnionSkinOpacity = value;
        }

        partial void OnPlaybackPositionChanged(long value)
        {
            // 当播放位置改变时更新UI
            if (FollowPlayback)
            {
                TimelinePosition = value * PixelsPerTick;
            }
        }
        
        /// <summary>
        /// 订阅音符属性变化事件
        /// </summary>
        internal void SubscribeToNoteEvents(NoteViewModel note)
        {
            note.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(NoteViewModel.StartPosition) ||
                    e.PropertyName == nameof(NoteViewModel.Duration) ||
                    e.PropertyName == nameof(NoteViewModel.Velocity))
                {
                    UpdateMidiEvents();
                }
            };
        }
        
        /// <summary>
        /// 更新MIDI事件列表
        /// </summary>
        public void UpdateMidiEvents()
        {
            MidiEvents.Clear();
            
            // 添加示例事件数据
            foreach (var note in Notes)
            {
                var startTick = note.StartPosition.ToTicks(TicksPerBeat);
                var duration = note.Duration.ToTicks(TicksPerBeat);
                var endTick = startTick + duration;
                
                var startMeasure = startTick / (TicksPerBeat * BeatsPerMeasure) + 1;
                var endMeasure = endTick / (TicksPerBeat * BeatsPerMeasure) + 1;
                
                // 添加音符开始事件
                MidiEvents.Add(new MidiEventItem
                {
                    Measure = (int)startMeasure,
                    Tick = (int)(startTick % (TicksPerBeat * BeatsPerMeasure)),
                    Event = $"Note On - Pitch:{note.Pitch}",
                    Gate = $"{duration}",
                    Velocity = note.Velocity
                });
                
                // 添加音符结束事件
                MidiEvents.Add(new MidiEventItem
                {
                    Measure = (int)endMeasure,
                    Tick = (int)(endTick % (TicksPerBeat * BeatsPerMeasure)),
                    Event = $"Note Off - Pitch:{note.Pitch}",
                    Gate = "",
                    Velocity = 0
                });
            }
        }
    }
}