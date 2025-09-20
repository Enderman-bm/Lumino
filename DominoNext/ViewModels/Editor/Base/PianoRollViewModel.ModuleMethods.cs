using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Components;
using Lumino.ViewModels.Editor.Enums;
using Lumino.ViewModels.Editor.State;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel的模块委托方法
    /// </summary>
    public partial class PianoRollViewModel
    {
        #region 音符创建相关
        /// <summary>
        /// 开始创建音符
        /// </summary>
        public void StartCreatingNote(Point position = default) => CreationModule.StartCreating(position);

        /// <summary>
        /// 更新创建中的音符
        /// </summary>
        public void UpdateCreatingNote(Point position = default) => CreationModule.UpdateCreating(position);

        /// <summary>
        /// 完成音符创建
        /// </summary>
        public void FinishCreatingNote() => CreationModule.FinishCreating();

        /// <summary>
        /// 取消音符创建
        /// </summary>
        public void CancelCreatingNote() => CreationModule.CancelCreating();
        #endregion

        #region 音符拖拽相关
        /// <summary>
        /// 开始音符拖拽
        /// </summary>
        public void StartNoteDrag(NoteViewModel note, Point startPoint) => DragModule.StartDrag(note, startPoint);

        /// <summary>
        /// 更新音符拖拽
        /// </summary>
        public void UpdateNoteDrag(Point currentPoint, Point startPoint) => DragModule.UpdateDrag(currentPoint);

        /// <summary>
        /// 结束音符拖拽
        /// </summary>
        public void EndNoteDrag() => DragModule.EndDrag();
        #endregion

        #region 音符调整大小相关
        /// <summary>
        /// 开始音符调整大小
        /// </summary>
        public void StartNoteResize(Point position, NoteViewModel note, ResizeHandle handle) => ResizeModule.StartResize(position, note, handle);

        /// <summary>
        /// 更新音符调整大小
        /// </summary>
        public void UpdateNoteResize(Point currentPosition) => ResizeModule.UpdateResize(currentPosition);

        /// <summary>
        /// 结束音符调整大小
        /// </summary>
        public void EndNoteResize() => ResizeModule.EndResize();

        /// <summary>
        /// 获取位置处的调整大小句柄
        /// </summary>
        public ResizeHandle GetResizeHandleAtPosition(Point position, NoteViewModel note) => ResizeModule.GetResizeHandleAtPosition(position, note);
        #endregion

        #region 音符选择相关
        /// <summary>
        /// 获取位置处的音符
        /// </summary>
        public NoteViewModel? GetNoteAtPosition(Point position) => SelectionModule.GetNoteAtPosition(position, CurrentTrackNotes, TimeToPixelScale, KeyHeight);
        #endregion

        #region 事件曲线绘制相关
        /// <summary>
        /// 开始绘制事件曲线
        /// </summary>
        public void StartDrawingEventCurve(Point startPoint, double canvasHeight)
        {
            EventCurveDrawingModule.StartDrawing(startPoint, CurrentEventType, CurrentCCNumber, canvasHeight);
        }

        /// <summary>
        /// 更新事件曲线绘制
        /// </summary>
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
        /// 获取位置处的事件值
        /// </summary>
        public int GetEventValueAtPosition(Point position, double canvasHeight)
        {
            return _eventCurveCalculationService.YToValue(position.Y, canvasHeight, CurrentEventType, CurrentCCNumber);
        }

        /// <summary>
        /// 获取事件值的Y坐标位置
        /// </summary>
        public double GetYPositionForEventValue(int value, double canvasHeight)
        {
            return _eventCurveCalculationService.ValueToY(value, canvasHeight, CurrentEventType, CurrentCCNumber);
        }
        #endregion

        #region 工具方法
        /// <summary>
        /// 吸附到网格
        /// </summary>
        public MusicalFraction SnapToGrid(MusicalFraction time) => Toolbar.SnapToGrid(time);

        /// <summary>
        /// 吸附到网格时间
        /// </summary>
        public double SnapToGridTime(double timeValue) => Toolbar.SnapToGridTime(timeValue);

        /// <summary>
        /// 检查是否为黑键
        /// </summary>
        public bool IsBlackKey(int midiNote) => Calculations.IsBlackKey(midiNote);

        /// <summary>
        /// 获取音符名称
        /// </summary>


        /// <summary>
        /// 添加音符
        /// </summary>
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

        /// <summary>
        /// 添加音符（时间值版本）
        /// </summary>
        public void AddNote(int pitch, double startTime, double duration = -1, int velocity = 100)
        {
            var startPosition = MusicalFraction.FromDouble(startTime);
            var noteDuration = duration < 0 ? Toolbar.UserDefinedNoteDuration : MusicalFraction.FromDouble(duration);
            AddNote(pitch, startPosition, noteDuration, velocity);
        }
        #endregion

        #region 工具命令
        /// <summary>
        /// 选择铅笔工具
        /// </summary>
        [RelayCommand] private void SelectPencilTool() => Toolbar.SelectPencilTool();

        /// <summary>
        /// 选择选择工具
        /// </summary>
        [RelayCommand] private void SelectSelectionTool() => Toolbar.SelectSelectionTool();

        /// <summary>
        /// 选择橡皮擦工具
        /// </summary>
        [RelayCommand] private void SelectEraserTool() => Toolbar.SelectEraserTool();

        /// <summary>
        /// 选择剪切工具
        /// </summary>
        [RelayCommand] private void SelectCutTool() => Toolbar.SelectCutTool();

        /// <summary>
        /// 切换音符时长下拉菜单
        /// </summary>
        [RelayCommand]
        private void ToggleNoteDurationDropDown() => Toolbar.ToggleNoteDurationDropDown();

        /// <summary>
        /// 选择音符时长
        /// </summary>
        [RelayCommand]
        private void SelectNoteDuration(NoteDurationOption option) => Toolbar.SelectNoteDuration(option);

        /// <summary>
        /// 应用自定义分数
        /// </summary>
        [RelayCommand]
        private void ApplyCustomFraction() => Toolbar.ApplyCustomFraction();

        /// <summary>
        /// 全选
        /// </summary>
        [RelayCommand] private void SelectAll() => SelectionModule.SelectAll(CurrentTrackNotes);

        /// <summary>
        /// 切换事件视图
        /// </summary>
        [RelayCommand]
        private void ToggleEventView() => Toolbar.ToggleEventView();

        /// <summary>
        /// 切换事件类型选择器
        /// </summary>
        [RelayCommand]
        private void ToggleEventTypeSelector()
        {
            IsEventTypeSelectorOpen = !IsEventTypeSelectorOpen;
        }

        /// <summary>
        /// 选择事件类型
        /// </summary>
        [RelayCommand]
        private void SelectEventType(EventType eventType)
        {
            CurrentEventType = eventType;
            IsEventTypeSelectorOpen = false;
        }

        /// <summary>
        /// 设置CC号
        /// </summary>
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
    }
}