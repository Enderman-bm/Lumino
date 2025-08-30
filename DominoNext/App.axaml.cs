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
            System.Diagnostics.Debug.WriteLine("App.Initialize() called");
        }

        public override async void OnFrameworkInitializationCompleted()
        {
            System.Diagnostics.Debug.WriteLine("OnFrameworkInitializationCompleted started");
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                System.Diagnostics.Debug.WriteLine("Detected desktop application lifetime");
                
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();
                System.Diagnostics.Debug.WriteLine("Validation plugins disabled");

                try
                {
                    // Initialize settings service
                    _settingsService = new SettingsService();
                    System.Diagnostics.Debug.WriteLine("Settings service created");
                    
                    await _settingsService.LoadSettingsAsync();
                    System.Diagnostics.Debug.WriteLine("Settings loaded");

                    var viewModel = new MainWindowViewModel(_settingsService);
                    System.Diagnostics.Debug.WriteLine("MainWindowViewModel created");

                    var mainWindow = new MainWindow
                    {
                        DataContext = viewModel,
                        // Make sure the window appears in the correct location
                        WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen,
                        Width = 1200,
                        Height = 800,
                        MinWidth = 800,
                        MinHeight = 600,
                        ShowInTaskbar = true,
                        CanResize = true
                    };
                    System.Diagnostics.Debug.WriteLine("MainWindow created");

                    desktop.MainWindow = mainWindow;
                    System.Diagnostics.Debug.WriteLine("MainWindow assigned to desktop application");

                    // Show window properly
                    mainWindow.Show();
                    System.Diagnostics.Debug.WriteLine("MainWindow.Show() called");
                    
                    // Make sure window is brought to front
                    mainWindow.Activate();
                    System.Diagnostics.Debug.WriteLine("MainWindow.Activate() called");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Exception during startup: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Exception type: {ex.GetType().Name}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    
                    // Show error in console as fallback
                    Console.WriteLine($"DominoNext startup failed: {ex.Message}");
                    Console.WriteLine($"Details: {ex.StackTrace}");
                    
                    // Create a simple error window as fallback
                    try
                    {
                        var errorWindow = new Avalonia.Controls.Window
                        {
                            Title = "DominoNext Startup Error",
                            Width = 500,
                            Height = 300,
                            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen,
                            Content = new Avalonia.Controls.TextBlock
                            {
                                Text = $"Startup failed: {ex.Message}\n\nDetails: {ex.StackTrace}",
                                Margin = new Avalonia.Thickness(10),
                                TextWrapping = Avalonia.Media.TextWrapping.Wrap
                            }
                        };
                        desktop.MainWindow = errorWindow;
                        errorWindow.Show();
                    }
                    catch
                    {
                        // If we can't even show an error window, just shutdown
                        desktop.Shutdown(1);
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Desktop application lifetime not detected");
            }

            base.OnFrameworkInitializationCompleted();
            System.Diagnostics.Debug.WriteLine("OnFrameworkInitializationCompleted finished");
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