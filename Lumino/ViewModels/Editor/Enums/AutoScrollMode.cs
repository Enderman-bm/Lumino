namespace Lumino.ViewModels.Editor.Enums
{
    /// <summary>
    /// 播放头自动滚动模式
    /// 参照 lumino-rs 的 AutoScrollMode 实现
    /// </summary>
    public enum AutoScrollMode
    {
        /// <summary>
        /// 模式1：固定指示线到左侧，钢琴卷帘自动左移
        /// 指示线固定在 fixedIndicatorPosition 位置，滚动偏移量随播放位置调整
        /// </summary>
        FixedIndicatorLeft,

        /// <summary>
        /// 模式2：指示线移动，到达右侧时翻页滚动
        /// 指示线随播放位置移动，到达右边界时触发自动滚动
        /// </summary>
        ScrollingIndicator,

        /// <summary>
        /// 关闭自动滚动
        /// </summary>
        Off
    }
}
