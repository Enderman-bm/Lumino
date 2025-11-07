using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using EnderDebugger;

namespace Lumino.Views
{
    public partial class LogViewerWindow : UserControl
    {
        private readonly ViewModels.LogViewerViewModel _viewModel;
        private readonly EnderLogger _logger;

        public LogViewerWindow()
        {
            InitializeComponent();
            
            _logger = new EnderLogger("LogViewer");
            _viewModel = new ViewModels.LogViewerViewModel();
            DataContext = _viewModel;
            
            // 设置命令和事件
            SetupCommands();
            SetupAutoScrollEvents();
            
            _logger.Info("Initialization", "日志查看器控件已初始化");
        }
        
        /// <summary>
        /// 设置命令
        /// </summary>
        private void SetupCommands()
        {
            if (DataContext is ViewModels.LogViewerViewModel vm)
            {
                vm.RefreshLogsCommand = new RelayCommand(RefreshLogs);
                vm.ClearLogsCommand = new RelayCommand(ClearLogs);
                vm.ExportLogsCommand = new RelayCommandAsync(async () => await ExportLogs());
                vm.FollowFileCommand = new RelayCommand(ToggleFollowFile);
            }
        }
        
        /// <summary>
        /// 设置自动滚动事件
        /// </summary>
        private void SetupAutoScrollEvents()
        {
            if (DataContext is ViewModels.LogViewerViewModel vm)
            {
                vm.AutoScrollToEndRequested += OnAutoScrollToEndRequested;
            }
        }
        
