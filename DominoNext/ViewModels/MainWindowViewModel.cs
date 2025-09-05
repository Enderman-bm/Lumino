using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        private readonly IViewModelFactory _viewModelFactory;
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
        /// 钢琴卷帘ViewModel - 通过工厂创建，确保依赖正确注入
        /// </summary>
        public PianoRollViewModel PianoRoll { get; }
        #endregion

        #region 构造函数
        /// <summary>
        /// 主构造函数 - 通过依赖注入获取所需服务
        /// </summary>
        /// <param name="settingsService">设置服务</param>
        /// <param name="dialogService">对话框服务</param>
        /// <param name="applicationService">应用程序服务</param>
        /// <param name="viewModelFactory">ViewModel工厂</param>
        public MainWindowViewModel(
            ISettingsService settingsService,
            IDialogService dialogService,
            IApplicationService applicationService,
            IViewModelFactory viewModelFactory)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
            _viewModelFactory = viewModelFactory ?? throw new ArgumentNullException(nameof(viewModelFactory));

            // 通过工厂创建PianoRollViewModel，确保依赖正确注入
            PianoRoll = _viewModelFactory.CreatePianoRollViewModel();

            // 初始化欢迎消息（可以从设置服务获取用户偏好语言）
            InitializeGreetingMessage();
        }

        /// <summary>
        /// 设计时构造函数 - 仅用于XAML设计器
        /// 注意：这个构造函数仅用于设计时，生产环境应使用依赖注入
        /// </summary>
        public MainWindowViewModel() : this(
            new DominoNext.Services.Implementation.SettingsService(),
            CreateDesignTimeDialogService(),
            new DominoNext.Services.Implementation.ApplicationService(),
            CreateDesignTimeViewModelFactory())
        {
        }
        
        /// <summary>
        /// 创建设计时使用的对话框服务
        /// </summary>
        private static IDialogService CreateDesignTimeDialogService()
        {
            var settingsService = new DominoNext.Services.Implementation.SettingsService();
            var coordinateService = new DominoNext.Services.Implementation.CoordinateService();
            var loggingService = new DominoNext.Services.Implementation.LoggingService();
            var viewModelFactory = new DominoNext.Services.Implementation.ViewModelFactory(coordinateService, settingsService);
            
            return new DominoNext.Services.Implementation.DialogService(viewModelFactory, loggingService);
        }
        
        /// <summary>
        /// 创建设计时使用的ViewModel工厂
        /// </summary>
        private static IViewModelFactory CreateDesignTimeViewModelFactory()
        {
            var settingsService = new DominoNext.Services.Implementation.SettingsService();
            var coordinateService = new DominoNext.Services.Implementation.CoordinateService();
            
            return new DominoNext.Services.Implementation.ViewModelFactory(coordinateService, settingsService);
        }
        #endregion

        #region 命令实现

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

                // TODO: 实现新建文件功能
                // 可以通过项目服务来创建新项目
                await _dialogService.ShowInfoDialogAsync("信息", "新建文件功能将在后续版本中实现");
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
                    // TODO: 实现文件打开功能
                    // 可以通过项目服务来加载项目
                    await _dialogService.ShowInfoDialogAsync("信息", $"文件打开功能将在后续版本中实现\n选择的文件：{filePath}");
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
                // TODO: 实现文件保存功能
                // 可以通过项目服务来保存当前项目
                await _dialogService.ShowInfoDialogAsync("信息", "文件保存功能将在后续版本中实现");
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorDialogAsync("错误", $"保存文件时发生错误：{ex.Message}");
                System.Diagnostics.Debug.WriteLine($"保存文件时发生错误: {ex.Message}");
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
        /// 初始化欢迎消息
        /// </summary>
        private void InitializeGreetingMessage()
        {
            try
            {
                // 可以根据设置服务中的语言设置来设置不同的欢迎消息
                var appInfo = _applicationService.GetApplicationInfo();
                Greeting = $"欢迎使用 {appInfo.Name} v{appInfo.Version}！";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化欢迎消息时发生错误: {ex.Message}");
                // 使用默认消息
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

        #endregion
    }
}