using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using DominoNext.Views.Controls.Editing;
using DominoNext.ViewModels.Editor;
using DominoNext.ViewModels.Editor.State;
using System;

namespace DominoNext.Views.Controls.Editing.Input
{
    /// <summary>
    /// 光标管理器（CursorManager）
    /// 该类的主要目的在于钢琴编辑控件的鼠标悬停交互中的光标样式管理，根据用户鼠标在 MVVM ViewModel 状态的动态切换光标样式。
    /// 在 MVVM 架构中，光标视图的工具反应与 ViewModel 的业务状态（拖拽、调整大小、工具切换等）有关联，实现给用户带来清晰的视觉提示，交互优化。
    /// </summary>
    public class CursorManager
    {
        private readonly Control _control;
        private string _currentCursorType = "Default";
        private bool _isHoveringResizeEdge = false;
        private bool _isHoveringNote = false;
        private DateTime _lastStateChangeTime = DateTime.MinValue;
        private const double STATE_CHANGE_DEBOUNCE_MS = 50; // 50ms防抖

        /// <summary>
        /// 构造函数，初始化光标管理器的目标控件。
        /// 在 MVVM 架构中将光标管理与视图控件绑定。
        /// </summary>
        public CursorManager(Control control)
        {
            _control = control;
        }

        /// <summary>
        /// 是否悬停在音符调整边缘。
        /// </summary>
        public bool IsHoveringResizeEdge => _isHoveringResizeEdge;
        /// <summary>
        /// 是否悬停在音符上。
        /// </summary>
        public bool IsHoveringNote => _isHoveringNote;
        /// <summary>
        /// 悬停状态是否发生变化。
        /// </summary>
        public bool HoveringStateChanged { get; private set; }

        /// <summary>
        /// 根据当前指针位置和 ViewModel 状态更新光标样式。
        /// 在 MVVM 架构中根据业务状态（拖拽、调整大小、工具切换等）动态调整光标样式，给用户提供清晰的交互提示。
        /// </summary>
        public void UpdateCursorForPosition(Point position, PianoRollViewModel? viewModel)
        {
            HoveringStateChanged = false;
            if (viewModel == null) return;

            string newCursorType = "Default";
            bool isHoveringResize = false;
            bool isHoveringNote = false;

            if (viewModel.ResizeState.IsResizing)
            {
                newCursorType = "SizeWE";
                isHoveringResize = true;
            }
            else if (viewModel.DragState.IsDragging)
            {
                newCursorType = "SizeAll"; // 拖拽时显示移动箭头
            }
            else if (viewModel.CurrentTool == EditorTool.Pencil)
            {
                var note = viewModel.GetNoteAtPosition(position);
                if (note != null)
                {
                    isHoveringNote = true;
                    var handle = viewModel.GetResizeHandleAtPosition(position, note);
                    if (handle == ResizeHandle.StartEdge || handle == ResizeHandle.EndEdge)
                    {
                        newCursorType = "SizeWE"; // 调整大小时显示左右箭头
                        isHoveringResize = true;
                    }
                    else
                    {
                        newCursorType = "SizeAll"; // 悬停在音符时显示移动箭头（拖拽模式）
                    }
                }
                else
                {
                    newCursorType = "Default"; // 空白区域默认光标
                }
            }
            else if (viewModel.CurrentTool == EditorTool.Select)
            {
                var note = viewModel.GetNoteAtPosition(position);
                if (note != null)
                {
                    isHoveringNote = true;
                    newCursorType = "SizeAll"; // 选择工具悬停在音符时也显示移动箭头
                }
                else
                {
                    newCursorType = "Default"; // 空白区域默认光标
                }
            }

            // 检测悬停状态变化，添加防抖机制
            bool previousHoveringResizeState = _isHoveringResizeEdge;
            bool previousHoveringNoteState = _isHoveringNote;
            
            _isHoveringResizeEdge = isHoveringResize;
            _isHoveringNote = isHoveringNote;
            
            var now = DateTime.Now;
            bool stateActuallyChanged = (previousHoveringResizeState != _isHoveringResizeEdge || 
                                       previousHoveringNoteState != _isHoveringNote);
            
            if (stateActuallyChanged)
            {
                var timeSinceLastChange = (now - _lastStateChangeTime).TotalMilliseconds;
                if (timeSinceLastChange >= STATE_CHANGE_DEBOUNCE_MS)
                {
                    HoveringStateChanged = true;
                    _lastStateChangeTime = now;
                }
            }
            
            UpdateCursor(newCursorType);
        }

        /// <summary>
        /// 重置光标状态为默认。
        /// 在 MVVM 中用于交互结束或控件失焦时恢复默认光标。
        /// </summary>
        public void Reset()
        {
            _isHoveringResizeEdge = false;
            _isHoveringNote = false;
            UpdateCursor("Default");
        }

        /// <summary>
        /// 实际设置控件的光标类型。
        /// 仅在光标类型变化时更新，避免重复设置。
        /// </summary>
        private void UpdateCursor(string cursorType)
        {
            if (_currentCursorType == cursorType) return;

            _currentCursorType = cursorType;

            _control.Cursor = cursorType switch
            {
                "SizeWE" => new Cursor(StandardCursorType.SizeWestEast),     // 左右箭头
                "SizeAll" => new Cursor(StandardCursorType.SizeAll),        // 四向箭头
                "Hand" => new Cursor(StandardCursorType.Hand),              // 手形
                _ => new Cursor(StandardCursorType.Arrow)                   // 默认箭头
            };
        }
    }
}