using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models.Music;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor;
using EnderDebugger;

namespace Lumino.ViewModels
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
        private readonly EnderLogger _logger;
        #endregion

        #region 属性
        /// <summary>
        /// 欢迎消息 - 可通过配置或本地化服务获取
        /// </summary>
        [ObservableProperty]
        private string _greeting = "欢迎使用 Lumino！";

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
        {
              _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
              _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
              _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
              _projectStorageService = projectStorageService ?? throw new ArgumentNullException(nameof(projectStorageService));
              _logger = EnderLogger.Instance;

              _logger.Info("MainWindowViewModel", "[EnderDebugger][2025-10-02 18:41:03.114][EnderLogger][MainWindowViewModel]主窗口ViewModel已创建");
              // 初始化欢迎消息
              InitializeGreetingMessage();
        }

        /// <summary>
        /// 异步初始化方法
        /// </summary>
        public async Task InitializeAsync()
        {
            _logger.Debug("MainWindowViewModel", "开始初始化主窗口");
            
            // 异步创建PianoRollViewModel
            PianoRoll = await Task.Run(() => new PianoRollViewModel());
            _logger.Debug("MainWindowViewModel", "PianoRollViewModel 创建完成");

            // 创建音轨选择器ViewModel
            TrackSelector = await Task.Run(() => new TrackSelectorViewModel());
            _logger.Debug("MainWindowViewModel", "TrackSelectorViewModel 创建完成");

            // 建立音轨选择器和钢琴卷帘之间的通信
            TrackSelector.PropertyChanged += OnTrackSelectorPropertyChanged;
            
            // 初始化CurrentTrack
            if (TrackSelector != null && TrackSelector.SelectedTrack != null && PianoRoll != null)
            {
                var selectedTrackIndex = TrackSelector.SelectedTrack.TrackNumber - 1;
                PianoRoll.SetCurrentTrackIndex(selectedTrackIndex);
                PianoRoll.SetCurrentTrack(TrackSelector.SelectedTrack);
                
                // 监听Tracks集合变化，确保CurrentTrack始终与CurrentTrackIndex保持同步
                if (TrackSelector.Tracks is INotifyCollectionChanged tracksCollection)
                {
                    tracksCollection.CollectionChanged += OnTracksCollectionChanged;
                }
            }
            
            _logger.Info("MainWindowViewModel", "主窗口初始化完成");
        }
        
        /// <summary>
        /// 处理音轨集合变化
        /// </summary>
        private void OnTracksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (TrackSelector != null && PianoRoll != null)
            {
                PianoRoll.UpdateCurrentTrackFromTrackList(TrackSelector.Tracks);
            }
        }
        
        /// <summary>
        /// 设计时构造函数 - 仅用于XAML设计器
        /// </summary>
        public MainWindowViewModel() : this(
            new Lumino.Services.Implementation.SettingsService(),
            CreateDesignTimeDialogService(),
            new Lumino.Services.Implementation.ApplicationService(),
            new Lumino.Services.Implementation.ProjectStorageService())
        {
            // 直接创建PianoRollViewModel用于设计时
            PianoRoll = new PianoRollViewModel();

            // 创建音轨选择器ViewModel
            TrackSelector = new TrackSelectorViewModel();

            // 建立音轨选择器和钢琴卷帘之间的通信
            TrackSelector.PropertyChanged += OnTrackSelectorPropertyChanged;
        }
        
        /// <summary>
        /// 创建设计时使用的对话框服务
        /// </summary>
        private static IDialogService CreateDesignTimeDialogService()
        {
            var loggingService = new Lumino.Services.Implementation.LoggingService();
            return new Lumino.Services.Implementation.DialogService(null, loggingService);
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
                _logger.Debug("MainWindowViewModel", "开始执行新建文件命令");
                
                // 检查是否有未保存的更改
                if (!await _applicationService.CanShutdownSafelyAsync())
                {
                    var shouldProceed = await _dialogService.ShowConfirmationDialogAsync(
                        "确认", "当前项目有未保存的更改，是否继续创建新文件？");
                    
                    if (!shouldProceed)
                    {
                        _logger.Debug("MainWindowViewModel", "用户取消新建文件操作");
                        return;
                    }
                }

                // 清空当前项目
                        _logger.Info("MainWindowViewModel", "[EnderDebugger][2025-10-02 18:41:03.114][EnderLogger][MainWindowViewModel]开始异步初始化主窗口");
                        // 异步创建PianoRollViewModel
                        PianoRoll = await Task.Run(() => new PianoRollViewModel());
                        _logger.Info("MainWindowViewModel", "[EnderDebugger][2025-10-02 18:41:03.114][EnderLogger][MainWindowViewModel]PianoRollViewModel 创建完成");

                        // 创建音轨选择器ViewModel
                        TrackSelector = await Task.Run(() => new TrackSelectorViewModel());
                        _logger.Info("MainWindowViewModel", "[EnderDebugger][2025-10-02 18:41:03.114][EnderLogger][MainWindowViewModel]TrackSelectorViewModel 创建完成");

                        // 建立音轨选择器和钢琴卷帘之间的通信
                        TrackSelector.PropertyChanged += OnTrackSelectorPropertyChanged;

                        // 初始化CurrentTrack
                        if (TrackSelector != null && TrackSelector.SelectedTrack != null && PianoRoll != null)
                        {
                            var selectedTrackIndex = TrackSelector.SelectedTrack.TrackNumber - 1;
                            PianoRoll.SetCurrentTrackIndex(selectedTrackIndex);
                            PianoRoll.SetCurrentTrack(TrackSelector.SelectedTrack);
                            // 监听Tracks集合变化，确保CurrentTrack始终与CurrentTrackIndex保持同步
                            if (TrackSelector.Tracks is INotifyCollectionChanged tracksCollection)
                            {
                                tracksCollection.CollectionChanged += OnTracksCollectionChanged;
                            }
                        }
                        _logger.Info("MainWindowViewModel", "[EnderDebugger][2025-10-02 18:41:03.114][EnderLogger][MainWindowViewModel]主窗口初始化完成");
                // 检查是否有未保存的更改
                if (!await _applicationService.CanShutdownSafelyAsync())
                {
                    var shouldProceed = await _dialogService.ShowConfirmationDialogAsync(
                        "确认", "当前项目有未保存的更改，是否继续打开新文件？");
                    
                    if (!shouldProceed)
                    {
                        _logger.Debug("MainWindowViewModel", "用户取消打开文件操作");
                        return;
                    }
                }

                var filePath = await _dialogService.ShowOpenFileDialogAsync(
                    "打开MIDI文件", 
                    new[] { "*.mid", "*.midi", "*.dmn" }); // dmn可能是Lumino的项目格式

                if (!string.IsNullOrEmpty(filePath))
                {
                    _logger.Debug("MainWindowViewModel", $"用户选择文件: {filePath}");
                    
                    // 判断文件类型
                    var extension = Path.GetExtension(filePath).ToLower();
                    
                    if (extension == ".mid" || extension == ".midi")
                    {
                        await ImportMidiFileAsync(filePath);
                    }
                    else if (extension == ".dmn")
                    {
                        // TODO: 实现Lumino项目文件的加载
                        await _dialogService.ShowInfoDialogAsync("信息", "Lumino项目文件加载功能将在后续版本中实现");
                    }
                }
                else
                {
                    _logger.Debug("MainWindowViewModel", "用户取消文件选择");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("MainWindowViewModel", "打开文件时发生错误");
                _logger.LogException(ex);
                await _dialogService.ShowErrorDialogAsync("错误", $"打开文件时发生错误：{ex.Message}");
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
                _logger.Debug("MainWindowViewModel", "开始执行保存文件命令");
                
                if (PianoRoll == null) 
                {
                    _logger.Debug("MainWindowViewModel", "PianoRoll为空，无法保存文件");
                    return;
                }
                
                // 获取所有音符
                var allNotes = PianoRoll.GetAllNotes().Select(vm => vm.ToNoteModel()).ToList();
                _logger.Debug("MainWindowViewModel", $"获取到 {allNotes.Count} 个音符用于导出");
                
                // 显示保存文件对话框
                var filePath = await _dialogService.ShowSaveFileDialogAsync(
                    "导出MIDI文件",
                    null,
                    new[] { "*.mid" });

                if (string.IsNullOrEmpty(filePath))
                {
                    _logger.Debug("MainWindowViewModel", "用户取消文件保存");
                    return;
                }

                // 确保文件扩展名为.mid
                if (!filePath.EndsWith(".mid", StringComparison.OrdinalIgnoreCase))
                {
                    filePath += ".mid";
                }
                
                _logger.Debug("MainWindowViewModel", $"准备导出MIDI文件到: {filePath}");

                // 使用DialogService的RunWithProgressAsync方法来处理带进度的操作
                await _dialogService.RunWithProgressAsync("导出MIDI文件", async (progress, cancellationToken) =>
                {
                    progress.Report((0, "正在导出MIDI文件..."));
                    _logger.Debug("MainWindowViewModel", "开始导出MIDI文件");

                    // 异步导出MIDI文件
                    bool success = await _projectStorageService.ExportMidiAsync(filePath, allNotes);

                    if (success)
                    {
                        progress.Report((100, "MIDI文件导出完成"));
                        _logger.Info("MainWindowViewModel", "MIDI文件导出成功");
                        await _dialogService.ShowInfoDialogAsync("成功", "MIDI文件导出完成。");
                    }
                    else
                    {
                        _logger.Error("MainWindowViewModel", "MIDI文件导出失败");
                        await _dialogService.ShowErrorDialogAsync("错误", "MIDI文件导出失败。");
                    }
                }, canCancel: true);
            }
            catch (OperationCanceledException)
            {
                _logger.Info("MainWindowViewModel", "MIDI文件导出已取消");
                await _dialogService.ShowInfoDialogAsync("信息", "MIDI文件导出已取消。");
            }
            catch (Exception ex)
            {
                _logger.Error("MainWindowViewModel", "导出MIDI文件时发生错误");
                _logger.LogException(ex);
                await _dialogService.ShowErrorDialogAsync("错误", $"导出MIDI文件失败：{ex.Message}");
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
                _logger.Debug("MainWindowViewModel", "开始执行打开设置对话框命令");
                
                var result = await _dialogService.ShowSettingsDialogAsync();
                _logger.Debug("MainWindowViewModel", $"设置对话框返回结果: {result}");
                
                if (result)
                {
                    _logger.Info("MainWindowViewModel", "设置已保存，开始刷新UI");
                    // 设置已保存，可能需要重新加载某些UI元素
                    await RefreshUIAfterSettingsChangeAsync();
                    _logger.Info("MainWindowViewModel", "设置UI刷新完成");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("MainWindowViewModel", "打开设置对话框时发生错误");
                _logger.LogException(ex);
                await _dialogService.ShowErrorDialogAsync("错误", $"打开设置时发生错误：{ex.Message}");
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
                _logger.Debug("MainWindowViewModel", "开始执行退出应用程序命令");
                
                // 检查是否可以安全退出
                if (await _applicationService.CanShutdownSafelyAsync())
                {
                    _logger.Info("MainWindowViewModel", "可以安全退出，开始关闭应用程序");
                    _applicationService.Shutdown();
                }
                else
                {
                    _logger.Debug("MainWindowViewModel", "有未保存的更改，显示确认对话框");
                    var shouldExit = await _dialogService.ShowConfirmationDialogAsync(
                        "确认退出", "有未保存的更改，是否确认退出？");
                    
                    if (shouldExit)
                    {
                        _logger.Info("MainWindowViewModel", "用户确认退出，开始关闭应用程序");
                        _applicationService.Shutdown();
                    }
                    else
                    {
                        _logger.Debug("MainWindowViewModel", "用户取消退出操作");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("MainWindowViewModel", "退出应用程序时发生错误");
                _logger.LogException(ex);
                await _dialogService.ShowErrorDialogAsync("错误", $"退出应用程序时发生错误：{ex.Message}");
                
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
                _logger.Debug("MainWindowViewModel", $"开始导入MIDI文件: {filePath}");
                
                // 使用DialogService的RunWithProgressAsync方法来处理带进度的操作
                await _dialogService.RunWithProgressAsync("导入MIDI文件", async (progress, cancellationToken) =>
                {
                    _logger.Debug("MainWindowViewModel", "开始异步导入MIDI文件");
                    
                    // 异步导入MIDI文件
                    var notes = await _projectStorageService.ImportMidiWithProgressAsync(filePath, progress, cancellationToken);
                    _logger.Debug("MainWindowViewModel", $"成功导入 {notes.Count()} 个音符");

                    // 在导入过程中获取MIDI文件的时长信息
                    var midiFile = await MidiReader.MidiFile.LoadFromFileAsync(filePath, null, cancellationToken);
                    var statistics = midiFile.GetStatistics();
                    
                    // 计算MIDI文件的总时长（以四分音符为单位）
                    var estimatedDurationSeconds = statistics.EstimatedDurationSeconds();
                    var durationInQuarterNotes = estimatedDurationSeconds / 0.5; // 120 BPM = 0.5秒每四分音符
                    _logger.Debug("MainWindowViewModel", $"MIDI文件时长: {estimatedDurationSeconds:F1} 秒, 四分音符数: {durationInQuarterNotes:F1}");

                    // 在UI线程中更新UI
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (PianoRoll == null || TrackSelector == null) 
                        {
                            _logger.Debug("MainWindowViewModel", "PianoRoll或TrackSelector为空，无法更新UI");
                            return;
                        }
                        
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
                            _logger.Debug("MainWindowViewModel", $"最大音轨索引: {maxTrackIndex}");
                            
                            // 检查并添加所需的音轨
                            while (TrackSelector.Tracks.Count <= maxTrackIndex)
                            {
                                TrackSelector.AddTrack();
                            }
                        }
                        
                        // 选中第一个非Conductor音轨（如果有音轨）
                        var firstNonConductorTrack = TrackSelector.Tracks.FirstOrDefault(t => !t.IsConductorTrack);
                        if (firstNonConductorTrack != null)
                        {
                            firstNonConductorTrack.IsSelected = true;
                            _logger.Debug("MainWindowViewModel", "已选中第一个非Conductor音轨");
                        }
                        else if (TrackSelector.Tracks.Count > 0)
                        {
                            // 如果只有Conductor轨，则选择它
                            var firstTrack = TrackSelector.Tracks[0];
                            firstTrack.IsSelected = true;
                            _logger.Debug("MainWindowViewModel", "已选中第一个音轨（Conductor轨）");
                        }
                        
                        // 批量添加音符
                        AddNotesInBatch(notes);
                        _logger.Debug("MainWindowViewModel", "音符批量添加完成");
                    });
                    
                    progress.Report((100, $"成功导入MIDI文件，共加载了 {notes.Count()} 个音符。文件时长：约 {estimatedDurationSeconds:F1} 秒"));
                    
                }, canCancel: true);
                
                _logger.Info("MainWindowViewModel", "MIDI文件导入完成");
                await _dialogService.ShowInfoDialogAsync("成功", "MIDI文件导入完成。");
            }
            catch (OperationCanceledException)
            {
                _logger.Info("MainWindowViewModel", "MIDI文件导入已取消");
                await _dialogService.ShowInfoDialogAsync("信息", "MIDI文件导入已取消。");
            }
            catch (Exception ex)
            {
                _logger.Error("MainWindowViewModel", "导入MIDI文件时发生错误");
                _logger.LogException(ex);
                await _dialogService.ShowErrorDialogAsync("错误", $"导入MIDI文件失败：{ex.Message}");
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
                _logger.Debug("MainWindowViewModel", "开始执行导入MIDI文件命令");
                
                // 获取用户选择的MIDI文件路径
                var filePath = await _dialogService.ShowOpenFileDialogAsync(
                    "选择MIDI文件",
                    new string[] { "*.mid", "*.midi" });

                if (string.IsNullOrEmpty(filePath))
                {
                    _logger.Debug("MainWindowViewModel", "用户取消文件选择");
                    return;
                }

                _logger.Debug("MainWindowViewModel", $"用户选择MIDI文件: {filePath}");
                await ImportMidiFileAsync(filePath);
            }
            catch (Exception ex)
            {
                _logger.Error("MainWindowViewModel", "导入MIDI文件时发生错误");
                _logger.LogException(ex);
                await _dialogService.ShowErrorDialogAsync("错误", $"导入MIDI文件失败：{ex.Message}");
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
                if (TrackSelector != null && TrackSelector.SelectedTrack != null && PianoRoll != null)
                {
                    var selectedTrackIndex = TrackSelector.SelectedTrack.TrackNumber - 1; // TrackNumber从1开始，索引从0开始
                    PianoRoll.SetCurrentTrackIndex(selectedTrackIndex);
                    
                    // 同时更新CurrentTrack属性，确保IsCurrentTrackConductor正确工作
                    PianoRoll.SetCurrentTrack(TrackSelector.SelectedTrack);
                    
                    // 确保切换音轨后滚动系统工作正常
                    PianoRoll.ForceRefreshScrollSystem();
                    
                    _logger.Debug("MainWindowViewModel", $"切换到音轨 {selectedTrackIndex}，强制刷新滚动系统");
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
                _logger.Debug("MainWindowViewModel", "开始初始化欢迎消息");
                var appInfo = _applicationService.GetApplicationInfo();
                Greeting = $"欢迎使用 {appInfo.Name} v{appInfo.Version}！";
                _logger.Debug("MainWindowViewModel", $"欢迎消息设置完成: {Greeting}");
            }
            catch (Exception ex)
            {
                _logger.Error("MainWindowViewModel", "初始化欢迎消息时发生错误");
                _logger.LogException(ex);
                Greeting = "欢迎使用 Lumino！";
            }
        }

        /// <summary>
        /// 设置更改后刷新UI
        /// </summary>
        private async Task RefreshUIAfterSettingsChangeAsync()
        {
            try
            {
                _logger.Debug("MainWindowViewModel", "开始刷新设置更改后的UI");
                
                // 重新初始化欢迎消息（可能语言已更改）
                InitializeGreetingMessage();

                // 通知PianoRoll等子组件刷新
                // 这里可以发送消息或调用相应的刷新方法

                _logger.Debug("MainWindowViewModel", "UI刷新完成");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error("MainWindowViewModel", "刷新UI时发生错误");
                _logger.LogException(ex);
            }
        }

        /// <summary>
        /// 批量添加音符到钢琴卷帘，优化性能
        /// </summary>
        /// <param name="notes">要添加的音符集合</param>
        private void AddNotesInBatch(IEnumerable<Models.Music.Note> notes)
        {
            _logger.Debug("MainWindowViewModel", $"开始批量添加 {notes.Count()} 个音符到钢琴卷帘");
            
            if (PianoRoll == null) 
            {
                _logger.Debug("MainWindowViewModel", "PianoRoll为空，无法添加音符");
                return;
            }
            
            // 检查当前轨道是否为Conductor轨，如果是则禁止创建音符
            if (PianoRoll.IsCurrentTrackConductor)
            {
                _logger.Debug("MainWindowViewModel", "禁止在Conductor轨上创建音符");
                return;
            }
            
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
            _logger.Debug("MainWindowViewModel", "音符批量添加完成");
            
            // 批量添加后强制刷新滚动系统，确保滚动范围正确更新
            PianoRoll.ForceRefreshScrollSystem();
            _logger.Debug("MainWindowViewModel", "滚动系统刷新完成");
        }

        /// <summary>
        /// 测试滚动系统的诊断方法（调试用）
        /// </summary>
        [RelayCommand]
        private async Task TestScrollSystemAsync()
        {
            try
            {
                _logger.Debug("MainWindowViewModel", "开始执行滚动系统诊断");
                
                if (PianoRoll == null) 
                {
                    _logger.Debug("MainWindowViewModel", "PianoRoll为空，无法执行诊断");
                    return;
                }
                
                var diagnostics = PianoRoll.GetScrollDiagnostics();
                _logger.Debug("MainWindowViewModel", $"滚动系统诊断结果: {diagnostics}");
                await _dialogService.ShowInfoDialogAsync("滚动系统诊断", diagnostics);
            }
            catch (Exception ex)
            {
                _logger.Error("MainWindowViewModel", "滚动系统诊断失败");
                _logger.LogException(ex);
                await _dialogService.ShowErrorDialogAsync("错误", $"滚动系统诊断失败：{ex.Message}");
            }
        }

        #endregion
    }
}