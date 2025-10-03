using Avalonia;
using Lumino.ViewModels.Editor;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// 坐标转换服务接口 - 性能优化版本
    /// 使用 TimeToPixelScale 替代 zoom 和 pixelsPerTick 参数组合
    /// </summary>
    public interface ICoordinateService
    {
        // 基础坐标转换方法
        int GetPitchFromY(double y, double keyHeight);
        double GetTimeFromX(double x, double timeToPixelScale);
        Point GetPositionFromNote(NoteViewModel note, double timeToPixelScale, double keyHeight);
        Rect GetNoteRect(NoteViewModel note, double timeToPixelScale, double keyHeight);

        // 支持滚动偏移量的方法
        int GetPitchFromY(double y, double keyHeight, double verticalScrollOffset);
        double GetTimeFromX(double x, double timeToPixelScale, double scrollOffset);
        Point GetPositionFromNote(NoteViewModel note, double timeToPixelScale, double keyHeight, double scrollOffset, double verticalScrollOffset);
        Rect GetNoteRect(NoteViewModel note, double timeToPixelScale, double keyHeight, double scrollOffset, double verticalScrollOffset);
    }
}