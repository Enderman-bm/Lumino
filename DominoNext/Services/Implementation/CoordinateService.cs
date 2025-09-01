using Avalonia;
using DominoNext.Services.Interfaces;
using DominoNext.ViewModels.Editor;
using System;

namespace DominoNext.Services.Implementation
{
    public class CoordinateService : ICoordinateService
    {
        public int GetPitchFromY(double y, double keyHeight)
        {
            var keyIndex = (int)(y / keyHeight);
            return Math.Max(0, Math.Min(127, 127 - keyIndex));
        }

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

        public Point GetPositionFromNote(NoteViewModel note, double timeToPixelScale, double keyHeight)
        {
            return new Point(
                note.GetX(timeToPixelScale),
                note.GetY(keyHeight)
            );
        }

        public Rect GetNoteRect(NoteViewModel note, double timeToPixelScale, double keyHeight)
        {
            var x = note.GetX(timeToPixelScale);
            var y = note.GetY(keyHeight);
            var width = note.GetWidth(timeToPixelScale);
            var height = note.GetHeight(keyHeight);

            return new Rect(x, y, width, height);
        }

        // 添加支持滚动偏移量的重载方法
        public int GetPitchFromY(double y, double keyHeight, double verticalScrollOffset)
        {
            // 将屏幕坐标转换为世界坐标
            var worldY = y + verticalScrollOffset;
            return GetPitchFromY(worldY, keyHeight);
        }

        public double GetTimeFromX(double x, double timeToPixelScale, double scrollOffset)
        {
            // 将屏幕坐标转换为世界坐标
            var worldX = x + scrollOffset;
            return GetTimeFromX(worldX, timeToPixelScale);
        }

        public Point GetPositionFromNote(NoteViewModel note, double timeToPixelScale, double keyHeight, double scrollOffset, double verticalScrollOffset)
        {
            var worldPosition = GetPositionFromNote(note, timeToPixelScale, keyHeight);
            // 转换为屏幕坐标
            return new Point(
                worldPosition.X - scrollOffset,
                worldPosition.Y - verticalScrollOffset
            );
        }

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