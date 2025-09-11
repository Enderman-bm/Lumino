using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DominoNext.Services.Interfaces;
using DominoNext.ViewModels.Base;
using DominoNext.ViewModels.Editor;
using DominoNext.Models.Music;

namespace DominoNext.ViewModels.Examples
{
    /// <summary>
    /// 重构后的主窗口ViewModel - 使用增强的基类减少重复代码
    /// 演示如何使用新的架构来提高代码复用性和可维护性
    /// </summary>
    public partial class RefactoredMainWindowViewModel : EnhancedViewModelBase
    {
        #region 服务依赖
        private readonly ISettingsService _settingsService;
        private readonly IApplicationService _applicationService;
        private readonly IProjectStorageService _projectStorageService;
        #endregion

        #region 属性
        [ObservableProperty]
        private string _greeting = "欢迎使用 DominoNext！";

        [ObservableProperty]
        private ViewType _currentView = ViewType.PianoRoll;

        [ObservableProperty]
        private PianoRollViewModel? _pianoRoll;

        [ObservableProperty]
        private TrackSelectorViewModel? _trackSelector;
        #endregion

        #region 构造函数
        /// <summary>
        /// 生产环境构造函数 - 通过依赖注入获取所需服务
        /// </summary>
        public RefactoredMainWindowViewModel(
            ISettingsService settingsService,
            IDialogService dialogService,
            IApplicationService applicationService,
            IProjectStorageService projectStorageService)
            : base(dialogService, null) // 传递对话框服务给基类
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
            _projectStorageService = projectStorageService ?? throw new ArgumentNullException(nameof(projectStorageService));

            InitializeGreetingMessage();
        }

        /// <summary>
        /// 设计时构造函数 - 使用统一的设计时服务提供者
        /// 消除重复的CreateDesignTimeXXXService方法
        /// </summary>
        public RefactoredMainWindowViewModel() : this(
            DesignTimeServiceProvider.GetSettingsService(),
            DesignTimeServiceProvider.GetDialogService(),
            DesignTimeServiceProvider.GetApplicationService(),
            DesignTimeServiceProvider.GetProjectStorageService())
        {
            // 设计时特定的初始化
            InitializeDesignTimeData();
        }
        #endregion

        #region 命令实现 - 使用基类的异常处理模式

        /// <summary>
        /// 新建文件命令 - 展示使用基类异常处理的简化写法
        /// </summary>
        [RelayCommand]
        private async Task NewFileAsync()
        {
            await ExecuteWithConfirmationAsync(
                operation: async () =>
                {
                    PianoRoll?.ClearContent();
                    TrackSelector?.ClearTracks();
                    TrackSelector?.AddTrack();
                },
                confirmationTitle: "确认",
                confirmationMessage: "当前项目有未保存的更改，是否继续创建新文件？",
                operationName: "新建文件"
            );
        }

        /// <summary>
        /// 打开文件命令 - 使用基类的异常处理
        /// </summary>
        [RelayCommand]
        private async Task OpenFileAsync()
        {
            await ExecuteWithExceptionHandlingAsync(
                operation: async () =>
                {
                    if (!await _applicationService.CanShutdownSafelyAsync())
                    {
                        var shouldProceed = await DialogService!.ShowConfirmationDialogAsync(
                            "确认", "当前项目有未保存的更改，是否继续打开新文件？");
                        
                        if (!shouldProceed) return;
                    }

                    var filePath = await DialogService!.ShowOpenFileDialogAsync(
                        "打开MIDI文件", 
                        new[] { "*.mid", "*.midi", "*.dmn" });

                    if (!string.IsNullOrEmpty(filePath))
                    {
                        await ProcessFileAsync(filePath);
                    }
                },
                errorTitle: "打开文件错误",
                operationName: "打开文件"
            );
        }

        /// <summary>
        /// 保存文件命令 - 使用基类的异常处理和返回值
        /// </summary>
        [RelayCommand]
        private async Task SaveFileAsync()
        {
            var success = await ExecuteWithExceptionHandlingAsync(
                operation: async () =>
                {
                    if (PianoRoll == null) return false;
                    
                    var allNotes = PianoRoll.GetAllNotes();
                    var filePath = await DialogService!.ShowSaveFileDialogAsync(
                        "导出MIDI文件", null, new[] { "*.mid" });

                    if (string.IsNullOrEmpty(filePath)) return false;

                    return await _projectStorageService.ExportMidiAsync(
                        filePath, 
                        allNotes.Select(vm => vm.ToNoteModel()));
                },
                defaultValue: false,
                errorTitle: "保存文件错误",
                operationName: "保存文件"
            );

            if (success && DialogService != null)
            {
                await DialogService.ShowInfoDialogAsync("成功", "文件保存成功！");
            }
        }

