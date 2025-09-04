using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using DominoNext.Constants;
using DominoNext.Services.Interfaces;
using DominoNext.Views.Dialogs;
using DominoNext.Views.Settings;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// 对话框服务实现 - 符合MVVM原则和开发规范的对话框操作封装
    /// 负责统一管理各种对话框的显示，确保错误处理和日志记录的一致性
    /// </summary>
    public class DialogService : IDialogService
    {
        #region 依赖服务
        
        private readonly IViewModelFactory _viewModelFactory;
        private readonly ILoggingService _loggingService;
        
        #endregion

        #region 构造函数
        
        /// <summary>
        /// 初始化对话框服务
        /// </summary>
        /// <param name="viewModelFactory">ViewModel工厂服务，用于创建对话框的ViewModel</param>
        /// <param name="loggingService">日志服务，用于记录错误和调试信息</param>
        public DialogService(IViewModelFactory viewModelFactory, ILoggingService loggingService)
        {
            _viewModelFactory = viewModelFactory ?? throw new ArgumentNullException(nameof(viewModelFactory));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }
        
        #endregion

        #region 公共方法实现

        public async Task<bool> ShowSettingsDialogAsync()
        {
            try
            {
                _loggingService.LogInfo("开始显示设置对话框", "DialogService");
                
                // 通过工厂创建ViewModel，保持单一职责
                var settingsViewModel = _viewModelFactory.CreateSettingsWindowViewModel();
                var settingsWindow = new SettingsWindow
                {
                    DataContext = settingsViewModel
                };

                // 安全地显示对话框
                var result = await ShowDialogWithParentAsync(settingsWindow);
                
                // 检查设置是否有变更
                var hasChanges = settingsViewModel.HasUnsavedChanges == false;
                
                _loggingService.LogInfo($"设置对话框关闭，有变更：{hasChanges}", "DialogService");
                return hasChanges;
            }
            catch (Exception ex)
            {
                // 统一的异常处理 - 记录详细错误信息并返回安全的默认值
                _loggingService.LogException(ex, DialogConstants.SETTINGS_DIALOG_ERROR, "DialogService");
                return false;
            }
        }

        public async Task<string?> ShowOpenFileDialogAsync(string title, string[]? filters = null)
        {
            try
            {
                _loggingService.LogInfo($"显示文件打开对话框：{title}", "DialogService");
                
                var window = GetMainWindow();
                if (window?.StorageProvider == null)
                {
                    _loggingService.LogWarning("无法获取主窗口或存储提供程序", "DialogService");
                    return null;
                }

                var options = CreateFilePickerOpenOptions(title, filters);
                var result = await window.StorageProvider.OpenFilePickerAsync(options);
                var selectedPath = result?.FirstOrDefault()?.Path.LocalPath;
                
                _loggingService.LogInfo($"文件选择结果：{selectedPath ?? "无选择"}", "DialogService");
                return selectedPath;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, DialogConstants.FILE_DIALOG_ERROR, "DialogService");
                return null;
            }
        }

        public async Task<string?> ShowSaveFileDialogAsync(string title, string? defaultFileName = null, string[]? filters = null)
        {
            try
            {
                _loggingService.LogInfo($"显示文件保存对话框：{title}", "DialogService");
                
                var window = GetMainWindow();
                if (window?.StorageProvider == null)
                {
                    _loggingService.LogWarning("无法获取主窗口或存储提供程序", "DialogService");
                    return null;
                }

                var options = CreateFilePickerSaveOptions(title, defaultFileName, filters);
                var result = await window.StorageProvider.SaveFilePickerAsync(options);
                var selectedPath = result?.Path.LocalPath;
                
                _loggingService.LogInfo($"保存文件路径：{selectedPath ?? "无选择"}", "DialogService");
                return selectedPath;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, DialogConstants.FILE_DIALOG_ERROR, "DialogService");
                return null;
            }
        }

        public async Task<bool> ShowConfirmationDialogAsync(string title, string message)
        {
            try
            {
                _loggingService.LogInfo($"显示确认对话框：{title} - {message}", "DialogService");
                
                var confirmationDialog = new ConfirmationDialog
                {
                    Title = title,
                    Message = message
                };

                var result = await ShowDialogWithParentAsync(confirmationDialog);
                
                // 如果result是bool类型，直接返回；否则使用对话框的Result属性
                var confirmationResult = result is bool boolResult ? boolResult : confirmationDialog.Result;
                
                _loggingService.LogInfo($"确认对话框结果：{confirmationResult}", "DialogService");
                return confirmationResult;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, DialogConstants.CONFIRMATION_DIALOG_ERROR, "DialogService");
                
                // 发生错误时返回安全的默认值 - 避免意外的破坏性操作
                return DialogConstants.DEFAULT_CONFIRMATION_RESULT;
            }
        }

        public async Task ShowErrorDialogAsync(string title, string message)
        {
            try
            {
                _loggingService.LogError($"错误对话框 - {title}: {message}", "DialogService");
                
                // TODO: 实现自定义错误对话框UI
                // 目前使用日志记录，后续可以创建专门的错误对话框View
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, DialogConstants.ERROR_DIALOG_ERROR, "DialogService");
            }
        }

        public async Task ShowInfoDialogAsync(string title, string message)
        {
            try
            {
                _loggingService.LogInfo($"信息对话框 - {title}: {message}", "DialogService");
                
                // TODO: 实现自定义信息对话框UI
                // 目前使用日志记录，后续可以创建专门的信息对话框View
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, DialogConstants.INFO_DIALOG_ERROR, "DialogService");
            }
        }

        #endregion

        #region 私有辅助方法

        /// <summary>
        /// 获取主窗口
        /// 提供统一的主窗口获取逻辑，避免代码重复
        /// </summary>
        /// <returns>主窗口实例，如果无法获取则返回null</returns>
        private Window? GetMainWindow()
        {
            try
            {
                if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    return desktop.MainWindow;
                }
                return null;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "获取主窗口时发生错误", "DialogService");
                return null;
            }
        }

        /// <summary>
        /// 以主窗口为父窗口显示对话框
        /// 统一的对话框显示逻辑，处理父窗口关联和异常情况
        /// </summary>
        /// <param name="dialog">要显示的对话框</param>
        /// <returns>对话框的返回结果</returns>
        private async Task<object?> ShowDialogWithParentAsync(Window dialog)
        {
            try
            {
                var parentWindow = GetMainWindow();
                if (parentWindow != null)
                {
                    // 以模态方式显示对话框
                    await dialog.ShowDialog(parentWindow);
                    
                    // 对于确认对话框，返回其Result属性
                    if (dialog is ConfirmationDialog confirmDialog)
                    {
                        return confirmDialog.Result;
                    }
                    
                    // 对于其他对话框，返回DataContext
                    return dialog.DataContext;
                }
                else
                {
                    // 如果没有主窗口，作为独立窗口显示
                    _loggingService.LogWarning("没有主窗口，对话框将作为独立窗口显示", "DialogService");
                    dialog.Show();
                    return null;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "显示对话框时发生错误", "DialogService");
                return null;
            }
        }

        /// <summary>
        /// 创建文件打开选项
        /// 封装文件打开对话框的配置逻辑，提高代码复用性
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <param name="filters">文件过滤器</param>
        /// <returns>配置好的文件选择选项</returns>
        private FilePickerOpenOptions CreateFilePickerOpenOptions(string title, string[]? filters)
        {
            var options = new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            };

            // 使用常量中定义的默认过滤器，避免硬编码
            var actualFilters = filters ?? DialogConstants.AllSupportedFilters;
            
            if (actualFilters.Length > 0)
            {
                options.FileTypeFilter = actualFilters.Select(filter => new FilePickerFileType(filter)
                {
                    Patterns = new[] { filter }
                }).ToArray();
            }

            return options;
        }

        /// <summary>
        /// 创建文件保存选项
        /// 封装文件保存对话框的配置逻辑，提高代码复用性
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <param name="defaultFileName">默认文件名</param>
        /// <param name="filters">文件过滤器</param>
        /// <returns>配置好的文件保存选项</returns>
        private FilePickerSaveOptions CreateFilePickerSaveOptions(string title, string? defaultFileName, string[]? filters)
        {
            var options = new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = defaultFileName
            };

            // 使用常量中定义的默认过滤器，避免硬编码
            var actualFilters = filters ?? DialogConstants.AllSupportedFilters;
            
            if (actualFilters.Length > 0)
            {
                options.FileTypeChoices = actualFilters.Select(filter => new FilePickerFileType(filter)
                {
                    Patterns = new[] { filter }
                }).ToArray();
            }

            return options;
        }

        #endregion
    }
}