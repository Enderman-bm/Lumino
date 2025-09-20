namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// 编辑器状态枚举
    /// </summary>
    public enum EditorState
    {
        /// <summary>
        /// 就绪状态
        /// </summary>
        Ready,

        /// <summary>
        /// 编辑状态
        /// </summary>
        Editing,

        /// <summary>
        /// 播放状态
        /// </summary>
        Playing,

        /// <summary>
        /// 录音状态
        /// </summary>
        Recording,

        /// <summary>
        /// 选择状态
        /// </summary>
        Selecting,

        /// <summary>
        /// 拖拽状态
        /// </summary>
        Dragging,

        /// <summary>
        /// 调整大小状态
        /// </summary>
        Resizing
    }
}