using Avalonia.Controls;
using Lumino.ViewModels.Editor.Components;
using EnderDebugger;

namespace Lumino.Views.Controls
{
    /// <summary>
    /// 工具栏用户控件
    /// </summary>
    public partial class Toolbar : UserControl
    {
        private readonly EnderLogger _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        public Toolbar()
        {
            _logger = EnderLogger.Instance;
            _logger.Info("Toolbar", "Toolbar 控件构造函数被调用");
            InitializeComponent();
        }

        /// <summary>
        /// 设置ViewModel
        /// </summary>
        /// <param name="viewModel">工具栏ViewModel</param>
        public void SetViewModel(ToolbarViewModel viewModel)
        {
            _logger.Info("Toolbar", $"SetViewModel 被调用, ViewModel类型: {viewModel?.GetType().Name}");
            _logger.Info("Toolbar", $"ViewModel.SelectPencilToolCommand: {viewModel?.SelectPencilToolCommand != null}");
            DataContext = viewModel;
            _logger.Info("Toolbar", $"DataContext 已设置: {DataContext != null}");
        }
    }
}