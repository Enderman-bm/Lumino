using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models.Settings;
using Lumino.Services.Interfaces;
using LuminoWaveTable.Interfaces;
using EnderDebugger;

namespace Lumino.ViewModels.Settings
{
    /// <summary>
    /// 播表设置ViewModel
    /// </summary>
    public partial class WaveTableSettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly ILuminoWaveTableService _waveTableService;
        private readonly EnderLogger _logger;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _selectedEngine = "KDMAPI";

        [ObservableProperty]
        private bool _autoDetectEnabled = true;

        [ObservableProperty]
        private bool _performanceMonitoringEnabled = false;

    public System.Collections.ObjectModel.ObservableCollection<LuminoWaveTable.Interfaces.WaveTableEngineInfo> AvailableEngines { get; } = new();
    public System.Collections.ObjectModel.ObservableCollection<LuminoWaveTable.Models.LuminoMidiDeviceInfo> MidiDevices { get; } = new();

        public WaveTableSettingsViewModel(ISettingsService settingsService, ILuminoWaveTableService waveTableService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _waveTableService = waveTableService ?? throw new ArgumentNullException(nameof(waveTableService));
            _logger = new EnderLogger("WaveTableSettingsViewModel");

            LoadSettings();
            _ = RefreshEnginesAsync();
            _ = RefreshMidiDevicesAsync();
        }

        /// <summary>
        /// 加载当前设置
        /// </summary>
    private void LoadSettings()
        {
            try
            {
                var settings = _settingsService.Settings;
                SelectedEngine = settings.PlaybackEngine;
                AutoDetectEnabled = settings.AutoDetectWaveTables;
                PerformanceMonitoringEnabled = settings.EnablePerformanceMonitoring;
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableSettingsViewModel", $"加载设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新可用的播表引擎
        /// </summary>
    [RelayCommand]
    private async Task RefreshEnginesAsync()
        {
            try
            {
                IsLoading = true;
                AvailableEngines.Clear();

                // 获取可用的播表引擎
                var engines = await Task.Run(() => _waveTableService.GetAvailableEngines());

                foreach (var engine in engines)
                {
                    AvailableEngines.Add(engine);
                }

                // 如果当前选择的引擎不在列表中，选择第一个可用的
                if (!AvailableEngines.Any(e => e.Id == SelectedEngine) && AvailableEngines.Count > 0)
                {
                    SelectedEngine = AvailableEngines.First().Id;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableSettingsViewModel", $"刷新引擎失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 刷新MIDI设备列表
        /// </summary>
        [RelayCommand]
        private async Task RefreshMidiDevicesAsync()
        {
            try
            {
                IsLoading = true;
                MidiDevices.Clear();

                var devices = await _waveTableService.GetMidiDevicesAsync();

                foreach (var device in devices)
                {
                    MidiDevices.Add(device);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableSettingsViewModel", $"刷新MIDI设备失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 应用播表引擎选择
        /// </summary>
        [RelayCommand]
        private void ApplyEngineSelection()
        {
            try
            {
                _settingsService.Settings.PlaybackEngine = SelectedEngine;
                _settingsService.ApplyWaveTableSettings();
                
                _logger.Info("WaveTableSettingsViewModel", $"播表引擎已设置为: {SelectedEngine}");
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableSettingsViewModel", $"应用引擎选择失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 切换自动检测
        /// </summary>
        [RelayCommand]
        private void ToggleAutoDetection()
        {
            try
            {
                _settingsService.Settings.AutoDetectWaveTables = AutoDetectEnabled;
                
                if (AutoDetectEnabled)
                {
                    _ = RefreshEnginesAsync();
                }
                
                _logger.Info("WaveTableSettingsViewModel", $"自动检测已{(AutoDetectEnabled ? "启用" : "禁用")}");
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableSettingsViewModel", $"切换自动检测失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 切换性能监控
        /// </summary>
        [RelayCommand]
        private void TogglePerformanceMonitoring()
        {
            try
            {
                _settingsService.Settings.EnablePerformanceMonitoring = PerformanceMonitoringEnabled;
                _logger.Info("WaveTableSettingsViewModel", $"性能监控已{(PerformanceMonitoringEnabled ? "启用" : "禁用")}");
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableSettingsViewModel", $"切换性能监控失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 播表引擎信息
    /// </summary>
    public partial class WaveTableEngineInfo : ObservableObject
    {
        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private bool _isAvailable = true;

        [ObservableProperty]
        private string _errorMessage = string.Empty;
    }

    /// <summary>
    /// MIDI设备信息
    /// </summary>
    public partial class MidiDeviceInfo : ObservableObject
    {
        [ObservableProperty]
        private int _deviceId;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private bool _isInputDevice;

        [ObservableProperty]
        private bool _isOutputDevice;
    }
}