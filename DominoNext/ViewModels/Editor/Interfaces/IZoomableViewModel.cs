using System;

namespace Lumino.ViewModels.Editor.Interfaces
{
    /// <summary>
    /// 可缩放视图模型接口
    /// </summary>
    public interface IZoomableViewModel
    {
        /// <summary>
        /// 水平缩放级别
        /// </summary>
        double HorizontalZoomLevel { get; set; }

        /// <summary>
        /// 垂直缩放级别
        /// </summary>
        double VerticalZoomLevel { get; set; }

        /// <summary>
        /// 时间到像素缩放比例
        /// </summary>
        double TimeToPixelScale { get; }

        /// <summary>
        /// 时间到像素缩放比例（四舍五入）
        /// </summary>
        double TimeToPixelScaleRounded { get; }

        /// <summary>
        /// 像素到时间缩放比例
        /// </summary>
        double PixelToTimeScale { get; }

        /// <summary>
        /// 音符高度
        /// </summary>
        double KeyHeight { get; }
    }
}