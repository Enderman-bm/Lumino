using Lumino.ViewModels.Editor;
using Lumino.ViewModels.Settings;
using Lumino.ViewModels;
using Lumino.Models.Music;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// ViewModel工厂服务接口 - 用于创建ViewModel实例
    /// 统一管理ViewModel的创建和依赖注入，保持代码的可测试性和可维护性
    /// 符合MVVM最佳实践和依赖注入原则
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
        /// 创建NoteViewModel实例
        /// </summary>
        /// <param name="note">音符数据模型，如果为null则创建默认音符</param>
        /// <returns>配置好依赖的NoteViewModel实例</returns>
        NoteViewModel CreateNoteViewModel(Note? note = null);

        /// <summary>
        /// 创建AudioAnalysisViewModel实例
        /// </summary>
        /// <param name="dialogService">对话框服务</param>
        /// <returns>配置好依赖的AudioAnalysisViewModel实例</returns>
        AudioAnalysisViewModel CreateAudioAnalysisViewModel(IDialogService dialogService);

        /// <summary>
        /// 其他ViewModel创建方法可以在这里扩展
        /// 例如：CreateProjectViewModel, CreateMidiEditorViewModel等
        ///
        /// 设计原则：
        /// 1. 所有ViewModel都应该通过工厂创建，确保依赖正确注入
        /// 2. 工厂方法应该隐藏具体的依赖配置细节
        /// 3. 支持单元测试时的Mock依赖注入
        /// 4. 遵循单一职责原则，只负责创建ViewModel
        /// </summary>
    }
}