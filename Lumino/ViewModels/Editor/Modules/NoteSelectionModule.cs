using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Lumino.ViewModels.Editor.State;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor.Modules.Base;

namespace Lumino.ViewModels.Editor.Modules
{
    /// <summary>
    /// 音符选择功能模块
    /// 重构后使用基类，减少重复代码
    /// </summary>
    public class NoteSelectionModule : EditorModuleBase
    {
        private readonly SelectionState _selectionState;

        public override string ModuleName => "NoteSelection";

        public NoteSelectionModule(SelectionState selectionState, ICoordinateService coordinateService) 
            : base(coordinateService)
        {
            _selectionState = selectionState;
        }

        /// <summary>
        /// 获取指定位置的音符 - 优化版本
        /// </summary>
        public NoteViewModel? GetNoteAtPosition(Point position, IEnumerable<NoteViewModel> notes, double timeToPixelScale, double keyHeight)
        {
            if (_pianoRollViewModel == null) return null;

            foreach (var note in notes)
            {
                // 使用支持滚动偏移量的坐标转换方法
                var noteRect = _coordinateService.GetNoteRect(note, timeToPixelScale, keyHeight, 
                    _pianoRollViewModel.CurrentScrollOffset, _pianoRollViewModel.VerticalScrollOffset);
                if (noteRect.Contains(position))
                {
                    return note;
                }
            }
            return null;
        }

        /// <summary>
        /// 开始选择框
        /// </summary>
        public void StartSelection(Point startPoint)
        {
            _selectionState.StartSelection(startPoint);
            OnSelectionUpdated?.Invoke();
        }

        /// <summary>
        /// 更新选择框
        /// </summary>
        public void UpdateSelection(Point currentPoint)
        {
            _selectionState.UpdateSelection(currentPoint);
            OnSelectionUpdated?.Invoke();
        }

        /// <summary>
        /// 完成选择框
        /// </summary>
        public void EndSelection(IEnumerable<NoteViewModel> notes)
        {
            if (_selectionState.SelectionStart.HasValue && _selectionState.SelectionEnd.HasValue && _pianoRollViewModel != null)
            {
                var start = _selectionState.SelectionStart.Value;
                var end = _selectionState.SelectionEnd.Value;

                var x = System.Math.Min(start.X, end.X);
                var y = System.Math.Min(start.Y, end.Y);
                var width = System.Math.Abs(end.X - start.X);
                var height = System.Math.Abs(end.Y - start.Y);

                var selectionRect = new Rect(x, y, width, height);
                SelectNotesInArea(selectionRect, notes);
            }

            _selectionState.EndSelection();
            OnSelectionUpdated?.Invoke();
        }

        /// <summary>
        /// 选择矩形区域的音符
        /// </summary>
        public void SelectNotesInArea(Rect area, IEnumerable<NoteViewModel> notes)
        {
            if (_pianoRollViewModel == null) return;

            foreach (var note in notes)
            {
                // 使用支持滚动偏移量的坐标转换方法
                var noteRect = _coordinateService.GetNoteRect(note, 
                    _pianoRollViewModel.TimeToPixelScale, 
                    _pianoRollViewModel.KeyHeight,
                    _pianoRollViewModel.CurrentScrollOffset,
                    _pianoRollViewModel.VerticalScrollOffset);
                
                if (area.Intersects(noteRect))
                {
                    note.IsSelected = true;
                }
            }
            OnSelectionUpdated?.Invoke();
        }

        /// <summary>
        /// 清除选择
        /// </summary>
        public void ClearSelection(IEnumerable<NoteViewModel> notes)
        {
            foreach (var note in notes)
            {
                note.IsSelected = false;
            }
            OnSelectionUpdated?.Invoke();
        }

        /// <summary>
        /// 选择所有音符
        /// </summary>
        public void SelectAll(IEnumerable<NoteViewModel> notes)
        {
            foreach (var note in notes)
            {
                note.IsSelected = true;
            }
            OnSelectionUpdated?.Invoke();
        }

        // 事件
        public event Action? OnSelectionUpdated;

        // 只读属性
        public bool IsSelecting => _selectionState.IsSelecting;
        public Point? SelectionStart => _selectionState.SelectionStart;
        public Point? SelectionEnd => _selectionState.SelectionEnd;
    }
}

