using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using DominoNext.Views.Controls.Editing;
using DominoNext.ViewModels.Editor;
using DominoNext.ViewModels.Editor.State;

namespace DominoNext.Views.Controls.Editing.Input
{
    /// <summary>
    /// 光标管理器（CursorManager）
    /// 该类在项目中用于管理编辑控件（如钢琴卷帘）中的鼠标光标形态，根据用户操作和 MVVM ViewModel 状态动态切换光标类型。
    /// 在 MVVM 架构中，负责将视图层的光标反馈与 ViewModel 的业务状态（如拖拽、调整大小、工具切换等）进行关联，实现用户交互的视觉提示和体验优化。
    /// </summary>
    public class CursorManager
    {
        private readonly Control _control;
        private string _currentCursorType = "Default";
        private bool _isHoveringResizeEdge = false;
        private bool _isHoveringNote = false;

        /// <summary>
        /// 构造函数，初始化光标管理器并绑定目标控件。
        /// 在 MVVM 中用于将光标管理与具体视图控件关联。
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
        /// 根据当前指针位置和 ViewModel 状态，更新光标类型。
        /// 在 MVVM 中用于根据业务状态（拖拽、调整大小、工具切换等）动态反馈光标形态，提升用户体验。
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
                newCursorType = "SizeAll"; // 拖拽时显示四向箭头
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
                        newCursorType = "SizeAll"; // 悬停在音符上时显示四向箭头（拖拽模式）
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
                    newCursorType = "SizeAll"; // 选择工具悬停在音符上时也显示四向箭头
                }
                else
                {
                    newCursorType = "Default"; // 空白区域默认光标
                }
            }

            // 检测悬停状态变化
            bool previousHoveringResizeState = _isHoveringResizeEdge;
            bool previousHoveringNoteState = _isHoveringNote;
            
            _isHoveringResizeEdge = isHoveringResize;
            _isHoveringNote = isHoveringNote;
            
            if (previousHoveringResizeState != _isHoveringResizeEdge || 
                previousHoveringNoteState != _isHoveringNote)
            {
                HoveringStateChanged = true;
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