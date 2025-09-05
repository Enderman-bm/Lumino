using DominoNext.Models.Music;

namespace DominoNext.ViewModels.Editor.Models
{
    /// <summary>
    /// 音符时值选项模型。
    /// 用于在编辑器中表示可选的音符时值（如四分音符、八分音符等），包含名称、时值和图标。
    /// </summary>
    public class NoteDurationOption
    {
        /// <summary>
        /// 选项名称（如“四分音符”、“八分音符”等）。
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 音符时值（使用 MusicalFraction 表示，如 1/4 表示四分音符）。
        /// </summary>
        public MusicalFraction Duration { get; set; }
        /// <summary>
        /// 图标资源路径或标识，用于在界面上显示该时值的图标。
        /// </summary>
        public string Icon { get; set; }
        /// <summary>
        /// 构造函数，初始化音符时值选项。
        /// </summary>
        /// <param name="name">选项名称</param>
        /// <param name="duration">音符时值（MusicalFraction 类型）</param>
        /// <param name="icon">显示什么图标（如四分音符）</param>
        public NoteDurationOption(string name, MusicalFraction duration, string icon)
        {
            Name = name;
            Duration = duration;
            Icon = icon;
        }
    }
}