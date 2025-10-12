namespace Lumino.ViewModels.Editor.Enums
{
    /// <summary>
    /// 洋葱皮显示模式枚举
    /// </summary>
    public enum OnionSkinMode
    {
        /// <summary>
        /// 只显示上一个音轨（动态切换）
        /// </summary>
        PreviousTrack,

        /// <summary>
        /// 显示下一个音轨（动态切换）
        /// </summary>
        NextTrack,

        /// <summary>
        /// 显示全部音轨
        /// </summary>
        AllTracks,

        /// <summary>
        /// 显示指定音轨
        /// </summary>
        SpecifiedTracks
    }
}