        /// <summary>
        /// 处理自动滚动请求事件
        /// </summary>
        private void OnAutoScrollToEndRequested(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await ScrollToEndAsync();
            });
        }
        
        /// <summary>
        /// 异步滚动到末尾
        /// </summary>
        private async Task ScrollToEndAsync()
        {
            try
            {
                // 等待UI更新完成
                await Task.Delay(20);
                
                var scrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");
                if (scrollViewer != null)
                {
                    // 使用正确的方法滚动到末尾
                    scrollViewer.Offset = new Avalonia.Vector(0, scrollViewer.Extent.Height);
                    
                    // 额外验证滚动是否成功
                    await Task.Delay(10);
                    if (scrollViewer.Offset.Y < scrollViewer.Extent.Height - 50)
                    {
                        // 再次尝试
                        scrollViewer.Offset = new Avalonia.Vector(0, scrollViewer.Extent.Height);
                    }
                    
                }
                else
                {
                    _logger.Debug("AutoScroll", "未找到ScrollViewer控件");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("AutoScroll", $"自动滚动失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 刷新日志
        /// </summary>
        private void RefreshLogs()
        {
            try
            {
                _logger.Debug("Command", "执行刷新日志命令");
                if (DataContext is ViewModels.LogViewerViewModel vm)
                {
                    vm.RefreshLogs();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Command", $"刷新日志失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 清除日志
        /// </summary>
        private void ClearLogs()
        {
            try
            {
                _logger.Debug("Command", "执行清除日志命令");
                if (DataContext is ViewModels.LogViewerViewModel vm)
                {
                    vm.ClearLogs();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Command", $"清除日志失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 导出日志
        /// </summary>
        private async Task ExportLogs()
        {
            try
            {
                _logger.Debug("Command", "执行导出日志命令");
                
                // 找到顶级窗口来显示对话框
                if (DataContext is ViewModels.LogViewerViewModel vm)
                {
                    // 通过VisualTreeHelper查找父窗口
                    var parent = this.Parent;
                    Window? parentWindow = null;
                    
                    while (parent != null && parentWindow == null)
                    {
                        if (parent is Window window)
                        {
                            parentWindow = window;
                            break;
                        }
                        parent = parent.Parent;
                    }
                    
                    if (parentWindow != null)
                    {
                        // 使用简单的文件保存对话框
                        var dialog = new SaveFileDialog
                        {
                            Title = "导出日志",
                            Filters = new System.Collections.Generic.List<FileDialogFilter>
                            {
                                new FileDialogFilter { Name = "文本文件 (*.txt)", Extensions = new System.Collections.Generic.List<string> { "txt" } },
                                new FileDialogFilter { Name = "所有文件 (*.*)", Extensions = new System.Collections.Generic.List<string> { "*" } }
                            }
                        };
                        
                        var result = await dialog.ShowAsync(parentWindow);
                        if (!string.IsNullOrEmpty(result))
                        {
                            await vm.ExportLogsAsync(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Command", $"导出日志失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 切换文件跟踪
        /// </summary>
        private void ToggleFollowFile()
        {
            try
            {
                _logger.Debug("Command", "执行切换文件跟踪命令");
                if (DataContext is ViewModels.LogViewerViewModel vm)
                {
                    vm.FollowFile = !vm.FollowFile;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Command", $"切换文件跟踪失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 处理键盘事件
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            switch (e.Key)
            {
                case Key.F5:
                    RefreshLogs();
                    e.Handled = true;
                    break;
                case Key.Delete:
                    if (e.KeyModifiers == KeyModifiers.Control)
                    {
                        ClearLogs();
                        e.Handled = true;
                    }
                    break;
                case Key.S:
                    if (e.KeyModifiers == KeyModifiers.Control)
                    {
                        _ = ExportLogs();
                        e.Handled = true;
                    }
                    break;
                case Key.F:
                    if (e.KeyModifiers == KeyModifiers.Control)
                    {
                        // 聚焦搜索框
                        var searchBox = this.FindControl<TextBox>("SearchBox");
                        searchBox?.Focus();
                        e.Handled = true;
                    }
                    break;
            }
        }
        
        /// <summary>
        /// 控件卸载时清理资源
        /// </summary>
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            try
            {
                _logger.Debug("Lifecycle", "日志查看器控件正在卸载");
                _viewModel?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Error("Lifecycle", $"控件卸载时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 日志条目鼠标悬停事件
        /// </summary>
        private void LogEntry_PointerEnter(object? sender, PointerEventArgs e)
        {
            // 鼠标悬停效果已在样式中定义
            _logger.Debug("UI", "鼠标悬停在日志条目上");
        }

        /// <summary>
        /// 日志条目鼠标离开事件
        /// </summary>
        private void LogEntry_PointerLeave(object? sender, PointerEventArgs e)
        {
            // 鼠标离开效果已在样式中定义
            _logger.Debug("UI", "鼠标离开日志条目");
        }

        /// <summary>
        /// 日志条目点击事件 - 复制到剪贴板
        /// </summary>
        private async void LogEntry_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.DataContext is ViewModels.LogEntryViewModel logEntry)
            {
                try
                {
                    _logger.Debug("UI", $"点击复制日志: {logEntry.Timestamp} {logEntry.Level}");
                    
                    // 构建完整的日志消息
                    var logText = $"[{logEntry.Timestamp}] [{logEntry.Level}] [{logEntry.Source}] [{logEntry.Component}] {logEntry.Message}";
                    
                    // 复制到剪贴板
                    await CopyToClipboardAsync(logText);
                    
                    // 更新状态消息
                    if (_viewModel != null)
                    {
                        _viewModel.StatusMessage = $"已复制日志到剪贴板: {logEntry.Timestamp} {logEntry.Level}";
                        
                        // 3秒后清除状态消息
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(3000);
                            Dispatcher.UIThread.Invoke(() =>
                            {
                                if (_viewModel != null)
                                {
                                    _viewModel.StatusMessage = "";
                                }
                            });
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("UI", $"复制日志失败: {ex.Message}");
                    if (_viewModel != null)
                    {
                        _viewModel.StatusMessage = $"复制失败: {ex.Message}";
                    }
                }
            }
        }

        /// <summary>
        /// 异步复制到剪贴板
        /// </summary>
        private async Task CopyToClipboardAsync(string text)
        {
            try
            {
                // 获取剪贴板服务
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(text);
                    _logger.Debug("Clipboard", "使用Avalonia剪贴板复制成功");
                }
                else
                {
                    throw new NotSupportedException("无法获取剪贴板服务");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Clipboard", $"剪贴板操作失败: {ex.Message}");
                throw new InvalidOperationException($"无法访问剪贴板: {ex.Message}", ex);
            }
        }
    }
    
    /// <summary>
    /// 简单的RelayCommand实现
    /// </summary>
    public class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;
        
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        
        public event EventHandler? CanExecuteChanged;
        
        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }
        
        public void Execute(object? parameter)
        {
            _execute();
        }
        
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    
    /// <summary>
    /// 异步RelayCommand实现
    /// </summary>
    public class RelayCommandAsync : System.Windows.Input.ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;
        private bool _isExecuting;
        
        public RelayCommandAsync(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        
        public event EventHandler? CanExecuteChanged;
        
        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke() ?? true);
        }
        
        public async void Execute(object? parameter)
        {
            if (CanExecute(parameter))
            {
                _isExecuting = true;
                RaiseCanExecuteChanged();
                
                try
                {
                    await _execute();
                }
                finally
                {
                    _isExecuting = false;
                    RaiseCanExecuteChanged();
                }
            }
        }
        
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}