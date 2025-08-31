using Avalonia;
using DominoNext.ViewModels.Editor;

namespace DominoNext.Services.Interfaces
{
    public interface ICoordinateService
    {
        int GetPitchFromY(double y, double keyHeight);
        double GetTimeFromX(double x, double zoom, double pixelsPerTick);
        Point GetPositionFromNote(NoteViewModel note, double zoom, double pixelsPerTick, double keyHeight);
        Rect GetNoteRect(NoteViewModel note, double zoom, double pixelsPerTick, double keyHeight);
        
        // 添加支持滚动偏移量的方法
        int GetPitchFromY(double y, double keyHeight, double verticalScrollOffset);
        double GetTimeFromX(double x, double zoom, double pixelsPerTick, double scrollOffset);
        Point GetPositionFromNote(NoteViewModel note, double zoom, double pixelsPerTick, double keyHeight, double scrollOffset, double verticalScrollOffset);
        Rect GetNoteRect(NoteViewModel note, double zoom, double pixelsPerTick, double keyHeight, double scrollOffset, double verticalScrollOffset);
    }
}