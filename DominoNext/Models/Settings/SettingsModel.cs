using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DominoNext.Models.Settings
{
    /// <summary>
    /// Ӧ�ó�������ģ��
    /// </summary>
    public partial class SettingsModel : ObservableObject
    {
        private static readonly string ConfigFileName = "appsettings.json";

        [ObservableProperty]
        private string _language = "zh-CN";

        [ObservableProperty]
        private ThemeVariant _theme = ThemeVariant.Default;

        [ObservableProperty]
        private bool _autoSave = true;

        [ObservableProperty]
        private int _autoSaveInterval = 5; // ����

        [ObservableProperty]
        private bool _showGridLines = true;

        [ObservableProperty]
        private bool _snapToGrid = true;

        [ObservableProperty]
        private double _defaultZoom = 1.0;

        [ObservableProperty]
        private bool _useNativeMenuBar = false;

        [ObservableProperty]
        private int _maxUndoSteps = 100;

        [ObservableProperty]
        private bool _confirmBeforeDelete = true;

        [ObservableProperty]
        private bool _showVelocityBars = true;

        [ObservableProperty]
        private double _pianoKeyWidth = 60.0;

        [ObservableProperty]
        private bool _enableKeyboardShortcuts = true;

        [ObservableProperty]
        private string _customShortcutsJson = "{}";

        // ������MIDI�Ͳ����������
        [ObservableProperty]
        private double _defaultBPM = 120.0;

        [ObservableProperty]
        private string _selectedPlaybackDeviceId = "-1"; // ʹ���ַ���ID�����Ǹ��Ӷ���

        [ObservableProperty]
        private string _selectedPlaybackDeviceName = "Ĭ���豸";

        [ObservableProperty]
        private string _playlistSettingsJson = "[]"; // �����б�����JSON

        [ObservableProperty]
        private string _playbackDevicesJson = "[]"; // �����豸����JSON

        // ���Ƥ��������
        [ObservableProperty]
        private bool _onionSkinEnabled = true;

        [ObservableProperty]
        private double _onionSkinOpacity = 0.3;

        [ObservableProperty]
        private int _onionSkinPreviousFrames = 1;

        [ObservableProperty]
        private int _onionSkinNextFrames = 1;

        [ObservableProperty]
        private bool _onionSkinShowFrameIndicators = true;

        // ������ɫ
        private string _backgroundColor = "#FFFAFAFA"; // ���汳��
        public string BackgroundColor
        {
            get => _backgroundColor;
            set => SetProperty(ref _backgroundColor, value);
        }

        private string _noteColor = "#FF4CAF50"; // ���������ɫ
        public string NoteColor
        {
            get => _noteColor;
            set => SetProperty(ref _noteColor, value);
        }

        private string _gridLineColor = "#1F000000"; // ��������ɫ����͸���ȣ�
        public string GridLineColor
        {
            get => _gridLineColor;
            set => SetProperty(ref _gridLineColor, value);
        }

        private string _keyWhiteColor = "#FFFFFFFF"; // �׼���ɫ
        public string KeyWhiteColor
        {
            get => _keyWhiteColor;
            set => SetProperty(ref _keyWhiteColor, value);
        }

        private string _keyBlackColor = "#FF1F1F1F"; // �ڼ���ɫ
        public string KeyBlackColor
        {
            get => _keyBlackColor;
            set => SetProperty(ref _keyBlackColor, value);
        }

        private string _selectionColor = "#800099FF"; // ѡ�������ɫ
        public string SelectionColor
        {
            get => _selectionColor;
            set => SetProperty(ref _selectionColor, value);
        }

        // �������������Ԫ����ɫ
        private string _noteSelectedColor = "#FFFF9800"; // ѡ��������ɫ
        public string NoteSelectedColor
        {
            get => _noteSelectedColor;
            set => SetProperty(ref _noteSelectedColor, value);
        }

        private string _noteDraggingColor = "#FF2196F3"; // ��ק������ɫ
        public string NoteDraggingColor
        {
            get => _noteDraggingColor;
            set => SetProperty(ref _noteDraggingColor, value);
        }

        private string _notePreviewColor = "#804CAF50"; // Ԥ��������ɫ
        public string NotePreviewColor
        {
            get => _notePreviewColor;
            set => SetProperty(ref _notePreviewColor, value);
        }

        private string _velocityIndicatorColor = "#FFFFC107"; // ����ָʾ����ɫ
        public string VelocityIndicatorColor
        {
            get => _velocityIndicatorColor;
            set => SetProperty(ref _velocityIndicatorColor, value);
        }

        private string _measureHeaderBackgroundColor = "#FFF5F5F5"; // С��ͷ����ɫ
        public string MeasureHeaderBackgroundColor
        {
            get => _measureHeaderBackgroundColor;
            set => SetProperty(ref _measureHeaderBackgroundColor, value);
        }

        private string _measureLineColor = "#FF000080"; // С������ɫ
        public string MeasureLineColor
        {
            get => _measureLineColor;
            set => SetProperty(ref _measureLineColor, value);
        }

        private string _measureTextColor = "#FF000000"; // С��������ɫ
        public string MeasureTextColor
        {
            get => _measureTextColor;
            set => SetProperty(ref _measureTextColor, value);
        }

        private string _separatorLineColor = "#FFCCCCCC"; // �ָ�����ɫ
        public string SeparatorLineColor
        {
            get => _separatorLineColor;
            set => SetProperty(ref _separatorLineColor, value);
        }

        private string _keyBorderColor = "#FF1F1F1F"; // ���ټ��߿���ɫ
        public string KeyBorderColor
        {
            get => _keyBorderColor;
            set => SetProperty(ref _keyBorderColor, value);
        }

        private string _keyTextWhiteColor = "#FF000000"; // �׼�������ɫ
        public string KeyTextWhiteColor
        {
            get => _keyTextWhiteColor;
            set => SetProperty(ref _keyTextWhiteColor, value);
        }

        private string _keyTextBlackColor = "#FFFFFFFF"; // �ڼ�������ɫ
        public string KeyTextBlackColor
        {
            get => _keyTextBlackColor;
            set => SetProperty(ref _keyTextBlackColor, value);
        }

        /// <summary>
        /// ��ȡ��ǰ���Ե���ʾ����
        /// </summary>
        public string LanguageDisplayName
        {
            get
            {
                return Language switch
                {
                    "zh-CN" => "��������",
                    "en-US" => "English",
                    "ja-JP" => "�ձ��Z",
                    _ => Language
                };
            }
        }

        /// <summary>
        /// ��ȡ��ǰ�������ʾ����
        /// </summary>
        public string ThemeDisplayName
        {
            get
            {
                if (Theme == ThemeVariant.Default) return "����ϵͳ";
                if (Theme == ThemeVariant.Light) return "ǳɫ����";
                if (Theme == ThemeVariant.Dark) return "��ɫ����";
                return Theme.ToString();
            }
        }

        /// <summary>
        /// �������ļ���������
        /// </summary>
        public void LoadFromFile()
        {
            try
            {
                string configPath = GetConfigFilePath();
                if (File.Exists(configPath))
                {
                    LoadFromFile(configPath);
                }
            }
            catch (Exception ex)
            {
                // �������ʧ�ܣ�ʹ��Ĭ������
                System.Diagnostics.Debug.WriteLine($"���������ļ�ʧ��: {ex.Message}");
            }
        }

        /// <summary>
        /// ��ָ��·����������
        /// </summary>
        public void LoadFromFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var loadedSettings = JsonSerializer.Deserialize<SettingsModel>(json, options);
                    if (loadedSettings != null)
                    {
                        CopyFromSettings(loadedSettings);
                    }
                }
            }
            catch (Exception ex)
            {
                // �������ʧ�ܣ�ʹ��Ĭ������
                System.Diagnostics.Debug.WriteLine($"���������ļ�ʧ��: {ex.Message}");
            }
        }

        private void CopyFromSettings(SettingsModel source)
        {
            // ��������
            Language = source.Language;
            Theme = source.Theme;
            AutoSave = source.AutoSave;
            AutoSaveInterval = source.AutoSaveInterval;
            ShowGridLines = source.ShowGridLines;
            SnapToGrid = source.SnapToGrid;
            DefaultZoom = source.DefaultZoom;
            UseNativeMenuBar = source.UseNativeMenuBar;
            MaxUndoSteps = source.MaxUndoSteps;
            ConfirmBeforeDelete = source.ConfirmBeforeDelete;
            ShowVelocityBars = source.ShowVelocityBars;
            PianoKeyWidth = source.PianoKeyWidth;
            EnableKeyboardShortcuts = source.EnableKeyboardShortcuts;
            CustomShortcutsJson = source.CustomShortcutsJson;

            // MIDI�Ͳ�������
            DefaultBPM = source.DefaultBPM;
            SelectedPlaybackDeviceId = source.SelectedPlaybackDeviceId;
            SelectedPlaybackDeviceName = source.SelectedPlaybackDeviceName;
            PlaylistSettingsJson = source.PlaylistSettingsJson;
            PlaybackDevicesJson = source.PlaybackDevicesJson;

            // ���Ƥ����
            OnionSkinEnabled = source.OnionSkinEnabled;
            OnionSkinOpacity = source.OnionSkinOpacity;
            OnionSkinPreviousFrames = source.OnionSkinPreviousFrames;
            OnionSkinNextFrames = source.OnionSkinNextFrames;
            OnionSkinShowFrameIndicators = source.OnionSkinShowFrameIndicators;

            // ������ɫ
            BackgroundColor = source.BackgroundColor;
            NoteColor = source.NoteColor;
            GridLineColor = source.GridLineColor;
            KeyWhiteColor = source.KeyWhiteColor;
            KeyBlackColor = source.KeyBlackColor;
            SelectionColor = source.SelectionColor;
            NoteSelectedColor = source.NoteSelectedColor;
            NoteDraggingColor = source.NoteDraggingColor;
            NotePreviewColor = source.NotePreviewColor;
            VelocityIndicatorColor = source.VelocityIndicatorColor;
            MeasureHeaderBackgroundColor = source.MeasureHeaderBackgroundColor;
            MeasureLineColor = source.MeasureLineColor;
            MeasureTextColor = source.MeasureTextColor;
            SeparatorLineColor = source.SeparatorLineColor;
            KeyBorderColor = source.KeyBorderColor;
            KeyTextWhiteColor = source.KeyTextWhiteColor;
            KeyTextBlackColor = source.KeyTextBlackColor;
        }

        /// <summary>
        /// �������õ������ļ�
        /// </summary>
        public void SaveToFile()
        {
            try
            {
                string configPath = GetConfigFilePath();
                SaveToFile(configPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"���������ļ�ʧ��: {ex.Message}");
            }
        }

        /// <summary>
        /// �������õ�ָ��·��
        /// </summary>
        public void SaveToFile(string filePath)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"���������ļ�ʧ��: {ex.Message}");
            }
        }

        /// <summary>
        /// ��ȡ�����ļ�·��
        /// </summary>
        /// <returns>�����ļ�����·��</returns>
        private string GetConfigFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appDataPath, "DominoNext");

            // ȷ��Ŀ¼����
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            return Path.Combine(appFolder, ConfigFileName);
        }

        /// <summary>
        /// ����ΪĬ������
        /// </summary>
        public void ResetToDefaults()
        {
            Language = "zh-CN";
            Theme = ThemeVariant.Default;
            AutoSave = true;
            AutoSaveInterval = 5;
            ShowGridLines = true;
            SnapToGrid = true;
            DefaultZoom = 1.0;
            UseNativeMenuBar = false;
            MaxUndoSteps = 100;
            ConfirmBeforeDelete = true;
            ShowVelocityBars = true;
            PianoKeyWidth = 60.0;
            EnableKeyboardShortcuts = true;
            CustomShortcutsJson = "{}";

            // MIDI�Ͳ�������Ĭ��ֵ
            DefaultBPM = 120.0;
            SelectedPlaybackDeviceId = "-1";
            SelectedPlaybackDeviceName = "Ĭ���豸";
            PlaylistSettingsJson = "[]";
            PlaybackDevicesJson = "[]";

            // ���Ƥ����Ĭ��ֵ
            OnionSkinEnabled = true;
            OnionSkinOpacity = 0.3;
            OnionSkinPreviousFrames = 1;
            OnionSkinNextFrames = 1;
            OnionSkinShowFrameIndicators = true;

            // ����ɫ�ָ�Ĭ�ϣ�ǳɫ����Ϊ��׼��
            BackgroundColor = "#FFFAFAFA";
            NoteColor = "#FF4CAF50";
            GridLineColor = "#1F000000";
            KeyWhiteColor = "#FFFFFFFF";
            KeyBlackColor = "#FF1F1F1F";
            SelectionColor = "#800099FF";

            // ��չԪ����ɫĬ��ֵ
            NoteSelectedColor = "#FFFF9800";
            NoteDraggingColor = "#FF2196F3";
            NotePreviewColor = "#804CAF50";
            VelocityIndicatorColor = "#FFFFC107";
            MeasureHeaderBackgroundColor = "#FFF5F5F5";
            MeasureLineColor = "#FF000080";
            MeasureTextColor = "#FF000000";
            SeparatorLineColor = "#FFCCCCCC";
            KeyBorderColor = "#FF1F1F1F";
            KeyTextWhiteColor = "#FF000000";
            KeyTextBlackColor = "#FFFFFFFF";
        }

        /// <summary>
        /// Ӧ����ɫ����Ĭ����ɫ - �Ż���
        /// </summary>
        public void ApplyDarkThemeDefaults()
        {
            // ��ɫ������
            BackgroundColor = "#FF1E1E1E";
            NoteColor = "#FF66BB6A";
            GridLineColor = "#40FFFFFF";
            
            // ���ټ��Ż�����߶Աȶ�
            KeyWhiteColor = "#FF2D2D30";  // ���ɫ�׼�
            KeyBlackColor = "#FF0F0F0F";  // ����ĺڼ�
            KeyBorderColor = "#FF404040"; // �߿���ɫ
            KeyTextWhiteColor = "#FFCCCCCC"; // �׼�����
            KeyTextBlackColor = "#FF999999"; // �ڼ�����
            
            SelectionColor = "#8064B5F6";

            // ������ɫ�Ż�
            NoteSelectedColor = "#FFFFB74D";
            NoteDraggingColor = "#FF64B5F6";
            NotePreviewColor = "#8066BB6A";
            VelocityIndicatorColor = "#FFFFCA28";
            
            // ����Ԫ���Ż�
            MeasureHeaderBackgroundColor = "#FF252526";
            MeasureLineColor = "#FF6495ED";
            MeasureTextColor = "#FFE0E0E0";
            SeparatorLineColor = "#FF3E3E42";
        }

        /// <summary>
        /// Ӧ��ǳɫ����Ĭ����ɫ
        /// </summary>
        public void ApplyLightThemeDefaults()
        {
            BackgroundColor = "#FFFAFAFA";
            NoteColor = "#FF4CAF50";
            GridLineColor = "#1F000000";
            KeyWhiteColor = "#FFFFFFFF";
            KeyBlackColor = "#FF1F1F1F";
            SelectionColor = "#800099FF";

            NoteSelectedColor = "#FFFF9800";
            NoteDraggingColor = "#FF2196F3";
            NotePreviewColor = "#804CAF50";
            VelocityIndicatorColor = "#FFFFC107";
            MeasureHeaderBackgroundColor = "#FFF5F5F5";
            MeasureLineColor = "#FF000080";
            MeasureTextColor = "#FF000000";
            SeparatorLineColor = "#FFCCCCCC";
            KeyBorderColor = "#FF1F1F1F";
            KeyTextWhiteColor = "#FF000000";
            KeyTextBlackColor = "#FFFFFFFF";
        }
        [ObservableProperty]
        private int _subdivisionLevel = 4; // 小节分割段数：4、8、16

        [ObservableProperty]
        private double _gridLineThickness = 0.8; // 网格线粗细

        [ObservableProperty]
        private double _measureLineThickness = 1.2; // 小节线粗细

        [ObservableProperty]
        private int _beatsPerMeasure = 4; // 每小节的拍数
    }
}