using System;
using Avalonia;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor;

namespace Lumino.ViewModels.Editor.Components
{
    /// <summary>
    /// 钢琴卷帘坐标转换组件 - 负责所有的坐标转换操作
    /// 符合单一职责原则，专注于坐标转换逻辑的封装
    /// </summary>
    public class PianoRollCoordinates
    {
        #region 依赖
        private readonly ICoordinateService _coordinateService;
        private readonly PianoRollCalculations _calculations;
        private readonly PianoRollViewport _viewport;
        #endregion

        #region 构造函数
        public PianoRollCoordinates(
            ICoordinateService coordinateService,
            PianoRollCalculations calculations,
            PianoRollViewport viewport)
        {
            _coordinateService = coordinateService ?? throw new ArgumentNullException(nameof(coordinateService));
            _calculations = calculations ?? throw new ArgumentNullException(nameof(calculations));
            _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        }
        #endregion

        #region 基础坐标转换方法
        /// <summary>
        /// 从Y坐标获取音高
        /// </summary>
        public int GetPitchFromY(double y)
        {
            return _coordinateService.GetPitchFromY(y, _calculations.KeyHeight);
        }

        /// <summary>
        /// 从X坐标获取时间
        /// </summary>
        public double GetTimeFromX(double x)
        {
            return _coordinateService.GetTimeFromX(x, _calculations.TimeToPixelScale);
        }

        /// <summary>
        /// 从音符获取位置
        /// </summary>
        public Point GetPositionFromNote(NoteViewModel note)
        {
            return _coordinateService.GetPositionFromNote(note, _calculations.TimeToPixelScale, _calculations.KeyHeight);
        }

        /// <summary>
        /// 获取音符的矩形区域
        /// </summary>
        public Rect GetNoteRect(NoteViewModel note)
        {
            return _coordinateService.GetNoteRect(note, _calculations.TimeToPixelScale, _calculations.KeyHeight);
        }
        #endregion

        #region 支持滚动偏移的坐标转换方法
        /// <summary>
        /// 从屏幕Y坐标获取音高（考虑垂直滚动）
        /// </summary>
        public int GetPitchFromScreenY(double screenY)
        {
            return _coordinateService.GetPitchFromY(screenY, _calculations.KeyHeight, _viewport.VerticalScrollOffset);
        }

        /// <summary>
        /// 从屏幕X坐标获取时间（考虑水平滚动）
        /// </summary>
        public double GetTimeFromScreenX(double screenX)
        {
            return _coordinateService.GetTimeFromX(screenX, _calculations.TimeToPixelScale, _viewport.CurrentScrollOffset);
        }

        /// <summary>
        /// 从音符获取屏幕位置（考虑滚动偏移）
        /// </summary>
        public Point GetScreenPositionFromNote(NoteViewModel note)
        {
            return _coordinateService.GetPositionFromNote(
                note, 
                _calculations.TimeToPixelScale, 
                _calculations.KeyHeight, 
                _viewport.CurrentScrollOffset, 
                _viewport.VerticalScrollOffset);
        }

        /// <summary>
        /// 获取音符的屏幕矩形区域（考虑滚动偏移）
        /// </summary>
        public Rect GetScreenNoteRect(NoteViewModel note)
        {
            return _coordinateService.GetNoteRect(
                note, 
                _calculations.TimeToPixelScale, 
                _calculations.KeyHeight, 
                _viewport.CurrentScrollOffset, 
                _viewport.VerticalScrollOffset);
        }
        #endregion

        #region 可见性检查
        /// <summary>
        /// 检查音符是否在当前可见区域内
        /// </summary>
        public bool IsNoteVisible(NoteViewModel note)
        {
            var noteRect = GetNoteRect(note);
            
            // 检查水平可见性
            var noteStartX = noteRect.X;
            var noteEndX = noteRect.X + noteRect.Width;
            var visibleStartX = _viewport.CurrentScrollOffset;
            var visibleEndX = _viewport.CurrentScrollOffset + _viewport.ViewportWidth;
            
            if (noteEndX < visibleStartX || noteStartX > visibleEndX)
                return false;
            
            // 检查垂直可见性
            var noteStartY = noteRect.Y;
            var noteEndY = noteRect.Y + noteRect.Height;
            var visibleStartY = _viewport.VerticalScrollOffset;
            var visibleEndY = _viewport.VerticalScrollOffset + _viewport.VerticalViewportSize;
            
            return !(noteEndY < visibleStartY || noteStartY > visibleEndY);
        }

        /// <summary>
        /// 获取当前可见区域的时间范围
        /// </summary>
        public (double startTime, double endTime) GetVisibleTimeRange()
        {
            var startTime = GetTimeFromScreenX(0);
            var endTime = GetTimeFromScreenX(_viewport.ViewportWidth);
            return (startTime, endTime);
        }

        /// <summary>
        /// 获取当前可见区域的音高范围
        /// </summary>
        public (int lowPitch, int highPitch) GetVisiblePitchRange()
        {
            var highPitch = GetPitchFromScreenY(0);
            var lowPitch = GetPitchFromScreenY(_viewport.VerticalViewportSize);
            return (lowPitch, highPitch);
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 将世界坐标转换为屏幕坐标
        /// </summary>
        public Point WorldToScreen(Point worldPosition)
        {
            return new Point(
                worldPosition.X - _viewport.CurrentScrollOffset,
                worldPosition.Y - _viewport.VerticalScrollOffset);
        }

        /// <summary>
        /// 将屏幕坐标转换为世界坐标
        /// </summary>
        public Point ScreenToWorld(Point screenPosition)
        {
            return new Point(
                screenPosition.X + _viewport.CurrentScrollOffset,
                screenPosition.Y + _viewport.VerticalScrollOffset);
        }

        /// <summary>
        /// 检查屏幕点是否在音符内
        /// </summary>
        public bool IsPointInNote(Point screenPoint, NoteViewModel note)
        {
            var screenRect = GetScreenNoteRect(note);
            return screenRect.Contains(screenPoint);
        }
        #endregion
    }
}