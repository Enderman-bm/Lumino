using System;
using Avalonia;
using DominoNext.Services.Interfaces;
using DominoNext.ViewModels.Editor;

namespace DominoNext.Services.Implementation
{
    public class CoordinateService : ICoordinateService
    {
        public int GetPitchFromY(double y, double keyHeight)
        {
            var keyIndex = (int)(y / keyHeight);
            return Math.Max(0, Math.Min(127, 127 - keyIndex));
        }

        public double GetTimeFromX(double x, double zoom, double pixelsPerTick)
        {
            // 修复：确保x=0时返回的时间值也是0，避免任何浮点精度问题
            if (Math.Abs(x) < 1e-10)
            {
                return 0;
            }
            
            var result = Math.Max(0, x / (pixelsPerTick * zoom));
            return result;
        }

        public Point GetPositionFromNote(NoteViewModel note, double zoom, double pixelsPerTick, double keyHeight)
        {
            return new Point(
                note.GetX(zoom, pixelsPerTick),
                note.GetY(keyHeight)
            );
        }

        public Rect GetNoteRect(NoteViewModel note, double zoom, double pixelsPerTick, double keyHeight)
        {
            var x = note.GetX(zoom, pixelsPerTick);
            var y = note.GetY(keyHeight);
            var width = note.GetWidth(zoom, pixelsPerTick);
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

        public double GetTimeFromX(double x, double zoom, double pixelsPerTick, double scrollOffset)
        {
            // 将屏幕坐标转换为世界坐标
            var worldX = x + scrollOffset;
            return GetTimeFromX(worldX, zoom, pixelsPerTick);
        }

        public Point GetPositionFromNote(NoteViewModel note, double zoom, double pixelsPerTick, double keyHeight, double scrollOffset, double verticalScrollOffset)
        {
            var worldPosition = GetPositionFromNote(note, zoom, pixelsPerTick, keyHeight);
            // 转换为屏幕坐标
            return new Point(
                worldPosition.X - scrollOffset,
                worldPosition.Y - verticalScrollOffset
            );
        }

        public Rect GetNoteRect(NoteViewModel note, double zoom, double pixelsPerTick, double keyHeight, double scrollOffset, double verticalScrollOffset)
        {
            var worldRect = GetNoteRect(note, zoom, pixelsPerTick, keyHeight);
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