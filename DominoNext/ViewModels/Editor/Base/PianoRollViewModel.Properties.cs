using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Enums;
using Lumino.ViewModels.Editor.Interfaces;
using Lumino.ViewModels.Editor.Components;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel的属性定义
    /// </summary>
    public partial class PianoRollViewModel : IScrollableViewModel, IZoomableViewModel
    {
        #region 基础属性（委托给组件）
        /// <inheritdoc/>
        public double HorizontalScrollOffset
        {
            get => Viewport.CurrentScrollOffset;
            set => Viewport.SetHorizontalScrollOffset(value);
        }

        /// <inheritdoc/>
        public double VerticalScrollOffset
        {
            get => Viewport.VerticalScrollOffset;
            set => Viewport.SetVerticalScrollOffset(value, MaxVerticalScrollExtent);
        }

        /// <inheritdoc/>
        public double MaxHorizontalScrollOffset => Viewport.GetHorizontalScrollableRange();

        /// <inheritdoc/>
        public double MaxVerticalScrollOffset => Viewport.GetEffectiveVerticalScrollMax(MaxVerticalScrollExtent);

        /// <inheritdoc/>
        public double ViewportWidth
        {
            get => Viewport.ViewportWidth;
            set => Viewport.ViewportWidth = value;
        }

        /// <inheritdoc/>
        public double ViewportHeight
        {
            get => Viewport.ViewportHeight;
            set => Viewport.ViewportHeight = value;
        }

        /// <summary>
        /// 水平缩放级别
        /// </summary>
        public double HorizontalZoomLevel
        {
            get => ZoomManager.Zoom;
            set => ZoomManager.Zoom = value;
        }

        /// <summary>
        /// 垂直缩放级别
        /// </summary>
        public double VerticalZoomLevel
        {
            get => ZoomManager.VerticalZoom;
            set => ZoomManager.VerticalZoom = value;
        }

        /// <summary>
        /// 时间到像素缩放比例
        /// </summary>
        public double TimeToPixelScale => ZoomManager.TimeToPixelScale;

        /// <summary>
        /// 时间到像素缩放比例（四舍五入）
        /// </summary>
        public double TimeToPixelScaleRounded => ZoomManager.TimeToPixelScaleRounded;

        /// <summary>
        /// 像素到时间缩放比例
        /// </summary>
        public double PixelToTimeScale => ZoomManager.PixelToTimeScale;

        /// <summary>
        /// 音符高度
        /// </summary>
        public double KeyHeight => ZoomManager.KeyHeight;

        /// <summary>
        /// 是否显示网格
        /// </summary>
        public bool ShowGrid
        {
            get => Configuration.ShowGrid;
            set => Configuration.ShowGrid = value;
        }

        /// <summary>
        /// 是否显示音符名称
        /// </summary>
        public bool ShowNoteNames
        {
            get => Configuration.ShowNoteNames;
            set => Configuration.ShowNoteNames = value;
        }

        /// <summary>
        /// 是否显示力度
        /// </summary>
        public bool ShowVelocity
        {
            get => Configuration.ShowVelocity;
            set => Configuration.ShowVelocity = value;
        }

        /// <summary>
        /// 是否显示黑键
        /// </summary>
        public bool ShowBlackKeys
        {
            get => Configuration.ShowBlackKeys;
            set => Configuration.ShowBlackKeys = value;
        }

        /// <summary>
        /// 是否显示白键
        /// </summary>
        public bool ShowWhiteKeys
        {
            get => Configuration.ShowWhiteKeys;
            set => Configuration.ShowWhiteKeys = value;
        }

        /// <summary>
        /// 网格颜色
        /// </summary>
        public Color GridColor
        {
            get => Configuration.GridColor;
            set => Configuration.GridColor = value;
        }

        /// <summary>
        /// 音符颜色
        /// </summary>
        public Color NoteColor
        {
            get => Configuration.NoteColor;
            set => Configuration.NoteColor = value;
        }

        /// <summary>
        /// 选中音符颜色
        /// </summary>
        public Color SelectedNoteColor
        {
            get => Configuration.SelectedNoteColor;
            set => Configuration.SelectedNoteColor = value;
        }

        /// <summary>
        /// 背景颜色
        /// </summary>
        public Color BackgroundColor
        {
            get => Configuration.BackgroundColor;
            set => Configuration.BackgroundColor = value;
        }

        /// <summary>
        /// 黑键颜色
        /// </summary>
        public Color BlackKeyColor
        {
            get => Configuration.BlackKeyColor;
            set => Configuration.BlackKeyColor = value;
        }

        /// <summary>
        /// 白键颜色
        /// </summary>
        public Color WhiteKeyColor
        {
            get => Configuration.WhiteKeyColor;
            set => Configuration.WhiteKeyColor = value;
        }

        /// <summary>
        /// 黑键文本颜色
        /// </summary>
        public Color BlackKeyTextColor
        {
            get => Configuration.BlackKeyTextColor;
            set => Configuration.BlackKeyTextColor = value;
        }

        /// <summary>
        /// 白键文本颜色
        /// </summary>
        public Color WhiteKeyTextColor
        {
            get => Configuration.WhiteKeyTextColor;
            set => Configuration.WhiteKeyTextColor = value;
        }

        /// <summary>
        /// 是否启用吸附
        /// </summary>
        public bool SnapToGridEnabled
        {
            get => Toolbar.SnapToGridEnabled;
            set => Toolbar.SnapToGridEnabled = value;
        }

        /// <summary>
        /// 吸附值
        /// </summary>
        public MusicalFraction SnapValue
        {
            get => Toolbar.SnapValue;
            set => Toolbar.SnapValue = value;
        }

        /// <summary>
        /// 当前工具类型
        /// </summary>
        public EditorTool CurrentTool
        {
            get => Toolbar.CurrentTool;
            set => Toolbar.CurrentTool = value;
        }

        /// <summary>
        /// 用户定义的音符时长
        /// </summary>
        public MusicalFraction UserDefinedNoteDuration
        {
            get => Toolbar.UserDefinedNoteDuration;
            set => Toolbar.UserDefinedNoteDuration = value;
        }

        /// <summary>
        /// 是否显示音符时长下拉菜单
        /// </summary>
        public bool IsNoteDurationDropDownOpen
        {
            get => Toolbar.IsNoteDurationDropDownOpen;
            set => Toolbar.IsNoteDurationDropDownOpen = value;
        }

        // 这些属性现在由 ObservableProperty 特性自动生成
        // public EventType CurrentEventType { get; set; }
        // public int CurrentCCNumber { get; set; }
        // public bool IsEventTypeSelectorOpen { get; set; }

        /// <summary>
        /// 是否显示CC号输入
        /// </summary>
        public bool ShowCCNumberInput => Toolbar.ShowCCNumberInput;
        #endregion

        #region 歌曲长度和滚动条相关属性
        /// <summary>
        /// 歌曲长度（秒）
        /// </summary>
        public double SongLengthInSeconds
        {
            get
            {
                if (Notes == null || !Notes.Any())
                    return 0;

                var lastNote = Notes.OrderByDescending(n => n.StartPosition + n.Duration).FirstOrDefault();
                if (lastNote == null)
                    return 0;

                return (lastNote.StartPosition + lastNote.Duration).ToDouble() / 480.0 * 60.0 / CurrentBPM;
            }
        }

        /// <summary>
        /// 歌曲长度（像素）
        /// </summary>
        public double SongLengthInPixels
        {
            get
            {
                if (Notes == null || !Notes.Any())
                    return 0;

                var lastNote = Notes.OrderByDescending(n => n.StartPosition + n.Duration).FirstOrDefault();
                if (lastNote == null)
                    return 0;

                return (lastNote.StartPosition + lastNote.Duration).ToDouble() * TimeToPixelScale;
            }
        }

        /// <summary>
        /// 最大滚动范围（像素）
        /// </summary>
        public double MaxScrollExtent
        {
            get
            {
                var contentWidth = Math.Max(SongLengthInPixels, ViewportWidth);
                return Math.Max(0, contentWidth - ViewportWidth);
            }
        }

        /// <summary>
        /// 垂直最大滚动范围（像素）
        /// </summary>
        public double MaxVerticalScrollExtent
        {
            get
            {
                var contentHeight = 128 * KeyHeight; // 128个MIDI音符
                return Math.Max(0, contentHeight - ViewportHeight);
            }
        }

        /// <summary>
        /// 水平滚动条可见性
        /// </summary>
        public bool HorizontalScrollBarVisible => MaxScrollExtent > 0;

        /// <summary>
        /// 垂直滚动条可见性
        /// </summary>
        public bool VerticalScrollBarVisible => MaxVerticalScrollExtent > 0;

        /// <summary>
        /// 当前水平滚动偏移量
        /// </summary>
        public double CurrentScrollOffset => Viewport.CurrentScrollOffset;
        #endregion

        #region 集合
        /// <summary>
        /// 音符集合
        /// </summary>
        public ObservableCollection<NoteViewModel> Notes { get; set; } = new ObservableCollection<NoteViewModel>();

        /// <summary>
        /// 选中音符集合
        /// </summary>
        public ObservableCollection<NoteViewModel> SelectedNotes { get; set; } = new ObservableCollection<NoteViewModel>();

        /// <summary>
        /// 当前音轨音符集合
        /// </summary>
        public ObservableCollection<NoteViewModel> CurrentTrackNotes { get; set; } = new ObservableCollection<NoteViewModel>();

        /// <summary>
        /// 事件曲线集合
        /// </summary>
        public ObservableCollection<EventCurveViewModel> EventCurves { get; set; } = new ObservableCollection<EventCurveViewModel>();

        /// <summary>
        /// 音轨集合
        /// </summary>
        public ObservableCollection<TrackViewModel> Tracks { get; set; } = new ObservableCollection<TrackViewModel>();
        #endregion

        #region 计算属性（委托给计算组件）
        /// <summary>
        /// 当前BPM
        /// </summary>
        public double CurrentBPM => Calculations.CurrentBPM;

        /// <summary>
        /// 最小可见音高
        /// </summary>
        public int MinVisiblePitch => Calculations.MinVisiblePitch;

        /// <summary>
        /// 最大可见音高
        /// </summary>
        public int MaxVisiblePitch => Calculations.MaxVisiblePitch;

        /// <summary>
        /// 可见音高范围
        /// </summary>
        public int VisiblePitchRange => MaxVisiblePitch - MinVisiblePitch;

        /// <summary>
        /// 可见时间范围
        /// </summary>
        public double VisibleTimeRange => Viewport.ViewportWidth / TimeToPixelScale;

        /// <summary>
        /// 是否在拖拽中
        /// </summary>
        public bool IsDragging => DragState.IsDragging;

        /// <summary>
        /// 是否在调整大小中
        /// </summary>
        public bool IsResizing => ResizeState.IsResizing;

        /// <summary>
        /// 是否在创建中
        /// </summary>
        public bool IsCreating => CreationModule?.IsCreatingNote ?? false;

        /// <summary>
        /// 是否在绘制事件曲线中
        /// </summary>
        public bool IsDrawingEventCurve => EventCurveDrawingModule?.IsDrawing ?? false;

        /// <summary>
        /// 是否在力度编辑中
        /// </summary>
        public bool IsVelocityEditing => VelocityEditingModule?.IsEditing ?? false;

        /// <summary>
        /// 是否在剪切中
        /// </summary>
        public bool IsCutting => false; // TODO: 实现剪切功能
        #endregion

        #region 代理属性（简化访问）
        // 这些属性现在由 ObservableProperty 特性自动生成
        // public int CurrentTrackIndex { get; set; }
        // public TrackViewModel? CurrentTrack { get; set; }

        /// <summary>
        /// 当前音轨名称
        /// </summary>
        public string CurrentTrackName
        {
            get
            {
                var track = CurrentTrack;
                return track?.Name ?? $"Track {CurrentTrackIndex + 1}";
            }
        }

        /// <summary>
        /// 当前音轨颜色
        /// </summary>
        public Color CurrentTrackColor
        {
            get
            {
                var track = CurrentTrack;
                return track?.Color ?? Colors.Blue;
            }
        }

        /// <summary>
        /// 是否有音符
        /// </summary>
        public bool HasNotes => Notes?.Any() == true;

        /// <summary>
        /// 是否有选中音符
        /// </summary>
        public bool HasSelectedNotes => SelectedNotes?.Any() == true;

        /// <summary>
        /// 选中音符数量
        /// </summary>
        public int SelectedNotesCount => SelectedNotes?.Count ?? 0;

        /// <summary>
        /// 是否正在操作
        /// </summary>
        public bool IsOperating => IsDragging || IsResizing || IsCreating || IsDrawingEventCurve || IsVelocityEditing || IsCutting;

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        public bool CanUndo => _undoRedoManager?.CanUndo ?? false;

        /// <summary>
        /// 是否可以重做
        /// </summary>
        public bool CanRedo => _undoRedoManager?.CanRedo ?? false;
        #endregion
    }
}