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

namespace DominoNext.ViewModels
{
    /// <summary>
    /// 主窗口ViewModel - 符合MVVM最佳实践
    /// 负责主窗口的UI逻辑协调，业务逻辑委托给专门的服务处理
    /// </summary>
    public partial class MainWindowViewModel : ViewModelBase
    {
        #region 服务依赖
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;
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
        public PianoRollViewModel PianoRoll { get; }

        /// <summary>
        /// 音轨选择器ViewModel - 管理音轨列表和选择状态
        /// </summary>
        public TrackSelectorViewModel TrackSelector { get; }
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
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
            _projectStorageService = projectStorageService ?? throw new ArgumentNullException(nameof(projectStorageService));

            // 直接创建PianoRollViewModel
            PianoRoll = new PianoRollViewModel();

            // 创建音轨选择器ViewModel
            TrackSelector = new TrackSelectorViewModel();

            // 建立音轨选择器和钢琴卷帘之间的通信
            TrackSelector.PropertyChanged += OnTrackSelectorPropertyChanged;

            // 初始化欢迎消息
            InitializeGreetingMessage();
        }

        /// <summary>
        /// 设计时构造函数 - 仅用于XAML设计器
        /// </summary>
        public MainWindowViewModel() : this(
            new DominoNext.Services.Implementation.SettingsService(),
            CreateDesignTimeDialogService(),
            new DominoNext.Services.Implementation.ApplicationService(),
            new DominoNext.Services.Implementation.ProjectStorageService())
        {
        }
        
        /// <summary>
        /// 创建设计时使用的对话框服务
        /// </summary>
        private static IDialogService CreateDesignTimeDialogService()
        {
            var loggingService = new DominoNext.Services.Implementation.LoggingService();
            return new DominoNext.Services.Implementation.DialogService(null, loggingService);
        }
        #endregion

        #region 命令实现

