using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models.Settings;
using Lumino.Services.Implementation;

namespace Lumino.ViewModels.Settings
{
    public partial class GraphicsSettingsViewModel : ViewModelBase
    {
        private readonly DispatcherTimer _timer;
        private PerformanceCounter? _gpuCounter;
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Lumino",
            "graphics_settings.json");

        [ObservableProperty]
        private string _renderMode = "Skia";

        [ObservableProperty]
        private string _gpuName = "Unknown";

        [ObservableProperty]
        private string _gpuUsage = "N/A";

        [ObservableProperty]
        private bool _isVulkan = false;

        [ObservableProperty]
        private string _vulkanApiVersion = "";

        [ObservableProperty]
        private string _vulkanDriverVersion = "";

        [ObservableProperty]
        private RenderingModeType _selectedRenderingMode = RenderingModeType.Hardware;

        [ObservableProperty]
        private bool _isRenderingModeChangePending = false;

        [ObservableProperty]
        private string _renderingModeStatus = "";

        public Dictionary<string, string> VulkanDetails { get; private set; } = new();

        public GraphicsSettingsViewModel()
        {
            Initialize();
            LoadRenderingModeSettings();
            
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        private void Initialize()
        {
            var vulkanService = VulkanRenderService.Instance;
            EnderDebugger.EnderLogger.Instance.Info("GraphicsSettingsViewModel", $"检查Vulkan状态: IsInitialized={vulkanService.IsInitialized}, VulkanManager={vulkanService.VulkanManager != null}");
            
            if (vulkanService.IsInitialized && vulkanService.VulkanManager != null)
            {
                RenderMode = "Vulkan (硬件加速)";
                IsVulkan = true;
                GpuName = vulkanService.VulkanManager.GetDeviceName();
                VulkanApiVersion = vulkanService.VulkanManager.GetApiVersion();
                VulkanDriverVersion = vulkanService.VulkanManager.GetDriverVersion();
                VulkanDetails = vulkanService.VulkanManager.GetDetailedInfo();
                EnderDebugger.EnderLogger.Instance.Info("GraphicsSettingsViewModel", "Vulkan检测成功");
            }
            else
            {
                // 检查是否是软件渲染模式
                var savedMode = LoadSavedRenderingMode();
                if (savedMode == RenderingModeType.Software)
                {
                    RenderMode = "Skia (软件渲染)";
                    GpuName = "CPU 软件渲染";
                }
                else
                {
                    RenderMode = "Skia (硬件加速)";
                    GpuName = "Skia GPU 后端";
                }
                IsVulkan = false;
                EnderDebugger.EnderLogger.Instance.Warn("GraphicsSettingsViewModel", "Vulkan未初始化，使用Skia渲染");
            }

            InitializeGpuCounter();
        }

        private void LoadRenderingModeSettings()
        {
            SelectedRenderingMode = LoadSavedRenderingMode();
            UpdateRenderingModeStatus();
        }

        private RenderingModeType LoadSavedRenderingMode()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<GraphicsSettings>(json);
                    if (settings != null)
                    {
                        return settings.RenderingMode;
                    }
                }
            }
            catch (Exception ex)
            {
                EnderDebugger.EnderLogger.Instance.Error("GraphicsSettingsViewModel", $"加载渲染模式设置失败: {ex.Message}");
            }
            return RenderingModeType.Hardware;
        }

        private void SaveRenderingModeSettings()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var settings = new GraphicsSettings
                {
                    RenderingMode = SelectedRenderingMode
                };
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
                EnderDebugger.EnderLogger.Instance.Info("GraphicsSettingsViewModel", $"已保存渲染模式设置: {SelectedRenderingMode}");
            }
            catch (Exception ex)
            {
                EnderDebugger.EnderLogger.Instance.Error("GraphicsSettingsViewModel", $"保存渲染模式设置失败: {ex.Message}");
                throw;
            }
        }

        private void UpdateRenderingModeStatus()
        {
            var currentMode = LoadSavedRenderingMode();
            if (currentMode != SelectedRenderingMode)
            {
                IsRenderingModeChangePending = true;
                RenderingModeStatus = "⚠️ 渲染模式已更改，重启应用后生效";
            }
            else
            {
                IsRenderingModeChangePending = false;
                RenderingModeStatus = SelectedRenderingMode == RenderingModeType.Hardware 
                    ? "✅ 当前使用硬件加速渲染" 
                    : "✅ 当前使用软件渲染";
            }
        }

        [RelayCommand]
        private async Task SwitchToHardwareRenderingAsync()
        {
            await SwitchRenderingModeAsync(RenderingModeType.Hardware);
        }

        [RelayCommand]
        private async Task SwitchToSoftwareRenderingAsync()
        {
            await SwitchRenderingModeAsync(RenderingModeType.Software);
        }

        private async Task SwitchRenderingModeAsync(RenderingModeType mode)
        {
            try
            {
                SelectedRenderingMode = mode;
                SaveRenderingModeSettings();
                UpdateRenderingModeStatus();
                
                EnderDebugger.EnderLogger.Instance.Info("GraphicsSettingsViewModel", $"渲染模式已切换为: {mode}");
            }
            catch (Exception ex)
            {
                // 显示错误对话框
                var errorMessage = $"切换渲染模式失败:\n\n{ex.Message}\n\n详细信息:\n{ex.StackTrace}";
                EnderDebugger.EnderLogger.Instance.Error("GraphicsSettingsViewModel", errorMessage);
                
                await ShowErrorDialogAsync("渲染模式切换失败", errorMessage);
            }
        }

        private async Task ShowErrorDialogAsync(string title, string message)
        {
            try
            {
                var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

                if (mainWindow != null)
                {
                    var dialog = new Avalonia.Controls.Window
                    {
                        Title = title,
                        Width = 500,
                        Height = 350,
                        WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                        Content = new Avalonia.Controls.StackPanel
                        {
                            Margin = new Avalonia.Thickness(20),
                            Spacing = 15,
                            Children =
                            {
                                new Avalonia.Controls.TextBlock
                                {
                                    Text = "❌ " + title,
                                    FontSize = 18,
                                    FontWeight = Avalonia.Media.FontWeight.Bold,
                                    Foreground = Avalonia.Media.Brushes.Red
                                },
                                new Avalonia.Controls.ScrollViewer
                                {
                                    MaxHeight = 200,
                                    Content = new Avalonia.Controls.TextBlock
                                    {
                                        Text = message,
                                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                                    }
                                },
                                new Avalonia.Controls.Button
                                {
                                    Content = "确定",
                                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                                    Classes = { "DialogButton", "Primary" }
                                }
                            }
                        }
                    };

                    var button = ((Avalonia.Controls.StackPanel)dialog.Content).Children[2] as Avalonia.Controls.Button;
                    if (button != null)
                    {
                        button.Click += (s, e) => dialog.Close();
                    }

                    await dialog.ShowDialog(mainWindow);
                }
            }
            catch (Exception ex)
            {
                EnderDebugger.EnderLogger.Instance.Error("GraphicsSettingsViewModel", $"显示错误对话框失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 静态方法用于在程序启动时获取保存的渲染模式
        /// </summary>
        public static RenderingModeType GetSavedRenderingMode()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<GraphicsSettings>(json);
                    if (settings != null)
                    {
                        return settings.RenderingMode;
                    }
                }
            }
            catch
            {
                // 忽略错误，返回默认值
            }
            return RenderingModeType.Hardware;
        }

        private void InitializeGpuCounter()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                GpuUsage = "N/A (Non-Windows)";
                return;
            }

            try
            {
                // 尝试查找 GPU 计数器
                #pragma warning disable CA1416 // 验证平台兼容性
                var category = new PerformanceCounterCategory("GPU Engine");
                var instanceNames = category.GetInstanceNames();
                
                var pid = Environment.ProcessId.ToString();
                foreach (var name in instanceNames)
                {
                    if (name.Contains($"pid_{pid}") && name.EndsWith("engtype_3D"))
                    {
                        _gpuCounter = new PerformanceCounter("GPU Engine", "Utilization Percentage", name);
                        break;
                    }
                }
                #pragma warning restore CA1416
            }
            catch
            {
                GpuUsage = "N/A";
            }
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_gpuCounter != null)
            {
                try
                {
                    #pragma warning disable CA1416
                    var usage = _gpuCounter.NextValue();
                    GpuUsage = $"{usage:F1}%";
                    #pragma warning restore CA1416
                }
                catch
                {
                    GpuUsage = "Error";
                }
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _gpuCounter?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 图形设置数据类
        /// </summary>
        private class GraphicsSettings
        {
            public RenderingModeType RenderingMode { get; set; } = RenderingModeType.Hardware;
        }
    }
}
