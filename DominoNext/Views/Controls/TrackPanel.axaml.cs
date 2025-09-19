using Avalonia.Controls;
using Avalonia.Input;
using Lumino.ViewModels;

namespace Lumino.Views.Controls
{
    public partial class TrackPanel : UserControl
    {
        public TrackPanel()
        {
            InitializeComponent();
            TrackBorder.Tapped += OnTrackBorderTapped;
        }

        private void OnTrackBorderTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is TrackViewModel trackViewModel)
            {
                trackViewModel.SelectTrackCommand.Execute(null);
            }
        }
    }
}