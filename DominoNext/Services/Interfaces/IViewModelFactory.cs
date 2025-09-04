using DominoNext.ViewModels.Editor;

namespace DominoNext.Services.Interfaces
{
    /// <summary>
    /// ViewModel工厂服务接口 - 用于创建ViewModel实例
    /// </summary>
    public interface IViewModelFactory
    {
        /// <summary>
        /// 创建PianoRollViewModel实例
        /// </summary>
        /// <returns>配置好依赖的PianoRollViewModel实例</returns>
        PianoRollViewModel CreatePianoRollViewModel();

        /// <summary>
        /// 创建其他ViewModel实例的方法可以在这里扩展
        /// </summary>
    }
}