namespace Lumino.ViewModels.Editor.Enums
{
    /// <summary>
    /// 洋葱皮模式选项类
    /// </summary>
    public class OnionSkinModeOption
    {
        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 模式枚举值
        /// </summary>
        public OnionSkinMode Mode { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="displayName">显示名称</param>
        /// <param name="mode">模式枚举值</param>
        public OnionSkinModeOption(string displayName, OnionSkinMode mode)
        {
            DisplayName = displayName;
            Mode = mode;
        }
    }
}