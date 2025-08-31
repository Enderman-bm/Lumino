using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DominoNext.Models.Settings;
using DominoNext.Services.Interfaces;

namespace DominoNext.ViewModels.Settings
{
    /// <summary>
    /// ���ô���ViewModel
    /// </summary>
    public partial class SettingsWindowViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        private SettingsPageType _selectedPageType = SettingsPageType.General;

        [ObservableProperty]
        private bool _hasUnsavedChanges = false;

        [ObservableProperty]
        private string _selectedThemeKey = "Default";

        [ObservableProperty]
        private string _selectedLanguageCode = "zh-CN";

        public SettingsModel Settings => _settingsService.Settings;

        public ObservableCollection<SettingsPageInfo> Pages { get; } = new();

        // ����ѡ��
        public ObservableCollection<LanguageOption> LanguageOptions { get; } = new()
        {
            new LanguageOption { Code = "zh-CN", Name = "��������", NativeName = "��������" },
            new LanguageOption { Code = "en-US", Name = "English", NativeName = "English" },
            new LanguageOption { Code = "ja-JP", Name = "Japanese", NativeName = "�ձ��Z" }
        };

        // ����ѡ�� - ����Ԥ��ľ�������
        public ObservableCollection<ThemeOption> ThemeOptions { get; } = new()
        {
            new ThemeOption { Key = "Default", Name = "����ϵͳ", Description = "����ϵͳ��������" },
            new ThemeOption { Key = "Light", Name = "ǳɫ����", Description = "�����ǳɫ���⣬�ʺ��ռ�ʹ��" },
            new ThemeOption { Key = "Dark", Name = "��ɫ����", Description = "��ɫ���⣬�����۲�ƣ��" },
            new ThemeOption { Key = "Green", Name = "�ഺ��", Description = "���µ���ɫ���⣬��������" },
            new ThemeOption { Key = "Blue", Name = "��ɫ�Ƽ�", Description = "�Ƽ��е���ɫ���⣬�ִ���Լ" },
            new ThemeOption { Key = "Purple", Name = "��ɫ�λ�", Description = "�λõ���ɫ���⣬��������" },
            new ThemeOption { Key = "Custom", Name = "�Զ���", Description = "��ȫ�Զ������ɫ���⣬��������" }
        };

        // ��ɫ������� - ��������֯
        public ObservableCollection<ColorSettingGroup> ColorSettingGroups { get; } = new();

        // ��ݼ�����
        public ObservableCollection<ShortcutSetting> ShortcutSettings { get; } = new();

        /// <summary>
        /// �Ƿ���ʾ�Զ����������
        /// </summary>
        public bool IsCustomThemeSelected => SelectedThemeKey == "Custom";

        public SettingsWindowViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;

            InitializePages();
            InitializeShortcutSettings();
            InitializeColorSettingGroups();

            // ��������
            LoadSettings();

            // �������ñ����ʵ���Զ�����
            Settings.PropertyChanged += (sender, e) => 
            {
                HasUnsavedChanges = true;
                AutoSave();
            };

