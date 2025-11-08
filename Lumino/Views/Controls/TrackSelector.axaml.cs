using Avalonia.Controls;
using Avalonia.Interactivity;
using Lumino.ViewModels;
using Lumino.Views.Controls;

namespace Lumino.Views.Controls
{
    public partial class TrackSelector : UserControl
    {
        public TrackSelector()
        {
            InitializeComponent();
        }

        private async void OnOpenTrackSettings(object? sender, RoutedEventArgs e)
        {
            // 打开统一的轨道设置窗口，默认编辑当前选中的音轨
            if (DataContext is TrackSelectorViewModel selector)
            {
                try
                {
                    var wnd = new TrackSettingsWindow(selector);
                    var parent = this.VisualRoot as Window;
                    if (parent != null)
                        await wnd.ShowDialog(parent);
                    else
                        wnd.Show();
                }
                catch
                {
                    // ignore for now
                }
            }
        }
    }
}