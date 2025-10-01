using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnderDebugger;
using EnderWaveTableAccessingParty.Models;
using EnderWaveTableAccessingParty.Services;

namespace EnderWaveTableAccessingParty.ViewModels
{
    /// <summary>
    /// 播表设置ViewModel
    /// </summary>
    public partial class WaveTableSettingsViewModel : ObservableObject
    {
        private readonly IWaveTableConfigService _configService;
        private readonly IMidiPlaybackService _playbackService;
        private readonly EnderLogger _logger;
        private WaveTableSettings _settings = new();

        /// <summary>
        /// 可用的MIDI设备列表
        /// </summary>
        public ObservableCollection<MidiDeviceInfo> MidiDevices { get; } = new();

        /// <summary>
        /// 可用的播表列表
        /// </summary>
        public ObservableCollection<WaveTableInfo> WaveTables { get; } = new();

        /// <summary>
        /// 当前设置
        /// </summary>
        public WaveTableSettings Settings
        {
            get => _settings;
            private set
            {
                if (SetProperty(ref _settings, value))
                {
                    OnPropertyChanged(nameof(IsAudioFeedbackEnabled));
                    OnPropertyChanged(nameof(SelectedMidiDeviceId));
                    OnPropertyChanged(nameof(SelectedWaveTableId));
                    OnPropertyChanged(nameof(DefaultNoteVelocity));
                    OnPropertyChanged(nameof(DefaultNoteDuration));
                    OnPropertyChanged(nameof(IsAutoPlayEnabled));
                    OnPropertyChanged(nameof(IsVelocitySensitivityEnabled));
                    OnPropertyChanged(nameof(MasterVolume));
                }
            }
        }

        /// <summary>
        /// 是否启用音频反馈
        /// </summary>
        public bool IsAudioFeedbackEnabled
        {
            get => Settings.EnableAudioFeedback;
            set
            {
                if (Settings.EnableAudioFeedback != value)
                {
                    Settings.EnableAudioFeedback = value;
                    OnPropertyChanged();
                    _ = SaveSettingsAsync();
                }
            }
        }

        /// <summary>
        /// 选中的MIDI设备ID
        /// </summary>
        public int SelectedMidiDeviceId
        {
            get => Settings.CurrentMidiDeviceId;
            set
            {
                if (Settings.CurrentMidiDeviceId != value)
                {
                    Settings.CurrentMidiDeviceId = value;
                    OnPropertyChanged();
                    _playbackService.CurrentDeviceId = value;
                    _ = SaveSettingsAsync();
                }
            }
        }

        /// <summary>
        /// 选中的播表ID
        /// </summary>
        public string SelectedWaveTableId
        {
            get => Settings.CurrentWaveTableId;
            set
            {
                if (Settings.CurrentWaveTableId != value)
                {
                    Settings.CurrentWaveTableId = value;
                    OnPropertyChanged();
                    _playbackService.CurrentWaveTableId = value;
                    _ = SaveSettingsAsync();
                }
            }
        }

        /// <summary>
        /// 默认音符力度
        /// </summary>
        public int DefaultNoteVelocity
        {
            get => Settings.DefaultNoteVelocity;
            set
            {
                if (Settings.DefaultNoteVelocity != value)
                {
                    Settings.DefaultNoteVelocity = Math.Clamp(value, 1, 127);
                    OnPropertyChanged();
                    _ = SaveSettingsAsync();
                }
            }
        }

        /// <summary>
        /// 默认音符持续时间
        /// </summary>
        public int DefaultNoteDuration
        {
            get => Settings.DefaultNoteDuration;
            set
            {
                if (Settings.DefaultNoteDuration != value)
                {
                    Settings.DefaultNoteDuration = Math.Clamp(value, 50, 2000);
                    OnPropertyChanged();
                    _ = SaveSettingsAsync();
                }
            }
        }

        /// <summary>
        /// 是否启用自动播放
        /// </summary>
        public bool IsAutoPlayEnabled
        {
            get => Settings.AutoPlayOnNotePlacement;
            set
            {
                if (Settings.AutoPlayOnNotePlacement != value)
                {
                    Settings.AutoPlayOnNotePlacement = value;
                    OnPropertyChanged();
                    _ = SaveSettingsAsync();
                }
            }
        }

        /// <summary>
        /// 是否启用力度感应
        /// </summary>
        public bool IsVelocitySensitivityEnabled
        {
            get => Settings.EnableVelocitySensitivity;
            set
            {
                if (Settings.EnableVelocitySensitivity != value)
                {
                    Settings.EnableVelocitySensitivity = value;
                    OnPropertyChanged();
                    _ = SaveSettingsAsync();
                }
            }
        }

