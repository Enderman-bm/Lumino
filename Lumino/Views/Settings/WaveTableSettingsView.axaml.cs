using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Lumino.Views.Settings
{
    public partial class WaveTableSettingsView : UserControl
    {
        public WaveTableSettingsView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}