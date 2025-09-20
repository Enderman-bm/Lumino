using System;

namespace Lumino.ViewModels.Editor.Interfaces
{
    /// <summary>
    /// 可滚动视图模型接口
    /// </summary>
    public interface IScrollableViewModel
    {
        /// <summary>
        /// 水平滚动偏移量
        /// </summary>
        double HorizontalScrollOffset { get; set; }

        /// <summary>
        /// 垂直滚动偏移量
        /// </summary>
        double VerticalScrollOffset { get; set; }

        /// <summary>
        /// 最大水平滚动偏移量
        /// </summary>
        double MaxHorizontalScrollOffset { get; }

        /// <summary>
        /// 最大垂直滚动偏移量
        /// </summary>
        double MaxVerticalScrollOffset { get; }

        /// <summary>
        /// 视口宽度
        /// </summary>
        double ViewportWidth { get; set; }

        /// <summary>
        /// 视口高度
        /// </summary>
        double ViewportHeight { get; set; }
    }
}