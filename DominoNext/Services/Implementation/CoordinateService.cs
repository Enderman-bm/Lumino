using Avalonia;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor;
using System;

namespace Lumino.Services.Implementation
{   
    /// <summary>
    /// 坐标转换服务实现类。
    /// 主要用于在MVVM架构中，将音符等音乐数据（如时间、音高）与界面像素坐标进行双向转换。
    /// 该服务被ViewModel（如NoteViewModel）调用，实现数据到界面坐标的映射，
    /// 是连接数据层与UI渲染的桥梁，确保音符编辑、选区、滚动等操作的准确性。
    /// </summary>
    public class CoordinateService : ICoordinateService
    {
        /// <summary>
        /// 根据Y坐标和琴键高度，计算对应的MIDI音高（Pitch）。
        /// 用于将界面上的Y坐标转换为音符的音高。
        /// </summary>
        public int GetPitchFromY(double y, double keyHeight)
        {
            var keyIndex = (int)(y / keyHeight);
            return Math.Max(0, Math.Min(127, 127 - keyIndex));
        }

        /// <summary>
        /// 根据X坐标和时间缩放比例，计算对应的时间值。
        /// 用于将界面上的X坐标转换为音符的起始时间。
        /// </summary>
        public double GetTimeFromX(double x, double timeToPixelScale)
        {
            // 修复：确保x=0时返回的时间值也是0，避免任何浮点精度问题
            if (Math.Abs(x) < 1e-10)
            {
                return 0;
            }
            
            var result = Math.Max(0, x / timeToPixelScale);
            return result;
        }

        /// <summary>
        /// 根据音符ViewModel、时间缩放比例和琴键高度，获取音符在界面上的坐标点。
        /// 用于将音符数据转换为界面上的位置。
        /// </summary>
        public Point GetPositionFromNote(NoteViewModel note, double timeToPixelScale, double keyHeight)
        {
            return new Point(
                note.GetX(timeToPixelScale),
                note.GetY(keyHeight)
            );
        }

        /// <summary>
        /// 根据音符ViewModel、时间缩放比例和琴键高度，获取音符在界面上的矩形区域。
        /// 用于音符的界面绘制和选区判断。
        /// </summary>
        public Rect GetNoteRect(NoteViewModel note, double timeToPixelScale, double keyHeight)
        {
            var x = note.GetX(timeToPixelScale);
            var y = note.GetY(keyHeight);
            var width = note.GetWidth(timeToPixelScale);
            var height = note.GetHeight(keyHeight);

            return new Rect(x, y, width, height);
        }

        /// <summary>
        /// 根据Y坐标、琴键高度和垂直滚动偏移量，计算对应的MIDI音高。
        /// 用于支持滚动后的坐标转换。
        /// </summary>
        public int GetPitchFromY(double y, double keyHeight, double verticalScrollOffset)
        {
            // 将屏幕坐标转换为世界坐标
            var worldY = y + verticalScrollOffset;
            return GetPitchFromY(worldY, keyHeight);
        }

        /// <summary>
        /// 根据X坐标、时间缩放比例和水平滚动偏移量，计算对应的时间值。
        /// 用于支持滚动后的时间坐标转换。
        /// </summary>
        public double GetTimeFromX(double x, double timeToPixelScale, double scrollOffset)
        {
            // 将屏幕坐标转换为世界坐标
            var worldX = x + scrollOffset;
            return GetTimeFromX(worldX, timeToPixelScale);
        }

        /// <summary>
        /// 根据音符ViewModel、时间缩放比例、琴键高度和滚动偏移量，获取音符在界面上的坐标点。
        /// 用于滚动后的音符位置计算。
        /// </summary>
        public Point GetPositionFromNote(NoteViewModel note, double timeToPixelScale, double keyHeight, double scrollOffset, double verticalScrollOffset)
        {
            var worldPosition = GetPositionFromNote(note, timeToPixelScale, keyHeight);
            // 转换为屏幕坐标
            return new Point(
                worldPosition.X - scrollOffset,
                worldPosition.Y - verticalScrollOffset
            );
        }

        /// <summary>
        /// 根据音符ViewModel、时间缩放比例、琴键高度和滚动偏移量，获取音符在界面上的矩形区域。
        /// 用于滚动后的音符绘制和选区判断。
        /// </summary>
        public Rect GetNoteRect(NoteViewModel note, double timeToPixelScale, double keyHeight, double scrollOffset, double verticalScrollOffset)
        {
            var worldRect = GetNoteRect(note, timeToPixelScale, keyHeight);
            // 转换为屏幕坐标
            return new Rect(
                worldRect.X - scrollOffset,
                worldRect.Y - verticalScrollOffset,
                worldRect.Width,
                worldRect.Height
            );
        }
    }
}