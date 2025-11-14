using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models.Settings;
using Lumino.Services.Interfaces;
using LuminoWaveTable.Interfaces;
using EnderDebugger;

namespace Lumino.ViewModels.Settings
{
    public partial class SettingsWindowViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly EnderLogger _logger;
        private readonly ILuminoWaveTableService _waveTableService;

        [ObservableProperty]
        private SettingsPageType _selectedPageType = SettingsPageType.General;

        [ObservableProperty]
        private bool _hasUnsavedChanges = false;

        [ObservableProperty]
        private string _selectedThemeKey = "Default";

        [ObservableProperty]
        private string _selectedLanguageCode = "zh-CN";

        [ObservableProperty]
        private string _selectedWaveTableEngine = "KDMAPI";

        [ObservableProperty]
        private bool _isWaveTableAutoDetectionEnabled = true;

        public SettingsModel Settings => _settingsService.Settings;

        public ObservableCollection<SettingsPageInfo> Pages { get; } = new();
        public ObservableCollection<WaveTableEngineOption> WaveTableEngineOptions { get; } = new();
    public ObservableCollection<LuminoWaveTable.Models.LuminoMidiDeviceInfo> AvailableMidiDevices { get; } = new();
    [ObservableProperty]
    private LuminoWaveTable.Models.LuminoMidiDeviceInfo? _selectedMidiDevice;

        public SettingsWindowViewModel(ISettingsService settingsService, ILuminoWaveTableService waveTableService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _waveTableService = waveTableService ?? throw new ArgumentNullException(nameof(waveTableService));
            _logger = new EnderLogger("SettingsWindowViewModel");

            InitializePages();
            InitializeWaveTableEngines();
            InitializeMidiDevices();
            LoadSettings();
        }

        private void InitializePages()
        {
            Pages.Clear();
            Pages.Add(new SettingsPageInfo { Type = SettingsPageType.Audio, Title = "播表", Icon = "🎵" });
            Pages.Add(new SettingsPageInfo { Type = SettingsPageType.General, Title = "常规", Icon = "⚙" });
        }

        private void InitializeWaveTableEngines()
        {
            WaveTableEngineOptions.Clear();
            WaveTableEngineOptions.Add(new WaveTableEngineOption { Id = "KDMAPI", Name = "KDMAPI", Description = "现有的KDMAPI播表调用方式" });
            WaveTableEngineOptions.Add(new WaveTableEngineOption { Id = "LuminoWaveTable", Name = "Lumino播表", Description = "lumino播表 - 完整的MIDI播表功能" });
        }

        private void InitializeMidiDevices()
        {
            AvailableMidiDevices.Clear();
            AvailableMidiDevices.Add(new LuminoWaveTable.Models.LuminoMidiDeviceInfo
            {
                DeviceId = 0,
                Name = "Microsoft GS Wavetable Synth",
                IsDefault = true,
                Technology = 0,
                Voices = 0,
                Notes = 0,
                ChannelMask = 0,
                Support = 0,
                IsAvailable = true
            });

            // 默认选择第一个可用设备
            SelectedMidiDevice = AvailableMidiDevices.FirstOrDefault();
        }

        public void LoadSettings()
        {
            SelectedWaveTableEngine = Settings.PlaybackEngine;
            IsWaveTableAutoDetectionEnabled = Settings.AutoDetectWaveTables;
        }

        [RelayCommand]
        private void ApplyWaveTableEngine(string engineId)
        {
            Settings.PlaybackEngine = engineId;
            SelectedWaveTableEngine = engineId;
            _settingsService.ApplyWaveTableSettings();
        }
    }
}