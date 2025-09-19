using Avalonia.Controls;
using Lumino.ViewModels.Editor.Components;

namespace Lumino.Views.Controls
{
    /// <summary>
    /// �������û��ؼ�
    /// </summary>
    public partial class Toolbar : UserControl
    {
        /// <summary>
        /// ���캯��
        /// </summary>
        public Toolbar()
        {
            InitializeComponent();
        }

        /// <summary>
        /// ����ViewModel
        /// </summary>
        /// <param name="viewModel">������ViewModel</param>
        public void SetViewModel(ToolbarViewModel viewModel)
        {
            DataContext = viewModel;
        }
    }
}