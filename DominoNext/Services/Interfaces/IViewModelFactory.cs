using DominoNext.ViewModels.Editor;
using DominoNext.ViewModels.Settings;

namespace DominoNext.Services.Interfaces
{
    /// <summary>
    /// ViewModel工厂服务接口 - 用于创建ViewModel实例
    /// 统一管理ViewModel的创建和依赖注入，保持代码的可测试性和可维护性
    /// </summary>
    public interface IViewModelFactory
    {
        /// <summary>
        /// 创建PianoRollViewModel实例
        /// </summary>
        /// <returns>配置好依赖的PianoRollViewModel实例</returns>
        PianoRollViewModel CreatePianoRollViewModel();

        /// <summary>
        /// 创建SettingsWindowViewModel实例
        /// </summary>
        /// <returns>配置好依赖的SettingsWindowViewModel实例</returns>
        SettingsWindowViewModel CreateSettingsWindowViewModel();

        /// <summary>
        /// 其他ViewModel创建方法可以在这里扩展
        /// 例如：CreateProjectViewModel, CreateMidiEditorViewModel等
        /// </summary>
    }
}