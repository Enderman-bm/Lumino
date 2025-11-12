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
    /// 当前正在加载或已加载的文件名（显示在主界面状态栏）
    /// </summary>
    [ObservableProperty]
    private string _currentOpenedFileName = string.Empty;

    /// <summary>
    /// 当前正在加载或已加载的文件大小文本（显示在主界面状态栏）
    /// </summary>
    [ObservableProperty]
    private string _currentOpenedFileSizeText = string.Empty;

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
        /// 音频分析ViewModel - 处理音频文件分析和音符检测
        /// </summary>
        [ObservableProperty]
        private AudioAnalysisViewModel? _audioAnalysisViewModel;
        
        /// <summary>
        /// 日志查看器ViewModel - 处理系统日志查看和调试
        /// </summary>
        [ObservableProperty]
        private LogViewerViewModel? _logViewerViewModel;

        /// <summary>
        /// 播放ViewModel - 管理MIDI播放控制、进度和实时演奏指示
        /// </summary>
        [ObservableProperty]
        private PlaybackViewModel? _playbackViewModel;
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
            
            // 初始化CurrentTrack
            if (TrackSelector != null && TrackSelector.SelectedTrack != null && PianoRoll != null)
            {
                var selectedTrackIndex = TrackSelector.SelectedTrack.TrackNumber - 1;
                PianoRoll.SetCurrentTrackIndex(selectedTrackIndex);
                PianoRoll.UpdateCurrentTrackFromTrackList(new[] { TrackSelector.SelectedTrack });
                PianoRoll.SetTrackSelector(TrackSelector);
                
                // 设置 TrackSelector 的 Toolbar 引用，用于洋葱皮功能
                TrackSelector.Toolbar = PianoRoll.Toolbar;
                
                // 监听Tracks集合变化，确保CurrentTrack始终与CurrentTrackIndex保持同步
                if (TrackSelector.Tracks is INotifyCollectionChanged tracksCollection)
                {
                    tracksCollection.CollectionChanged += OnTracksCollectionChanged;
                }
            }
            
            // 创建音频分析ViewModel
            AudioAnalysisViewModel = await Task.Run(() => new AudioAnalysisViewModel(_dialogService));
            _logger.Debug("MainWindowViewModel", "AudioAnalysisViewModel 创建完成");

            // 建立音频分析和钢琴卷帘之间的连接（用于频谱图显示）
            if (AudioAnalysisViewModel != null && PianoRoll != null)
            {
                AudioAnalysisViewModel.PianoRollViewModel = PianoRoll;
                _logger.Debug("MainWindowViewModel", "已建立AudioAnalysisViewModel和PianoRollViewModel之间的连接");
            }

            // 建立播放ViewModel和钢琴卷帘之间的连接（用于播放头指示和实时演奏）
            if (PlaybackViewModel != null && PianoRoll != null)
            {
                PianoRoll.PlaybackViewModel = PlaybackViewModel;
                _logger.Debug("MainWindowViewModel", "已建立PlaybackViewModel和PianoRollViewModel之间的连接");
            }

            // 创建日志查看器ViewModel（仅在需要时创建，延迟初始化）
            _logger.Debug("MainWindowViewModel", "LogViewerViewModel 将在需要时延迟初始化");

            // 创建播放ViewModel - 注意：此时 PlaybackService 已在 App.InitializeServicesAsync 中初始化
            // 在这里我们创建一个占位符，实际的播放ViewModel将通过属性注入获得
            _logger.Debug("MainWindowViewModel", "PlaybackViewModel 将通过依赖注入获得");

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
                    PianoRoll.UpdateCurrentTrackFromTrackList(new[] { TrackSelector.SelectedTrack });
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
                var controllerEvents = CollectControllerEvents();
                var snapshot = new ProjectSnapshot
                {
                    Notes = allNotes,
                    ControllerEvents = controllerEvents
                };

                _logger.Debug("MainWindowViewModel", $"获取到 {allNotes.Count} 个音符、{controllerEvents.Count} 个控制事件用于导出");
                
                // 显示保存文件对话框（支持项目文件 .lmpf 与 MIDI 导出 .mid）
                var filePath = await _dialogService.ShowSaveFileDialogAsync(
                    "保存项目或导出 MIDI",
                    null,
                    new[] { "*.lmpf", "*.mid" });

                if (string.IsNullOrEmpty(filePath))
                {
                    _logger.Debug("MainWindowViewModel", "用户取消文件保存");
                    return;
                }

                var extension = System.IO.Path.GetExtension(filePath).ToLower();

                if (extension == ".lmpf")
                {
                    // 保存为 Lumino 项目文件 (.lmpf)
                    _logger.Debug("MainWindowViewModel", $"准备保存项目文件到: {filePath}");
                    var metadata = new Services.Interfaces.ProjectMetadata
                    {
                        Title = System.IO.Path.GetFileNameWithoutExtension(filePath) ?? "Untitled",
                        Created = DateTime.Now,
                        LastModified = DateTime.Now,
                        Tempo = 120.0
                    };

                    // 收集当前音轨的元数据（如果 TrackSelector 可用）
                    try
                    {
                        if (TrackSelector != null)
                        {
                            foreach (var t in TrackSelector.Tracks)
                            {
                                var tm = new Services.Interfaces.TrackMetadata
                                {
                                    TrackNumber = t.TrackNumber,
                                    TrackName = t.TrackName,
                                    MidiChannel = t.MidiChannel,
                                    ChannelGroupIndex = t.ChannelGroupIndex,
                                    ChannelNumberInGroup = t.ChannelNumberInGroup,
                                    Instrument = t.Instrument,
                                    ColorTag = t.ColorTag,
                                    IsConductorTrack = t.IsConductorTrack
                                };
                                metadata.Tracks.Add(tm);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("MainWindowViewModel", "收集音轨元数据时发生错误");
                        _logger.LogException(ex);
                    }

                    // 使用与加载相同的预加载/进度对话框以获得一致的动画体验
                    var fileSize = 0L;
                    try { fileSize = new System.IO.FileInfo(filePath).Length; } catch { fileSize = 0; }

                    var runResult = await _dialogService.ShowPreloadAndRunAsync<bool>(System.IO.Path.GetFileName(filePath), fileSize,
                        async (progress, cancellationToken) =>
                        {
                            // 保存项目逻辑
                            try
                            {
                                progress.Report((0, "正在保存项目..."));
                                var ok = await _projectStorageService.SaveProjectAsync(filePath, snapshot, metadata, cancellationToken);
                                if (ok)
                                {
                                    progress.Report((100, "项目保存完成"));
                                }
                                else
                                {
                                    progress.Report((100, "项目保存失败"));
                                }
                                return ok;
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                        }, canCancel: true);

                    if (runResult.Choice == Services.Interfaces.PreloadDialogResult.Cancel)
                    {
                        _logger.Info("MainWindowViewModel", "用户取消了项目保存");
                        await _dialogService.ShowInfoDialogAsync("信息", "项目保存已取消。");
                    }
                    else if (runResult.Choice == Services.Interfaces.PreloadDialogResult.Load)
                    {
                        var ok = runResult.Result;
                        if (ok)
                        {
                            _logger.Info("MainWindowViewModel", "项目文件保存成功");
                            await _dialogService.ShowInfoDialogAsync("成功", "项目已保存。");
                        }
                        else
                        {
                            _logger.Error("MainWindowViewModel", "项目文件保存失败");
                            await _dialogService.ShowErrorDialogAsync("错误", "项目保存失败。");
                        }
                    }
                }
                else
                {
                    // 默认导出为 MIDI
                    if (!filePath.EndsWith(".mid", StringComparison.OrdinalIgnoreCase))
                    {
                        filePath += ".mid";
                    }

                    _logger.Debug("MainWindowViewModel", $"准备导出MIDI文件到: {filePath}");

                    await _dialogService.RunWithProgressAsync("导出MIDI文件", async (progress, cancellationToken) =>
                    {
                        progress.Report((0, "正在导出MIDI文件..."));
                        _logger.Debug("MainWindowViewModel", "开始导出MIDI文件");

                        // 异步导出MIDI文件
                        bool success = await _projectStorageService.ExportMidiAsync(filePath, snapshot);

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
        /// 关闭文件命令
        /// </summary>
        [RelayCommand]
        private async Task CloseFileAsync()
        {
            try
            {
                _logger.Debug("MainWindowViewModel", "开始执行关闭文件命令");

                // 检查是否有未保存的更改
                if (!await _applicationService.CanShutdownSafelyAsync())
                {
                    var shouldProceed = await _dialogService.ShowConfirmationDialogAsync(
                        "确认", "当前项目有未保存的更改，是否关闭而不保存？");

                    if (!shouldProceed)
                    {
                        _logger.Debug("MainWindowViewModel", "用户取消关闭文件操作");
                        return;
                    }
                }

                // 清空当前文件信息
                _logger.Info("MainWindowViewModel", "清空项目内容");
                CurrentOpenedFileName = string.Empty;
                CurrentOpenedFileSizeText = string.Empty;

                // 清空PianoRoll内容
                if (PianoRoll != null)
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        PianoRoll.ClearContent();
                    });
                }

                // 清空TrackSelector内容
                if (TrackSelector != null)
                {
                    // 清空轨道列表（先备份属性变化事件处理程序）
                    TrackSelector.PropertyChanged -= OnTrackSelectorPropertyChanged;

                    // 清空音轨列表
                    TrackSelector.ClearTracks();

                    // 重新建立事件监听
                    TrackSelector.PropertyChanged += OnTrackSelectorPropertyChanged;
                }

                // 清空TrackOverview内容
                if (TrackOverview != null)
                {
                    // TrackOverview应该基于PianoRoll和TrackSelector的数据，自动更新
                    // 这里只需要确保相关数据已清空
                }

                _logger.Info("MainWindowViewModel", "文件已关闭，项目内容已清空");
                await _dialogService.ShowInfoDialogAsync("成功", "文件已关闭。");
            }
            catch (Exception ex)
            {
                _logger.Error("MainWindowViewModel", "关闭文件时发生错误");
                _logger.LogException(ex);
                await _dialogService.ShowErrorDialogAsync("错误", $"关闭文件失败：{ex.Message}");
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
                    "打开MIDI文件或项目", 
                    new[] { "*.mid", "*.midi", "*.lmpf", "*.dmn" });

                if (!string.IsNullOrEmpty(filePath))
                {
                    _logger.Debug("MainWindowViewModel", $"用户选择文件: {filePath}");
                    
                    // 判断文件类型
                    var extension = Path.GetExtension(filePath).ToLower();
                    
                    if (extension == ".mid" || extension == ".midi")
                    {
                        await ImportMidiFileAsync(filePath);
                    }
                    else if (extension == ".lmpf")
                    {
                        _logger.Debug("MainWindowViewModel", $"准备加载 Lumino 项目文件: {filePath}");

                        long fileSize = 0;
                        try { fileSize = new System.IO.FileInfo(filePath).Length; } catch { fileSize = 0; }

                        var runResult = await _dialogService.ShowPreloadAndRunAsync<System.Tuple<ProjectSnapshot, Services.Interfaces.ProjectMetadata>>(System.IO.Path.GetFileName(filePath), fileSize,
                            async (progress, cancellationToken) =>
                            {
                                var tuple = await _projectStorageService.LoadProjectAsync(filePath, cancellationToken);
                                return new System.Tuple<ProjectSnapshot, Services.Interfaces.ProjectMetadata>(tuple.snapshot, tuple.metadata);
                            }, canCancel: true);

                        if (runResult.Choice == Services.Interfaces.PreloadDialogResult.Cancel)
                        {
                            _logger.Info("MainWindowViewModel", "用户在预加载对话框中取消了项目加载");
                            return;
                        }

                        var resultTuple = runResult.Result;
                        if (resultTuple == null)
                        {
                            _logger.Error("MainWindowViewModel", "加载项目时未返回数据");
                            await _dialogService.ShowErrorDialogAsync("错误", "项目加载失败。");
                            return;
                        }

                        var snapshot = resultTuple.Item1 ?? new ProjectSnapshot();
                        var notes = snapshot.Notes?.ToList() ?? new List<Note>();
                        var metadata = resultTuple.Item2;

                        // 在UI线程中恢复音轨与音符
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            if (PianoRoll == null || TrackSelector == null)
                            {
                                _logger.Debug("MainWindowViewModel", "PianoRoll或TrackSelector为空，无法恢复项目");
                                return;
                            }

                            // 清空现有内容
                            PianoRoll.ClearContent();

                            // 如果 metadata 包含 Tracks 描述，则根据之创建/更新 TrackSelector
                            if (metadata?.Tracks != null && metadata.Tracks.Count > 0)
                            {
                                // 清空并按 metadata 重建轨道
                                TrackSelector.ClearTracks();

                                // 先添加 Conductor（如果 metadata 包含则以 metadata 的第一项为准）
                                var conductorMeta = metadata.Tracks.FirstOrDefault(t => t.IsConductorTrack);
                                if (conductorMeta != null)
                                {
                                    var conductor = new TrackViewModel(conductorMeta.TrackNumber, "COND", conductorMeta.TrackName, isConductorTrack: true);
                                    conductor.TrackSelector = TrackSelector;
                                    TrackSelector.Tracks.Add(conductor);
                                }
                                else
                                {
                                    // 保证至少有一个 Conductor
                                    var conductor = new TrackViewModel(0, "COND", "Conductor", isConductorTrack: true);
                                    conductor.TrackSelector = TrackSelector;
                                    TrackSelector.Tracks.Add(conductor);
                                }

                                // 按 metadata 列表创建其余 TrackViewModel
                                foreach (var src in metadata.Tracks.Where(t => !t.IsConductorTrack))
                                {
                                    // 生成通道名称（与 TrackSelector.GenerateChannelName 相同的规则）
                                    var letterIndex = (src.TrackNumber - 1) / 16;
                                    var numberIndex = ((src.TrackNumber - 1) % 16) + 1;
                                    var letter = (char)('A' + Math.Max(0, letterIndex));
                                    var channelName = $"{letter}{numberIndex}";
                                    var t = new TrackViewModel(src.TrackNumber, channelName, src.TrackName);
                                    t.TrackSelector = TrackSelector;
                                    t.MidiChannel = src.MidiChannel;
                                    t.ChannelGroupIndex = src.ChannelGroupIndex;
                                    t.ChannelNumberInGroup = src.ChannelNumberInGroup;
                                    t.Instrument = src.Instrument;
                                    t.ColorTag = src.ColorTag ?? "#FFFFFF";
                                    t.IsConductorTrack = src.IsConductorTrack;
                                    TrackSelector.Tracks.Add(t);
                                }
                            }

                            // 确保足够的轨道以容纳 notes 中的最大 TrackIndex
                            if (notes.Any())
                            {
                                int maxTrackIndex = notes.Max(n => n.TrackIndex);
                                while (TrackSelector.Tracks.Count <= maxTrackIndex)
                                {
                                    TrackSelector.AddTrack();
                                }
                            }

                            // 添加音符
                            var viewModels = notes.Select(n => new NoteViewModel
                            {
                                Pitch = n.Pitch,
                                StartPosition = n.StartPosition,
                                Duration = n.Duration,
                                Velocity = n.Velocity,
                                TrackIndex = n.TrackIndex
                            }).ToList();

                            await PianoRoll.AddNotesInBatchAsync(viewModels);
                            PianoRoll.SetControllerEvents(snapshot.ControllerEvents ?? new List<ControllerEvent>());
                            PianoRoll.ForceRefreshScrollSystem();

                            _logger.Info("MainWindowViewModel", "项目加载完成");
                            await _dialogService.ShowInfoDialogAsync("成功", "项目已加载。");
                        });
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

                // 在真正导入之前，使用集成到预加载对话框内的进度来执行导入任务
                while (true)
                {
                    long fileSize = 0;
                    try { fileSize = new System.IO.FileInfo(filePath).Length; } catch { fileSize = 0; }

                    // 更新主界面显示当前准备加载的文件名与大小
                    CurrentOpenedFileName = System.IO.Path.GetFileName(filePath);
                    CurrentOpenedFileSizeText = FormatFileSize(fileSize);

                    // 清空PianoRoll的内容以准备加载（UI刷新）
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => PianoRoll?.ClearContent());

                    var runResult = await _dialogService.ShowPreloadAndRunAsync<System.Collections.Generic.List<Models.Music.Note>>(CurrentOpenedFileName, fileSize,
                        async (progress, cancellationToken) =>
                        {
                            _logger.Debug("MainWindowViewModel", "开始异步导入MIDI文件（在预加载对话框内）");

                            // 执行带进度的导入操作（解析 MIDI）
                            var notes = await _projectStorageService.ImportMidiWithProgressAsync(filePath, progress, cancellationToken);
                            _logger.Debug("MainWindowViewModel", $"成功导入 {notes.Count()} 个音符（解析完成）");

                            // 报告进入添加音符阶段，让对话框切换为横向不确定性动画
                            try { progress.Report((100, "正在添加音符")); } catch { }

                            // 为了更新 UI（轨道信息、时长等），加载 MidiFile（轻量）
                            MidiReader.MidiFile? midiFile = null;
                            try
                            {
                                midiFile = await MidiReader.MidiFile.LoadFromFileAsync(filePath, null);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogException(ex, "加载 MidiFile 以获取统计信息时失败");
                            }

                            // 在 UI 线程中执行添加音符和相关 UI 更新（保持对话框打开时进行）
                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                            {
                                if (PianoRoll == null || TrackSelector == null)
                                {
                                    _logger.Debug("MainWindowViewModel", "PianoRoll或TrackSelector为空，无法在UI上添加音符");
                                    return;
                                }

                                // 清空现有内容
                                PianoRoll.ClearContent();

                                // 更新音轨列表以匹配MIDI文件中的音轨（若获得了 midiFile）
                                if (midiFile != null)
                                {
                                    TrackSelector.LoadTracksFromMidi(midiFile);

                                    var statistics = midiFile.GetStatistics();
                                    var estimatedDurationSeconds = statistics.EstimatedDurationSeconds();
                                    var durationInQuarterNotes = estimatedDurationSeconds / 0.5; // 120 BPM = 0.5秒每四分音符
                                    PianoRoll.SetMidiFileDuration(durationInQuarterNotes);
                                }

                                // 确保足够的轨道
                                if (notes.Any())
                                {
                                    int maxTrackIndex = notes.Max(n => n.TrackIndex);
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
                                }
                                else if (TrackSelector.Tracks.Count > 0)
                                {
                                    TrackSelector.Tracks[0].IsSelected = true;
                                }

                                // 构造视图模型并使用异步分块添加音符
                                var viewModels = notes.Select(n => new NoteViewModel
                                {
                                    Pitch = n.Pitch,
                                    StartPosition = n.StartPosition,
                                    Duration = n.Duration,
                                    Velocity = n.Velocity,
                                    TrackIndex = n.TrackIndex
                                }).ToList();

                                _logger.Debug("MainWindowViewModel", "开始在UI上异步添加音符（由预加载任务触发）");
                                await PianoRoll.AddNotesInBatchAsync(viewModels);
                                _logger.Debug("MainWindowViewModel", "音符异步批量添加完成（由预加载任务触发）");

                                // 批量添加后强制刷新滚动系统
                                PianoRoll.ForceRefreshScrollSystem();
                            });

                            return System.Linq.Enumerable.ToList(notes);
                        }, canCancel: true);

                    if (runResult.Choice == Lumino.Services.Interfaces.PreloadDialogResult.Cancel)
                    {
                        _logger.Info("MainWindowViewModel", "用户在预加载对话框中选择取消或取消导入，停止导入");
                        CurrentOpenedFileName = string.Empty;
                        CurrentOpenedFileSizeText = string.Empty;
                        return;
                    }
                    else if (runResult.Choice == Lumino.Services.Interfaces.PreloadDialogResult.Reselect)
                    {
                        // 重新选择文件
                        var newPath = await _dialogService.ShowOpenFileDialogAsync(
                            "选择MIDI文件",
                            new string[] { "*.mid", "*.midi" });

                        if (string.IsNullOrEmpty(newPath))
                        {
                            _logger.Debug("MainWindowViewModel", "用户取消重新选择文件");
                            CurrentOpenedFileName = string.Empty;
                            CurrentOpenedFileSizeText = string.Empty;
                            return;
                        }
                        filePath = newPath;
                        _logger.Debug("MainWindowViewModel", $"用户重新选择文件: {filePath}");
                        continue; // 重新显示预加载对话框并执行
                    }

                    // 如果Load且任务已成功完成，runResult.Result包含导入的音符列表
                    var notesList = runResult.Result ?? new System.Collections.Generic.List<Models.Music.Note>();

                    // 在导入过程中或导入后获取MIDI文件的时长信息（在UI线程以便后续更新UI）
                    var midiFile = await MidiReader.MidiFile.LoadFromFileAsync(filePath, null);
                    var statistics = midiFile.GetStatistics();
                    var estimatedDurationSeconds = statistics.EstimatedDurationSeconds();
                    var durationInQuarterNotes = estimatedDurationSeconds / 0.5; // 120 BPM = 0.5秒每四分音符

                    // 在UI线程中更新UI
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
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

                        // 确定MIDI文件中最大的音轨索引并添加必要的音轨
                        if (notesList.Any())
                        {
                            int maxTrackIndex = notesList.Max(n => n.TrackIndex);
                            _logger.Debug("MainWindowViewModel", $"最大音轨索引: {maxTrackIndex}");
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
                            var firstTrack = TrackSelector.Tracks[0];
                            firstTrack.IsSelected = true;
                            _logger.Debug("MainWindowViewModel", "已选中第一个音轨（Conductor轨）");
                        }

                        // 批量添加音符（异步分块以保持 UI 响应）
                        _logger.Debug("MainWindowViewModel", "开始在UI上异步添加音符");
                        var viewModels = notesList.Select(n => new NoteViewModel
                        {
                            Pitch = n.Pitch,
                            StartPosition = n.StartPosition,
                            Duration = n.Duration,
                            Velocity = n.Velocity,
                            TrackIndex = n.TrackIndex
                        }).ToList();

                        await PianoRoll.AddNotesInBatchAsync(viewModels);
                        _logger.Debug("MainWindowViewModel", "音符异步批量添加完成");

                        // 加载音符到播放ViewModel以支持播放功能
                        if (PlaybackViewModel != null && notesList.Count > 0)
                        {
                            try
                            {
                                // 获取MIDI文件的参数
                                int tpq = 480; // 标准MIDI TPQ值
                                double tempoMicroSeconds = 500000.0; // 默认 120 BPM (500000 μs/quarter)
                                
                                // 尝试从 midiFile 获取正确的TPQ和Tempo
                                // （实际的 API 可能因 MidiReader 库而异，这里使用安全的方式）
                                try
                                {
                                    if (midiFile != null)
                                    {
                                        // 假设 MidiFile 有类似的属性或方法
                                        // 这里我们使用默认值，开发者可以根据实际 MidiReader API 调整
                                        tpq = 480;
                                        tempoMicroSeconds = 500000.0;
                                    }
                                }
                                catch { /* 使用默认值 */ }
                                
                                PlaybackViewModel.LoadNotes(notesList, tpq, tempoMicroSeconds);
                                _logger.Debug("MainWindowViewModel", $"已加载 {notesList.Count} 个音符到播放系统（TPQ: {tpq}, Tempo: {tempoMicroSeconds} μs/quarter）");
                            }
                            catch (Exception exPlayback)
                            {
                                _logger.Warn("MainWindowViewModel", $"加载音符到播放系统失败: {exPlayback.Message}");
                                // 继续进行，不中断导入流程
                            }
                        }
                    });

                    _logger.Info("MainWindowViewModel", "MIDI文件导入完成");
                    await _dialogService.ShowInfoDialogAsync("成功", "MIDI文件导入完成。");
                    break;
                }
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
        private async Task SelectView(ViewType viewType)
        {
            CurrentView = viewType;
            if (TrackSelector != null)
            {
                TrackSelector.CurrentView = viewType;
            }
            
            // 如果选择的是日志查看器视图，进行延迟初始化
            if (viewType == ViewType.LogViewer && LogViewerViewModel == null)
            {
                await InitializeLogViewerAsync();
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
                    PianoRoll.UpdateCurrentTrackFromTrackList(new[] { TrackSelector.SelectedTrack });
                    
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

        private static string FormatFileSize(long bytes)
        {
            var kb = 1024.0;
            var mb = kb * 1024.0;
            var gb = mb * 1024.0;

            if (bytes >= gb) return (bytes / gb).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " GB";
            if (bytes >= mb) return (bytes / mb).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " MB";
            if (bytes >= kb) return (bytes / kb).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " KB";
            return bytes + " B";
        }

        /// <summary>
        /// 批量添加音符到钢琴卷帘，优化性能
        /// </summary>
        /// <param name="notes">要添加的音符集合</param>
    private async System.Threading.Tasks.Task AddNotesInBatch(IEnumerable<Models.Music.Note> notes)
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
                        
                        noteViewModels.Add(noteViewModel);
                    }
                });
            
            await PianoRoll.AddNotesInBatchAsync(noteViewModels);
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

        /// <summary>
        /// 初始化日志查看器命令（延迟初始化优化性能）
        /// </summary>
        [RelayCommand]
        private async Task InitializeLogViewerAsync()
        {
            try
            {
                if (LogViewerViewModel != null)
                {
                    _logger.Debug("MainWindowViewModel", "LogViewerViewModel 已存在，跳过初始化");
                    return;
                }

                _logger.Debug("MainWindowViewModel", "开始初始化日志查看器ViewModel");
                
                // 使用后台任务创建LogViewerViewModel，避免阻塞UI线程
                LogViewerViewModel = await Task.Run(() => new LogViewerViewModel());
                
                _logger.Debug("MainWindowViewModel", "日志查看器ViewModel 初始化完成");
            }
            catch (Exception ex)
            {
                _logger.Error("MainWindowViewModel", "初始化日志查看器ViewModel失败");
                _logger.LogException(ex);
                await _dialogService.ShowErrorDialogAsync("错误", $"日志查看器初始化失败：{ex.Message}");
            }
        }

        private List<ControllerEvent> CollectControllerEvents()
        {
            if (PianoRoll == null)
            {
                _logger.Debug("MainWindowViewModel", "PianoRoll为空，控制器事件收集返回空列表");
                return new List<ControllerEvent>();
            }

            var events = PianoRoll.GetAllControllerEvents()
                .Select(vm => vm.ToControllerEvent())
                .OrderBy(evt => evt.TrackIndex)
                .ThenBy(evt => evt.ControllerNumber)
                .ThenBy(evt => evt.Time.ToDouble())
                .ToList();

            _logger.Debug("MainWindowViewModel", $"收集到 {events.Count} 个控制器事件用于保存");
            return events;
        }

        #endregion
    }
}