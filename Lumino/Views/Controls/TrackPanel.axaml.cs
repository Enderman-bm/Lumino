// 文件用途：
// TrackPanel 是一个用户控件类，负责处理轨道面板的用户交互。
// 使用限制：
// 1. 仅供 Lumino 项目使用。
// 2. 修改此文件需经过代码审查，不然末影君锤爆你！

using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

        #region 右键菜单事件处理

        private async void OnContextMenuSettings(object? sender, RoutedEventArgs e)
        {
            _logger.Info("ContextMenu", "用户点击了轨道设置菜单项。");
            if (DataContext is TrackViewModel vm)
            {
                try
                {
                    var wnd = new TrackSettingsWindow(vm);
                    var parent = this.VisualRoot as Window;
                    _logger.Info("UserAction", $"从右键菜单打开轨道设置: {vm.TrackNumber} - {vm.TrackName}");
                    if (parent != null)
                    {
                        await wnd.ShowDialog(parent);
                    }
                    else
                    {
                        wnd.Show();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("OpenSettings", ex.ToString());
                }
            }
        }

        private void OnContextMenuMute(object? sender, RoutedEventArgs e)
        {
            _logger.Info("ContextMenu", "用户点击了静音菜单项。");
            if (DataContext is TrackViewModel vm)
            {
                vm.ToggleMuteCommand.Execute(null);
            }
        }

        private void OnContextMenuSolo(object? sender, RoutedEventArgs e)
        {
            _logger.Info("ContextMenu", "用户点击了独奏菜单项。");
            if (DataContext is TrackViewModel vm)
            {
                vm.ToggleSoloCommand.Execute(null);
            }
        }

        private void OnContextMenuOnionSkin(object? sender, RoutedEventArgs e)
        {
            _logger.Info("ContextMenu", "用户点击了洋葱皮菜单项。");
            if (DataContext is TrackViewModel vm)
            {
                vm.ToggleOnionSkinCommand.Execute(null);
            }
        }

        private void OnContextMenuDeleteTrack(object? sender, RoutedEventArgs e)
        {
            _logger.Info("ContextMenu", "用户点击了删除轨道菜单项。");
            if (DataContext is TrackViewModel vm)
            {
                // 不允许删除Conductor轨
                if (vm.IsConductorTrack)
                {
                    _logger.Warn("ContextMenu", "尝试删除Conductor轨，操作被阻止。");
                    return;
                }

                // 获取 TrackSelectorViewModel 以执行删除操作
                var trackSelector = GetTrackSelectorViewModel();
                if (trackSelector != null)
                {
                    _logger.Info("ContextMenu", $"正在删除轨道: {vm.TrackNumber} - {vm.TrackName}");
                    trackSelector.RemoveTrackCommand.Execute(vm);
                }
                else
                {
                    _logger.Warn("ContextMenu", "无法获取 TrackSelectorViewModel，删除操作失败。");
                }
            }
        }

        private void OnContextMenuAddTrack(object? sender, RoutedEventArgs e)
        {
            _logger.Info("ContextMenu", "用户点击了添加新轨道菜单项。");
            
            var trackSelector = GetTrackSelectorViewModel();
            if (trackSelector != null)
            {
                _logger.Info("ContextMenu", "正在添加新轨道。");
                trackSelector.AddTrackCommand.Execute(null);
            }
            else
            {
                _logger.Warn("ContextMenu", "无法获取 TrackSelectorViewModel，添加操作失败。");
            }
        }

        /// <summary>
        /// 获取 TrackSelectorViewModel 实例
        /// </summary>
        private TrackSelectorViewModel? GetTrackSelectorViewModel()
        {
            try
            {
                // 尝试通过父控件查找 TrackSelector
                var parent = this.Parent;
                while (parent != null)
                {
                    if (parent is ItemsControl itemsControl && 
                        itemsControl.DataContext is TrackSelectorViewModel trackSelectorVm)
                    {
                        return trackSelectorVm;
                    }
                    
                    if (parent.DataContext is TrackSelectorViewModel vm)
                    {
                        return vm;
                    }
                    
                    parent = parent.Parent;
                }
                
                // 尝试通过 VisualRoot 获取 MainWindow 的 ViewModel
                if (this.VisualRoot is Window mainWindow && 
                    mainWindow.DataContext is MainWindowViewModel mainVm)
                {
                    return mainVm.TrackSelector;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("GetTrackSelector", ex.ToString());
            }
            
            return null;
        }

        #endregion
    }
}