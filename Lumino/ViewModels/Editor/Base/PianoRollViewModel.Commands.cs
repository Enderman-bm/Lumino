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
    /// PianoRollViewModel命令定义
    /// 包含所有用户界面命令的实现
    /// </summary>
    public partial class PianoRollViewModel : ViewModelBase
    {
        #region 工具选择命令
        /// <summary>
        /// 选择铅笔工具命令
        /// </summary>
        [RelayCommand]
        private void SelectPencilTool() => Toolbar.SelectPencilTool();

        /// <summary>
        /// 选择选择工具命令
        /// </summary>
        [RelayCommand]
        private void SelectSelectionTool() => Toolbar.SelectSelectionTool();

        /// <summary>
        /// 选择橡皮擦工具命令
        /// </summary>
        [RelayCommand]
        private void SelectEraserTool() => Toolbar.SelectEraserTool();

        /// <summary>
        /// 选择剪切工具命令
        /// </summary>
        [RelayCommand]
        private void SelectCutTool() => Toolbar.SelectCutTool();
        #endregion

        #region 音符时长相关命令
        /// <summary>
        /// 切换音符时长下拉框显示状态
        /// </summary>
        [RelayCommand]
        private void ToggleNoteDurationDropDown() => Toolbar.ToggleNoteDurationDropDown();

        /// <summary>
        /// 选择指定的音符时长选项
        /// </summary>
        /// <param name="option">音符时长选项</param>
        [RelayCommand]
        private void SelectNoteDuration(NoteDurationOption option) => Toolbar.SelectNoteDuration(option);

        /// <summary>
        /// 应用自定义分数输入
        /// </summary>
        [RelayCommand]
        private void ApplyCustomFraction() => Toolbar.ApplyCustomFraction();
        #endregion

        #region 选择相关命令
        /// <summary>
        /// 全选当前轨道的所有音符
        /// </summary>
        [RelayCommand]
        private void SelectAll() => SelectionModule.SelectAll(CurrentTrackNotes);
        #endregion

        #region 视图切换命令
        /// <summary>
        /// 切换事件视图显示状态
        /// </summary>
        [RelayCommand]
        private void ToggleEventView() => Toolbar.ToggleEventView();
        #endregion

        #region 事件类型选择相关命令
        /// <summary>
        /// 切换事件类型选择器显示状态
        /// </summary>
        [RelayCommand]
        private void ToggleEventTypeSelector()
        {
            IsEventTypeSelectorOpen = !IsEventTypeSelectorOpen;
        }

        /// <summary>
        /// 选择指定的事件类型
        /// </summary>
        /// <param name="eventType">要选择的事件类型</param>
        [RelayCommand]
        private void SelectEventType(EventType eventType)
        {
            CurrentEventType = eventType;
            IsEventTypeSelectorOpen = false;
        }

        /// <summary>
        /// 设置CC控制器号
        /// </summary>
        /// <param name="ccNumber">CC控制器号（0-127）</param>
        [RelayCommand]
        private void SetCCNumber(int ccNumber)
        {
            if (ccNumber >= 0 && ccNumber <= 127)
            {
                CurrentCCNumber = ccNumber;
            }
        }

        /// <summary>
        /// 验证并设置CC号（支持字符串输入）
        /// </summary>
        /// <param name="ccNumberText">CC号的字符串表示</param>
        [RelayCommand]
        private void ValidateAndSetCCNumber(string ccNumberText)
        {
            if (int.TryParse(ccNumberText, out int ccNumber))
            {
                ccNumber = Math.Max(0, Math.Min(127, ccNumber)); // 限制在0-127范围内
                CurrentCCNumber = ccNumber;
            }
        }
        #endregion

        #region 撤销重做命令
        /// <summary>
        /// 撤销命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanUndoCommand))]
        public void Undo() => _undoRedoService.Undo();

        /// <summary>
        /// 重做命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanRedoCommand))]
        public void Redo() => _undoRedoService.Redo();

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        private bool CanUndoCommand => _undoRedoService.CanUndo;

        /// <summary>
        /// 是否可以重做
        /// </summary>
        private bool CanRedoCommand => _undoRedoService.CanRedo;
        #endregion
    }
}