using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DominoNext.Models.Music;
using DominoNext.Services.Interfaces;
using DominoNext.ViewModels.Editor.Commands;
using DominoNext.ViewModels.Editor.Modules;
using DominoNext.ViewModels.Editor.State;
using DominoNext.ViewModels.Editor.Components;
using DominoNext.ViewModels.Editor.Enums;
using EnderDebugger;

namespace DominoNext.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel项目初始化
    /// 处理新项目的初始化和默认设置
    /// </summary>
    public partial class PianoRollViewModel : ViewModelBase
    {
        #region 项目初始化方法
        /// <summary>
        /// 初始化新项目，添加默认的Tempo事件
        /// </summary>
        public void InitializeNewProject()
        {
            // 在时值0位置添加默认的BPM120事件
            AddDefaultTempoEvent();
        }

        /// <summary>
        /// 添加默认的Tempo事件（BPM120在时值0位置）
        /// </summary>
        private void AddDefaultTempoEvent()
        {
            // TODO: 实际应该将Tempo事件添加到项目的事件列表中
            // 这里先设置当前Tempo值作为显示
            CurrentTempo = 120;

            _logger.Info("PianoRollViewModel", "已初始化默认Tempo事件：BPM120 在时值0位置");
        }

        /// <summary>
        /// 设置当前Tempo值（用于显示和编辑）
        /// </summary>
        /// <param name="bpm">每分钟节拍数（BPM）</param>
        public void SetCurrentTempo(int bpm)
        {
            if (bpm >= 20 && bpm <= 300)
            {
                CurrentTempo = bpm;
                Toolbar.SetCurrentTempo(bpm);
            }
        }
        #endregion
    }
}