        /// <summary>
        /// 导入MIDI文件命令 - 展示复杂操作的简化处理
        /// </summary>
        [RelayCommand]
        private async Task ImportMidiFileAsync()
        {
            await ExecuteWithExceptionHandlingAsync(
                operation: async () =>
                {
                    var filePath = await DialogService!.ShowOpenFileDialogAsync(
                        "选择MIDI文件", new[] { "*.mid", "*.midi" });

                    if (!string.IsNullOrEmpty(filePath))
                    {
                        await ProcessMidiImportAsync(filePath);
                    }
                },
                errorTitle: "导入MIDI文件错误",
                operationName: "导入MIDI文件",
                showSuccessMessage: true,
                successMessage: "MIDI文件导入完成！"
            );
        }

        /// <summary>
        /// 打开设置对话框命令
        /// </summary>
        [RelayCommand]
        private async Task OpenSettingsAsync()
        {
            var result = await ExecuteWithExceptionHandlingAsync(
                operation: async () => await DialogService!.ShowSettingsDialogAsync(),
                defaultValue: false,
                errorTitle: "设置错误",
                operationName: "打开设置"
            );

            if (result)
            {
                await RefreshUIAfterSettingsChangeAsync();
            }
        }

        /// <summary>
        /// 退出应用程序命令
        /// </summary>
        [RelayCommand]
        private async Task ExitApplicationAsync()
        {
            await ExecuteWithConfirmationAsync(
                operation: async () =>
                {
                    if (await _applicationService.CanShutdownSafelyAsync())
                    {
                        _applicationService.Shutdown();
                    }
                    else
                    {
                        _applicationService.Shutdown(); // 强制退出
                    }
                },
                confirmationTitle: "确认退出",
                confirmationMessage: "有未保存的更改，是否确认退出？",
                operationName: "退出应用程序"
            );
        }
        #endregion

        #region 私有辅助方法
        /// <summary>
        /// 初始化欢迎消息
        /// </summary>
        private void InitializeGreetingMessage()
        {
            try
            {
                var appInfo = _applicationService.GetApplicationInfo();
                Greeting = $"欢迎使用 {appInfo.Name} v{appInfo.Version}！";
            }
            catch (Exception ex)
            {
                LoggingService?.LogException(ex, "初始化欢迎消息失败", GetType().Name);
                Greeting = "欢迎使用 DominoNext！";
            }
        }

        /// <summary>
        /// 初始化设计时数据
        /// </summary>
        private void InitializeDesignTimeData()
        {
            if (DesignTimeServiceProvider.IsInDesignMode())
            {
                PianoRoll = new PianoRollViewModel();
                TrackSelector = new TrackSelectorViewModel();
            }
        }

        /// <summary>
        /// 处理文件（根据扩展名判断类型）
        /// </summary>
        private async Task ProcessFileAsync(string filePath)
        {
            var extension = System.IO.Path.GetExtension(filePath).ToLower();
            
            switch (extension)
            {
                case ".mid":
                case ".midi":
                    await ProcessMidiImportAsync(filePath);
                    break;
                case ".dmn":
                    await DialogService!.ShowInfoDialogAsync("信息", "DominoNext项目文件加载功能将在后续版本中实现");
                    break;
                default:
                    await DialogService!.ShowErrorDialogAsync("错误", "不支持的文件格式");
                    break;
            }
        }

        /// <summary>
        /// 处理MIDI文件导入
        /// </summary>
        private async Task ProcessMidiImportAsync(string filePath)
        {
            // 使用对话框服务的进度显示功能
            await DialogService!.RunWithProgressAsync("导入MIDI文件", async (progress, cancellationToken) =>
            {
                var notes = await _projectStorageService.ImportMidiWithProgressAsync(filePath, progress, cancellationToken);
                
                // 更新UI（在UI线程中执行）
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateUIAfterMidiImport(notes, filePath);
                });
            });
        }

        /// <summary>
        /// MIDI导入后更新UI
        /// </summary>
        private void UpdateUIAfterMidiImport(IEnumerable<Note> notes, string filePath)
        {
            if (PianoRoll == null || TrackSelector == null) return;
            
            PianoRoll.ClearContent();
            
            // 更新音轨信息等...
            // 这里可以继续实现具体的UI更新逻辑
        }

        /// <summary>
        /// 设置更改后刷新UI
        /// </summary>
        private async Task RefreshUIAfterSettingsChangeAsync()
        {
            InitializeGreetingMessage();
            // 其他UI刷新逻辑...
            await Task.CompletedTask;
        }
        #endregion

        #region 资源清理
        /// <summary>
        /// 释放特定资源
        /// </summary>
        protected override void DisposeCore()
        {
            // 清理特定于MainWindow的资源
            PianoRoll?.Dispose();
            TrackSelector = null;
        }
        #endregion
    }
}