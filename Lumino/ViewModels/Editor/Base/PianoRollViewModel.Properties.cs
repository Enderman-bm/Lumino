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
    /// PianoRollViewModel计算属性和代理属性
    /// 包含所有委托给组件的属性和计算属性
    /// </summary>
    public partial class PianoRollViewModel : ViewModelBase
    {
        #region 基本属性（委托给组件）
        /// <summary>
        /// 当前水平缩放级别
        /// </summary>
        public double Zoom => ZoomManager.Zoom;

        /// <summary>
        /// 当前垂直缩放级别
        /// </summary>
        public double VerticalZoom => ZoomManager.VerticalZoom;

        /// <summary>
        /// 当前时间轴位置（四分音符单位）
        /// </summary>
        public double TimelinePosition => Viewport.TimelinePosition;

        /// <summary>
        /// 水平缩放滑块值（0.0-1.0）
        /// </summary>
        public double ZoomSliderValue
        {
            get => ZoomManager.ZoomSliderValue;
            set => ZoomManager.ZoomSliderValue = value;
        }

        /// <summary>
        /// 垂直缩放滑块值（0.0-1.0）
        /// </summary>
        public double VerticalZoomSliderValue
        {
            get => ZoomManager.VerticalZoomSliderValue;
            set => ZoomManager.VerticalZoomSliderValue = value;
        }

        /// <summary>
        /// 当前选择的工具类型
        /// </summary>
        public EditorTool CurrentTool => Toolbar.CurrentTool;

        /// <summary>
        /// 当前网格量化设置
        /// </summary>
        public MusicalFraction GridQuantization => Toolbar.GridQuantization;

        /// <summary>
        /// 用户定义的音符时长
        /// </summary>
        public MusicalFraction UserDefinedNoteDuration => Toolbar.UserDefinedNoteDuration;

        /// <summary>
        /// 是否显示事件视图
        /// </summary>
        public bool IsEventViewVisible => Toolbar.IsEventViewVisible;

        // 动态滚动相关属性
        /// <summary>
        /// 当前水平滚动偏移量（像素）
        /// </summary>
        public double CurrentScrollOffset => Viewport.CurrentScrollOffset;

        /// <summary>
        /// 水平滚动偏移量别名(用于兼容)
        /// </summary>
        public double HorizontalOffset => Viewport.CurrentScrollOffset;

        /// <summary>
        /// 当前垂直滚动偏移量（像素）
        /// </summary>
        public double VerticalScrollOffset => Viewport.VerticalScrollOffset;

        /// <summary>
        /// 垂直滚动偏移量别名(用于兼容)
        /// </summary>
        public double VerticalOffset => Viewport.VerticalScrollOffset;

        /// <summary>
        /// 视口宽度（像素）
        /// </summary>
        public double ViewportWidth => Viewport.ViewportWidth;

        /// <summary>
        /// 视口高度（像素）
        /// </summary>
        public double ViewportHeight => Viewport.ViewportHeight;

        /// <summary>
        /// 最大水平滚动范围（像素）
        /// </summary>
        public double MaxScrollExtent => Viewport.MaxScrollExtent;

        /// <summary>
        /// 垂直视口大小（像素）
        /// </summary>
        public double VerticalViewportSize => Viewport.VerticalViewportSize;

        // UI相关属性
        /// <summary>
        /// 音符时长下拉框是否打开
        /// </summary>
        public bool IsNoteDurationDropDownOpen => Toolbar.IsNoteDurationDropDownOpen;

        /// <summary>
        /// 自定义分数输入文本
        /// </summary>
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
        /// <summary>
        /// 是否正在绘制曲线
        /// </summary>
        public bool IsDrawingCurve => EventCurveDrawingModule?.IsDrawing ?? false;

        /// <summary>
        /// 当前曲线绘制点集合
        /// </summary>
        public List<CurvePoint> CurrentCurvePoints => EventCurveDrawingModule?.CurrentCurvePoints ?? new List<CurvePoint>();
        #endregion

        #region 计算属性 - 委托给计算组件
        /// <summary>
        /// 基础四分音符宽度（像素）
        /// </summary>
        public double BaseQuarterNoteWidth => Calculations.BaseQuarterNoteWidth;

        /// <summary>
        /// 时间到像素的缩放比例
        /// </summary>
        public double TimeToPixelScale => Calculations.TimeToPixelScale;

        /// <summary>
        /// 琴键高度（像素）
        /// </summary>
        public double KeyHeight => Calculations.KeyHeight;

        /// <summary>
        /// 小节宽度（像素）
        /// </summary>
        public double MeasureWidth => Calculations.MeasureWidth;

        /// <summary>
        /// 拍子宽度（像素）
        /// </summary>
        public double BeatWidth => Calculations.BeatWidth;

        /// <summary>
        /// 八分音符宽度（像素）
        /// </summary>
        public double EighthNoteWidth => Calculations.EighthNoteWidth;

        /// <summary>
        /// 十六分音符宽度（像素）
        /// </summary>
        public double SixteenthNoteWidth => Calculations.SixteenthNoteWidth;

        /// <summary>
        /// 每小节的拍子数
        /// </summary>
        public int BeatsPerMeasure => Calculations.BeatsPerMeasure;

        // UI相关计算属性
        /// <summary>
        /// 当前音符时长的文本显示
        /// </summary>
        public string CurrentNoteDurationText => Toolbar.CurrentNoteDurationText;

        /// <summary>
        /// 当前音符时间的文本显示
        /// </summary>
        public string CurrentNoteTimeValueText => Toolbar.CurrentNoteTimeValueText;

        /// <summary>
        /// 钢琴卷帘的总高度（像素）
        /// </summary>
        public double TotalHeight => Calculations.TotalHeight;

        // 有效滚动范围
        /// <summary>
        /// 有效的垂直可滚动高度
        /// </summary>
        public double EffectiveScrollableHeight => Viewport.GetEffectiveScrollableHeight(TotalHeight, Toolbar.IsEventViewVisible);

        // 实际渲染高度
        /// <summary>
        /// 实际渲染高度（考虑事件视图）
        /// </summary>
        public double ActualRenderHeight => Viewport.GetActualRenderHeight(Toolbar.IsEventViewVisible);
        #endregion

        #region 代理属性 - 简化访问
        // 拖拽相关
        /// <summary>
        /// 是否正在拖拽音符
        /// </summary>
        public bool IsDragging => DragState.IsDragging;

        /// <summary>
        /// 当前正在拖拽的音符
        /// </summary>
        public NoteViewModel? DraggingNote => DragState.DraggingNote;

        /// <summary>
        /// 当前正在拖拽的音符集合
        /// </summary>
        public List<NoteViewModel> DraggingNotes => DragState.DraggingNotes;

        // 调整大小相关
        /// <summary>
        /// 是否正在调整音符大小
        /// </summary>
        public bool IsResizing => ResizeState.IsResizing;

        /// <summary>
        /// 当前调整大小的句柄类型
        /// </summary>
        public ResizeHandle CurrentResizeHandle => ResizeState.CurrentResizeHandle;

        /// <summary>
        /// 当前正在调整大小的音符
        /// </summary>
        public NoteViewModel? ResizingNote => ResizeState.ResizingNote;

        /// <summary>
        /// 当前正在调整大小的音符集合
        /// </summary>
        public List<NoteViewModel> ResizingNotes => ResizeState.ResizingNotes;

        // 创建音符
        /// <summary>
        /// 是否正在创建音符
        /// </summary>
        public bool IsCreatingNote => CreationModule.IsCreatingNote;

        /// <summary>
        /// 当前正在创建的音符
        /// </summary>
        public NoteViewModel? CreatingNote => CreationModule.CreatingNote;

        // 选择框
        /// <summary>
        /// 是否正在进行选择操作
        /// </summary>
        public bool IsSelecting => SelectionState.IsSelecting;

        /// <summary>
        /// 选择框的起始点
        /// </summary>
        public Point? SelectionStart => SelectionState.SelectionStart;

        /// <summary>
        /// 选择框的结束点
        /// </summary>
        public Point? SelectionEnd => SelectionState.SelectionEnd;

        // 预览音符
        /// <summary>
        /// 当前预览的音符
        /// </summary>
        public NoteViewModel? PreviewNote => PreviewModule.PreviewNote;

        // 曲线绘制
        /// <summary>
        /// 是否正在绘制事件曲线
        /// </summary>
        public bool IsDrawingEventCurve => EventCurveDrawingModule?.IsDrawing ?? false;

        /// <summary>
        /// 当前事件曲线绘制点集合
        /// </summary>
        public List<CurvePoint> CurrentEventCurvePoints => EventCurveDrawingModule?.CurrentCurvePoints ?? new List<CurvePoint>();
        #endregion

        #region 歌曲长度和滚动条相关属性
        /// <summary>
        /// 获取当前歌曲的有效长度（四分音符单位）
        /// 基于所有音符的结束位置和MIDI文件时长计算
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

        #region 撤销重做属性
        /// <summary>
        /// 是否可以撤销
        /// </summary>
        public bool CanUndo => _undoRedoService.CanUndo;

        /// <summary>
        /// 是否可以重做
        /// </summary>
        public bool CanRedo => _undoRedoService.CanRedo;

        /// <summary>
        /// 当前撤销操作的描述
        /// </summary>
        public string? UndoDescription => _undoRedoService.UndoDescription;

        /// <summary>
        /// 当前重做操作的描述
        /// </summary>
        public string? RedoDescription => _undoRedoService.RedoDescription;

        /// <summary>
        /// 撤销重做服务
        /// </summary>
        public IUndoRedoService UndoRedoService => _undoRedoService;
        #endregion

        #region 菜单命令CanExecute属性
        /// <summary>
        /// 是否有选中的音符
        /// </summary>
        public bool HasSelectedNotes => Notes.Any(n => n.IsSelected);

        /// <summary>
        /// 是否可以粘贴
        /// </summary>
        public bool CanPaste => _clipboardNotes != null && _clipboardNotes.Any();

        /// <summary>
        /// 是否可以放大
        /// </summary>
        public bool CanZoomIn => !ZoomManager.IsAtMaximumZoom;

        /// <summary>
        /// 是否可以缩小
        /// </summary>
        public bool CanZoomOut => !ZoomManager.IsAtMinimumZoom;

        /// <summary>
        /// 是否可以播放
        /// </summary>
        public bool CanPlay => true; // TODO: 根据播放状态判断

        /// <summary>
        /// 是否可以暂停
        /// </summary>
        public bool CanPause => false; // TODO: 根据播放状态判断

        /// <summary>
        /// 是否可以停止
        /// </summary>
        public bool CanStop => false; // TODO: 根据播放状态判断
        #endregion
    }
}