using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Components;
using Lumino.ViewModels.Editor.Dialogs;
using Lumino.ViewModels.Editor.Enums;
using Lumino.Views.Dialogs;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel的对话框和UI相关功能
    /// </summary>
    public partial class PianoRollViewModel
    {
        #region 对话框显示方法
        /// <summary>
        /// 显示确认对话框
        /// </summary>
        private async Task<bool> ShowConfirmationDialogAsync(string title, string message, string? confirmText = null, string? cancelText = null)
        {
            try
            {
                var dialog = new ConfirmationDialog
                {
                    Title = title,
                    Message = message
                };

                var result = await dialog.ShowDialog<bool>(GetMainWindow());
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示确认对话框失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 显示信息对话框
        /// </summary>
        private async Task ShowInfoDialogAsync(string title, string message, string? closeText = null)
        {
            try
            {
                await ShowInfoDialogAsync(title, message, closeText);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示信息对话框失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示错误对话框
        /// </summary>
        private async Task ShowErrorDialogAsync(string title, string message, string? closeText = null)
        {
            try
            {
                await ShowErrorDialogAsync(title, message, closeText);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示错误对话框失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示警告对话框
        /// </summary>
        private async Task ShowWarningDialogAsync(string title, string message, string? primaryText = null, string? closeText = null)
        {
            try
            {
                await ShowConfirmationDialogAsync(title, message, primaryText, closeText);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示警告对话框失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示输入对话框
        /// </summary>
        private async Task<string?> ShowInputDialogAsync(string title, string message, string defaultValue = "", string? confirmText = null, string? cancelText = null)
        {
            try
            {
                var textBox = new TextBox
                {
                    Text = defaultValue,
                    Watermark = "请输入...",
                    MaxLength = 100
                };

                var dialog = new ConfirmationDialog
                {
                    Title = title,
                    Message = message
                };

                var result = await dialog.ShowDialog<bool>(GetMainWindow());
                return result ? textBox.Text : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示输入对话框失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 显示选择对话框
        /// </summary>
        private async Task<T?> ShowSelectionDialogAsync<T>(string title, string message, IEnumerable<T> options, T? defaultValue = default, string? confirmText = null, string? cancelText = null) where T : class
        {
            try
            {
                var dialog = new ConfirmationDialog
                {
                    Title = title,
                    Message = message
                };

                var result = await dialog.ShowDialog<bool>(GetMainWindow());
                return result ? (defaultValue ?? options.FirstOrDefault()) : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示选择对话框失败: {ex.Message}");
                return null;
            }
        }
        #endregion

        #region 文件对话框方法
        /// <summary>
        /// 显示打开文件对话框
        /// </summary>
        private async Task<string[]?> ShowOpenFileDialogAsync(string title, IReadOnlyList<FilePickerFileType>? fileTypes = null, bool allowMultiple = false)
        {
            try
            {
                var topLevel = GetTopLevel();
                if (topLevel == null) return null;

                var options = new FilePickerOpenOptions
                {
                    Title = title,
                    FileTypeFilter = fileTypes,
                    AllowMultiple = allowMultiple
                };

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
                return files?.Select(f => f.Path.LocalPath).ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示打开文件对话框失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 显示保存文件对话框
        /// </summary>
        private async Task<string?> ShowSaveFileDialogAsync(string title, IReadOnlyList<FilePickerFileType>? fileTypes = null, string? defaultFileName = null)
        {
            try
            {
                var topLevel = GetTopLevel();
                if (topLevel == null) return null;

                var options = new FilePickerSaveOptions
                {
                    Title = title,
                    FileTypeChoices = fileTypes,
                    SuggestedFileName = defaultFileName
                };

                var file = await topLevel.StorageProvider.SaveFilePickerAsync(options);
                return file?.Path.LocalPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示保存文件对话框失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 显示打开文件夹对话框
        /// </summary>
        private async Task<string[]?> ShowOpenFolderDialogAsync(string title, bool allowMultiple = false)
        {
            try
            {
                var topLevel = GetTopLevel();
                if (topLevel == null) return null;

                var options = new FolderPickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = allowMultiple
                };

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
                return folders?.Select(f => f.Path.LocalPath).ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示打开文件夹对话框失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取文件类型过滤器
        /// </summary>
        private IReadOnlyList<FilePickerFileType> GetMidiFileTypes()
        {
            return new[]
            {
                new FilePickerFileType("MIDI文件")
                {
                    Patterns = new[] { "*.mid", "*.midi" },
                    MimeTypes = new[] { "audio/midi", "audio/x-midi" }
                },
                new FilePickerFileType("所有文件")
                {
                    Patterns = new[] { "*.*" }
                }
            };
        }

        /// <summary>
        /// 获取项目文件类型过滤器
        /// </summary>
        private IReadOnlyList<FilePickerFileType> GetProjectFileTypes()
        {
            return new[]
            {
                new FilePickerFileType("Lumino项目文件")
                {
                    Patterns = new[] { "*.lumino" },
                    MimeTypes = new[] { "application/json" }
                },
                new FilePickerFileType("所有文件")
                {
                    Patterns = new[] { "*.*" }
                }
            };
        }

        /// <summary>
        /// 获取音频文件类型过滤器
        /// </summary>
        private IReadOnlyList<FilePickerFileType> GetAudioFileTypes()
        {
            return new[]
            {
                new FilePickerFileType("WAV文件")
                {
                    Patterns = new[] { "*.wav" },
                    MimeTypes = new[] { "audio/wav" }
                },
                new FilePickerFileType("MP3文件")
                {
                    Patterns = new[] { "*.mp3" },
                    MimeTypes = new[] { "audio/mpeg" }
                },
                new FilePickerFileType("所有音频文件")
                {
                    Patterns = new[] { "*.wav", "*.mp3", "*.ogg", "*.flac" }
                },
                new FilePickerFileType("所有文件")
                {
                    Patterns = new[] { "*.*" }
                }
            };
        }
        #endregion

        #region 进度对话框方法
        /// <summary>
        /// 显示进度对话框
        /// </summary>
        private async Task<IProgressDialog?> ShowProgressDialogAsync(string title, string message, bool isIndeterminate = true, int maxValue = 100)
        {
            try
            {
                var progressDialog = new ProgressDialog
                {
                    Title = title,
                    Message = message,
                    IsIndeterminate = isIndeterminate,
                    Maximum = maxValue,
                    Value = 0
                };

                await progressDialog.ShowAsync();
                return progressDialog;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示进度对话框失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 更新进度
        /// </summary>
        private void UpdateProgress(IProgressDialog? progressDialog, int value, string? message = null)
        {
            if (progressDialog == null) return;
            
            try
            {
                progressDialog.Value = value;
                if (!string.IsNullOrEmpty(message))
                {
                    progressDialog.Message = message;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新进度失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 关闭进度对话框
        /// </summary>
        private void CloseProgressDialog(IProgressDialog? progressDialog)
        {
            if (progressDialog == null) return;
            
            try
            {
                progressDialog.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"关闭进度对话框失败: {ex.Message}");
            }
        }
        #endregion

        #region UI状态管理
        /// <summary>
        /// 设置UI状态
        /// </summary>
        private void SetUIState(bool isEnabled, string? statusMessage = null)
        {
            try
            {
                IsUIEnabled = isEnabled;
                
                if (!string.IsNullOrEmpty(statusMessage))
                {
                    StatusMessage = statusMessage;
                }
                
                // 更新光标
                CurrentCursor = isEnabled ? new Cursor(StandardCursorType.Arrow) : new Cursor(StandardCursorType.Wait);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置UI状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示状态消息
        /// </summary>
        private void ShowStatusMessage(string message, int durationMilliseconds = 3000)
        {
            try
            {
                StatusMessage = message;
                
                // 延迟清除状态消息
                _ = Task.Delay(durationMilliseconds).ContinueWith(_ =>
                {
                    if (StatusMessage == message)
                    {
                        StatusMessage = string.Empty;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示状态消息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示临时通知
        /// </summary>
        private void ShowNotification(string message, NotificationType type = NotificationType.Info, int durationMilliseconds = 3000)
        {
            try
            {
                // TODO: 实现通知显示逻辑
                // 这里应该调用通知服务或UI框架的通知功能
                System.Diagnostics.Debug.WriteLine($"通知 [{type}]: {message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示通知失败: {ex.Message}");
            }
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 获取顶级窗口
        /// </summary>
        private TopLevel? GetTopLevel()
        {
            try
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    return desktop.MainWindow;
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取顶级窗口失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取主窗口
        /// </summary>
        private Window? GetMainWindow()
        {
            try
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    return desktop.MainWindow;
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取主窗口失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 在主线程上执行操作
        /// </summary>
        private async Task ExecuteOnUIThread(Action action)
        {
            if (action == null) return;
            
            try
            {
                await Dispatcher.UIThread.InvokeAsync(action);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"在UI线程上执行操作失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 延迟执行UI操作
        /// </summary>
        private async Task DelayUIExecute(Action action, int delayMilliseconds = 100)
        {
            if (action == null) return;
            
            await Task.Delay(delayMilliseconds);
            await ExecuteOnUIThread(action);
        }
        #endregion

        #region 通知类型枚举
        /// <summary>
        /// 通知类型
        /// </summary>
        public enum NotificationType
        {
            Info,
            Success,
            Warning,
            Error
        }
        #endregion

        #region UI状态属性
        /// <summary>
        /// UI是否启用
        /// </summary>
        public bool IsUIEnabled
        {
            get => _isUIEnabled;
            set => SetProperty(ref _isUIEnabled, value);
        }
        private bool _isUIEnabled = true;

        /// <summary>
        /// 状态消息
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value ?? string.Empty);
        }
        private string _statusMessage = string.Empty;

        /// <summary>
        /// 当前光标
        /// </summary>
        public Cursor CurrentCursor
        {
            get => _currentCursor;
            set => SetProperty(ref _currentCursor, value);
        }
        private Cursor _currentCursor = new Cursor(StandardCursorType.Arrow);
        #endregion
    }
}