        /// <summary>
        /// 主音量
        /// </summary>
        public int MasterVolume
        {
            get => Settings.MasterVolume;
            set
            {
                if (Settings.MasterVolume != value)
                {
                    Settings.MasterVolume = Math.Clamp(value, 0, 100);
                    OnPropertyChanged();
                    _ = SaveSettingsAsync();
                }
            }
        }

        /// <summary>
        /// 测试播放命令
        /// </summary>
        [RelayCommand]
        private async Task TestPlaybackAsync()
        {
            try
            {
                _logger.Debug("WaveTableSettingsViewModel", $"开始测试播放 - 音符: 60, 力度: {DefaultNoteVelocity}, 持续时间: {DefaultNoteDuration}");
                // 播放一个中音C音符进行测试
                await _playbackService.PlayNoteAsync(60, DefaultNoteVelocity, DefaultNoteDuration);
                _logger.Debug("WaveTableSettingsViewModel", "测试播放完成");
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableSettingsViewModel", "测试播放失败");
                _logger.LogException(ex);
            }
        }

        /// <summary>
        /// 刷新设备列表命令
        /// </summary>
        [RelayCommand]
        private async Task RefreshDevicesAsync()
        {
            await LoadDevicesAndWaveTablesAsync();
        }

        /// <summary>
        /// 重置为默认设置命令
        /// </summary>
        [RelayCommand]
        private async Task ResetToDefaultsAsync()
        {
            try
            {
                _logger.Info("WaveTableSettingsViewModel", "开始重置为默认设置");
                await _configService.ResetToDefaultsAsync();
                Settings = await _configService.GetSettingsAsync();
                _logger.Info("WaveTableSettingsViewModel", "已重置为默认设置");
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableSettingsViewModel", "重置设置失败");
                _logger.LogException(ex);
            }
        }

        /// <summary>
        /// 保存设置命令
        /// </summary>
        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            try
            {
                _logger.Debug("WaveTableSettingsViewModel", "开始保存播表设置");
                await _configService.SaveSettingsAsync(Settings);
                _logger.Debug("WaveTableSettingsViewModel", "播表设置保存完成");
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableSettingsViewModel", "保存设置失败");
                _logger.LogException(ex);
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public WaveTableSettingsViewModel(IWaveTableConfigService configService, IMidiPlaybackService playbackService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
            _logger = EnderLogger.Instance;

            _logger.Info("WaveTableSettingsViewModel", "WaveTableSettingsViewModel 已创建");
            _ = InitializeAsync();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        private async Task InitializeAsync()
        {
            try
            {
                _logger.Debug("WaveTableSettingsViewModel", "开始初始化播表设置");
                
                // 加载设置
                Settings = await _configService.GetSettingsAsync();
                _logger.Debug("WaveTableSettingsViewModel", "播表设置加载完成");

                // 初始化播放服务
                await _playbackService.InitializeAsync();
                _logger.Debug("WaveTableSettingsViewModel", "播放服务初始化完成");

                // 加载设备和播表列表
                await LoadDevicesAndWaveTablesAsync();

                // 应用当前设置
                _playbackService.CurrentDeviceId = Settings.CurrentMidiDeviceId;
                _playbackService.CurrentWaveTableId = Settings.CurrentWaveTableId;
                
                _logger.Info("WaveTableSettingsViewModel", "播表设置初始化完成");
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableSettingsViewModel", "初始化播表设置失败");
                _logger.LogException(ex);
            }
        }

        /// <summary>
        /// 加载设备和播表列表
        /// </summary>
        private async Task LoadDevicesAndWaveTablesAsync()
        {
            try
            {
                _logger.Debug("WaveTableSettingsViewModel", "开始加载设备和播表列表");
                
                // 加载MIDI设备
                var devices = await _playbackService.GetMidiDevicesAsync();
                MidiDevices.Clear();
                foreach (var device in devices)
                {
                    MidiDevices.Add(device);
                }
                _logger.Debug("WaveTableSettingsViewModel", $"MIDI设备加载完成 - 设备数量: {MidiDevices.Count}");

                // 加载播表
                var waveTables = await _playbackService.GetWaveTablesAsync();
                WaveTables.Clear();
                foreach (var waveTable in waveTables)
                {
                    WaveTables.Add(waveTable);
                }
                _logger.Debug("WaveTableSettingsViewModel", $"播表加载完成 - 播表数量: {WaveTables.Count}");
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableSettingsViewModel", "加载设备和播表失败");
                _logger.LogException(ex);
            }
        }
    }
}