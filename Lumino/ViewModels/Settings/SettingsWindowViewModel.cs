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

        [ObservableProperty]
        private AnimationMode _selectedAnimationMode = AnimationMode.Full;

        public SettingsModel Settings => _settingsService.Settings;

        public ObservableCollection<SettingsPageInfo> Pages { get; } = new();
        public ObservableCollection<WaveTableEngineOption> WaveTableEngineOptions { get; } = new();
        public ObservableCollection<LuminoWaveTable.Models.LuminoMidiDeviceInfo> AvailableMidiDevices { get; } = new();
        public ObservableCollection<LanguageOption> LanguageOptions { get; } = new();
        public ObservableCollection<ThemeOption> ThemeOptions { get; } = new();
        public ObservableCollection<ShortcutSetting> ShortcutSettings { get; } = new();

        [ObservableProperty]
        private LuminoWaveTable.Models.LuminoMidiDeviceInfo? _selectedMidiDevice;

        [ObservableProperty]
        private SoundFontOption? _selectedSoundFont;

        public ObservableCollection<SoundFontOption> AvailableSoundFonts { get; } = new();

        public SettingsWindowViewModel(ISettingsService settingsService, ILuminoWaveTableService waveTableService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _waveTableService = waveTableService ?? throw new ArgumentNullException(nameof(waveTableService));
            _logger = new EnderLogger("SettingsWindowViewModel");

            InitializePages();
            InitializeLanguages();
            InitializeThemes();
            InitializeShortcuts();
            InitializeWaveTableEngines();
            InitializeMidiDevices();
            LoadSettings();
        }

        private void InitializePages()
        {
            Pages.Clear();
            Pages.Add(new SettingsPageInfo { Type = SettingsPageType.General, Title = "常规", Icon = "⚙", Description = "常规应用设置" });
            Pages.Add(new SettingsPageInfo { Type = SettingsPageType.Language, Title = "语言", Icon = "🌐", Description = "选择界面语言" });
            Pages.Add(new SettingsPageInfo { Type = SettingsPageType.Theme, Title = "主题", Icon = "🎨", Description = "应用主题设置" });
            Pages.Add(new SettingsPageInfo { Type = SettingsPageType.Editor, Title = "编辑器", Icon = "✏️", Description = "编辑器相关设置" });
            Pages.Add(new SettingsPageInfo { Type = SettingsPageType.Shortcuts, Title = "快捷键", Icon = "⌨️", Description = "快捷键配置" });
            Pages.Add(new SettingsPageInfo { Type = SettingsPageType.Audio, Title = "播表", Icon = "🎵", Description = "音频播表设置" });
            Pages.Add(new SettingsPageInfo { Type = SettingsPageType.Animation, Title = "动画", Icon = "✨", Description = "动画效果设置" });
            Pages.Add(new SettingsPageInfo { Type = SettingsPageType.Advanced, Title = "高级", Icon = "🔧", Description = "高级选项" });
            Pages.Add(new SettingsPageInfo { Type = SettingsPageType.About, Title = "关于", Icon = "ℹ️", Description = "关于应用程序" });
        }

        private void InitializeLanguages()
        {
            LanguageOptions.Clear();
            LanguageOptions.Add(new LanguageOption { Code = "zh-CN", Name = "Chinese (Simplified)", NativeName = "中文（简体）" });
            LanguageOptions.Add(new LanguageOption { Code = "en-US", Name = "English (US)", NativeName = "English" });
            LanguageOptions.Add(new LanguageOption { Code = "ja-JP", Name = "Japanese", NativeName = "日本語" });
        }

        private void InitializeThemes()
        {
            ThemeOptions.Clear();
            ThemeOptions.Add(new ThemeOption { Key = "Default", Name = "默认浅色", Description = "默认浅色主题" });
            ThemeOptions.Add(new ThemeOption { Key = "Dark", Name = "深色", Description = "暗黑主题" });
            ThemeOptions.Add(new ThemeOption { Key = "HighContrast", Name = "高对比度", Description = "适合视力低下用户的高对比度主题" });
        }

        private void InitializeShortcuts()
        {
            ShortcutSettings.Clear();
            // 添加常见快捷键
            ShortcutSettings.Add(new ShortcutSetting { Category = "文件", Command = "New", Description = "新建文件", DefaultShortcut = "Ctrl+N", CurrentShortcut = "Ctrl+N" });
            ShortcutSettings.Add(new ShortcutSetting { Category = "文件", Command = "Open", Description = "打开文件", DefaultShortcut = "Ctrl+O", CurrentShortcut = "Ctrl+O" });
            ShortcutSettings.Add(new ShortcutSetting { Category = "文件", Command = "Save", Description = "保存文件", DefaultShortcut = "Ctrl+S", CurrentShortcut = "Ctrl+S" });
            ShortcutSettings.Add(new ShortcutSetting { Category = "编辑", Command = "Undo", Description = "撤销", DefaultShortcut = "Ctrl+Z", CurrentShortcut = "Ctrl+Z" });
            ShortcutSettings.Add(new ShortcutSetting { Category = "编辑", Command = "Redo", Description = "重做", DefaultShortcut = "Ctrl+Y", CurrentShortcut = "Ctrl+Y" });
            ShortcutSettings.Add(new ShortcutSetting { Category = "编辑", Command = "Cut", Description = "剪切", DefaultShortcut = "Ctrl+X", CurrentShortcut = "Ctrl+X" });
            ShortcutSettings.Add(new ShortcutSetting { Category = "编辑", Command = "Copy", Description = "复制", DefaultShortcut = "Ctrl+C", CurrentShortcut = "Ctrl+C" });
            ShortcutSettings.Add(new ShortcutSetting { Category = "编辑", Command = "Paste", Description = "粘贴", DefaultShortcut = "Ctrl+V", CurrentShortcut = "Ctrl+V" });
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

        [RelayCommand]
        private void SelectPage(SettingsPageType pageType)
        {
            SelectedPageType = pageType;
        }

        [RelayCommand]
        private void ApplyLanguage(string languageCode)
        {
            SelectedLanguageCode = languageCode;
            HasUnsavedChanges = true;
            _logger.Info("SettingsWindowViewModel", $"语言已更改为: {languageCode}");
        }

        [RelayCommand]
        private void ApplyTheme(string themeKey)
        {
            SelectedThemeKey = themeKey;
            HasUnsavedChanges = true;
            _logger.Info("SettingsWindowViewModel", $"主题已更改为: {themeKey}");
        }

        [RelayCommand]
        private void ResetAllShortcuts()
        {
            foreach (var shortcut in ShortcutSettings)
            {
                shortcut.CurrentShortcut = shortcut.DefaultShortcut;
            }
            HasUnsavedChanges = true;
            _logger.Info("SettingsWindowViewModel", "所有快捷键已重置");
        }

        [RelayCommand]
        private void ResetShortcut(ShortcutSetting shortcut)
        {
            shortcut.CurrentShortcut = shortcut.DefaultShortcut;
            HasUnsavedChanges = true;
        }

        [RelayCommand]
        private async Task ResetToDefaults()
        {
            await _settingsService.ResetToDefaultsAsync();
            LoadSettings();
            HasUnsavedChanges = false;
            _logger.Info("SettingsWindowViewModel", "所有设置已重置为默认值");
        }

        [RelayCommand]
        private void ApplyAnimationMode(AnimationMode mode)
        {
            SelectedAnimationMode = mode;
            HasUnsavedChanges = true;
            _logger.Info("SettingsWindowViewModel", $"动画模式已更改为: {mode}");
        }

        [RelayCommand]
        private void TestPlayback()
        {
            try
            {
                _logger.Info("SettingsWindowViewModel", "正在播放测试音符...");
                // 这里调用播表服务播放测试音符
                // _waveTableService.PlayTestNote();
            }
            catch (Exception ex)
            {
                _logger.Error("SettingsWindowViewModel", $"测试播放出错: {ex.Message}");
            }
        }

        [RelayCommand]
        private void BrowseSoundFont()
        {
            try
            {
                _logger.Info("SettingsWindowViewModel", "浏览音色库文件...");
                // 这里应该打开文件对话框
                // 供用户选择SF2、SF3等音色库文件
            }
            catch (Exception ex)
            {
                _logger.Error("SettingsWindowViewModel", $"浏览音色库出错: {ex.Message}");
            }
        }
    }
}