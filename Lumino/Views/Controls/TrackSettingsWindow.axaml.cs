using Avalonia.Controls;
using System;
using Lumino.ViewModels;
using EnderDebugger;

namespace Lumino.Views.Controls
{
    public partial class TrackSettingsWindow : Window
    {
        private static readonly EnderLogger _logger = new EnderLogger("TrackSettingsWindow");

        public TrackSettingsWindow()
        {
            InitializeComponent();
        }

        public TrackSettingsWindow(TrackViewModel vm) : this()
        {
            DataContext = vm ?? throw new ArgumentNullException(nameof(vm));
        }

        private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _logger.Info("UserAction", "Track settings canceled");
            Close();
        }

        private void OnOk(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // 保存由绑定自动更新 ViewModel
            if (DataContext is TrackViewModel vm)
            {
                _logger.Info("UserAction", $"Save track settings: {vm.TrackNumber} - {vm.TrackName}");
            }

            Close();
        }
    }
}
