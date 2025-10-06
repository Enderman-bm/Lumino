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
        private readonly IViewModelFactory _viewModelFactory;
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

        /// <summary>
        /// 音轨总览ViewModel - 显示所有音轨及其音符预览
        /// </summary>
        [ObservableProperty]
        private TrackOverviewViewModel? _trackOverview;

        /// <summary>
        /// 项目设置
        /// </summary>
        [ObservableProperty]
        private Models.ProjectSettings _projectSettings = new Models.ProjectSettings();

        /// <summary>
        /// 窗口标题
        /// </summary>
        [ObservableProperty]
        private string _windowTitle = "未命名 - Lumino";
        #endregion

        #region 构造函数
        /// <summary>
        /// 主构造函数 - 通过依赖注入获取所需服务
        /// </summary>
        public MainWindowViewModel(
            ISettingsService settingsService,
            IDialogService dialogService,
            IApplicationService applicationService,
            IProjectStorageService projectStorageService,
            IViewModelFactory viewModelFactory)
        {
              _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
              _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
              _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
              _projectStorageService = projectStorageService ?? throw new ArgumentNullException(nameof(projectStorageService));
              _viewModelFactory = viewModelFactory ?? throw new ArgumentNullException(nameof(viewModelFactory));
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
            PianoRoll = await Task.Run(() => _viewModelFactory.CreatePianoRollViewModel());
            _logger.Debug("MainWindowViewModel", "PianoRollViewModel 创建完成");

            // 创建音轨选择器ViewModel
            TrackSelector = await Task.Run(() => new TrackSelectorViewModel());
            _logger.Debug("MainWindowViewModel", "TrackSelectorViewModel 创建完成");

            // 创建音轨总览ViewModel
            TrackOverview = await Task.Run(() => new TrackOverviewViewModel());
            _logger.Debug("MainWindowViewModel", "TrackOverviewViewModel 创建完成");

            // 建立音轨选择器和钢琴卷帘之间的通信
            TrackSelector.PropertyChanged += OnTrackSelectorPropertyChanged;
            
            // 订阅工具栏的工程设置请求事件
            if (PianoRoll != null && PianoRoll.Toolbar != null)
            {
                PianoRoll.Toolbar.ProjectSettingsRequested += OnProjectSettingsRequested;
            }
            
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
        /// 处理工程设置请求
        /// </summary>
        private async void OnProjectSettingsRequested()
        {
            await OpenProjectSettingsAsync();
        }
        
        /// <summary>
        /// 设计时构造函数 - 仅用于XAML设计器
        /// </summary>
        public MainWindowViewModel() : this(
            new Lumino.Services.Implementation.SettingsService(),
            CreateDesignTimeDialogService(),
            new Lumino.Services.Implementation.ApplicationService(new Lumino.Services.Implementation.SettingsService()),
            new Lumino.Services.Implementation.ProjectStorageService(),
            new Lumino.Services.Implementation.ViewModelFactory(
                new Lumino.Services.Implementation.CoordinateService(),
                new Lumino.Services.Implementation.SettingsService()))
        {
            // 直接创建PianoRollViewModel用于设计时
            PianoRoll = _viewModelFactory.CreatePianoRollViewModel();

            // 创建音轨选择器ViewModel
            TrackSelector = new TrackSelectorViewModel();

            // 创建音轨总览ViewModel
            TrackOverview = new TrackOverviewViewModel();

            // 建立音轨选择器和钢琴卷帘之间的通信
            TrackSelector.PropertyChanged += OnTrackSelectorPropertyChanged;
        }
        
        /// <summary>
        /// 创建设计时使用的对话框服务
        /// </summary>
        private static IDialogService CreateDesignTimeDialogService()
        {
            var loggingService = new Lumino.Services.Implementation.LoggingService();
            var viewModelFactory = new Lumino.Services.Implementation.ViewModelFactory(
                new Lumino.Services.Implementation.CoordinateService(),
                new Lumino.Services.Implementation.SettingsService());
            return new Lumino.Services.Implementation.DialogService(viewModelFactory, loggingService);
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
                _logger.Info("MainWindowViewModel", "开始异步初始化主窗口");
                // 异步创建PianoRollViewModel
                PianoRoll = await Task.Run(() => _viewModelFactory.CreatePianoRollViewModel());
                _logger.Info("MainWindowViewModel", "PianoRollViewModel 创建完成");

                // 创建音轨选择器ViewModel
                TrackSelector = await Task.Run(() => new TrackSelectorViewModel());
                _logger.Info("MainWindowViewModel", "TrackSelectorViewModel 创建完成");

                // 创建音轨总览ViewModel
                TrackOverview = await Task.Run(() => new TrackOverviewViewModel());
                _logger.Info("MainWindowViewModel", "TrackOverviewViewModel 创建完成");

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
            catch (Exception ex)
            {
                _logger.Error("MainWindowViewModel", "新建文件时发生错误");
                _logger.LogException(ex);
                await _dialogService.ShowErrorDialogAsync("错误", $"新建文件失败：{ex.Message}");
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

                    // 异步导出MIDI文件，传入项目设置
                    bool success = await _projectStorageService.ExportMidiAsync(filePath, allNotes, ProjectSettings);

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
        /// 打开文件命令
        /// </summary>
        [RelayCommand]
        private async Task OpenFileAsync()
        {
            try
            {
                _logger.Debug("MainWindowViewModel", "开始执行打开文件命令");
                
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
        /// 打开工程设置对话框
        /// </summary>
        public async Task OpenProjectSettingsAsync()
        {
            try
            {
                _logger.Debug("MainWindowViewModel", "开始打开工程设置对话框");
                
                var window = new Views.ProjectSettingsWindow
                {
                    DataContext = new ProjectSettingsViewModel(ProjectSettings, OnProjectSettingsSaved)
                };

                if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    if (desktop.MainWindow != null)
                    {
                        await window.ShowDialog(desktop.MainWindow);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("MainWindowViewModel", "打开工程设置对话框时发生错误");
                _logger.LogException(ex);
                await _dialogService.ShowErrorDialogAsync("错误", $"打开工程设置时发生错误：{ex.Message}");
            }
        }

        /// <summary>
        /// 工程设置保存回调
        /// </summary>
        private void OnProjectSettingsSaved(Models.ProjectSettings settings)
        {
            _logger.Info("MainWindowViewModel", $"工程设置已保存: BPM={settings.BPM}, PPQ={settings.PPQ}, ProjectName={settings.ProjectName}");
            
            // 更新窗口标题
            UpdateWindowTitle();
        }

        /// <summary>
        /// 更新窗口标题
        /// </summary>
        private void UpdateWindowTitle()
        {
            if (string.IsNullOrWhiteSpace(ProjectSettings.ProjectName))
            {
                WindowTitle = "未命名 - Lumino";
            }
            else
            {
                WindowTitle = $"{ProjectSettings.ProjectName} - Lumino";
            }
            
            _logger.Debug("MainWindowViewModel", $"窗口标题已更新: {WindowTitle}");
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
            if (TrackSelector != null)
            {
                TrackSelector.CurrentView = viewType;
            }
        }

        /// <summary>
        /// 撤销命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanUndo))]
        private void Undo()
        {
            PianoRoll?.Undo();
        }

        /// <summary>
        /// 重做命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanRedo))]
        private void Redo()
        {
            PianoRoll?.Redo();
        }

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        private bool CanUndo => PianoRoll?.CanUndo ?? false;

        /// <summary>
        /// 是否可以重做
        /// </summary>
        private bool CanRedo => PianoRoll?.CanRedo ?? false;

        /// <summary>
        /// 复制命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCopy))]
        private void Copy()
        {
            PianoRoll?.CopySelectedNotes();
        }

        /// <summary>
        /// 粘贴命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanPaste))]
        private void Paste()
        {
            PianoRoll?.PasteNotes();
        }

        /// <summary>
        /// 全选命令
        /// </summary>
        [RelayCommand]
        private void SelectAll()
        {
            PianoRoll?.SelectAllNotes();
        }

        /// <summary>
        /// 取消选择命令
        /// </summary>
        [RelayCommand]
        private void DeselectAll()
        {
            PianoRoll?.DeselectAllNotes();
        }

        /// <summary>
        /// 删除选中的音符命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDelete))]
        private void Delete()
        {
            PianoRoll?.DeleteSelectedNotes();
        }

        /// <summary>
        /// 剪切选中的音符命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCut))]
        private void Cut()
        {
            PianoRoll?.CutSelectedNotes();
        }

        /// <summary>
        /// 复制选中的音符命令（创建副本）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDuplicate))]
        private void Duplicate()
        {
            PianoRoll?.DuplicateSelectedNotes();
        }

        /// <summary>
        /// 量化选中的音符命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanQuantize))]
        private void Quantize()
        {
            PianoRoll?.QuantizeSelectedNotes();
        }

        /// <summary>
        /// 是否可以复制
        /// </summary>
        private bool CanCopy => PianoRoll?.HasSelectedNotes ?? false;

        /// <summary>
        /// 是否可以粘贴
        /// </summary>
        private bool CanPaste => PianoRoll?.CanPaste ?? false;

        /// <summary>
        /// 是否可以删除
        /// </summary>
        private bool CanDelete => PianoRoll?.HasSelectedNotes ?? false;

        /// <summary>
        /// 是否可以剪切
        /// </summary>
        private bool CanCut => PianoRoll?.HasSelectedNotes ?? false;

        /// <summary>
        /// 是否可以复制（创建副本）
        /// </summary>
        private bool CanDuplicate => PianoRoll?.HasSelectedNotes ?? false;

        /// <summary>
        /// 是否可以量化
        /// </summary>
        private bool CanQuantize => PianoRoll?.HasSelectedNotes ?? false;

        /// <summary>
        /// 放大命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanZoomIn))]
        private void ZoomIn()
        {
            PianoRoll?.ZoomIn();
        }

        /// <summary>
        /// 缩小命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanZoomOut))]
        private void ZoomOut()
        {
            PianoRoll?.ZoomOut();
        }

        /// <summary>
        /// 适应窗口命令
        /// </summary>
        [RelayCommand]
        private void FitToWindow()
        {
            PianoRoll?.FitToWindow();
        }

        /// <summary>
        /// 重置缩放命令
        /// </summary>
        [RelayCommand]
        private void ResetZoom()
        {
            PianoRoll?.ResetZoom();
        }

        /// <summary>
        /// 是否可以放大
        /// </summary>
        private bool CanZoomIn => PianoRoll?.CanZoomIn ?? false;

        /// <summary>
        /// 是否可以缩小
        /// </summary>
        private bool CanZoomOut => PianoRoll?.CanZoomOut ?? false;

        /// <summary>
        /// 选择工具命令
        /// </summary>
        [RelayCommand]
        private void SelectTool()
        {
            PianoRoll?.SelectSelectionTool();
        }

        /// <summary>
        /// 铅笔工具命令
        /// </summary>
        [RelayCommand]
        private void PencilTool()
        {
            PianoRoll?.SelectPencilTool();
        }

        /// <summary>
        /// 橡皮工具命令
        /// </summary>
        [RelayCommand]
        private void EraserTool()
        {
            PianoRoll?.SelectEraserTool();
        }

        /// <summary>
        /// 切割工具命令
        /// </summary>
        [RelayCommand]
        private void CutTool()
        {
            PianoRoll?.SelectCutTool();
        }

        /// <summary>
        /// 播放命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanPlay))]
        private void Play()
        {
            PianoRoll?.Play();
        }

        /// <summary>
        /// 暂停命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanPause))]
        private void Pause()
        {
            PianoRoll?.Pause();
        }

        /// <summary>
        /// 停止命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStop))]
        private void Stop()
        {
            PianoRoll?.Stop();
        }

        /// <summary>
        /// 添加音轨命令
        /// </summary>
        [RelayCommand]
        private void AddTrack()
        {
            TrackSelector?.AddTrack();
        }

        /// <summary>
        /// 删除音轨命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanRemoveTrack))]
        private void RemoveTrack()
        {
            TrackSelector?.RemoveSelectedTrack();
        }

        /// <summary>
        /// 是否选择工具处于激活状态
        /// </summary>
        private bool IsSelectToolActive => PianoRoll?.CurrentTool == EditorTool.Select;

        /// <summary>
        /// 是否铅笔工具处于激活状态
        /// </summary>
        private bool IsPencilToolActive => PianoRoll?.CurrentTool == EditorTool.Pencil;

        /// <summary>
        /// 是否橡皮工具处于激活状态
        /// </summary>
        private bool IsEraserToolActive => PianoRoll?.CurrentTool == EditorTool.Eraser;

        /// <summary>
        /// 是否切割工具处于激活状态
        /// </summary>
        private bool IsCutToolActive => PianoRoll?.CurrentTool == EditorTool.Cut;

        /// <summary>
        /// 是否可以播放
        /// </summary>
        private bool CanPlay => PianoRoll?.CanPlay ?? false;

        /// <summary>
        /// 是否可以暂停
        /// </summary>
        private bool CanPause => PianoRoll?.CanPause ?? false;

        /// <summary>
        /// 是否可以停止
        /// </summary>
        private bool CanStop => PianoRoll?.CanStop ?? false;

        /// <summary>
        /// 是否可以删除音轨
        /// </summary>
        private bool CanRemoveTrack => TrackSelector?.CanRemoveSelectedTrack ?? false;

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
            
            // 将音符分成128段，使用并行处理
            var notesList = notes.ToList();
            var segmentSize = Math.Max(1, notesList.Count / 128);
            var segments = new List<List<Models.Music.Note>>();
            
            for (int i = 0; i < notesList.Count; i += segmentSize)
            {
                var segment = notesList.Skip(i).Take(segmentSize).ToList();
                segments.Add(segment);
            }
            
            // 使用128线程并行转换音符
            var noteViewModels = new System.Collections.Concurrent.ConcurrentBag<NoteViewModel>();
            System.Threading.Tasks.Parallel.ForEach(
                segments,
                new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 128 },
                segment =>
                {
                    foreach (var noteModel in segment)
                    {
                        var noteViewModel = new NoteViewModel
                        {
                            Pitch = noteModel.Pitch,
                            StartPosition = noteModel.StartPosition,
                            Duration = noteModel.Duration,
                            Velocity = noteModel.Velocity,
                            TrackIndex = noteModel.TrackIndex
                        };
                        
                        // 🔍 添加调试日志检查音符Duration
                        if (noteViewModel.Duration.ToDouble() < 0.01 || noteViewModel.Duration.ToDouble() > 100)
                        {
                            _logger.Debug("MainWindowViewModel", 
                                $"异常音符Duration: {noteViewModel.Duration.ToDouble():F6}, " +
                                $"Pitch={noteViewModel.Pitch}, StartPos={noteViewModel.StartPosition.ToDouble():F6}, " +
                                $"Track={noteViewModel.TrackIndex}");
                        }
                        
                        noteViewModels.Add(noteViewModel);
                    }
                });
            
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