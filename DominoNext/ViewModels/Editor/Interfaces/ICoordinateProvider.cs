using System;
using Avalonia;
using Lumino.ViewModels.Editor.Components;

namespace Lumino.ViewModels.Editor.Interfaces
{
    /// <summary>
    /// 坐标提供器接口
    /// </summary>
    public interface ICoordinateProvider
    {
        /// <summary>
        /// 将时间转换为像素坐标
        /// </summary>
        /// <param name="time">时间值</param>
        /// <returns>像素坐标</returns>
        double TimeToPixels(double time);

        /// <summary>
        /// 将像素坐标转换为时间
        /// </summary>
        /// <param name="pixels">像素坐标</param>
        /// <returns>时间值</returns>
        double PixelsToTime(double pixels);

        /// <summary>
        /// 将音高转换为Y坐标
        /// </summary>
        /// <param name="pitch">音高值</param>
        /// <returns>Y坐标</returns>
        double PitchToY(int pitch);

        /// <summary>
        /// 将Y坐标转换为音高
        /// </summary>
        /// <param name="y">Y坐标</param>
        /// <returns>音高值</returns>
        int YToPitch(double y);

        /// <summary>
        /// 获取视口边界
        /// </summary>
        /// <returns>视口边界矩形</returns>
        Rect GetViewportBounds();

        /// <summary>
        /// 检查点是否在视口内
        /// </summary>
        /// <param name="point">要检查的点</param>
        /// <returns>是否在视口内</returns>
        bool IsPointInViewport(Point point);

        /// <summary>
        /// 获取音符在屏幕上的可见区域
        /// </summary>
        /// <param name="note">音符视图模型</param>
        /// <returns>可见区域矩形，如果不可见则为null</returns>
        Rect? GetNoteVisibleBounds(NoteViewModel note);

        /// <summary>
        /// 将屏幕坐标转换为逻辑坐标
        /// </summary>
        /// <param name="screenPoint">屏幕坐标点</param>
        /// <returns>逻辑坐标点</returns>
        Point ScreenToLogical(Point screenPoint);

        /// <summary>
        /// 将逻辑坐标转换为屏幕坐标
        /// </summary>
        /// <param name="logicalPoint">逻辑坐标点</param>
        /// <returns>屏幕坐标点</returns>
        Point LogicalToScreen(Point logicalPoint);
    }
}