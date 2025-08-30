using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using DominoNext.Services.Implementation;
using DominoNext.Services.Interfaces;
using DominoNext.ViewModels;
using DominoNext.Views;

namespace DominoNext
{
    public partial class App : Application
    {
        private ISettingsService? _settingsService;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            System.Diagnostics.Debug.WriteLine("App.Initialize() 完成");
        }

        public override async void OnFrameworkInitializationCompleted()
        {
            System.Diagnostics.Debug.WriteLine("OnFrameworkInitializationCompleted 开始");
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                System.Diagnostics.Debug.WriteLine("检测到桌面应用程序生命周期");
                
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();
                System.Diagnostics.Debug.WriteLine("数据验证插件已禁用");

                try
                {
                    // 初始化设置服务
                    _settingsService = new SettingsService();
                    System.Diagnostics.Debug.WriteLine("设置服务已创建");
                    
                    await _settingsService.LoadSettingsAsync();
                    System.Diagnostics.Debug.WriteLine("设置已加载");

                    var viewModel = new MainWindowViewModel(_settingsService);
                    System.Diagnostics.Debug.WriteLine("MainWindowViewModel 已创建");

                    var mainWindow = new MainWindow
                    {
                        DataContext = viewModel,
                        // 修复：确保窗口属性设置正确
                        WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen,
                        Width = 1200,
                        Height = 800,
                        MinWidth = 800,
                        MinHeight = 600,
                        ShowInTaskbar = true,
                        CanResize = true
                    };
                    System.Diagnostics.Debug.WriteLine("MainWindow 已创建");

                    desktop.MainWindow = mainWindow;
                    System.Diagnostics.Debug.WriteLine("MainWindow 已设置为应用程序主窗口");

                    // 显式显示窗口
                    mainWindow.Show();
                    System.Diagnostics.Debug.WriteLine("MainWindow.Show() 已调用");
                    
                    // 修复：确保窗口获得焦点和置顶
                    mainWindow.Activate();
                    System.Diagnostics.Debug.WriteLine("MainWindow.Activate() 已调用");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"创建主窗口时发生异常: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"异常类型: {ex.GetType().Name}");
                    System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                    
                    // 修复：在控制台和调试输出中显示错误信息
                    Console.WriteLine($"DominoNext 启动失败：{ex.Message}");
                    Console.WriteLine($"详细信息：{ex.StackTrace}");
                    
                    // 尝试创建一个简单的错误窗口
                    try
                    {
                        var errorWindow = new Avalonia.Controls.Window
                        {
                            Title = "DominoNext 启动错误",
                            Width = 500,
                            Height = 300,
                            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen,
                            Content = new Avalonia.Controls.TextBlock
                            {
                                Text = $"启动失败：{ex.Message}\n\n详细信息：{ex.StackTrace}",
                                Margin = new Avalonia.Thickness(10),
                                TextWrapping = Avalonia.Media.TextWrapping.Wrap
                            }
                        };
                        desktop.MainWindow = errorWindow;
                        errorWindow.Show();
                    }
                    catch
                    {
                        // 如果连错误窗口都无法创建，直接退出
                        desktop.Shutdown(1);
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("未检测到桌面应用程序生命周期");
            }

            base.OnFrameworkInitializationCompleted();
            System.Diagnostics.Debug.WriteLine("OnFrameworkInitializationCompleted 完成");
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}