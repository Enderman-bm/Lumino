using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DominoNext.Models.Music;
using DominoNext.Services.Interfaces;
using DominoNext.ViewModels.Editor;
using DominoNext.ViewModels.Base;

namespace DominoNext.ViewModels
{
    /// <summary>
    /// 主窗口ViewModel - 重构后使用增强基类减少重复代码
    /// 负责主窗口的UI逻辑协调，业务逻辑委托给专门的服务处理
    /// </summary>
    public partial class MainWindowViewModel : EnhancedViewModelBase
    {
        #region 服务依赖
        private readonly ISettingsService _settingsService;
        private readonly IApplicationService _applicationService;
        private readonly IProjectStorageService _projectStorageService;
        #endregion

        #region 属性
        /// <summary>
        /// 欢迎消息 - 可通过配置或本地化服务获取
        /// </summary>
        [ObservableProperty]
        private string _greeting = "欢迎使用 DominoNext！";

        /// <summary>
        /// 当前选中的视图类型，默认为钢琴卷帘
        /// </summary>
        [ObservableProperty]
        private ViewType _currentView = ViewType.PianoRoll;

        /// <summary>
        /// 钢琴卷帘ViewModel
        /// </summary>
        [ObservableProperty]
        private PianoRollViewModel? _pianoRoll;

        /// <summary>
        /// 音轨选择器ViewModel - 管理音轨列表和选择状态
        /// </summary>
        [ObservableProperty]
        private TrackSelectorViewModel? _trackSelector;
        #endregion

        #region 构造函数
        /// <summary>
        /// 主构造函数 - 通过依赖注入获取所需服务
        /// </summary>
        public MainWindowViewModel(
            ISettingsService settingsService,
            IDialogService dialogService,
            IApplicationService applicationService,
            IProjectStorageService projectStorageService)
            : base(dialogService, null) // 使用增强基类，传递对话框服务
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
            _projectStorageService = projectStorageService ?? throw new ArgumentNullException(nameof(projectStorageService));

            // 初始化欢迎消息
            InitializeGreetingMessage();
        }

        /// <summary>
        /// 异步初始化方法
        /// </summary>
        public async Task InitializeAsync()
        {
            // 异步创建PianoRollViewModel
            PianoRoll = await Task.Run(() => new PianoRollViewModel());

            // 创建音轨选择器ViewModel
            TrackSelector = await Task.Run(() => new TrackSelectorViewModel());

            // 建立音轨选择器和钢琴卷帘之间的通信
            TrackSelector.PropertyChanged += OnTrackSelectorPropertyChanged;
        }
        
        /// <summary>
        /// 设计时构造函数 - 使用统一的设计时服务提供者
        /// </summary>
        public MainWindowViewModel() : this(
            DesignTimeServiceProvider.GetSettingsService(),
            DesignTimeServiceProvider.GetDialogService(),
            DesignTimeServiceProvider.GetApplicationService(),
            DesignTimeServiceProvider.GetProjectStorageService())
        {
            // 直接创建PianoRollViewModel用于设计时
            PianoRoll = new PianoRollViewModel();

            // 创建音轨选择器ViewModel
            TrackSelector = new TrackSelectorViewModel();

            // 建立音轨选择器和钢琴卷帘之间的通信
            TrackSelector.PropertyChanged += OnTrackSelectorPropertyChanged;
        }
        #endregion

        #region 命令实现 - 使用增强基类的简化异常处理

        /// <summary>
        /// 新建文件命令 - 重构后的简化版本
        /// </summary>
        [RelayCommand]
        private async Task NewFileAsync()
        {
            await ExecuteWithConfirmationAsync(
                operation: async () =>
                {
                    // 清空当前项目
                    PianoRoll?.ClearContent();
                    TrackSelector?.ClearTracks();
                    TrackSelector?.AddTrack(); // 添加默认音轨
                },
                confirmationTitle: "确认",
                confirmationMessage: "当前项目有未保存的更改，是否继续创建新文件？",
                operationName: "新建文件"
            );
        }