            // 初始化命令
            ResetZoomCommand = new RelayCommand(() => 
            {
                Settings.DefaultZoom = 1.0;
            });
        }

        // ���ʱʹ�õ��޲ι��캯��
        public SettingsWindowViewModel() : this(new DominoNext.Services.Implementation.SettingsService())
        {
        }

        public IRelayCommand ResetZoomCommand { get; }

        partial void OnSelectedThemeKeyChanged(string value)
        {
            OnPropertyChanged(nameof(IsCustomThemeSelected));
        }

        private void InitializePages()
        {
            Pages.Clear();
            Pages.Add(new SettingsPageInfo
            {
                Type = SettingsPageType.General,
                Title = "����",
                Icon = "??",
                Description = "����Ӧ������"
            });
            Pages.Add(new SettingsPageInfo
            {
                Type = SettingsPageType.Language,
                Title = "����",
                Icon = "??",
                Description = "������������"
            });
            Pages.Add(new SettingsPageInfo
            {
                Type = SettingsPageType.Theme,
                Title = "����",
                Icon = "??",
                Description = "������������"
            });
            Pages.Add(new SettingsPageInfo
            {
                Type = SettingsPageType.Editor,
                Title = "�༭��",
                Icon = "??",
                Description = "�༭����Ϊ����"
            });
            Pages.Add(new SettingsPageInfo
            {
                Type = SettingsPageType.Shortcuts,
                Title = "��ݼ�",
                Icon = "??",
                Description = "���̿�ݼ�����"
            });
            Pages.Add(new SettingsPageInfo
            {
                Type = SettingsPageType.Advanced,
                Title = "�߼�",
                Icon = "???",
                Description = "�߼�ѡ��͵���"
            });
            // 添加钢琴卷帘设置页面
            Pages.Add(new SettingsPageInfo
            {
                Type = SettingsPageType.PianoRoll,
                Title = "钢琴卷帘",
                Icon = "🎹",
                Description = "钢琴卷帘网格和显示设置"
            });
        }

        private void InitializeColorSettingGroups()
        {
            ColorSettingGroups.Clear();

            // ����������ɫ��
            var interfaceGroup = new ColorSettingGroup("����", "��������ص���ɫ����");
            interfaceGroup.Items.Add(new ColorSettingItem("���汳��", "BackgroundColor", "������ı�����ɫ", "Interface"));
            interfaceGroup.Items.Add(new ColorSettingItem("������", "GridLineColor", "�༭����������ɫ", "Interface"));
            interfaceGroup.Items.Add(new ColorSettingItem("ѡ���", "SelectionColor", "ѡ������ɫ", "Interface"));
            interfaceGroup.Items.Add(new ColorSettingItem("�ָ���", "SeparatorLineColor", "���ַָ��ߵ���ɫ", "Interface"));
            ColorSettingGroups.Add(interfaceGroup);

            // ���ټ���ɫ��
            var pianoGroup = new ColorSettingGroup("���ټ�", "���ټ�����ص���ɫ����");
            pianoGroup.Items.Add(new ColorSettingItem("�׼�", "KeyWhiteColor", "���ٰ׼���ɫ", "Piano"));
            pianoGroup.Items.Add(new ColorSettingItem("�ڼ�", "KeyBlackColor", "���ٺڼ���ɫ", "Piano"));
            pianoGroup.Items.Add(new ColorSettingItem("���̱߿�", "KeyBorderColor", "���ټ��߿���ɫ", "Piano"));
            pianoGroup.Items.Add(new ColorSettingItem("�׼�����", "KeyTextWhiteColor", "�׼��ϵ�������ɫ", "Piano"));
            pianoGroup.Items.Add(new ColorSettingItem("�ڼ�����", "KeyTextBlackColor", "�ڼ��ϵ�������ɫ", "Piano"));
            ColorSettingGroups.Add(pianoGroup);

            // ������ɫ��
            var noteGroup = new ColorSettingGroup("����", "������ص���ɫ����");
            noteGroup.Items.Add(new ColorSettingItem("��ͨ����", "NoteColor", "��ͨ�����������ɫ", "Note"));
            noteGroup.Items.Add(new ColorSettingItem("ѡ������", "NoteSelectedColor", "ѡ����������ɫ", "Note"));
            noteGroup.Items.Add(new ColorSettingItem("��ק����", "NoteDraggingColor", "��ק����������ɫ", "Note"));
            noteGroup.Items.Add(new ColorSettingItem("Ԥ������", "NotePreviewColor", "Ԥ����������ɫ", "Note"));
            noteGroup.Items.Add(new ColorSettingItem("����ָʾ��", "VelocityIndicatorColor", "��������ָʾ����ɫ", "Note"));
            ColorSettingGroups.Add(noteGroup);

            // С�ں�������
            var measureGroup = new ColorSettingGroup("С��", "С�ں�������ص���ɫ����");
            measureGroup.Items.Add(new ColorSettingItem("С��ͷ����", "MeasureHeaderBackgroundColor", "С��ͷ�ı�����ɫ", "Measure"));
            measureGroup.Items.Add(new ColorSettingItem("С����", "MeasureLineColor", "С�ڷָ�����ɫ", "Measure"));
            measureGroup.Items.Add(new ColorSettingItem("С������", "MeasureTextColor", "С�����ֵ���ɫ", "Measure"));
            ColorSettingGroups.Add(measureGroup);
        }

        private void InitializeShortcutSettings()
        {
            ShortcutSettings.Clear();

            // �ļ�����
            ShortcutSettings.Add(new ShortcutSetting
            {
                Command = "NewFile",
                Description = "�½��ļ�",
                DefaultShortcut = "Ctrl+N",
                CurrentShortcut = "Ctrl+N",
                Category = "�ļ�"
            });
            ShortcutSettings.Add(new ShortcutSetting
            {
                Command = "OpenFile",
                Description = "���ļ�",
                DefaultShortcut = "Ctrl+O",
                CurrentShortcut = "Ctrl+O",
                Category = "�ļ�"
            });
            ShortcutSettings.Add(new ShortcutSetting
            {
                Command = "SaveFile",
                Description = "�����ļ�",
                DefaultShortcut = "Ctrl+S",
                CurrentShortcut = "Ctrl+S",
                Category = "�ļ�"
            });

            // �༭����
            ShortcutSettings.Add(new ShortcutSetting
            {
                Command = "Undo",
                Description = "����",
                DefaultShortcut = "Ctrl+Z",
                CurrentShortcut = "Ctrl+Z",
                Category = "�༭"
            });
            ShortcutSettings.Add(new ShortcutSetting
            {
                Command = "Redo",
                Description = "����",
                DefaultShortcut = "Ctrl+Y",
                CurrentShortcut = "Ctrl+Y",
                Category = "�༭"
            });

            // ����
            ShortcutSettings.Add(new ShortcutSetting
            {
                Command = "PencilTool",
                Description = "Ǧ�ʹ���",
                DefaultShortcut = "P",
                CurrentShortcut = "P",
                Category = "����"
            });
        }

        /// <summary>
        /// �������ļ���������
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                // ���ļ��������ã���������Ӧ�ã����⸲�ǵ�ǰ����״̬
                Settings.LoadFromFile();

                // ���µ�ǰѡ��״̬
                UpdateCurrentSelections();

                // ��Ҫ����Ӧ�����ã���Ϊ��Ḳ�ǵ�ǰ���е�����
                // ApplyLoadedSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��������ʧ��: {ex.Message}");
                // ʹ��Ĭ������
                UpdateCurrentSelections();
            }
        }

        /// <summary>
        /// ���µ�ǰѡ��״̬
        /// </summary>
        public void UpdateCurrentSelections()
        {
            // ����ѡ�е�����
            SelectedLanguageCode = Settings.Language;

            // ����ѡ�е����� - ���ڵ�ǰ�����ж���������
            SelectedThemeKey = DetermineCurrentThemeKey();

            // ֪ͨ���Ա��
            OnPropertyChanged(nameof(IsCustomThemeSelected));
        }

        /// <summary>
        /// ���ݵ�ǰ��ɫ�����ж���������
        /// </summary>
        private string DetermineCurrentThemeKey()
        {
            // ����Ƿ�ƥ��Ԥ������
            if (IsMatchingLightTheme()) return "Light";
            if (IsMatchingDarkTheme()) return "Dark";
            if (IsMatchingGreenTheme()) return "Green";
            if (IsMatchingBlueTheme()) return "Blue";
            if (IsMatchingPurpleTheme()) return "Purple";
            
            // �����ƥ���κ�Ԥ�����⣬��Ϊ�Զ���
            return "Custom";
        }

        private bool IsMatchingLightTheme()
        {
            return Settings.BackgroundColor == "#FFFAFAFA" &&
                   Settings.NoteColor == "#FF4CAF50" &&
                   Settings.KeyWhiteColor == "#FFFFFFFF" &&
                   Settings.KeyBlackColor == "#FF1F1F1F";
        }

        private bool IsMatchingDarkTheme()
        {
            return Settings.BackgroundColor == "#FF1E1E1E" &&
                   Settings.NoteColor == "#FF66BB6A" &&
                   Settings.KeyWhiteColor == "#FF2D2D30" &&
                   Settings.KeyBlackColor == "#FF0F0F0F";
        }

        private bool IsMatchingGreenTheme()
        {
            return Settings.BackgroundColor == "#FFF1F8E9" &&
                   Settings.NoteColor == "#FF66BB6A" &&
                   Settings.KeyWhiteColor == "#FFFAFAFA" &&
                   Settings.KeyBlackColor == "#FF2E7D32";
        }

        private bool IsMatchingBlueTheme()
        {
            return Settings.BackgroundColor == "#FFE3F2FD" &&
                   Settings.NoteColor == "#FF42A5F5" &&
                   Settings.KeyWhiteColor == "#FFFAFAFA" &&
                   Settings.KeyBlackColor == "#FF0D47A1";
        }

        private bool IsMatchingPurpleTheme()
        {
            return Settings.BackgroundColor == "#FFF3E5F5" &&
                   Settings.NoteColor == "#FFAB47BC" &&
                   Settings.KeyWhiteColor == "#FFFAFAFA" &&
                   Settings.KeyBlackColor == "#FF4A148C";
        }

        /// <summary>
        /// Ӧ�ü��ص�����
        /// </summary>
        private void ApplyLoadedSettings()
        {
            // Ӧ����������
            _settingsService.ApplyLanguageSettings();

            // Ӧ��������
            _settingsService.ApplyThemeSettings();
        }

        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            try
            {
                // ���浽��
                await _settingsService.SaveSettingsAsync();
                HasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��������ʧ��: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ResetToDefaultsAsync()
        {
            try
            {
                // ���÷����е�����
                await _settingsService.ResetToDefaultsAsync();

                // ���ÿ�ݼ�����
                foreach (var shortcut in ShortcutSettings)
                {
                    shortcut.CurrentShortcut = shortcut.DefaultShortcut;
                }

                // ���µ�ǰѡ��״̬
                UpdateCurrentSelections();

                // �Զ������Settings���Ա������������Ҫ�ֶ�����
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��������ʧ��: {ex.Message}");
            }
        }

        [RelayCommand]
        private void SelectPage(SettingsPageType pageType)
        {
            SelectedPageType = pageType;
        }

        [RelayCommand]
        private void ApplyLanguage(string languageCode)
        {
            Settings.Language = languageCode;
            SelectedLanguageCode = languageCode;
            _settingsService.ApplyLanguageSettings();
            
            // �Զ�����
            AutoSave();
        }

        [RelayCommand]
        private void ApplyTheme(string themeKey)
        {
            SelectedThemeKey = themeKey;

            Settings.Theme = themeKey switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                "Green" => ThemeVariant.Default,
                "Blue" => ThemeVariant.Default,
                "Purple" => ThemeVariant.Default,
                "Custom" => ThemeVariant.Default,
                _ => ThemeVariant.Default
            };

            // ��������Ӧ�ö�Ӧ��ɫ����
            switch (themeKey)
            {
                case "Light":
                    Settings.ApplyLightThemeDefaults();
                    break;
                case "Dark":
                    Settings.ApplyDarkThemeDefaults();
                    break;
                case "Green":
                    ApplyGreenTheme();
                    break;
                case "Blue":
                    ApplyBlueTheme();
                    break;
                case "Purple":
                    ApplyPurpleTheme();
                    break;
                case "Custom":
                    // �Զ������ⲻ�Զ�Ӧ���κ���ɫ�������û�����
                    break;
            }

            _settingsService.ApplyThemeSettings();
            
            // �Զ�����
            AutoSave();
        }

        /// <summary>
        /// �Զ���������
        /// </summary>
        private async void AutoSave()
        {
            try
            {
                await _settingsService.SaveSettingsAsync();
                HasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"�Զ���������ʧ��: {ex.Message}");
                HasUnsavedChanges = true;
            }
        }

        private void AutoSwitchToCustomTheme()
        {
            if (SelectedThemeKey != "Custom")
            {
                SelectedThemeKey = "Custom";
                OnPropertyChanged(nameof(IsCustomThemeSelected));
            }
            
            _settingsService.ApplyThemeSettings();
            
            // �Զ�����
            AutoSave();
        }

        private void ApplyGreenTheme()
        {
            Settings.BackgroundColor = "#FFF1F8E9";
            Settings.NoteColor = "#FF66BB6A";
            Settings.NoteSelectedColor = "#FFFF8A65";
            Settings.NoteDraggingColor = "#FF26A69A";
            Settings.NotePreviewColor = "#8066BB6A";
            Settings.GridLineColor = "#20388E3C";
            Settings.KeyWhiteColor = "#FFFAFAFA";
            Settings.KeyBlackColor = "#FF2E7D32";
            Settings.SelectionColor = "#8026A69A";
            Settings.MeasureHeaderBackgroundColor = "#FFE8F5E8";
            Settings.MeasureLineColor = "#FF4CAF50";
            Settings.MeasureTextColor = "#FF1B5E20";
            Settings.SeparatorLineColor = "#FF81C784";
            Settings.KeyBorderColor = "#FF1B5E20";
            Settings.KeyTextWhiteColor = "#FF1B5E20";
            Settings.KeyTextBlackColor = "#FFFFFFFF";
            Settings.VelocityIndicatorColor = "#FF8BC34A";
        }

        private void ApplyBlueTheme()
        {
            Settings.BackgroundColor = "#FFE3F2FD";
            Settings.NoteColor = "#FF42A5F5";
            Settings.NoteSelectedColor = "#FFFF7043";
            Settings.NoteDraggingColor = "#FF1E88E5";
            Settings.NotePreviewColor = "#8042A5F5";
            Settings.GridLineColor = "#201976D2";
            Settings.KeyWhiteColor = "#FFFAFAFA";
            Settings.KeyBlackColor = "#FF0D47A1";
            Settings.SelectionColor = "#801E88E5";
            Settings.MeasureHeaderBackgroundColor = "#FFE1F5FE";
            Settings.MeasureLineColor = "#FF2196F3";
            Settings.MeasureTextColor = "#FF0D47A1";
            Settings.SeparatorLineColor = "#FF64B5F6";
            Settings.KeyBorderColor = "#FF0D47A1";
            Settings.KeyTextWhiteColor = "#FF0D47A1";
            Settings.KeyTextBlackColor = "#FFFFFFFF";
            Settings.VelocityIndicatorColor = "#FF03A9F4";
        }

        private void ApplyPurpleTheme()
        {
            Settings.BackgroundColor = "#FFF3E5F5";
            Settings.NoteColor = "#FFAB47BC";
            Settings.NoteSelectedColor = "#FFFF8A65";
            Settings.NoteDraggingColor = "#FF8E24AA";
            Settings.NotePreviewColor = "#80AB47BC";
            Settings.GridLineColor = "#204A148C";
            Settings.KeyWhiteColor = "#FFFAFAFA";
            Settings.KeyBlackColor = "#FF4A148C";
            Settings.SelectionColor = "#808E24AA";
            Settings.MeasureHeaderBackgroundColor = "#FFEDE7F6";
            Settings.MeasureLineColor = "#FF9C27B0";
            Settings.MeasureTextColor = "#FF4A148C";
            Settings.SeparatorLineColor = "#FFCE93D8";
            Settings.KeyBorderColor = "#FF4A148C";
            Settings.KeyTextWhiteColor = "#FF4A148C";
            Settings.KeyTextBlackColor = "#FFFFFFFF";
            Settings.VelocityIndicatorColor = "#FFBA68C8";
        }

        [RelayCommand]
        private void ResetShortcut(ShortcutSetting shortcut)
        {
            shortcut.CurrentShortcut = shortcut.DefaultShortcut;
            AutoSave();
        }

        [RelayCommand]
        private void ResetAllShortcuts()
        {
            foreach (var shortcut in ShortcutSettings)
            {
                shortcut.CurrentShortcut = shortcut.DefaultShortcut;
            }
            AutoSave();
        }

        /// <summary>
        /// ��ȡָ����ɫ�������Ӧ����ɫֵ
        /// </summary>
        public string GetColorValue(string propertyName)
        {
            var property = typeof(SettingsModel).GetProperty(propertyName);
            return property?.GetValue(Settings) as string ?? "#FFFFFFFF";
        }

        /// <summary>
        /// ����ָ����ɫ���������ɫֵ
        /// </summary>
        public void SetColorValue(string propertyName, string colorValue)
        {
            var property = typeof(SettingsModel).GetProperty(propertyName);
            if (property != null && property.CanWrite)
            {
                property.SetValue(Settings, colorValue);
                
                // ����û��޸�����ɫ���Զ��л����Զ�������
                if (SelectedThemeKey != "Custom")
                {
                    SelectedThemeKey = "Custom";
                    OnPropertyChanged(nameof(IsCustomThemeSelected));
                }
                
                _settingsService.ApplyThemeSettings();
                HasUnsavedChanges = true;
            }
        }

        /// <summary>
        /// ����������ɫΪ��ǰ�����Ĭ��ֵ
        /// </summary>
        [RelayCommand]
        private void ResetAllColors()
        {
            ApplyTheme(SelectedThemeKey);
        }

        /// <summary>
        /// Ϊ�ض���ɫ���Դ������õ�Command
        /// </summary>
        [RelayCommand]
        private void UpdateColor(object parameter)
        {
            if (parameter is (string propertyName, string colorValue))
            {
                SetColorValue(propertyName, colorValue);
            }
        }

        // Ϊÿ����ɫ���Դ����ض�������
        public string BackgroundColorValue 
        { 
            get => Settings.BackgroundColor; 
            set { Settings.BackgroundColor = value; OnPropertyChanged(); }
        }

        public string NoteColorValue 
        { 
            get => Settings.NoteColor; 
            set { Settings.NoteColor = value; OnPropertyChanged(); }
        }

        public string GridLineColorValue 
        { 
            get => Settings.GridLineColor; 
            set { Settings.GridLineColor = value; OnPropertyChanged(); }
        }

        public string KeyWhiteColorValue 
        { 
            get => Settings.KeyWhiteColor; 
            set { Settings.KeyWhiteColor = value; OnPropertyChanged(); }
        }

        public string KeyBlackColorValue 
        { 
            get => Settings.KeyBlackColor; 
            set { Settings.KeyBlackColor = value; OnPropertyChanged(); }
        }

        public string SelectionColorValue 
        { 
            get => Settings.SelectionColor; 
            set { Settings.SelectionColor = value; OnPropertyChanged(); }
        }

        public string NoteSelectedColorValue 
        { 
            get => Settings.NoteSelectedColor; 
            set { Settings.NoteSelectedColor = value; OnPropertyChanged(); }
        }

        public string NoteDraggingColorValue 
        { 
            get => Settings.NoteDraggingColor; 
            set { Settings.NoteDraggingColor = value; OnPropertyChanged(); }
        }

        public string NotePreviewColorValue 
        { 
            get => Settings.NotePreviewColor; 
            set { Settings.NotePreviewColor = value; OnPropertyChanged(); }
        }

        public string VelocityIndicatorColorValue 
        { 
            get => Settings.VelocityIndicatorColor; 
            set { Settings.VelocityIndicatorColor = value; OnPropertyChanged(); }
        }

        public string MeasureHeaderBackgroundColorValue 
        { 
            get => Settings.MeasureHeaderBackgroundColor; 
            set { Settings.MeasureHeaderBackgroundColor = value; OnPropertyChanged(); }
        }

        public string MeasureLineColorValue 
        { 
            get => Settings.MeasureLineColor; 
            set { Settings.MeasureLineColor = value; OnPropertyChanged(); }
        }

        public string MeasureTextColorValue 
        { 
            get => Settings.MeasureTextColor; 
            set { Settings.MeasureTextColor = value; OnPropertyChanged(); }
        }

        public string SeparatorLineColorValue 
        { 
            get => Settings.SeparatorLineColor; 
            set { Settings.SeparatorLineColor = value; OnPropertyChanged(); }
        }

        public string KeyBorderColorValue 
        { 
            get => Settings.KeyBorderColor; 
            set { Settings.KeyBorderColor = value; OnPropertyChanged(); }
        }

        public string KeyTextWhiteColorValue 
        { 
            get => Settings.KeyTextWhiteColor; 
            set { Settings.KeyTextWhiteColor = value; OnPropertyChanged(); }
        }

        public string KeyTextBlackColorValue 
        { 
            get => Settings.KeyTextBlackColor; 
            set { Settings.KeyTextBlackColor = value; OnPropertyChanged(); }
        }
    }
}