using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models;
using System;
using System.Windows.Input;

namespace Lumino.ViewModels
{
    /// <summary>
    /// 项目设置窗口ViewModel
    /// </summary>
    public partial class ProjectSettingsViewModel : ObservableObject
    {
        private readonly ProjectSettings _originalSettings;
        private readonly Action<ProjectSettings>? _onSettingsSaved;

        [ObservableProperty]
        private double _bpm = 120.0;

        [ObservableProperty]
        private int _ppq = 1920;

        [ObservableProperty]
        private string _projectName = string.Empty;

        [ObservableProperty]
        private string _midiAuthor = string.Empty;

        [ObservableProperty]
        private string _midiDescription = string.Empty;

        /// <summary>
        /// 窗口是否应该关闭
        /// </summary>
        public bool ShouldClose { get; private set; }

        /// <summary>
        /// 设置是否被保存
        /// </summary>
        public bool SettingsSaved { get; private set; }

        public ProjectSettingsViewModel(ProjectSettings settings, Action<ProjectSettings>? onSettingsSaved = null)
        {
            _originalSettings = settings;
            _onSettingsSaved = onSettingsSaved;

            // 加载当前设置
            LoadSettings(settings);
        }

        private void LoadSettings(ProjectSettings settings)
        {
            Bpm = settings.BPM;
            Ppq = settings.PPQ;
            ProjectName = settings.ProjectName;
            MidiAuthor = settings.MidiAuthor;
            MidiDescription = settings.MidiDescription;
        }

        [RelayCommand]
        private void Save()
        {
            // 验证输入
            if (Bpm <= 0)
            {
                // 可以添加错误提示
                return;
            }

            if (Ppq < 96 || Ppq > 9600)
            {
                // PPQ范围验证：96-9600是合理范围
                return;
            }

            // 更新原始设置对象
            _originalSettings.BPM = Bpm;
            _originalSettings.PPQ = Ppq;
            _originalSettings.ProjectName = ProjectName?.Trim() ?? string.Empty;
            _originalSettings.MidiAuthor = MidiAuthor?.Trim() ?? string.Empty;
            _originalSettings.MidiDescription = MidiDescription?.Trim() ?? string.Empty;
            _originalSettings.LastModifiedTime = DateTime.Now;

            // 通知保存回调
            _onSettingsSaved?.Invoke(_originalSettings);

            SettingsSaved = true;
            ShouldClose = true;
        }

        [RelayCommand]
        private void Cancel()
        {
            SettingsSaved = false;
            ShouldClose = true;
        }

        /// <summary>
        /// 重置为默认值
        /// </summary>
        [RelayCommand]
        private void ResetToDefault()
        {
            Bpm = 120.0;
            Ppq = 1920;
            ProjectName = string.Empty;
            MidiAuthor = string.Empty;
            MidiDescription = string.Empty;
        }
    }
}
