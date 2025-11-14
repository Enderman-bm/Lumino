// 文件用途：
// SettingsWindow 是一个设置窗口类，允许用户加载和保存应用程序设置。
// 使用限制：
// 1. 仅供 Lumino 项目使用。
// 2. 修改此文件需经过代码审查。

using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Lumino.ViewModels.Settings;
using Lumino.Models.Settings;
using Lumino.Services.Implementation;
using EnderWaveTableAccessingParty;
using EnderDebugger;

namespace Lumino.Views.Settings
{
    public partial class SettingsWindow : Window
    {
        private WaveTableManager _waveTableManager;
        private readonly EnderLogger _logger;
        private SettingsWindowViewModel? _viewModel;

        public SettingsWindow()
        {
            InitializeComponent();
            _waveTableManager = new WaveTableManager();
            _logger = new EnderLogger("SettingsWindow");
            _logger.Info("Initialization", "[SettingsWindow] 设置窗口已初始化");

            // 延迟加载 ViewModel
            this.Loaded += async (sender, e) =>
            {
                await System.Threading.Tasks.Task.Delay(50); // 让 UI 先渲染
                _viewModel = DataContext as SettingsWindowViewModel;
            };
        }

        // 从文件加载设置的按钮事件
        private async void LoadSettingsFromFile_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsWindowViewModel viewModel)
            {
                try
                {
                    _logger.Info("UserAction", "[SettingsWindow] 用户尝试加载设置文件");

                    // 打开文件选择对话框
                    var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "选择设置文件",
                        FileTypeFilter = new[] { new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } } },
                        AllowMultiple = false
                    });

                    if (files.Count > 0)
                    {
                        string filePath = files[0].Path.LocalPath;
                        _logger.Info("FileOperation", $"[SettingsWindow] 加载文件: {filePath}");

                        using (var reader = new StreamReader(filePath))
                        {
                            string content = await reader.ReadToEndAsync();
                            viewModel.LoadSettings();
                            _logger.Info("Success", "[SettingsWindow] 设置文件加载成功");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Exception", $"[SettingsWindow] 加载设置文件时发生错误: {ex.Message}");
                }
            }
        }

        // 保存设置到文件的按钮事件
        private async void SaveSettingsToFile_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsWindowViewModel viewModel)
            {
                try
                {
                    _logger.Info("UserAction", "[SettingsWindow] 用户尝试保存设置文件");

                    var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "保存设置文件",
                        FileTypeChoices = new[] { new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } } },
                        SuggestedFileName = "settings.json"
                    });

                    if (file != null)
                    {
                        string filePath = file.Path.LocalPath;
                        viewModel.Settings.SaveToFile(filePath);
                        _logger.Info("Success", $"[SettingsWindow] 设置文件保存成功: {filePath}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Exception", $"[SettingsWindow] 保存设置文件时发生错误: {ex.Message}");
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _waveTableManager?.Dispose();
        }
    }
}