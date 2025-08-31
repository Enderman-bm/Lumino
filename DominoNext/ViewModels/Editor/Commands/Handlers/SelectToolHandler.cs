using System;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using DominoNext.ViewModels;
using DominoNext.ViewModels.Editor.Modules;
using DominoNext.Services.Interfaces;
using DominoNext.Views.Controls.Editing;
using DominoNext.ViewModels.Editor.State;

namespace DominoNext.ViewModels.Editor.Commands.Handlers
{
    /// <summary>
    /// 选择工具处理器 - 支持拖拽和调整大小
    /// </summary>
    public class SelectToolHandler
    {
        private readonly NoteDragModule _dragModule;
        private readonly NoteResizeModule _resizeModule;
        private readonly ICoordinateService _coordinateService;
        private readonly PianoRollViewModel _pianoRollViewModel;

        public SelectToolHandler(NoteDragModule dragModule, ICoordinateService coordinateService, PianoRollViewModel pianoRollViewModel)
        {
            _dragModule = dragModule;
            _resizeModule = pianoRollViewModel.ResizeModule; // 获取 ResizeModule 的引用
            _coordinateService = coordinateService;
            _pianoRollViewModel = pianoRollViewModel;
        }

        public void HandlePress(EditorInteractionArgs args)
        {
            if (args.MouseButtons != MouseButtons.Left) return;

            var note = _pianoRollViewModel.GetNoteAtPosition(args.Position);
            if (note != null)
            {
                // 首先检查是否点击在 resize 边缘
                var resizeHandle = _pianoRollViewModel.GetResizeHandleAtPosition(args.Position, note);
                
                if (resizeHandle != ResizeHandle.None)
                {
                    // 处理选择逻辑（确保音符被选中）
                    if (!note.IsSelected)
                    {
                        if (!args.Modifiers.HasFlag(KeyModifiers.Control))
                        {
                            // 清除其他选择
                            foreach (var n in _pianoRollViewModel.Notes)
                            {
                                n.IsSelected = false;
                            }
                        }
                        note.IsSelected = true;
                    }
                    
                    // 开始调整大小操作
                    _resizeModule.StartResize(args.Position, note, resizeHandle);
                    return;
                }

                // 如果不是调整大小，则处理拖拽或选择
                if (note.IsSelected)
                {
                    // 已选中的音符：开始拖拽
                    _dragModule.StartDrag(note, args.Position);
                }
                else
                {
                    // 未选中的音符：选择并开始拖拽
                    if (args.Modifiers.HasFlag(KeyModifiers.Control))
                    {
                        // Ctrl+点击：添加到选择集合并开始拖拽
                        note.IsSelected = true;
                    }
                    else
                    {
                        // 普通点击：清除其他选择，只选择当前音符并开始拖拽
                        foreach (var n in _pianoRollViewModel.Notes)
                        {
                            n.IsSelected = false;
                        }
                        note.IsSelected = true;
                    }
                    _dragModule.StartDrag(note, args.Position);
                }
            }
            else
            {
                // 点击空白区域：开始选择框或清除选择
                if (!args.Modifiers.HasFlag(KeyModifiers.Control))
                {
                    // 清除所有选择
                    foreach (var n in _pianoRollViewModel.Notes)
                    {
                        n.IsSelected = false;
                    }
                }
                
                // 开始选择框操作
                _pianoRollViewModel.SelectionModule.StartSelection(args.Position);
            }
        }

        public void HandleMove(EditorInteractionArgs args)
        {
            if (args.MouseButtons == MouseButtons.Left)
            {
                if (_resizeModule.IsResizing)
                {
                    // 更新调整大小操作
                    _resizeModule.UpdateResize(args.Position);
                }
                else if (_dragModule.IsDragging)
                {
                    // 更新拖拽操作
                    _dragModule.UpdateDrag(args.Position);
                }
                else if (_pianoRollViewModel.SelectionState.IsSelecting)
                {
                    // 更新选择框
                    _pianoRollViewModel.SelectionModule.UpdateSelection(args.Position);
                }
            }
        }

        public void HandleRelease(EditorInteractionArgs args)
        {
            if (_resizeModule.IsResizing)
            {
                _resizeModule.EndResize();
            }
            else if (_dragModule.IsDragging)
            {
                _dragModule.EndDrag();
            }
            else if (_pianoRollViewModel.SelectionState.IsSelecting)
            {
                _pianoRollViewModel.SelectionModule.EndSelection(_pianoRollViewModel.Notes);
            }
        }
    }
}