using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DominoNext.ViewModels.Settings;
using DominoNext.Models.Settings;
using DominoNext.Services.Implementation;
using EnderWaveTableAccessingParty;
using EnderDebugger;

namespace DominoNext.Views.Settings
{
    public partial class SettingsWindow : Window
    {
        private WaveTableManager _waveTableManager;
        private readonly EnderLogger _logger;

        public SettingsWindow()
        {
            InitializeComponent();
            _waveTableManager = new WaveTableManager();
            _logger = new EnderLogger("SettingsWindow");
        }

        // 从文件加载设置的按钮事件
        private async void LoadSettingsFromFile_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsWindowViewModel viewModel)
            {
                try
                {
                    // 打开文件选择对话框
                    var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "选择设置文件",
                        FileTypeFilter = new[] { new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } } },
                        AllowMultiple = false
                    });

                    if (files.Count > 0)
                    {
                        var file = files[0];
                        var filePath = file.TryGetLocalPath();

                        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                        {
                            // 使用SettingsModel的自定义路径加载方法
                            viewModel.Settings.LoadFromFile(filePath);

                            // 重新加载当前选择
                            viewModel.UpdateCurrentSelections();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "LoadSettings", "从文件加载设置失败");
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
                    // 打开文件保存对话框
                    var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "保存设置文件",
                        FileTypeChoices = new[] { new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } } },
                        DefaultExtension = "json",
                        SuggestedFileName = "settings.json"
                    });

                    if (file != null)
                    {
                        var filePath = file.TryGetLocalPath();

                        if (!string.IsNullOrEmpty(filePath))
                        {
                            // 使用SettingsModel的自定义路径保存方法
                            viewModel.Settings.SaveToFile(filePath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "SaveSettings", "保存设置到文件失败");
                }
            }
        }

        // 测试C4音符的按钮事件
        private void TestC4Note_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsWindowViewModel viewModel && viewModel.Settings.EnableAudioFeedback)
            {
                try
                {
                    // C4音符的MIDI音高是60
                    _waveTableManager.PlayNote(60, 100);
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "PlayTestNote", "播放C4音符失败");
                }
            }
        }

        // 测试C5音符的按钮事件
        private void TestC5Note_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsWindowViewModel viewModel && viewModel.Settings.EnableAudioFeedback)
            {
                try
                {
                    // C5音符的MIDI音高是72
                    _waveTableManager.PlayNote(72, 100);
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "PlayTestNote", "播放C5音符失败");
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