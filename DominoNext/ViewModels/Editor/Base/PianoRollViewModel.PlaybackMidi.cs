using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Components;
using Lumino.ViewModels.Editor.Enums;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel的播放和MIDI功能
    /// </summary>
    public partial class PianoRollViewModel
    {
        #region 播放控制命令
        /// <summary>
        /// 播放命令
        /// </summary>
        [RelayCommand]
        private void Play()
        {
            // TODO: 实现播放逻辑
            // 这里应该调用播放服务来开始播放
            IsPlaying = true;
            OnPropertyChanged(nameof(IsPlaying));
        }

        /// <summary>
        /// 暂停命令
        /// </summary>
        [RelayCommand]
        private void Pause()
        {
            // TODO: 实现暂停逻辑
            // 这里应该调用播放服务来暂停播放
            IsPlaying = false;
            OnPropertyChanged(nameof(IsPlaying));
        }

        /// <summary>
        /// 停止命令
        /// </summary>
        [RelayCommand]
        private void Stop()
        {
            // TODO: 实现停止逻辑
            // 这里应该调用播放服务来停止播放并重置位置
            IsPlaying = false;
            CurrentPlaybackTime = 0;
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(CurrentPlaybackTime));
        }

        /// <summary>
        /// 循环播放命令
        /// </summary>
        [RelayCommand]
        private void ToggleLoop()
        {
            IsLooping = !IsLooping;
            OnPropertyChanged(nameof(IsLooping));
        }

        /// <summary>
        /// 设置播放位置命令
        /// </summary>
        [RelayCommand]
        private void SetPlaybackPosition(double time)
        {
            CurrentPlaybackTime = Math.Max(0, Math.Min(time, SongLengthInSeconds));
            OnPropertyChanged(nameof(CurrentPlaybackTime));
        }

        /// <summary>
        /// 跳转到开始命令
        /// </summary>
        [RelayCommand]
        private void JumpToStart()
        {
            CurrentPlaybackTime = 0;
            OnPropertyChanged(nameof(CurrentPlaybackTime));
        }

        /// <summary>
        /// 跳转到结束命令
        /// </summary>
        [RelayCommand]
        private void JumpToEnd()
        {
            CurrentPlaybackTime = SongLengthInSeconds;
            OnPropertyChanged(nameof(CurrentPlaybackTime));
        }

        /// <summary>
        /// 设置循环开始位置命令
        /// </summary>
        [RelayCommand]
        private void SetLoopStart(double time)
        {
            LoopStartTime = Math.Max(0, Math.Min(time, SongLengthInSeconds));
            if (LoopStartTime > LoopEndTime)
            {
                LoopEndTime = LoopStartTime;
            }
            OnPropertyChanged(nameof(LoopStartTime));
            OnPropertyChanged(nameof(LoopEndTime));
        }

        /// <summary>
        /// 设置循环结束位置命令
        /// </summary>
        [RelayCommand]
        private void SetLoopEnd(double time)
        {
            LoopEndTime = Math.Max(LoopStartTime, Math.Min(time, SongLengthInSeconds));
            OnPropertyChanged(nameof(LoopEndTime));
        }
        #endregion

        #region 播放状态属性
        /// <summary>
        /// 是否正在播放
        /// </summary>
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (SetProperty(ref _isPlaying, value))
                {
                    OnPropertyChanged(nameof(PlaybackButtonText));
                    OnPropertyChanged(nameof(PlaybackButtonIcon));
                }
            }
        }
        private bool _isPlaying;

        /// <summary>
        /// 是否循环播放
        /// </summary>
        public bool IsLooping
        {
            get => _isLooping;
            set
            {
                if (SetProperty(ref _isLooping, value))
                {
                    OnPropertyChanged(nameof(LoopButtonText));
                    OnPropertyChanged(nameof(LoopButtonIcon));
                }
            }
        }
        private bool _isLooping;

        /// <summary>
        /// 当前播放时间（秒）
        /// </summary>
        public double CurrentPlaybackTime
        {
            get => _currentPlaybackTime;
            set
            {
                if (SetProperty(ref _currentPlaybackTime, value))
                {
                    OnPropertyChanged(nameof(CurrentPlaybackTimeText));
                    OnPropertyChanged(nameof(CurrentPlaybackTimePixels));
                    OnPropertyChanged(nameof(PlaybackProgressPercentage));
                }
            }
        }
        private double _currentPlaybackTime;

        /// <summary>
        /// 循环开始时间（秒）
        /// </summary>
        public double LoopStartTime
        {
            get => _loopStartTime;
            set
            {
                if (SetProperty(ref _loopStartTime, value))
                {
                    OnPropertyChanged(nameof(LoopStartTimeText));
                    OnPropertyChanged(nameof(LoopStartTimePixels));
                }
            }
        }
        private double _loopStartTime;

        /// <summary>
        /// 循环结束时间（秒）
        /// </summary>
        public double LoopEndTime
        {
            get => _loopEndTime;
            set
            {
                if (SetProperty(ref _loopEndTime, value))
                {
                    OnPropertyChanged(nameof(LoopEndTimeText));
                    OnPropertyChanged(nameof(LoopEndTimePixels));
                }
            }
        }
        private double _loopEndTime;

        /// <summary>
        /// 播放速度（倍速）
        /// </summary>
        public double PlaybackSpeed
        {
            get => _playbackSpeed;
            set
            {
                if (SetProperty(ref _playbackSpeed, Math.Max(0.25, Math.Min(4.0, value))))
                {
                    OnPropertyChanged(nameof(PlaybackSpeedText));
                }
            }
        }
        private double _playbackSpeed = 1.0;

        /// <summary>
        /// 是否启用节拍器
        /// </summary>
        public bool IsMetronomeEnabled
        {
            get => _isMetronomeEnabled;
            set
            {
                if (SetProperty(ref _isMetronomeEnabled, value))
                {
                    OnPropertyChanged(nameof(MetronomeButtonText));
                    OnPropertyChanged(nameof(MetronomeButtonIcon));
                }
            }
        }
        private bool _isMetronomeEnabled;

        /// <summary>
        /// 播放按钮文本
        /// </summary>
        public string PlaybackButtonText => IsPlaying ? "Pause" : "Play";

        /// <summary>
        /// 播放按钮图标
        /// </summary>
        public string PlaybackButtonIcon => IsPlaying ? "PauseIcon" : "PlayIcon";

        /// <summary>
        /// 循环按钮文本
        /// </summary>
        public string LoopButtonText => IsLooping ? "Disable Loop" : "Enable Loop";

        /// <summary>
        /// 循环按钮图标
        /// </summary>
        public string LoopButtonIcon => IsLooping ? "LoopIcon" : "NoLoopIcon";

        /// <summary>
        /// 节拍器按钮文本
        /// </summary>
        public string MetronomeButtonText => IsMetronomeEnabled ? "Disable Metronome" : "Enable Metronome";

        /// <summary>
        /// 节拍器按钮图标
        /// </summary>
        public string MetronomeButtonIcon => IsMetronomeEnabled ? "MetronomeOnIcon" : "MetronomeOffIcon";

        /// <summary>
        /// 当前播放时间（像素）
        /// </summary>
        public double CurrentPlaybackTimePixels => CurrentPlaybackTime * CurrentBPM / 60.0 * 480.0 * TimeToPixelScale;

        /// <summary>
        /// 播放进度百分比
        /// </summary>
        public double PlaybackProgressPercentage => SongLengthInSeconds > 0 ? (CurrentPlaybackTime / SongLengthInSeconds) * 100 : 0;

        /// <summary>
        /// 循环开始时间（像素）
        /// </summary>
        public double LoopStartTimePixels => LoopStartTime * CurrentBPM / 60.0 * 480.0 * TimeToPixelScale;

        /// <summary>
        /// 循环结束时间（像素）
        /// </summary>
        public double LoopEndTimePixels => LoopEndTime * CurrentBPM / 60.0 * 480.0 * TimeToPixelScale;

        /// <summary>
        /// 播放速度文本
        /// </summary>
        public string PlaybackSpeedText => $"{PlaybackSpeed:F1}x";

        /// <summary>
        /// 当前播放时间文本
        /// </summary>
        public string CurrentPlaybackTimeText => $"{CurrentPlaybackTime:F2}s";

        /// <summary>
        /// 循环开始时间文本
        /// </summary>
        public string LoopStartTimeText => $"{LoopStartTime:F2}s";

        /// <summary>
        /// 循环结束时间文本
        /// </summary>
        public string LoopEndTimeText => $"{LoopEndTime:F2}s";
        #endregion

        #region MIDI设备属性
        /// <summary>
        /// MIDI输入设备列表
        /// </summary>
        public List<string> MidiInputDevices
        {
            get => _midiInputDevices;
            set
            {
                if (SetProperty(ref _midiInputDevices, value))
                {
                    OnPropertyChanged(nameof(HasMidiInputDevices));
                    OnPropertyChanged(nameof(MidiInputDevicesText));
                }
            }
        }
        private List<string> _midiInputDevices = new List<string>();

        /// <summary>
        /// MIDI输出设备列表
        /// </summary>
        public List<string> MidiOutputDevices
        {
            get => _midiOutputDevices;
            set
            {
                if (SetProperty(ref _midiOutputDevices, value))
                {
                    OnPropertyChanged(nameof(HasMidiOutputDevices));
                    OnPropertyChanged(nameof(MidiOutputDevicesText));
                }
            }
        }
        private List<string> _midiOutputDevices = new List<string>();

        /// <summary>
        /// 当前MIDI输入设备
        /// </summary>
        public string? CurrentMidiInputDevice
        {
            get => _currentMidiInputDevice;
            set
            {
                if (SetProperty(ref _currentMidiInputDevice, value))
                {
                    OnPropertyChanged(nameof(CurrentMidiInputDeviceText));
                    OnMidiInputDeviceChanged();
                }
            }
        }
        private string? _currentMidiInputDevice;

        /// <summary>
        /// 当前MIDI输出设备
        /// </summary>
        public string? CurrentMidiOutputDevice
        {
            get => _currentMidiOutputDevice;
            set
            {
                if (SetProperty(ref _currentMidiOutputDevice, value))
                {
                    OnPropertyChanged(nameof(CurrentMidiOutputDeviceText));
                    OnMidiOutputDeviceChanged();
                }
            }
        }
        private string? _currentMidiOutputDevice;

        /// <summary>
        /// 是否有MIDI输入设备
        /// </summary>
        public bool HasMidiInputDevices => MidiInputDevices?.Any() == true;

        /// <summary>
        /// 是否有MIDI输出设备
        /// </summary>
        public bool HasMidiOutputDevices => MidiOutputDevices?.Any() == true;

        /// <summary>
        /// MIDI输入设备文本
        /// </summary>
        public string MidiInputDevicesText => HasMidiInputDevices ? $"{MidiInputDevices.Count} devices" : "No devices";

        /// <summary>
        /// MIDI输出设备文本
        /// </summary>
        public string MidiOutputDevicesText => HasMidiOutputDevices ? $"{MidiOutputDevices.Count} devices" : "No devices";

        /// <summary>
        /// 当前MIDI输入设备文本
        /// </summary>
        public string? CurrentMidiInputDeviceText => CurrentMidiInputDevice ?? "None";

        /// <summary>
        /// 当前MIDI输出设备文本
        /// </summary>
        public string? CurrentMidiOutputDeviceText => CurrentMidiOutputDevice ?? "None";
        #endregion

        #region 辅助方法


        /// <summary>
        /// MIDI输入设备变化处理
        /// </summary>
        private void OnMidiInputDeviceChanged()
        {
            // TODO: 实现MIDI输入设备连接逻辑
            // 这里应该连接或断开MIDI输入设备
        }

        /// <summary>
        /// MIDI输出设备变化处理
        /// </summary>
        private void OnMidiOutputDeviceChanged()
        {
            // TODO: 实现MIDI输出设备连接逻辑
            // 这里应该连接或断开MIDI输出设备
        }

        /// <summary>
        /// 刷新MIDI设备列表
        /// </summary>
        public void RefreshMidiDevices()
        {
            // TODO: 实现MIDI设备列表刷新逻辑
            // 这里应该扫描可用的MIDI输入输出设备
            
            // 示例代码（实际实现需要根据具体的MIDI库）
            // MidiInputDevices = MidiDeviceManager.GetInputDevices();
            // MidiOutputDevices = MidiDeviceManager.GetOutputDevices();
        }

        /// <summary>
        /// 发送MIDI音符开消息
        /// </summary>
        public void SendMidiNoteOn(int pitch, int velocity, int channel = 0)
        {
            // TODO: 实现MIDI音符开消息发送
            // 这里应该通过MIDI输出设备发送音符开消息
            
            // 示例代码（实际实现需要根据具体的MIDI库）
            // if (MidiOutputDevice != null)
            // {
            //     MidiOutputDevice.SendNoteOn(channel, pitch, velocity);
            // }
        }

        /// <summary>
        /// 发送MIDI音符关消息
        /// </summary>
        public void SendMidiNoteOff(int pitch, int channel = 0)
        {
            // TODO: 实现MIDI音符关消息发送
            // 这里应该通过MIDI输出设备发送音符关消息
            
            // 示例代码（实际实现需要根据具体的MIDI库）
            // if (MidiOutputDevice != null)
            // {
            //     MidiOutputDevice.SendNoteOff(channel, pitch, 0);
            // }
        }

        /// <summary>
        /// 发送MIDI控制变化消息
        /// </summary>
        public void SendMidiControlChange(int controller, int value, int channel = 0)
        {
            // TODO: 实现MIDI控制变化消息发送
            // 这里应该通过MIDI输出设备发送控制变化消息
            
            // 示例代码（实际实现需要根据具体的MIDI库）
            // if (MidiOutputDevice != null)
            // {
            //     MidiOutputDevice.SendControlChange(channel, controller, value);
            // }
        }

        /// <summary>
        /// 播放音符预览
        /// </summary>
        public void PlayNotePreview(NoteViewModel note)
        {
            // 发送音符开消息
            SendMidiNoteOn(note.Pitch, note.Velocity);
            
            // 设置定时器在音符时长后发送音符关消息
            // TODO: 实现定时器逻辑
            // var noteDurationMs = (note.Duration.ToDouble() / 480.0 * 60.0 / CurrentBPM * 1000);
            // TimerService.SetTimeout(() => SendMidiNoteOff(note.Pitch), noteDurationMs);
        }

        /// <summary>
        /// 停止所有音符预览
        /// </summary>
        public void StopAllNotePreviews()
        {
            // 发送所有音符关消息
            for (int channel = 0; channel < 16; channel++)
            {
                // TODO: 实现MIDI所有音符关消息发送
                // MidiOutputDevice?.SendAllNotesOff(channel);
            }
        }
        #endregion
    }
}