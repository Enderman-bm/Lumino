using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using DominoNext.Services.Interfaces;
using DominoNext.ViewModels.Settings;
using DominoNext.Views.Settings;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// 对话框服务实现 - 符合MVVM原则的对话框操作封装
    /// </summary>
    public class DialogService : IDialogService
    {
        private readonly ISettingsService _settingsService;

        public DialogService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public async Task<bool> ShowSettingsDialogAsync()
        {
            try
            {
                var settingsViewModel = new SettingsWindowViewModel(_settingsService);
                var settingsWindow = new SettingsWindow
                {
                    DataContext = settingsViewModel
                };

                // 获取主窗口作为父窗口
                var result = await ShowDialogWithParentAsync(settingsWindow);
                
                // 检查设置是否有变更
                return settingsViewModel.HasUnsavedChanges == false; // 如果没有未保存的更改，说明用户保存了
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示设置对话框时发生错误: {ex.Message}");
                return false;
            }
        }

        public async Task<string?> ShowOpenFileDialogAsync(string title, string[]? filters = null)
        {
            try
            {
                var window = GetMainWindow();
                if (window?.StorageProvider == null)
                    return null;

                var options = new FilePickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false
                };

                if (filters != null && filters.Length > 0)
                {
                    options.FileTypeFilter = filters.Select(f => new FilePickerFileType(f)
                    {
                        Patterns = new[] { f }
                    }).ToArray();
                }

                var result = await window.StorageProvider.OpenFilePickerAsync(options);
                return result?.FirstOrDefault()?.Path.LocalPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示文件打开对话框时发生错误: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> ShowSaveFileDialogAsync(string title, string? defaultFileName = null, string[]? filters = null)
        {
            try
            {
                var window = GetMainWindow();
                if (window?.StorageProvider == null)
                    return null;

                var options = new FilePickerSaveOptions
                {
                    Title = title,
                    SuggestedFileName = defaultFileName
                };

                if (filters != null && filters.Length > 0)
                {
                    options.FileTypeChoices = filters.Select(f => new FilePickerFileType(f)
                    {
                        Patterns = new[] { f }
                    }).ToArray();
                }

                var result = await window.StorageProvider.SaveFilePickerAsync(options);
                return result?.Path.LocalPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示文件保存对话框时发生错误: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> ShowConfirmationDialogAsync(string title, string message)
        {
            try
            {
                var window = GetMainWindow();
                if (window == null)
                    return false;

                // 这里可以创建自定义的确认对话框，或使用系统对话框
                // 暂时返回true作为默认实现
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示确认对话框时发生错误: {ex.Message}");
                return false;
            }
        }

        public async Task ShowErrorDialogAsync(string title, string message)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"错误对话框 - {title}: {message}");
                // 这里可以实现自定义错误对话框
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示错误对话框时发生错误: {ex.Message}");
            }
        }

        public async Task ShowInfoDialogAsync(string title, string message)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"信息对话框 - {title}: {message}");
                // 这里可以实现自定义信息对话框
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示信息对话框时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取主窗口
        /// </summary>
        private Window? GetMainWindow()
        {
            if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }

        /// <summary>
        /// 以主窗口为父窗口显示对话框
        /// </summary>
        private async Task<object?> ShowDialogWithParentAsync(Window dialog)
        {
            var parentWindow = GetMainWindow();
            if (parentWindow != null)
            {
                await dialog.ShowDialog(parentWindow);
                return dialog.DataContext; // 返回DataContext作为结果
            }
            else
            {
                // 如果没有主窗口，就作为独立窗口显示
                dialog.Show();
                return null;
            }
        }
    }
}