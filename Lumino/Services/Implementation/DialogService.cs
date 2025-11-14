using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Lumino.Constants;
using Lumino.Services.Interfaces;
using Lumino.Views.Dialogs;
using Lumino.Views.Settings;
using Lumino.Views.Progress;
using Lumino.ViewModels.Progress;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// 对话框服务实现 - 遵循MVVM原则和编码规范的对话框服务封装
    /// 提供统一的各种弹窗管理，包括显示、确认、错误、日志记录等一体化
    /// </summary>
    public class DialogService : IDialogService
    {
        #region 私有字段
        
        private readonly IViewModelFactory _viewModelFactory;
        private readonly ILoggingService _loggingService;
        
        #endregion

        #region 构造函数
        
        /// <summary>
        /// 初始化对话框服务
        /// </summary>
        /// <param name="viewModelFactory">ViewModel工厂，用于创建对话框ViewModel</param>
        /// <param name="loggingService">日志服务，用于记录各种错误信息</param>
        public DialogService(IViewModelFactory viewModelFactory, ILoggingService loggingService)
        {
            _viewModelFactory = viewModelFactory ?? throw new ArgumentNullException(nameof(viewModelFactory));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }
        
        #endregion

        #region 接口方法实现

        public async Task<bool> ShowSettingsDialogAsync()
        {
            try
            {
                _loggingService.LogInfo("开始显示设置对话框", "DialogService");
                
                // 通过工厂创建ViewModel，保证值的一致性
                var settingsViewModel = _viewModelFactory.CreateSettingsWindowViewModel();
                var settingsWindow = new SettingsWindow
                {
                    DataContext = settingsViewModel
                };

                // 安全地显示对话框
                var result = await ShowDialogWithParentAsync(settingsWindow);
                
                // 检查设置是否有变更
                var hasChanges = settingsViewModel.HasUnsavedChanges == false;
                
                _loggingService.LogInfo($"设置对话框关闭，有变更: {hasChanges}", "DialogService");
                return hasChanges;
            }
            catch (Exception ex)
            {
                // 统一异常处理 - 记录详细错误信息并返回安全的默认值
                _loggingService.LogException(ex, DialogConstants.SETTINGS_DIALOG_ERROR, "DialogService");
                return false;
            }
        }

        public async Task<string?> ShowOpenFileDialogAsync(string title, string[]? filters = null)
        {
            try
            {
                _loggingService.LogInfo($"显示文件打开对话框: {title}", "DialogService");
                
                var window = GetMainWindow();
                if (window?.StorageProvider == null)
                {
                    _loggingService.LogWarning("无法获取主窗口或存储提供程序", "DialogService");
                    return null;
                }

                var options = CreateFilePickerOpenOptions(title, filters);
                var result = await window.StorageProvider.OpenFilePickerAsync(options);
                var selectedPath = result?.FirstOrDefault()?.Path.LocalPath;
                
                _loggingService.LogInfo($"文件选择结果: {selectedPath ?? "未选择"}", "DialogService");
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
                _loggingService.LogInfo($"显示文件保存对话框: {title}", "DialogService");
                
                var window = GetMainWindow();
                if (window?.StorageProvider == null)
                {
                    _loggingService.LogWarning("无法获取主窗口或存储提供程序", "DialogService");
                    return null;
                }

                var options = CreateFilePickerSaveOptions(title, defaultFileName, filters);
                var result = await window.StorageProvider.SaveFilePickerAsync(options);
                var selectedPath = result?.Path.LocalPath;
                
                _loggingService.LogInfo($"保存文件路径: {selectedPath ?? "未选择"}", "DialogService");
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
                _loggingService.LogInfo($"显示确认对话框: {title} - {message}", "DialogService");
                
                var confirmationDialog = new ConfirmationDialog
                {
                    Title = title,
                    Message = message
                };

                var result = await ShowDialogWithParentAsync(confirmationDialog);
                
                // 如果result是bool类型直接返回，否则使用对话框的Result属性
                var confirmationResult = result is bool boolResult ? boolResult : confirmationDialog.Result;
                
                _loggingService.LogInfo($"确认对话框结果: {confirmationResult}", "DialogService");
                return confirmationResult;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, DialogConstants.CONFIRMATION_DIALOG_ERROR, "DialogService");
                
                // 出现错误时返回安全的默认值 - 避免意外的破坏性操作
                return DialogConstants.DEFAULT_CONFIRMATION_RESULT;
            }
        }

        public async Task ShowErrorDialogAsync(string title, string message)
        {
            try
            {
                _loggingService.LogError($"错误对话框 - {title}: {message}", "DialogService");
                
                // TODO: 实现自定义错误对话框UI
                // 目前使用日志记录，未来需要开发专门的错误对话框View
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
                // 目前使用日志记录，未来需要开发专门的信息对话框View
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, DialogConstants.INFO_DIALOG_ERROR, "DialogService");
            }
        }

        public async Task ShowLoadingDialogAsync(string message)
        {
            try
            {
                _loggingService.LogInfo($"显示加载中对话框: {message}", "DialogService");
                
                // TODO: 实现自定义加载中对话框UI
                // 当前使用日志记录，未来需要开发专门的加载中对话框View
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, DialogConstants.LOADING_DIALOG_ERROR, "DialogService");
            }
        }

        public async Task CloseLoadingDialogAsync()
        {
            try
            {
                _loggingService.LogInfo($"关闭加载中对话框", "DialogService");
                
                // TODO: 实现关闭加载中对话框UI
                // 当前使用日志记录，未来需要开发专门的加载中对话框View
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, DialogConstants.LOADING_DIALOG_ERROR, "DialogService");
            }
        }

        public async Task<ProgressWindow> ShowProgressDialogAsync(string title, bool canCancel = false)
        {
            try
            {
                _loggingService.LogInfo($"显示进度窗口: {title}, 可取消: {canCancel}", "DialogService");
                
                var progressViewModel = new ProgressViewModel(title, canCancel);
                var progressWindow = new ProgressWindow(progressViewModel);
                
                var parentWindow = GetMainWindow();
                if (parentWindow != null)
                {
                    // 设置窗口位置为父窗口中心
                    progressWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    
                    // 在Avalonia中，我们需要在Show之前设置位置
                    var parentX = parentWindow.Position.X;
                    var parentY = parentWindow.Position.Y;
                    var parentWidth = (int)parentWindow.Width;
                    var parentHeight = (int)parentWindow.Height;
                    var windowWidth = (int)progressWindow.Width;
                    var windowHeight = (int)progressWindow.Height;
                    
                    progressWindow.Position = new Avalonia.PixelPoint(
                        parentX + (parentWidth - windowWidth) / 2,
                        parentY + (parentHeight - windowHeight) / 2
                    );
                }
                
                // 使用非模态方式显示进度窗口
                progressWindow.Show();
                
                // 确保窗口已经显示
                await Task.Delay(50);
                
                return progressWindow;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "显示进度窗口时发生错误", "DialogService");
                throw;
            }
        }

        public async Task CloseProgressDialogAsync(ProgressWindow progressWindow)
        {
            try
            {
                if (progressWindow != null)
                {
                    _loggingService.LogInfo("关闭进度窗口", "DialogService");
                    
                    // 在UI线程中关闭窗口
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        progressWindow.Close();
                    });
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "关闭进度窗口时发生错误", "DialogService");
            }
        }

        public async Task<T> RunWithProgressAsync<T>(string title, 
            Func<IProgress<(double Progress, string Status)>, CancellationToken, Task<T>> task, 
            bool canCancel = false)
        {
            ProgressWindow? progressWindow = null;
            try
            {
                // 显示进度窗口
                progressWindow = await ShowProgressDialogAsync(title, canCancel);
                
                // 创建进度报告器
                var progress = new Progress<(double Progress, string Status)>((update) =>
                {
                    progressWindow.UpdateProgress(update.Progress, update.Status);
                });
                
                // 获取取消令牌
                var cancellationToken = CancellationToken.None;
                if (canCancel && progressWindow.DataContext is ProgressViewModel viewModel)
                {
                    cancellationToken = viewModel.CancellationToken;
                }
                
                // 执行任务
                var result = await task(progress, cancellationToken);
                
                // 显示完成状态
                if (progressWindow.DataContext is ProgressViewModel vm)
                {
                    vm.Complete();
                }
                
                return result;
            }
            catch (OperationCanceledException)
            {
                _loggingService.LogInfo($"任务被取消: {title}", "DialogService");
                throw;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, $"执行带进度的任务时发生错误: {title}", "DialogService");
                
                // 显示错误状态
                if (progressWindow?.DataContext is ProgressViewModel vm)
                {
                    vm.SetError(ex.Message);
                }
                
                throw;
            }
            finally
            {
                // 延迟关闭，让用户看到完成或错误状态
                if (progressWindow != null)
                {
                    await Task.Delay(1000);
                    await CloseProgressDialogAsync(progressWindow);
                }
            }
        }

        public async Task RunWithProgressAsync(string title, 
            Func<IProgress<(double Progress, string Status)>, CancellationToken, Task> task, 
            bool canCancel = false)
        {
            await RunWithProgressAsync(title, async (progress, cancellationToken) =>
            {
                await task(progress, cancellationToken);
                return true; // 返回一个虚拟值
            }, canCancel);
        }

        public async Task<PreloadDialogResult> ShowPreloadMidiDialogAsync(string fileName, long fileSize)
        {
            try
            {
                _loggingService.LogInfo($"显示MIDI预加载对话框: {fileName} ({fileSize} bytes)", "DialogService");

                // 使用 MVVM 的 PreloadMidiViewModel 来驱动对话框
                var vm = new Lumino.ViewModels.Dialogs.PreloadMidiViewModel(fileName, fileSize);
                var preloadDialog = new Lumino.Views.Dialogs.PreloadMidiDialog(vm)
                {
                    Title = $"Lumino - 准备加载：{fileName}"
                };

                // 在显示对话框前启动由 ViewModel 驱动的入场动画
                try
                {
                    vm.StartIntroAnimation();
                }
                catch { }

                var result = await ShowDialogWithParentAsync(preloadDialog);

                // 对话框关闭后停止动画（以防遗留）
                try
                {
                    vm.StopIntroAnimation();
                }
                catch { }

                return preloadDialog.ResultChoice;
            }
            catch (System.Exception ex)
            {
                _loggingService.LogException(ex, "显示MIDI预加载对话框时发生错误", "DialogService");
                return PreloadDialogResult.Cancel;
            }
        }

        public async Task<(PreloadDialogResult Choice, T? Result)> ShowPreloadAndRunAsync<T>(string fileName, long fileSize,
            Func<IProgress<(double Progress, string Status)>, CancellationToken, Task<T>> task,
            bool canCancel = false)
        {
            try
            {
                _loggingService.LogInfo($"显示MIDI预加载对话框并在其中运行任务: {fileName} ({fileSize} bytes)", "DialogService");

                var vm = new Lumino.ViewModels.Dialogs.PreloadMidiViewModel(fileName, fileSize);
                var preloadDialog = new Lumino.Views.Dialogs.PreloadMidiDialog(vm)
                {
                    Title = $"Lumino - 准备加载：{fileName}"
                };

                // Start intro animation
                try { vm.StartIntroAnimation(); } catch { }

                var resultTcs = new TaskCompletionSource<(PreloadDialogResult, T?)>();

                // Handle user cancel/reselect before loading
                void OnRequestClose(PreloadDialogResult r)
                {
                    // If user canceled/reselected before pressing Load, set result and close dialog
                    if (r == PreloadDialogResult.Cancel || r == PreloadDialogResult.Reselect)
                    {
                        try { resultTcs.TrySetResult((r, default)); } catch { }
                        try { preloadDialog.Close(); } catch { }
                    }
                }

                vm.RequestClose += OnRequestClose;

                // When Load is requested, start the provided task and keep the dialog open; update VM progress
                vm.LoadRequested += async () =>
                {
                    _loggingService.LogInfo($"Preload dialog: LoadRequested for {fileName}", "DialogService");
                    // prepare cancellation
                    var cts = canCancel ? new CancellationTokenSource() : null;
                    vm.SetCancellationSource(cts);

                    var progress = new Progress<(double Progress, string Status)>((update) =>
                    {
                        try
                        {
                            // 兼容不同进度量纲：如果上报值大于1，则视为0-100的百分比；否则视为0-1范围
                            double raw = update.Progress;
                            double normalized = raw;
                            if (raw > 1.0)
                            {
                                normalized = Math.Min(1.0, raw / 100.0);
                            }

                            vm.Progress = Math.Max(0.0, Math.Min(1.0, normalized));
                            vm.StatusText = update.Status ?? string.Empty;

                            // 如果上报已经到达 100% 且状态文字提示进入“添加音符”阶段，切换为不确定性横向加载动画
                            try
                            {
                                if (vm.Progress >= 1.0 && !string.IsNullOrEmpty(vm.StatusText) && (vm.StatusText.Contains("添加") || vm.StatusText.Contains("Adding")))
                                {
                                    vm.IsIndeterminate = true;
                                }
                                else
                                {
                                    vm.IsIndeterminate = false;
                                }
                            }
                            catch { }

                            // 额外记录关键进度点或含有状态文本的更新，帮助诊断未响应或日志不完整问题
                            if (!string.IsNullOrEmpty(vm.StatusText) || raw <= 1.0 || raw >= 99.0)
                            {
                                _loggingService.LogInfo($"Preload progress: raw={raw}, norm={vm.Progress:P0} - {vm.StatusText}", "DialogService");
                            }
                        }
                        catch { }
                    });

                    try
                    {
                        _loggingService.LogInfo($"Preload dialog: Starting task for {fileName}", "DialogService");
                        // Execute the user's task
                        var result = await task(progress, cts?.Token ?? CancellationToken.None);
                        // Show completion state briefly
                        vm.Progress = 1.0;
                        vm.StatusText = "完成";

                        _loggingService.LogInfo($"Preload dialog: Task completed for {fileName}", "DialogService");

                        resultTcs.TrySetResult((PreloadDialogResult.Load, result));
                    }
                    catch (OperationCanceledException)
                    {
                        vm.StatusText = "已取消";
                        _loggingService.LogInfo($"Preload dialog: Task canceled for {fileName}", "DialogService");
                        resultTcs.TrySetResult((PreloadDialogResult.Cancel, default));
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogException(ex, "预加载对话框内任务执行失败", "DialogService");
                        vm.StatusText = "发生错误" + (ex.Message.Length > 0 ? ": " + ex.Message : string.Empty);
                        _loggingService.LogInfo($"Preload dialog: Task error for {fileName} - {ex.Message}", "DialogService");
                        resultTcs.TrySetException(ex);
                    }
                    finally
                    {
                        // stop intro animation to let progress UI stabilize
                        try { vm.StopIntroAnimation(); } catch { }

                        // allow a short delay for user to see final state
                        await Task.Delay(800);

                        // close dialog on UI thread
                        try
                        {
                            _loggingService.LogInfo($"Preload dialog: Closing dialog for {fileName}", "DialogService");
                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => preloadDialog.Close());
                        }
                        catch { }
                    }
                };

                // Show dialog modally. The dialog will be closed either by RequestClose handler (Cancel/Reselect)
                // or by the LoadRequested handler after the task finishes.
                var showTask = ShowDialogWithParentAsync(preloadDialog);

                // Wait for either the result TCS to be set (Load/Cancel/Reselect path), or the dialog to be closed by other means.
                var completed = await Task.WhenAny(resultTcs.Task, showTask);

                // Ensure animation stopped
                try { vm.StopIntroAnimation(); } catch { }

                if (resultTcs.Task.IsCompleted)
                {
                    return await resultTcs.Task;
                }

                // If dialog was closed without result (fallback), return Cancel
                return (PreloadDialogResult.Cancel, default);
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "ShowPreloadAndRunAsync 时发生错误", "DialogService");
                throw;
            }
        }

        #endregion

        #region 导出对话框

        public async Task<(PreloadDialogResult Choice, T? Result)> ShowExportAndRunAsync<T>(string fileName, long fileSize,
            Func<IProgress<(double Progress, string Status)>, CancellationToken, Task<T>> task,
            bool canCancel = false)
        {
            try
            {
                _loggingService.LogInfo($"显示导出对话框并在其中运行任务: {fileName} ({fileSize} bytes)", "DialogService");

                var vm = new Lumino.ViewModels.Dialogs.ExportViewModel(fileName, fileSize);
                var exportDialog = new Lumino.Views.Dialogs.ExportDialog(vm)
                {
                    Title = $"Lumino - 准备导出：{fileName}"
                };

                // Start intro animation
                try { vm.StartIntroAnimation(); } catch { }

                var resultTcs = new TaskCompletionSource<(PreloadDialogResult, T?)>();

                // Handle user cancel before exporting
                void OnRequestClose(PreloadDialogResult r)
                {
                    // If user canceled before pressing Export, set result and close dialog
                    if (r == PreloadDialogResult.Cancel)
                    {
                        try { resultTcs.TrySetResult((r, default)); } catch { }
                        try { exportDialog.Close(); } catch { }
                    }
                }

                vm.RequestClose += OnRequestClose;

                // When Export is requested, start the provided task and keep the dialog open; update VM progress
                vm.ExportRequested += async () =>
                {
                    _loggingService.LogInfo($"Export dialog: ExportRequested for {fileName}", "DialogService");
                    // prepare cancellation
                    var cts = canCancel ? new CancellationTokenSource() : null;
                    vm.SetCancellationSource(cts);

                    var progress = new Progress<(double Progress, string Status)>((update) =>
                    {
                        try
                        {
                            // 兼容不同进度量纲：如果上报值大于1，则视为0-100的百分比；否则视为0-1范围
                            double raw = update.Progress;
                            double normalized = raw;
                            if (raw > 1.0)
                            {
                                normalized = Math.Min(1.0, raw / 100.0);
                            }

                            vm.Progress = Math.Max(0.0, Math.Min(1.0, normalized));
                            vm.StatusText = update.Status ?? string.Empty;

                            // 如果上报已经到达 100% 且状态文字提示进入"写入文件"阶段，切换为不确定性横向加载动画
                            try
                            {
                                if (vm.Progress >= 1.0 && !string.IsNullOrEmpty(vm.StatusText) && (vm.StatusText.Contains("写入") || vm.StatusText.Contains("Writing")))
                                {
                                    vm.IsIndeterminate = true;
                                }
                                else
                                {
                                    vm.IsIndeterminate = false;
                                }
                            }
                            catch { }

                            // 额外记录关键进度点或含有状态文本的更新，帮助诊断未响应或日志不完整问题
                            if (!string.IsNullOrEmpty(vm.StatusText) || raw <= 1.0 || raw >= 99.0)
                            {
                                _loggingService.LogInfo($"Export progress: raw={raw}, norm={vm.Progress:P0} - {vm.StatusText}", "DialogService");
                            }
                        }
                        catch { }
                    });

                    try
                    {
                        _loggingService.LogInfo($"Export dialog: Starting task for {fileName}", "DialogService");
                        // Execute the user's task
                        var result = await task(progress, cts?.Token ?? CancellationToken.None);
                        // Show completion state briefly
                        vm.Progress = 1.0;
                        vm.StatusText = "导出完成";

                        _loggingService.LogInfo($"Export dialog: Task completed for {fileName}", "DialogService");

                        resultTcs.TrySetResult((PreloadDialogResult.Load, result)); // Using Load as success indicator for export
                    }
                    catch (OperationCanceledException)
                    {
                        vm.StatusText = "已取消";
                        _loggingService.LogInfo($"Export dialog: Task canceled for {fileName}", "DialogService");
                        resultTcs.TrySetResult((PreloadDialogResult.Cancel, default));
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogException(ex, "导出对话框内任务执行失败", "DialogService");
                        vm.StatusText = "发生错误" + (ex.Message.Length > 0 ? ": " + ex.Message : string.Empty);
                        _loggingService.LogInfo($"Export dialog: Task error for {fileName} - {ex.Message}", "DialogService");
                        resultTcs.TrySetException(ex);
                    }
                    finally
                    {
                        // stop intro animation to let progress UI stabilize
                        try { vm.StopIntroAnimation(); } catch { }

                        // allow a short delay for user to see final state
                        await Task.Delay(800);

                        // close dialog on UI thread
                        try
                        {
                            _loggingService.LogInfo($"Export dialog: Closing dialog for {fileName}", "DialogService");
                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => exportDialog.Close());
                        }
                        catch { }
                    }
                };

                // Show dialog modally. The dialog will be closed either by RequestClose handler (Cancel)
                // or by the ExportRequested handler after the task finishes.
                var showTask = ShowDialogWithParentAsync(exportDialog);

                // Wait for either the result TCS to be set (Cancel/Export path), or the dialog to be closed by other means.
                var completed = await Task.WhenAny(resultTcs.Task, showTask);

                // Ensure animation stopped
                try { vm.StopIntroAnimation(); } catch { }

                if (resultTcs.Task.IsCompleted)
                {
                    return await resultTcs.Task;
                }

                // If dialog was closed without result (fallback), return Cancel
                return (PreloadDialogResult.Cancel, default);
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "ShowExportAndRunAsync 时发生错误", "DialogService");
                throw;
            }
        }

        #endregion

        /// <summary>
        /// 获取主窗口
        /// 提供统一的主窗口获取逻辑，避免重复代码
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
        /// 以主窗口为父级显示对话框
        /// 统一的对话框显示逻辑，提供异常处理和错误处理
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
                    
                    // 如果是确认对话框，返回Result属性
                    if (dialog is ConfirmationDialog confirmDialog)
                    {
                        return confirmDialog.Result;
                    }
                    
                    // 其他类型对话框，返回DataContext
                    return dialog.DataContext;
                }
                else
                {
                    // 如果没有父窗口，则为独立窗口显示
                    _loggingService.LogWarning("没有父窗口，对话框将为独立窗口显示", "DialogService");
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
        /// 封装文件打开对话框配置逻辑，减少代码复制
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <param name="filters">文件过滤器</param>
        /// <returns>配置好的文件选择器选项</returns>
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
        /// 封装文件保存对话框配置逻辑，减少代码复制
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
    }
}