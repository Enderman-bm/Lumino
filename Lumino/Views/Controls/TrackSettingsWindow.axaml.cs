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
            // 初始化 MIDI 通道下拉
            InitializeMidiChannelCombo();
            // 如果已有 DataContext（TrackViewModel），设置选中项
            if (DataContext is TrackViewModel tvm)
            {
                MidiChannelCombo.SelectedItem = tvm.MidiChannel;
            }
        }

        public TrackSettingsWindow(ViewModels.TrackSelectorViewModel selector) : this()
        {
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            // 将下拉框的 DataContext 指向 selector（XAML 中 Items 绑定为 {Binding Tracks}）
            TrackCombo.DataContext = selector;
            TrackCombo.SelectedItem = selector.SelectedTrack ?? (selector.Tracks.Count > 0 ? selector.Tracks[0] : null);

            // 默认窗口 DataContext 指向所选 TrackViewModel，以便下方的绑定生效
            if (TrackCombo.SelectedItem is TrackViewModel tvm)
            {
                DataContext = tvm;
                InitializeMidiChannelCombo();
                MidiChannelCombo.SelectedItem = tvm.MidiChannel;
            }
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

        private void OnTrackSelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
        {
            // 当下拉选中变化时，把窗口 DataContext 切换到所选的 TrackViewModel
            if (TrackCombo.SelectedItem is TrackViewModel tvm)
            {
                DataContext = tvm;
                // 更新 Midi 下拉
                InitializeMidiChannelCombo();
                MidiChannelCombo.SelectedItem = tvm.MidiChannel;
            }
        }

        private void InitializeMidiChannelCombo()
        {
            // 构造可选项：-1 表示未指定，显示为“未指定”，其余为 0..15 对应 CH.1..CH.16
            var list = new System.Collections.Generic.List<int>();
            list.Add(-1);
            for (int i = 0; i < 16; i++) list.Add(i);
            MidiChannelCombo.ItemsSource = list;
        }

        private void OnMidiChannelChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
        {
            if (DataContext is TrackViewModel tvm && MidiChannelCombo.SelectedItem is int ch)
            {
                tvm.MidiChannel = ch;
            }
        }
    }
}
