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
    /// PianoRollViewModel公共方法
    /// 包含所有外部可调用的方法接口
    /// </summary>
    public partial class PianoRollViewModel : ViewModelBase
    {
        #region 音轨管理方法
        /// <summary>
        /// 设置当前音轨索引
        /// </summary>
        /// <param name="trackIndex">新的音轨索引</param>
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
        /// 更新当前音轨信息，从TrackSelector获取对应的TrackViewModel
        /// </summary>
        /// <param name="tracks">音轨列表</param>
        public void UpdateCurrentTrackFromTrackList(IEnumerable<TrackViewModel> tracks)
        {
            // 尝试根据CurrentTrackIndex找到对应的TrackViewModel
            var track = tracks.FirstOrDefault(t => t.TrackNumber - 1 == CurrentTrackIndex);
            if (track != null && track != CurrentTrack)
            {
                CurrentTrack = track;
                OnPropertyChanged(nameof(IsCurrentTrackConductor));
            }
        }

        /// <summary>
        /// 设置音轨选择器
        /// </summary>
        /// <param name="trackSelector">音轨选择器ViewModel</param>
        public void SetTrackSelector(TrackSelectorViewModel trackSelector)
        {
            _toolbar.SetTrackSelector(trackSelector);
        }
        #endregion

        #region 坐标转换委托方法
        /// <summary>
        /// 从Y坐标获取音高值
        /// </summary>
        public int GetPitchFromY(double y) => Coordinates.GetPitchFromY(y);

        /// <summary>
        /// 从X坐标获取时间值
        /// </summary>
        public double GetTimeFromX(double x) => Coordinates.GetTimeFromX(x);

        /// <summary>
        /// 从音符获取位置坐标
        /// </summary>
        public Point GetPositionFromNote(NoteViewModel note) => Coordinates.GetPositionFromNote(note);

        /// <summary>
        /// 获取音符的矩形区域
        /// </summary>
        public Rect GetNoteRect(NoteViewModel note) => Coordinates.GetNoteRect(note);

        /// <summary>
        /// 从屏幕Y坐标获取音高值
        /// </summary>
        public int GetPitchFromScreenY(double screenY) => Coordinates.GetPitchFromScreenY(screenY);

        /// <summary>
        /// 从屏幕X坐标获取时间值
        /// </summary>
        public double GetTimeFromScreenX(double screenX) => Coordinates.GetTimeFromScreenX(screenX);

        /// <summary>
        /// 从音符获取屏幕位置坐标
        /// </summary>
        public Point GetScreenPositionFromNote(NoteViewModel note) => Coordinates.GetScreenPositionFromNote(note);

        /// <summary>
        /// 获取音符在屏幕上的矩形区域
        /// </summary>
        public Rect GetScreenNoteRect(NoteViewModel note) => Coordinates.GetScreenNoteRect(note);
        #endregion

        #region 模块操作委托方法
        /// <summary>
        /// 开始创建音符
        /// </summary>
        /// <param name="position">起始位置</param>
        public void StartCreatingNote(Point position = default) => CreationModule.StartCreating(position);

        /// <summary>
        /// 更新正在创建的音符
        /// </summary>
        /// <param name="position">当前位置</param>
        public void UpdateCreatingNote(Point position = default) => CreationModule.UpdateCreating(position);

        /// <summary>
        /// 完成音符创建
        /// </summary>
        public void FinishCreatingNote() => CreationModule.FinishCreating();

        /// <summary>
        /// 取消音符创建
        /// </summary>
        public void CancelCreatingNote() => CreationModule.CancelCreating();

        /// <summary>
        /// 开始拖拽音符
        /// </summary>
        /// <param name="note">要拖拽的音符</param>
        /// <param name="startPoint">拖拽起始点</param>
        public void StartNoteDrag(NoteViewModel note, Point startPoint) => DragModule.StartDrag(note, startPoint);

        /// <summary>
        /// 更新音符拖拽
        /// </summary>
        /// <param name="currentPoint">当前位置</param>
        /// <param name="startPoint">起始点</param>
        public void UpdateNoteDrag(Point currentPoint, Point startPoint) => DragModule.UpdateDrag(currentPoint);

        /// <summary>
        /// 结束音符拖拽
        /// </summary>
        public void EndNoteDrag() => DragModule.EndDrag();

        /// <summary>
        /// 开始调整音符大小
        /// </summary>
        /// <param name="position">起始位置</param>
        /// <param name="note">要调整的音符</param>
        /// <param name="handle">调整句柄类型</param>
        public void StartNoteResize(Point position, NoteViewModel note, ResizeHandle handle) => ResizeModule.StartResize(position, note, handle);

        /// <summary>
        /// 更新音符大小调整
        /// </summary>
        /// <param name="currentPosition">当前位置</param>
        public void UpdateNoteResize(Point currentPosition) => ResizeModule.UpdateResize(currentPosition);

        /// <summary>
        /// 结束音符大小调整
        /// </summary>
        public void EndNoteResize() => ResizeModule.EndResize();

        /// <summary>
        /// 获取指定位置的调整句柄
        /// </summary>
        /// <param name="position">位置</param>
        /// <param name="note">音符</param>
        /// <returns>调整句柄类型</returns>
        public ResizeHandle GetResizeHandleAtPosition(Point position, NoteViewModel note) => ResizeModule.GetResizeHandleAtPosition(position, note);

        /// <summary>
        /// 获取指定位置的音符
        /// </summary>
        /// <param name="position">位置</param>
        /// <returns>找到的音符，如果没有则返回null</returns>
        public NoteViewModel? GetNoteAtPosition(Point position) => SelectionModule.GetNoteAtPosition(position, CurrentTrackNotes, TimeToPixelScale, KeyHeight);

        /// <summary>
        /// 开始绘制事件曲线
        /// </summary>
        /// <param name="startPoint">起始点</param>
        /// <param name="canvasHeight">画布高度</param>
        public void StartDrawingEventCurve(Point startPoint, double canvasHeight)
        {
            EventCurveDrawingModule.StartDrawing(startPoint, CurrentEventType, CurrentCCNumber, canvasHeight);
        }

        /// <summary>
        /// 更新事件曲线绘制
        /// </summary>
        /// <param name="currentPoint">当前位置</param>
        public void UpdateDrawingEventCurve(Point currentPoint)
        {
            EventCurveDrawingModule.UpdateDrawing(currentPoint);
        }

        /// <summary>
        /// 完成事件曲线绘制
        /// </summary>
        public void FinishDrawingEventCurve()
        {
            EventCurveDrawingModule.FinishDrawing();
        }

        /// <summary>
        /// 取消事件曲线绘制
        /// </summary>
        public void CancelDrawingEventCurve()
        {
            EventCurveDrawingModule.CancelDrawing();
        }

        /// <summary>
        /// 获取指定位置的事件值
        /// </summary>
        /// <param name="position">位置</param>
        /// <param name="canvasHeight">画布高度</param>
        /// <returns>事件值</returns>
        public int GetEventValueAtPosition(Point position, double canvasHeight)
        {
            return _eventCurveCalculationService.YToValue(position.Y, canvasHeight, CurrentEventType, CurrentCCNumber);
        }

        /// <summary>
        /// 获取事件值的Y坐标位置
        /// </summary>
        /// <param name="value">事件值</param>
        /// <param name="canvasHeight">画布高度</param>
        /// <returns>Y坐标</returns>
        public double GetYPositionForEventValue(int value, double canvasHeight)
        {
            return _eventCurveCalculationService.ValueToY(value, canvasHeight, CurrentEventType, CurrentCCNumber);
        }
        #endregion

        #region 工具方法
        /// <summary>
        /// 将时间值吸附到网格
        /// </summary>
        /// <param name="time">时间值</param>
        /// <returns>吸附后的时间值</returns>
        public MusicalFraction SnapToGrid(MusicalFraction time) => Toolbar.SnapToGrid(time);

        /// <summary>
        /// 将时间值吸附到网格（双精度版本）
        /// </summary>
        /// <param name="timeValue">时间值</param>
        /// <returns>吸附后的时间值</returns>
        public double SnapToGridTime(double timeValue) => Toolbar.SnapToGridTime(timeValue);

        /// <summary>
        /// 判断指定的MIDI音高是否为黑键
        /// </summary>
        /// <param name="midiNote">MIDI音高值</param>
        /// <returns>如果是黑键返回true，否则返回false</returns>
        public bool IsBlackKey(int midiNote) => Calculations.IsBlackKey(midiNote);

        /// <summary>
        /// 获取MIDI音高的音符名称
        /// </summary>
        /// <param name="midiNote">MIDI音高值</param>
        /// <returns>音符名称（如"C4"）</returns>
        public string GetNoteName(int midiNote) => Calculations.GetNoteName(midiNote);

        /// <summary>
        /// 添加一个新的音符
        /// </summary>
        /// <param name="pitch">音高（MIDI音高值）</param>
        /// <param name="startPosition">起始位置</param>
        /// <param name="duration">时长，如果为null则使用默认时长</param>
        /// <param name="velocity">力度（0-127）</param>
        public void AddNote(int pitch, MusicalFraction startPosition, MusicalFraction? duration = null, int velocity = 100)
        {
            // 检查当前轨道是否为Conductor轨，如果是则禁止创建音符
            if (IsCurrentTrackConductor)
            {
                _logger.Debug("PianoRollViewModel", "禁止在Conductor轨上创建音符");
                return;
            }

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

        /// <summary>
        /// 添加一个新的音符（双精度时间版本）
        /// </summary>
        /// <param name="pitch">音高（MIDI音高值）</param>
        /// <param name="startTime">起始时间（双精度）</param>
        /// <param name="duration">时长（双精度），如果小于等于0则使用默认时长</param>
        /// <param name="velocity">力度（0-127）</param>
        public void AddNote(int pitch, double startTime, double duration = -1, int velocity = 100)
        {
            // 检查当前轨道是否为Conductor轨，如果是则禁止创建音符
            if (IsCurrentTrackConductor)
            {
                _logger.Debug("PianoRollViewModel", "禁止在Conductor轨上创建音符");
                return;
            }

            var startPosition = MusicalFraction.FromDouble(startTime);
            var noteDuration = duration < 0 ? Toolbar.UserDefinedNoteDuration : MusicalFraction.FromDouble(duration);
            AddNote(pitch, startPosition, noteDuration, velocity);
        }

        /// <summary>
        /// 延长钢琴卷帘小节
        /// 当滚动到末尾时自动增加10个小节
        /// </summary>
        public void ExtendPianoRollMeasures()
        {
            // 计算当前最大滚动范围
            var currentMaxScroll = MaxScrollExtent;
            if (currentMaxScroll <= 0) return;

            // 计算10个小节的宽度（假设4/4拍，每个小节4个四分音符）
            var measuresToAdd = 10;
            var quarterNotesPerMeasure = 4; // 4/4拍
            var totalQuarterNotesToAdd = measuresToAdd * quarterNotesPerMeasure;

            // 计算像素宽度
            var pixelsPerQuarterNote = BaseQuarterNoteWidth * Zoom;
            var additionalWidth = totalQuarterNotesToAdd * pixelsPerQuarterNote;

            // 更新MIDI文件时长
            var currentDuration = HasMidiFileDuration ? MidiFileDuration : 0;
            var newDuration = currentDuration + totalQuarterNotesToAdd;
            SetMidiFileDuration(newDuration);

            // 重新计算滚动范围，确保滚动条正确更新
            UpdateMaxScrollExtent();

            // 强制更新滚动条参数，确保滚动条范围和位置正确
            ScrollBarManager.ForceUpdateScrollBars();

            _logger.Info("PianoRollViewModel", $"自动延长钢琴卷帘: 增加 {measuresToAdd} 个小节 ({totalQuarterNotesToAdd} 个四分音符), 新时长: {newDuration:F1} 四分音符, 重新计算滚动范围和滚动条参数");
        }
        #endregion
    }
}