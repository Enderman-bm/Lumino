using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Lumino.ViewModels;

namespace Lumino.Views
{
    /// <summary>
    /// ImageToMidi View - 图片转MIDI工具窗口
    /// </summary>
    public partial class ImageToMidiView : Window
    {
        public ImageToMidiView()
        {
            InitializeComponent();

            // 订阅关闭事件
            if (DataContext is ImageToMidiViewModel viewModel)
            {
                viewModel.CloseRequested += (sender, e) => Close();
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // 窗口关闭时释放资源
        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (DataContext is ImageToMidiViewModel viewModel)
            {
                viewModel.Dispose();
            }
        }
    }
}
