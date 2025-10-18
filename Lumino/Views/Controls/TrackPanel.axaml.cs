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
        private readonly EnderLogger _logger;

        public TrackPanel()
        {
            InitializeComponent();
            _logger = new EnderLogger("TrackPanel");
            _logger.Info("Initialization", "[EnderDebugger][{DateTime.Now}][EnderLogger][TrackPanel] 轨道面板已初始化。");

            TrackBorder.Tapped += OnTrackBorderTapped;
        }

        private void OnTrackBorderTapped(object? sender, TappedEventArgs e)
        {
            _logger.Info("UserAction", "[EnderDebugger][{DateTime.Now}][EnderLogger][TrackPanel] 用户点击了轨道边框。");

            if (DataContext is TrackViewModel trackViewModel)
            {
                _logger.Info("DataBinding", "[EnderDebugger][{DateTime.Now}][EnderLogger][TrackPanel] 触发 SelectTrackCommand。");
                trackViewModel.SelectTrackCommand.Execute(null);
            }
        }

        private void OnOnionSkinButtonPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // 阻止事件冒泡，避免触发音轨选择
            e.Handled = true;
            _logger.Info("UserAction", "[EnderDebugger][{DateTime.Now}][EnderLogger][TrackPanel] 用户点击了洋葱皮按钮，事件已阻止冒泡。");
        }

        private void OnMuteButtonPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // 阻止事件冒泡，避免触发音轨选择
            e.Handled = true;
            _logger.Info("UserAction", "[EnderDebugger][{DateTime.Now}][EnderLogger][TrackPanel] 用户点击了静音按钮，事件已阻止冒泡。");
        }

        private void OnSoloButtonPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // 阻止事件冒泡，避免触发音轨选择
            e.Handled = true;
            _logger.Info("UserAction", "[EnderDebugger][{DateTime.Now}][EnderLogger][TrackPanel] 用户点击了独奏按钮，事件已阻止冒泡。");
        }

        private void OnOnionSkinButtonTapped(object? sender, TappedEventArgs e)
        {
            // 阻止Tapped事件冒泡，避免触发音轨选择
            e.Handled = true;
            _logger.Info("UserAction", "[EnderDebugger][{DateTime.Now}][EnderLogger][TrackPanel] 用户点击了洋葱皮按钮，Tapped事件已阻止冒泡。");
        }

        private void OnMuteButtonTapped(object? sender, TappedEventArgs e)
        {
            // 阻止Tapped事件冒泡，避免触发音轨选择
            e.Handled = true;
            _logger.Info("UserAction", "[EnderDebugger][{DateTime.Now}][EnderLogger][TrackPanel] 用户点击了静音按钮，Tapped事件已阻止冒泡。");
        }

        private void OnSoloButtonTapped(object? sender, TappedEventArgs e)
        {
            // 阻止Tapped事件冒泡，避免触发音轨选择
            e.Handled = true;
            _logger.Info("UserAction", "[EnderDebugger][{DateTime.Now}][EnderLogger][TrackPanel] 用户点击了独奏按钮，Tapped事件已阻止冒泡。");
        }
    }
}