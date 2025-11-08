// 文件用途：
// TrackPanel 是一个用户控件类，负责处理轨道面板的用户交互。
// 使用限制：
// 1. 仅供 Lumino 项目使用。
// 2. 修改此文件需经过代码审查，不然末影君锤爆你！

using System;
using Avalonia.Controls;
using Avalonia.Input;
using Lumino.ViewModels;
using EnderDebugger;

namespace Lumino.Views.Controls
{
    public partial class TrackPanel : UserControl
    {
        private static readonly EnderLogger _logger = new EnderLogger("TrackPanel");
        private static bool _isInitialized = false;

        public TrackPanel()
        {
            InitializeComponent();
            
            // 只在第一次初始化时输出日志
            if (!_isInitialized)
            {
                _logger.Info("Initialization", "轨道面板已初始化。");
                _isInitialized = true;
            }

            TrackBorder.Tapped += OnTrackBorderTapped;
        }

        private void OnTrackBorderTapped(object? sender, TappedEventArgs e)
        {
            _logger.Info("UserAction", "用户点击了轨道边框。");

            if (DataContext is TrackViewModel trackViewModel)
            {
                _logger.Info("DataBinding", "触发 SelectTrackCommand。");
                trackViewModel.SelectTrackCommand.Execute(null);
            }
        }

        private void OnOnionSkinButtonPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // 阻止事件冒泡，避免触发音轨选择
            e.Handled = true;
            _logger.Info("UserAction", "用户点击了洋葱皮按钮，事件已阻止冒泡。");
        }

        private void OnMuteButtonPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // 阻止事件冒泡，避免触发音轨选择
            e.Handled = true;
            _logger.Info("UserAction", "用户点击了静音按钮，事件已阻止冒泡。");
        }

        private void OnSoloButtonPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // 阻止事件冒泡，避免触发音轨选择
            e.Handled = true;
            _logger.Info("UserAction", "用户点击了独奏按钮，事件已阻止冒泡。");
        }

        private void OnOnionSkinButtonTapped(object? sender, TappedEventArgs e)
        {
            // 阻止Tapped事件冒泡，避免触发音轨选择
            e.Handled = true;
            _logger.Info("UserAction", "用户点击了洋葱皮按钮，Tapped事件已阻止冒泡。");
        }

        private void OnMuteButtonTapped(object? sender, TappedEventArgs e)
        {
            // 阻止Tapped事件冒泡，避免触发音轨选择
            e.Handled = true;
            _logger.Info("UserAction", "用户点击了静音按钮，Tapped事件已阻止冒泡。");
        }

        private void OnSoloButtonTapped(object? sender, TappedEventArgs e)
        {
            // 阻止Tapped事件冒泡，避免触发音轨选择
            e.Handled = true;
            _logger.Info("UserAction", "用户点击了独奏按钮，Tapped事件已阻止冒泡。");
        }

        private async void OnSettingsButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // 阻止事件冒泡，避免触发音轨选择
            e.Handled = true;

            if (DataContext is TrackViewModel vm)
            {
                try
                {
                    var wnd = new TrackSettingsWindow(vm);
                    var parent = this.VisualRoot as Window;
                    _logger.Info("UserAction", $"打开轨道设置: {vm.TrackNumber} - {vm.TrackName}");
                    if (parent != null)
                    {
                        await wnd.ShowDialog(parent);
                    }
                    else
                    {
                        // fallback to non-modal
                        wnd.Show();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("OpenSettings", ex.ToString());
                }
            }
        }
    }
}