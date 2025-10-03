using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models.Music;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor.Commands;
using Lumino.ViewModels.Editor.Modules;
using Lumino.ViewModels.Editor.State;
using Lumino.ViewModels.Editor.Components;
using Lumino.ViewModels.Editor.Enums;
using EnderDebugger;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel MIDI文件管理
    /// 处理MIDI文件的时长设置和管理
    /// </summary>
    public partial class PianoRollViewModel : ViewModelBase
    {
        #region MIDI文件时长管理
        /// <summary>
        /// 设置MIDI文件的总时长（被四分音符数表示）
        /// </summary>
        /// <param name="durationInQuarterNotes">时长（四分音符单位）</param>
        public void SetMidiFileDuration(double durationInQuarterNotes)
        {
            if (durationInQuarterNotes < 0)
            {
                throw new ArgumentException("MIDI文件时长不能为负数", nameof(durationInQuarterNotes));
            }

            MidiFileDuration = durationInQuarterNotes;

            // 设置时长后立即更新滚动范围
            UpdateMaxScrollExtent();

            OnPropertyChanged(nameof(HasMidiFileDuration));

            // 更新歌曲长度相关属性
            OnPropertyChanged(nameof(EffectiveSongLength));
            OnPropertyChanged(nameof(ScrollbarTotalLength));
            OnPropertyChanged(nameof(CurrentViewportRatio));
            OnPropertyChanged(nameof(CurrentScrollPositionRatio));
        }

        /// <summary>
        /// 设置MIDI文件的总时长（以秒为单位）
        /// </summary>
        /// <param name="durationInSeconds">时长（秒）</param>
        /// <param name="microsecondsPerQuarterNote">每四分音符的微秒数（用于转换）</param>
        public void SetMidiFileDurationFromSeconds(double durationInSeconds, int microsecondsPerQuarterNote = 500000)
        {
            if (durationInSeconds < 0)
            {
                throw new ArgumentException("MIDI文件时长不能为负数", nameof(durationInSeconds));
            }

            // 将秒转换为四分音符单位
            // 每四分音符的秒数 = 微秒数 / 1,000,000
            double secondsPerQuarterNote = microsecondsPerQuarterNote / 1_000_000.0;
            double durationInQuarterNotes = durationInSeconds / secondsPerQuarterNote;

            SetMidiFileDuration(durationInQuarterNotes);
        }

        /// <summary>
        /// 清除MIDI文件时长设置
        /// </summary>
        public void ClearMidiFileDuration()
        {
            MidiFileDuration = 0.0;
            UpdateMaxScrollExtent();
            OnPropertyChanged(nameof(HasMidiFileDuration));

            // 更新歌曲长度相关属性
            OnPropertyChanged(nameof(EffectiveSongLength));
            OnPropertyChanged(nameof(ScrollbarTotalLength));
            OnPropertyChanged(nameof(CurrentViewportRatio));
            OnPropertyChanged(nameof(CurrentScrollPositionRatio));
        }
        #endregion
    }
}