        /// <summary>
        /// 新建文件命令
        /// </summary>
        [RelayCommand]
        private async Task NewFileAsync()
        {
            try
            {
                // 检查是否有未保存的更改
                if (!await _applicationService.CanShutdownSafelyAsync())
                {
                    var shouldProceed = await _dialogService.ShowConfirmationDialogAsync(
                        "确认", "当前项目有未保存的更改，是否继续创建新文件？");
                    
                    if (!shouldProceed)
                        return;
                }

                // 清空当前项目
                PianoRoll.Cleanup();
                TrackSelector.ClearTracks();
                TrackSelector.AddTrack(); // 添加默认音轨

                await _dialogService.ShowInfoDialogAsync("信息", "已创建新项目。");
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorDialogAsync("错误", $"新建文件时发生错误：{ex.Message}");
                System.Diagnostics.Debug.WriteLine($"新建文件时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 打开文件命令
        /// </summary>
        [RelayCommand]
        private async Task OpenFileAsync()
        {
            try
            {
                // 检查是否有未保存的更改
                if (!await _applicationService.CanShutdownSafelyAsync())
                {
                    var shouldProceed = await _dialogService.ShowConfirmationDialogAsync(
                        "确认", "当前项目有未保存的更改，是否继续打开新文件？");
                    
                    if (!shouldProceed)
                        return;
                }

                var filePath = await _dialogService.ShowOpenFileDialogAsync(
                    "打开MIDI文件", 
                    new[] { "*.mid", "*.midi", "*.dmn" }); // dmn可能是DominoNext的项目格式

                if (!string.IsNullOrEmpty(filePath))
                {
                    // 判断文件类型
                    var extension = Path.GetExtension(filePath).ToLower();
                    
                    if (extension == ".mid" || extension == ".midi")
                    {
                        await ImportMidiFileAsync(filePath);
                    }
                    else if (extension == ".dmn")
                    {
                        // TODO: 实现DominoNext项目文件的加载
                        await _dialogService.ShowInfoDialogAsync("信息", "DominoNext项目文件加载功能将在后续版本中实现");
                    }
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorDialogAsync("错误", $"打开文件时发生错误：{ex.Message}");
                System.Diagnostics.Debug.WriteLine($"打开文件时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存文件命令
        /// </summary>
        [RelayCommand]
        private async Task SaveFileAsync()
        {
            try
            {
                // 获取所有音符
                var allNotes = PianoRoll.GetAllNotes().Select(vm => vm.ToNoteModel()).ToList();
                
                // 显示保存文件对话框
                var filePath = await _dialogService.ShowSaveFileDialogAsync(
                    "导出MIDI文件",
                    null,
                    new[] { "*.mid" });

                if (string.IsNullOrEmpty(filePath))
                {
                    return;
                }

                // 确保文件扩展名为.mid
                if (!filePath.EndsWith(".mid", StringComparison.OrdinalIgnoreCase))
                {
                    filePath += ".mid";
                }

                // 使用DialogService的RunWithProgressAsync方法来处理带进度的操作
                await _dialogService.RunWithProgressAsync("导出MIDI文件", async (progress, cancellationToken) =>
                {
                    progress.Report((0, "正在导出MIDI文件..."));

                    // 异步导出MIDI文件
                    bool success = await _projectStorageService.ExportMidiAsync(filePath, allNotes);

                    if (success)
                    {
                        progress.Report((100, "MIDI文件导出完成"));
                        await _dialogService.ShowInfoDialogAsync("成功", "MIDI文件导出完成。");
                    }
                    else
                    {
                        await _dialogService.ShowErrorDialogAsync("错误", "MIDI文件导出失败。");
                    }
                }, canCancel: true);
            }
            catch (OperationCanceledException)
            {
                await _dialogService.ShowInfoDialogAsync("信息", "MIDI文件导出已取消。");
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorDialogAsync("错误", $"导出MIDI文件失败：{ex.Message}");
                System.Diagnostics.Debug.WriteLine($"导出MIDI文件时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 打开设置对话框命令
        /// </summary>
        [RelayCommand]
        private async Task OpenSettingsAsync()
        {
            try
            {
                var result = await _dialogService.ShowSettingsDialogAsync();
                
                if (result)
                {
                    // 设置已保存，可能需要重新加载某些UI元素
                    await RefreshUIAfterSettingsChangeAsync();
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorDialogAsync("错误", $"打开设置时发生错误：{ex.Message}");
                System.Diagnostics.Debug.WriteLine($"打开设置对话框时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 退出应用程序命令
        /// </summary>
        [RelayCommand]
        private async Task ExitApplicationAsync()
        {
            try
            {
                // 检查是否可以安全退出
                if (await _applicationService.CanShutdownSafelyAsync())
                {
                    _applicationService.Shutdown();
                }
                else
                {
                    var shouldExit = await _dialogService.ShowConfirmationDialogAsync(
                        "确认退出", "有未保存的更改，是否确认退出？");
                    
                    if (shouldExit)
                    {
                        _applicationService.Shutdown();
                    }
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorDialogAsync("错误", $"退出应用程序时发生错误：{ex.Message}");
                System.Diagnostics.Debug.WriteLine($"退出应用程序时发生错误: {ex.Message}");
                
                // 即使发生错误也尝试退出
                _applicationService.Shutdown();
            }
        }

        /// <summary>
        /// 导入MIDI文件的私有方法（带文件路径参数）
        /// </summary>
        /// <param name="filePath">MIDI文件路径</param>
        private async Task ImportMidiFileAsync(string filePath)
        {
            try
            {
                // 使用DialogService的RunWithProgressAsync方法来处理带进度的操作
                await _dialogService.RunWithProgressAsync("导入MIDI文件", async (progress, cancellationToken) =>
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
                        // 清空现有的音符
                        PianoRoll.Cleanup();

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
                
                await _dialogService.ShowInfoDialogAsync("成功", "MIDI文件导入完成。");
            }
            catch (OperationCanceledException)
            {
                await _dialogService.ShowInfoDialogAsync("信息", "MIDI文件导入已取消。");
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorDialogAsync("错误", $"导入MIDI文件失败：{ex.Message}");
                System.Diagnostics.Debug.WriteLine($"导入MIDI文件时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 导入MIDI文件命令
        /// </summary>
        [RelayCommand]
        private async Task ImportMidiFileAsync()
        {
            try
            {
                // 获取用户选择的MIDI文件路径
                var filePath = await _dialogService.ShowOpenFileDialogAsync(
                    "选择MIDI文件",
                    new string[] { "*.mid", "*.midi" });

                if (string.IsNullOrEmpty(filePath))
                {
                    return;
                }

                await ImportMidiFileAsync(filePath);
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorDialogAsync("错误", $"导入MIDI文件失败：{ex.Message}");
                System.Diagnostics.Debug.WriteLine($"导入MIDI文件时发生错误: {ex.Message}");
            }
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

        #region 私有方法

        /// <summary>
        /// 处理音轨选择器属性变化
        /// </summary>
        private void OnTrackSelectorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TrackSelectorViewModel.SelectedTrack))
            {
                // 当前选中的音轨发生变化时，更新钢琴卷帘的当前音轨
                if (TrackSelector.SelectedTrack != null)
                {
                    var selectedTrackIndex = TrackSelector.SelectedTrack.TrackNumber - 1; // TrackNumber从1开始，索引从0开始
                    PianoRoll.SetCurrentTrackIndex(selectedTrackIndex);
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
                System.Diagnostics.Debug.WriteLine($"初始化欢迎消息时发生错误: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"刷新UI时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 批量添加音符到钢琴卷帘，优化性能
        /// </summary>
        /// <param name="notes">要添加的音符集合</param>
        private void AddNotesInBatch(IEnumerable<Models.Music.Note> notes)
        {
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
        }

        #endregion
    }
}