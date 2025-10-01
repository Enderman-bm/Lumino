using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using DominoNext.Services.Implementation;
using DominoNext.Services.Interfaces;
using DominoNext.ViewModels;
using DominoNext.Views;
using EnderDebugger;

namespace DominoNext;

public partial class App : Application
{
    // 服务依赖 - 简单的依赖注入实现
    private ISettingsService? _settingsService;
    private IDialogService? _dialogService;
    private IApplicationService? _applicationService;
    private ICoordinateService? _coordinateService;
    private IViewModelFactory? _viewModelFactory;
    private IProjectStorageService? _projectStorageService;
    private EnderLogger _logger;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        _logger = new EnderLogger("App");
        _logger.Debug("App", "Initialize() 完成");
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        _logger.Debug("App", "OnFrameworkInitializationCompleted 开始");
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _logger.Debug("App", "检测到桌面应用程序生命周期");
            
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            _logger.Debug("App", "数据验证插件已禁用");

            try
            {
                // 初始化各种服务
                await InitializeServicesAsync();
                _logger.Debug("App", "服务依赖初始化完成");

                // 等待资源预加载完成
                _logger.Debug("App", "开始等待资源预加载...");
                await ResourcePreloadService.Instance.PreloadResourcesAsync();
                _logger.Debug("App", "资源预加载完成");

                // 短暂等待确保资源系统稳定
                await Task.Delay(100);

                // 创建主ViewModel - 使用依赖注入
                var viewModel = CreateMainWindowViewModel();
                _logger.Debug("App", "MainWindowViewModel 已创建");
                
                // 异步初始化主窗口ViewModel
                await viewModel.InitializeAsync();
                _logger.Debug("App", "MainWindowViewModel 异步初始化完成");

                var mainWindow = new MainWindow
                {
                    DataContext = viewModel,
                };
                _logger.Debug("App", "MainWindow 已创建");

                desktop.MainWindow = mainWindow;
                _logger.Debug("App", "MainWindow 设置为应用程序主窗口");

                // 正式显示窗口
                mainWindow.Show();
                _logger.Debug("App", "MainWindow.Show() 已调用");
            }
            catch (Exception ex)
            {
                _logger.Error("App", $"应用程序初始化时发生错误: {ex.Message}");
                _logger.Error("App", $"堆栈跟踪: {ex.StackTrace}");
            }
        }
        else
        {
            _logger.Debug("App", "未检测到桌面应用程序生命周期");
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 初始化各种服务 - 简单的依赖注入实现
    /// </summary>
    private async Task InitializeServicesAsync()
    {
        try
        {
            // 按照顺序初始化服务

            // 1. 基础服务 - 无依赖
            _settingsService = new SettingsService();
            _coordinateService = new CoordinateService();
            
            // 2. 日志服务 - 无依赖
            var loggingService = new LoggingService(DominoNext.Services.Interfaces.LogLevel.Debug);

            // 3. MIDI转换服务 - 无依赖
            var midiConversionService = new MidiConversionService();

            // 4. 依赖基础服务的服务
            _applicationService = new ApplicationService(_settingsService);
            _viewModelFactory = new ViewModelFactory(_coordinateService, _settingsService, midiConversionService);
            
            // 5. 依赖多个服务的复杂服务
            _dialogService = new DialogService(_viewModelFactory, loggingService);
            
            // 6. 存储服务
            _projectStorageService = new ProjectStorageService();

            // 7. 加载配置
            await _settingsService.LoadSettingsAsync();
            _logger.Debug("App", "配置已加载");
        }
        catch (Exception ex)
        {
            _logger.Error("App", $"初始化各种服务时发生错误: {ex.Message}");
            throw; // 重新抛出异常，让上级处理
        }
    }

    /// <summary>
    /// 创建主窗口ViewModel - 使用依赖注入
    /// </summary>
    private MainWindowViewModel CreateMainWindowViewModel()
    {
        if (_settingsService == null || _dialogService == null || 
            _applicationService == null || _projectStorageService == null)
        {
            throw new InvalidOperationException("服务依赖未正确初始化");
        }

        return new MainWindowViewModel(
            _settingsService,
            _dialogService,
            _applicationService,
            _projectStorageService);
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.Where(plugin => plugin is DataAnnotationsValidationPlugin).ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}