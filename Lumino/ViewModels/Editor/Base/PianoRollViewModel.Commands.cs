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
using Lumino.Services.Implementation;
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
        public void SelectPencilTool() => Toolbar.SelectPencilTool();

        /// <summary>
        /// 选择选择工具命令
        /// </summary>
        [RelayCommand]
        public void SelectSelectionTool() => Toolbar.SelectSelectionTool();

        /// <summary>
        /// 选择橡皮擦工具命令
        /// </summary>
        [RelayCommand]
        public void SelectEraserTool() => Toolbar.SelectEraserTool();

        /// <summary>
        /// 选择剪切工具命令
        /// </summary>
        [RelayCommand]
        public void SelectCutTool() => Toolbar.SelectCutTool();
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

        #region 复制粘贴命令
        /// <summary>
        /// 复制选中的音符
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCopyCommand))]
        public void CopySelectedNotes()
        {
            var selectedNotes = Notes.Where(n => n.IsSelected).ToList();
            if (selectedNotes.Any())
            {
                // 存储选中的音符数据用于粘贴
                _clipboardNotes = selectedNotes.Select(note => new NoteClipboardData
                {
                    StartTime = note.StartPosition,
                    Duration = note.Duration,
                    Pitch = note.Pitch,
                    Velocity = note.Velocity
                }).ToList();
                
                _logger.Debug("PianoRollViewModel", $"已复制 {selectedNotes.Count} 个音符到剪贴板");
            }
        }

        /// <summary>
        /// 粘贴音符
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanPasteCommand))]
        public void PasteNotes()
        {
            if (_clipboardNotes == null || !_clipboardNotes.Any()) return;

            // 清除当前选择
            SelectionModule.ClearSelection(Notes);

            // 计算粘贴位置（基于当前时间轴位置）
            var pasteStartTime = TimelinePosition;
            
            // 创建新的音符
            var newNotes = new List<NoteViewModel>();
            var baseTime = new MusicalFraction((int)TimelinePosition, 1); // 将double转换为MusicalFraction
            foreach (var clipboardNote in _clipboardNotes)
            {
                var relativeStartTime = clipboardNote.StartTime - _clipboardNotes.Min(n => n.StartTime);
                var newNote = new NoteViewModel(_midiConversionService)
                {
                    StartPosition = baseTime + relativeStartTime,
                    Duration = clipboardNote.Duration,
                    Pitch = clipboardNote.Pitch,
                    Velocity = clipboardNote.Velocity,
                    IsSelected = true
                };
                newNotes.Add(newNote);
            }

            // 添加到当前音轨
            foreach (var note in newNotes)
            {
                Notes.Add(note);
            }

            _logger.Debug("PianoRollViewModel", $"已粘贴 {newNotes.Count} 个音符");
        }

        /// <summary>
        /// 全选音符
        /// </summary>
        [RelayCommand]
        public void SelectAllNotes()
        {
            SelectionModule.SelectAll(Notes);
        }

        /// <summary>
        /// 取消选择所有音符
        /// </summary>
        [RelayCommand]
        public void DeselectAllNotes()
        {
            SelectionModule.ClearSelection(Notes);
        }

        /// <summary>
        /// 删除选中的音符
        /// </summary>
        [RelayCommand]
        public void DeleteSelectedNotes()
        {
            var selectedNotes = Notes.Where(n => n.IsSelected).ToList();
            if (selectedNotes.Any())
            {
                // 创建包含索引信息的删除列表
                var notesWithIndices = selectedNotes.Select(note => (note, Notes.IndexOf(note))).ToList();
                var deleteOperation = new DeleteNotesOperation(this, notesWithIndices);
                _undoRedoService.ExecuteAndRecord(deleteOperation);

                _logger.Debug("PianoRollViewModel", $"删除了 {selectedNotes.Count} 个音符");
            }
        }

        /// <summary>
        /// 剪切选中的音符（复制到剪贴板然后删除）
        /// </summary>
        [RelayCommand]
        public void CutSelectedNotes()
        {
            CopySelectedNotes();
            DeleteSelectedNotes();
        }

        /// <summary>
        /// 复制选中的音符（创建副本）
        /// </summary>
        [RelayCommand]
        public void DuplicateSelectedNotes()
        {
            var selectedNotes = Notes.Where(n => n.IsSelected).ToList();
            if (selectedNotes.Any())
            {
                var duplicatedNotes = new List<NoteViewModel>();

                foreach (var note in selectedNotes)
                {
                    var newNote = new NoteViewModel
                    {
                        Pitch = note.Pitch,
                        StartPosition = note.StartPosition + new MusicalFraction(1, 4), // 向右偏移一个四分音符
                        Duration = note.Duration,
                        Velocity = note.Velocity,
                        IsSelected = false // 新复制的音符设为未选中状态
                    };
                    duplicatedNotes.Add(newNote);
                    Notes.Add(newNote);
                }

                // 创建撤销操作
                var duplicateOperation = new DuplicateNotesOperation(this, duplicatedNotes);
                _undoRedoService.ExecuteAndRecord(duplicateOperation);

                _logger.Debug("PianoRollViewModel", $"复制了 {duplicatedNotes.Count} 个音符");
            }
        }

        /// <summary>
        /// 量化选中的音符
        /// </summary>
        [RelayCommand]
        public void QuantizeSelectedNotes()
        {
            var selectedNotes = Notes.Where(n => n.IsSelected).ToList();
            if (selectedNotes.Any())
            {
                var originalPositions = selectedNotes.ToDictionary(n => n, n => n.StartPosition);

                foreach (var note in selectedNotes)
                {
                    // 根据当前网格量化设置量化音符起始位置
                    var quantizedPosition = MusicalFraction.QuantizeToGrid(note.StartPosition, Toolbar.GridQuantization);
                    note.StartPosition = quantizedPosition;
                }

                // 创建撤销操作
                var quantizeOperation = new QuantizeNotesOperation(selectedNotes, originalPositions);
                _undoRedoService.ExecuteAndRecord(quantizeOperation);

                _logger.Debug("PianoRollViewModel", $"量化了 {selectedNotes.Count} 个音符");
            }
        }

        /// <summary>
        /// 是否可以复制
        /// </summary>
        private bool CanCopyCommand => Notes.Any(n => n.IsSelected);

        /// <summary>
        /// 是否可以粘贴
        /// </summary>
        private bool CanPasteCommand => _clipboardNotes != null && _clipboardNotes.Any();

        /// <summary>
        /// 放大视图
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanZoomInCommand))]
        public void ZoomIn()
        {
            ZoomManager.ZoomIn();
        }

        /// <summary>
        /// 缩小视图
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanZoomOutCommand))]
        public void ZoomOut()
        {
            ZoomManager.ZoomOut();
        }

        /// <summary>
        /// 适应窗口大小
        /// </summary>
        [RelayCommand]
        public void FitToWindow()
        {
            // 重置到默认缩放
            ZoomManager.SetZoomSliderValue(50.0);
            ZoomManager.SetVerticalZoomSliderValue(50.0);
        }

        /// <summary>
        /// 重置缩放
        /// </summary>
        [RelayCommand]
        public void ResetZoom()
        {
            ZoomManager.SetZoomSliderValue(50.0);
            ZoomManager.SetVerticalZoomSliderValue(50.0);
        }

        /// <summary>
        /// 是否可以放大
        /// </summary>
        private bool CanZoomInCommand => !ZoomManager.IsAtMaximumZoom;

        /// <summary>
        /// 是否可以缩小
        /// </summary>
        private bool CanZoomOutCommand => !ZoomManager.IsAtMinimumZoom;
        #endregion

        #region 播放控制命令
        /// <summary>
        /// 开始播放
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanPlayCommand))]
        public void Play()
        {
            // TODO: 实现播放逻辑
            _logger.Info("PianoRollViewModel", "播放功能待实现");
        }

        /// <summary>
        /// 暂停播放
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanPauseCommand))]
        public void Pause()
        {
            // TODO: 实现暂停逻辑
            _logger.Info("PianoRollViewModel", "暂停功能待实现");
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStopCommand))]
        public void Stop()
        {
            // TODO: 实现停止逻辑
            _logger.Info("PianoRollViewModel", "停止功能待实现");
        }

        /// <summary>
        /// 是否可以播放
        /// </summary>
        private bool CanPlayCommand => true; // TODO: 根据播放状态判断

        /// <summary>
        /// 是否可以暂停
        /// </summary>
        private bool CanPauseCommand => false; // TODO: 根据播放状态判断

        /// <summary>
        /// 是否可以停止
        /// </summary>
        private bool CanStopCommand => false; // TODO: 根据播放状态判断
        #endregion
    }
}