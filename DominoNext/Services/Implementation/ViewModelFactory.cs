using DominoNext.Services.Interfaces;
using DominoNext.ViewModels.Editor;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// ViewModel工厂服务实现 - 负责创建和配置ViewModel实例
    /// </summary>
    public class ViewModelFactory : IViewModelFactory
    {
        private readonly ICoordinateService _coordinateService;

        public ViewModelFactory(ICoordinateService coordinateService)
        {
            _coordinateService = coordinateService;
        }

        public PianoRollViewModel CreatePianoRollViewModel()
        {
            return new PianoRollViewModel(_coordinateService);
        }
    }
}