        /// <summary>
        /// 打开文件命令 - 重构后的简化版本
        /// </summary>
        [RelayCommand]
        private async Task OpenFileAsync()
        {
            await ExecuteWithExceptionHandlingAsync(
                operation: async () =>
                {
                    // 检查是否有未保存的更改
                    if (!await _applicationService.CanShutdownSafelyAsync())
                    {
                        var shouldProceed = await DialogService!.ShowConfirmationDialogAsync(
                            "确认", "当前项目有未保存的更改，是否继续打开新文件？");
                        
                        if (!shouldProceed)
                            return;
                    }

                    var filePath = await DialogService!.ShowOpenFileDialogAsync(
                        "打开MIDI文件", 
                        new[] { "*.mid", "*.midi", "*.dmn" }); // dmn可能是DominoNext的项目格式

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
        /// 保存文件命令 - 重构后的简化版本
        /// </summary>
        [RelayCommand]
        private async Task SaveFileAsync()
        {
            var success = await ExecuteWithExceptionHandlingAsync(
                operation: async () =>
                {
                    if (PianoRoll == null) return false;
                    
                    // 获取所有音符
                    var allNotes = PianoRoll.GetAllNotes().Select(vm => vm.ToNoteModel()).ToList();
                    
                    // 显示保存文件对话框
                    var filePath = await DialogService!.ShowSaveFileDialogAsync(
                        "导出MIDI文件",
                        null,
                        new[] { "*.mid" });

                    if (string.IsNullOrEmpty(filePath))
                    {
                        return false;
                    }

                    // 确保文件扩展名为.mid
                    if (!filePath.EndsWith(".mid", StringComparison.OrdinalIgnoreCase))
                    {
                        filePath += ".mid";
                    }

                    // 使用DialogService的RunWithProgressAsync方法来处理带进度的操作
                    return await DialogService.RunWithProgressAsync("导出MIDI文件", async (progress, cancellationToken) =>
                    {
                        progress.Report((0, "正在导出MIDI文件..."));

                        // 异步导出MIDI文件
                        bool exportSuccess = await _projectStorageService.ExportMidiAsync(filePath, allNotes);

                        if (exportSuccess)
                        {
                            progress.Report((100, "MIDI文件导出完成"));
                        }
                        
                        return exportSuccess;
                    }, canCancel: true);
                },
                defaultValue: false,
                errorTitle: "保存文件错误",
                operationName: "保存文件"
            );

            if (success && DialogService != null)
            {
                await DialogService.ShowInfoDialogAsync("成功", "MIDI文件导出完成。");
            }
        }

        /// <summary>
        /// 打开设置对话框命令 - 重构后的简化版本
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
                // 设置已保存，可能需要重新加载某些UI元素
                await RefreshUIAfterSettingsChangeAsync();
            }
        }

        /// <summary>
        /// 退出应用程序命令 - 重构后的简化版本
        /// </summary>
        [RelayCommand]
        private async Task ExitApplicationAsync()
        {
            await ExecuteWithConfirmationAsync(
                operation: async () =>
                {
                    // 检查是否可以安全退出
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

        /// <summary>
        /// 导入MIDI文件命令 - 重构后的简化版本
        /// </summary>
        [RelayCommand]
        private async Task ImportMidiFileAsync()
        {
            await ExecuteWithExceptionHandlingAsync(
                operation: async () =>
                {
                    // 获取用户选择的MIDI文件路径
                    var filePath = await DialogService!.ShowOpenFileDialogAsync(
                        "选择MIDI文件",
                        new string[] { "*.mid", "*.midi" });

                    if (!string.IsNullOrEmpty(filePath))
                    {
                        await ImportMidiFileAsync(filePath);
                    }
                },
                errorTitle: "导入MIDI文件错误",
                operationName: "导入MIDI文件",
                showSuccessMessage: true,
                successMessage: "MIDI文件导入完成。"
            );
        }

        /// <summary>
        /// 选择视图命令
        /// </summary>
        [RelayCommand]
        private void SelectView(ViewType viewType)
        {
            CurrentView = viewType;
        }

        #endregion

        #region 私有方法 - 重构后的简化版本

        /// <summary>
        /// 处理音轨选择器属性变化
        /// </summary>
        private void OnTrackSelectorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TrackSelectorViewModel.SelectedTrack))
            {
                // 当前选中的音轨发生变化时，更新钢琴卷帘的当前音轨
                if (TrackSelector != null && TrackSelector.SelectedTrack != null && PianoRoll != null)
                {
                    var selectedTrackIndex = TrackSelector.SelectedTrack.TrackNumber - 1; // TrackNumber从1开始，索引从0开始
                    PianoRoll.SetCurrentTrackIndex(selectedTrackIndex);
                    
                    // 确保切换音轨后滚动系统工作正常
                    PianoRoll.ForceRefreshScrollSystem();
                    
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] 切换到音轨 {selectedTrackIndex}，强制刷新滚动系统");
                }
            }
        }

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
        /// 设置更改后刷新UI
        /// </summary>
        private async Task RefreshUIAfterSettingsChangeAsync()
        {
            try
            {
                // 重新初始化欢迎消息（可能语言已更改）
                InitializeGreetingMessage();

                // 通知PianoRoll等子组件刷新
                // 这里可以发送消息或调用相应的刷新方法

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LoggingService?.LogException(ex, "刷新UI失败", GetType().Name);
            }
        }

