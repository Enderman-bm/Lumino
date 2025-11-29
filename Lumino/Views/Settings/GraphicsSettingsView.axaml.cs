using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Lumino.Views.Settings
{
    public partial class GraphicsSettingsView : UserControl
    {
        public GraphicsSettingsView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}