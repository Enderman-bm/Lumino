using DominoNext.Services.Interfaces;
using DominoNext.ViewModels.Editor;
using DominoNext.ViewModels.Settings;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// ViewModel工厂服务实现 - 负责创建和配置ViewModel实例
    /// 集中管理ViewModel的依赖注入，确保所有实例都正确配置
    /// </summary>
    public class ViewModelFactory : IViewModelFactory
    {
        private readonly ICoordinateService _coordinateService;
        private readonly ISettingsService _settingsService;

        /// <summary>
        /// 初始化ViewModel工厂
        /// </summary>
        /// <param name="coordinateService">坐标转换服务</param>
        /// <param name="settingsService">设置服务</param>
        public ViewModelFactory(ICoordinateService coordinateService, ISettingsService settingsService)
        {
            _coordinateService = coordinateService;
            _settingsService = settingsService;
        }

        public PianoRollViewModel CreatePianoRollViewModel()
        {
            return new PianoRollViewModel(_coordinateService);
        }

        public SettingsWindowViewModel CreateSettingsWindowViewModel()
        {
            return new SettingsWindowViewModel(_settingsService);
        }
    }
}