        /// <summary>
        /// 处理文件（根据扩展名判断类型）
        /// </summary>
        private async Task ProcessFileAsync(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            
            if (extension == ".mid" || extension == ".midi")
            {
                await ImportMidiFileAsync(filePath);
            }
            else if (extension == ".dmn")
            {
                // TODO: 实现DominoNext项目文件的加载
                await DialogService!.ShowInfoDialogAsync("信息", "DominoNext项目文件加载功能将在后续版本中实现");
            }
        }

        /// <summary>
        /// 导入MIDI文件的私有方法（带文件路径参数）
        /// </summary>
        /// <param name="filePath">MIDI文件路径</param>
        private async Task ImportMidiFileAsync(string filePath)
        {
            // 使用DialogService的RunWithProgressAsync方法来处理带进度的操作
            await DialogService!.RunWithProgressAsync("导入MIDI文件", async (progress, cancellationToken) =>
            {
                // 异步导入MIDI文件
                var notes = await _projectStorageService.ImportMidiWithProgressAsync(filePath, progress, cancellationToken);

                // 在导入过程中获取MIDI文件的时长信息
                var midiFile = await MidiReader.MidiFile.LoadFromFileAsync(filePath, null, cancellationToken);
                var statistics = midiFile.GetStatistics();
                
                // 计算MIDI文件的总时长（以四分音符为单位）
                var estimatedDurationSeconds = statistics.EstimatedDurationSeconds();
                var durationInQuarterNotes = estimatedDurationSeconds / 0.5; // 120 BPM = 0.5秒每四分音符

                // 在UI线程中更新UI
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (PianoRoll == null || TrackSelector == null) return;
                    
                    // 使用轻量级清理，保持ScrollBarManager连接
                    PianoRoll.ClearContent();

                    // 更新音轨列表以匹配MIDI文件中的音轨
                    TrackSelector.LoadTracksFromMidi(midiFile);

                    // 设置MIDI文件的时长信息
                    PianoRoll.SetMidiFileDuration(durationInQuarterNotes);

                    // 确定MIDI文件中最大的音轨索引
                    if (notes.Any())
                    {
                        int maxTrackIndex = notes.Max(n => n.TrackIndex);
                        
                        // 检查并添加所需的音轨
                        while (TrackSelector.Tracks.Count <= maxTrackIndex)
                        {
                            TrackSelector.AddTrack();
                        }
                    }
                    
                    // 选中第一个音轨（如果有音轨）
                    if (TrackSelector.Tracks.Count > 0)
                    {
                        var firstTrack = TrackSelector.Tracks[0];
                        firstTrack.IsSelected = true;
                    }
                    
                    // 批量添加音符
                    AddNotesInBatch(notes);
                });
                
                progress.Report((100, $"成功导入MIDI文件，共加载了 {notes.Count()} 个音符。文件时长：约 {estimatedDurationSeconds:F1} 秒"));
                
            }, canCancel: true);
        }

        /// <summary>
        /// 批量添加音符到钢琴卷帘，优化性能
        /// </summary>
        /// <param name="notes">要添加的音符集合</param>
        private void AddNotesInBatch(IEnumerable<Models.Music.Note> notes)
        {
            if (PianoRoll == null) return;
            
            var noteViewModels = new List<NoteViewModel>();
            
            foreach (var noteModel in notes)
            {
                var noteViewModel = new NoteViewModel
                {
                    Pitch = noteModel.Pitch,
                    StartPosition = noteModel.StartPosition,
                    Duration = noteModel.Duration,
                    Velocity = noteModel.Velocity,
                    TrackIndex = noteModel.TrackIndex
                };
                
                noteViewModels.Add(noteViewModel);
            }
            
            PianoRoll.AddNotesInBatch(noteViewModels);
            
            // 批量添加后强制刷新滚动系统，确保滚动范围正确更新
            PianoRoll.ForceRefreshScrollSystem();
        }

        /// <summary>
        /// 测试滚动系统的诊断方法（调试用）
        /// </summary>
        [RelayCommand]
        private async Task TestScrollSystemAsync()
        {
            var diagnostics = PianoRoll?.GetScrollDiagnostics() ?? "PianoRoll 未初始化";
            await DialogService!.ShowInfoDialogAsync("滚动系统诊断", diagnostics);
        }

        #endregion

        #region 资源清理
        /// <summary>
        /// 释放特定资源 - 重写基类方法
        /// </summary>
        protected override void DisposeCore()
        {
            // 清理特定于MainWindow的资源
            if (TrackSelector != null)
            {
                TrackSelector.PropertyChanged -= OnTrackSelectorPropertyChanged;
            }
            
            PianoRoll?.Dispose();
            TrackSelector = null;
        }
        #endregion
    }
}