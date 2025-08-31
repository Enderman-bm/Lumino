using System;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using DominoNext.ViewModels;
using DominoNext.ViewModels.Editor.Modules;
using DominoNext.Services.Interfaces;
using DominoNext.Views.Controls.Editing;

namespace DominoNext.ViewModels.Editor.Commands.Handlers
{
    /// <summary>
    /// 选择工具处理器
    /// </summary>
    public class SelectToolHandler
    {
        private readonly NoteDragModule _dragModule;
        private readonly ICoordinateService _coordinateService;
        private readonly PianoRollViewModel _pianoRollViewModel;

        public SelectToolHandler(NoteDragModule dragModule, ICoordinateService coordinateService, PianoRollViewModel pianoRollViewModel)
        {
            _dragModule = dragModule;
            _coordinateService = coordinateService;
            _pianoRollViewModel = pianoRollViewModel;
        }

        public void HandlePress(EditorInteractionArgs args)
        {
            if (args.MouseButtons != MouseButtons.Left) return;

            var note = _pianoRollViewModel.GetNoteAtPosition(args.Position);
            if (note != null)
            {
                // 如果点击的是已选中的音符，开始拖拽
                if (note.IsSelected)
                {
                    _dragModule.StartDrag(note, args.Position);
                    return;
                }

                // 如果按下了Ctrl键，添加到选择集合并开始拖拽
                if (args.Modifiers.HasFlag(KeyModifiers.Control))
                {
                    note.IsSelected = true;
                    _dragModule.StartDrag(note, args.Position);
                }
                else
                {
                    // 清除其他选择，只选择当前音符并开始拖拽
                    foreach (var n in _pianoRollViewModel.Notes)
                    {
                        n.IsSelected = false;
                    }
                    note.IsSelected = true;
                    _dragModule.StartDrag(note, args.Position);
                }
            }
            else
            {
                // 点击空白区域，清除选择（除非按住Ctrl）
                if (!args.Modifiers.HasFlag(KeyModifiers.Control))
                {
                    foreach (var n in _pianoRollViewModel.Notes)
                    {
                        n.IsSelected = false;
                    }
                }
            }
        }

        public void HandleMove(EditorInteractionArgs args)
        {
            if (args.MouseButtons == MouseButtons.Left && _dragModule.IsDragging)
            {
                _dragModule.UpdateDrag(args.Position);
            }
        }

        public void HandleRelease(EditorInteractionArgs args)
        {
            if (_dragModule.IsDragging)
            {
                _dragModule.EndDrag();
            }
        }
    }
}