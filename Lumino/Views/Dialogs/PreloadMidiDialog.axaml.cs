using System;
using Avalonia.Controls;
using Lumino.ViewModels.Dialogs;
using Lumino.Services.Interfaces;
using Avalonia.Threading;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace Lumino.Views.Dialogs
{
    public partial class PreloadMidiDialog : Window
    {
        public PreloadDialogResult ResultChoice { get; private set; } = PreloadDialogResult.Cancel;

    // View is intentionally lightweight: animation and visual state are driven by the ViewModel.

        public PreloadMidiDialog()
        {
            InitializeComponent();
        }

        public PreloadMidiDialog(PreloadMidiViewModel viewModel) : this()
        {
            DataContext = viewModel;

            // 当 ViewModel 请求关闭时，关闭窗口并保留结果
            viewModel.RequestClose += (res) =>
            {
                ResultChoice = res;
                Close();
            };

            // 视图无需管理动画定时器；所有视觉动画由 ViewModel 的绑定属性驱动。
        }
        
    }
}
