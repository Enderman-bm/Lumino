using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace EnderDebugger
{
    public partial class TestWindow : Window
    {
        public TestWindow()
        {
            Title = "EnderDebugger Test";
            Width = 400;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var textBlock = new TextBlock
            {
                Text = "EnderDebugger 已启动成功！",
                FontSize = 16,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Foreground = Brushes.Black
            };

            Background = Brushes.White;
            Content = textBlock;
        }
    }
}