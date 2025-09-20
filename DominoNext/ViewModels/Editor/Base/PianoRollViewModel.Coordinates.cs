using System;
using Avalonia;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Components;
using Lumino.ViewModels.Editor.Interfaces;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel的坐标转换相关功能
    /// </summary>
    public partial class PianoRollViewModel : ICoordinateProvider
    {
        #region 坐标转换委托方法
        /// <summary>
        /// 从Y坐标获取音高
        /// </summary>
        public int GetPitchFromY(double y) => Coordinates.GetPitchFromY(y);

        /// <summary>
        /// 从X坐标获取时间
        /// </summary>
        public double GetTimeFromX(double x) => Coordinates.GetTimeFromX(x);

        /// <summary>
        /// 从音符获取位置
        /// </summary>
        public Point GetPositionFromNote(NoteViewModel note) => Coordinates.GetPositionFromNote(note);

        /// <summary>
        /// 获取音符的矩形区域
        /// </summary>
        public Rect GetNoteRect(NoteViewModel note) => Coordinates.GetNoteRect(note);

        /// <summary>
        /// 从屏幕Y坐标获取音高
        /// </summary>
        public int GetPitchFromScreenY(double screenY) => Coordinates.GetPitchFromScreenY(screenY);

        /// <summary>
        /// 从屏幕X坐标获取时间
        /// </summary>
        public double GetTimeFromScreenX(double screenX) => Coordinates.GetTimeFromScreenX(screenX);

        /// <summary>
        /// 从音符获取屏幕位置
        /// </summary>
        public Point GetScreenPositionFromNote(NoteViewModel note) => Coordinates.GetScreenPositionFromNote(note);

        /// <summary>
        /// 获取音符的屏幕矩形区域
        /// </summary>
        public Rect GetScreenNoteRect(NoteViewModel note) => Coordinates.GetScreenNoteRect(note);
        #endregion

        #region ICoordinateProvider接口实现
        /// <summary>
        /// 将时间转换为像素坐标
        /// </summary>
        public double TimeToPixels(double time) => Coordinates.TimeToPixels(time);

        /// <summary>
        /// 将像素坐标转换为时间
        /// </summary>
        public double PixelsToTime(double pixels) => Coordinates.PixelsToTime(pixels);

        /// <summary>
        /// 将音高转换为Y坐标
        /// </summary>
        public double PitchToY(int pitch) => Coordinates.PitchToY(pitch);

        /// <summary>
        /// 将Y坐标转换为音高
        /// </summary>
        public int YToPitch(double y) => Coordinates.YToPitch(y);

        /// <summary>
        /// 获取视口边界
        /// </summary>
        public Rect GetViewportBounds() => Coordinates.GetViewportBounds();

        /// <summary>
        /// 检查点是否在视口内
        /// </summary>
        public bool IsPointInViewport(Point point) => Coordinates.IsPointInViewport(point);

        /// <summary>
        /// 获取音符在屏幕上的可见区域
        /// </summary>
        public Rect? GetNoteVisibleBounds(NoteViewModel note) => Coordinates.GetNoteVisibleBounds(note);

        /// <summary>
        /// 将屏幕坐标转换为逻辑坐标
        /// </summary>
        public Point ScreenToLogical(Point screenPoint) => Coordinates.ScreenToLogical(screenPoint);

        /// <summary>
        /// 将逻辑坐标转换为屏幕坐标
        /// </summary>
        public Point LogicalToScreen(Point logicalPoint) => Coordinates.LogicalToScreen(logicalPoint);
        #endregion
    }
}