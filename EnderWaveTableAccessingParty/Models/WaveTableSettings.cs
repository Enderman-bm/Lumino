using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using EnderDebugger;

namespace EnderWaveTableAccessingParty.Models
{
    /// <summary>
    /// 播表设置模型
    /// </summary>
    public partial class WaveTableSettings : ObservableObject
    {
        private static readonly EnderLogger _logger = EnderLogger.Instance;

        [ObservableProperty]
        private string _currentWaveTableId = "default";

        [ObservableProperty]
        private int _currentMidiDeviceId = -1;

        [ObservableProperty]
        private bool _enableAudioFeedback = true;

        [ObservableProperty]
        private int _defaultNoteVelocity = 100;

        [ObservableProperty]
        private int _defaultNoteDuration = 200;

        [ObservableProperty]
        private bool _autoPlayOnNotePlacement = true;

        [ObservableProperty]
        private bool _enableVelocitySensitivity = true;

        [ObservableProperty]
        private int _masterVolume = 100;

        [ObservableProperty]
        private Dictionary<string, object> _customSettings = new();

        /// <summary>
        /// 播表设置变更事件
        /// </summary>
        public event EventHandler<WaveTableSettingsChangedEventArgs>? SettingsChanged;

        partial void OnCurrentWaveTableIdChanged(string value)
        {
            _logger.Debug("WaveTableSettings", $"播表ID变更 - 旧值: {CurrentWaveTableId}, 新值: {value}");
            SettingsChanged?.Invoke(this, new WaveTableSettingsChangedEventArgs
            {
                PropertyName = nameof(CurrentWaveTableId),
                OldValue = CurrentWaveTableId,
                NewValue = value
            });
        }

        partial void OnCurrentMidiDeviceIdChanged(int value)
        {
            _logger.Debug("WaveTableSettings", $"MIDI设备ID变更 - 旧值: {CurrentMidiDeviceId}, 新值: {value}");
            SettingsChanged?.Invoke(this, new WaveTableSettingsChangedEventArgs
            {
                PropertyName = nameof(CurrentMidiDeviceId),
                OldValue = CurrentMidiDeviceId,
                NewValue = value
            });
        }

        partial void OnEnableAudioFeedbackChanged(bool value)
        {
            _logger.Debug("WaveTableSettings", $"音频反馈设置变更 - 旧值: {EnableAudioFeedback}, 新值: {value}");
            SettingsChanged?.Invoke(this, new WaveTableSettingsChangedEventArgs
            {
                PropertyName = nameof(EnableAudioFeedback),
                OldValue = EnableAudioFeedback,
                NewValue = value
            });
        }

        /// <summary>
        /// 克隆设置
        /// </summary>
        public WaveTableSettings Clone()
        {
            _logger.Debug("WaveTableSettings", "开始克隆播表设置");
            var clonedSettings = new WaveTableSettings
            {
                CurrentWaveTableId = CurrentWaveTableId,
                CurrentMidiDeviceId = CurrentMidiDeviceId,
                EnableAudioFeedback = EnableAudioFeedback,
                DefaultNoteVelocity = DefaultNoteVelocity,
                DefaultNoteDuration = DefaultNoteDuration,
                AutoPlayOnNotePlacement = AutoPlayOnNotePlacement,
                EnableVelocitySensitivity = EnableVelocitySensitivity,
                MasterVolume = MasterVolume,
                CustomSettings = new Dictionary<string, object>(CustomSettings)
            };
            _logger.Debug("WaveTableSettings", "播表设置克隆完成");
            return clonedSettings;
        }

        /// <summary>
        /// 应用设置
        /// </summary>
        public void ApplySettings(WaveTableSettings settings)
        {
            _logger.Debug("WaveTableSettings", $"开始应用播表设置 - 播表ID: {settings.CurrentWaveTableId}, 设备ID: {settings.CurrentMidiDeviceId}");
            CurrentWaveTableId = settings.CurrentWaveTableId;
            CurrentMidiDeviceId = settings.CurrentMidiDeviceId;
            EnableAudioFeedback = settings.EnableAudioFeedback;
            DefaultNoteVelocity = settings.DefaultNoteVelocity;
            DefaultNoteDuration = settings.DefaultNoteDuration;
            AutoPlayOnNotePlacement = settings.AutoPlayOnNotePlacement;
            EnableVelocitySensitivity = settings.EnableVelocitySensitivity;
            MasterVolume = settings.MasterVolume;
            CustomSettings = new Dictionary<string, object>(settings.CustomSettings);
            _logger.Debug("WaveTableSettings", "播表设置应用完成");
        }
    }

    /// <summary>
    /// 播表设置变更事件参数
    /// </summary>
    public class WaveTableSettingsChangedEventArgs : EventArgs
    {
        public string PropertyName { get; set; } = string.Empty;
        public object? OldValue { get; set; }
        public object? NewValue { get; set; }
    }
}