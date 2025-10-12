using Lumino.ViewModels.Editor.Enums;

namespace Lumino.ViewModels.Editor.Components
{
    /// <summary>
    /// 洋葱皮模式选项
    /// </summary>
    public class OnionSkinModeOption
    {
        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 模式值
        /// </summary>
        public OnionSkinMode Mode { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public OnionSkinModeOption(string displayName, OnionSkinMode mode)
        {
            DisplayName = displayName;
            Mode = mode;
        }
    }
}