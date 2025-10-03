using Avalonia.Controls;
using Lumino.ViewModels.Editor.Components;

namespace Lumino.Views.Controls
{
    /// <summary>
    /// 工具栏用户控件
    /// </summary>
    public partial class Toolbar : UserControl
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public Toolbar()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 设置ViewModel
        /// </summary>
        /// <param name="viewModel">工具栏ViewModel</param>
        public void SetViewModel(ToolbarViewModel viewModel)
        {
            DataContext = viewModel;
        }
    }
}