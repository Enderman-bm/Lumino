using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Lumino.Views.Controls.Editing;
using Lumino.ViewModels.Editor;
using Lumino.ViewModels.Editor.State;
using System;

namespace Lumino.Views.Controls.Editing.Input
{
    /// <summary>
    /// ����������CursorManager��
    /// �������ҪĿ�����ڸ��ٱ༭�ؼ��������ͣ�����еĹ����ʽ�����������û������ MVVM ViewModel ״̬�Ķ�̬�л������ʽ��
    /// �� MVVM �ܹ��У������ͼ�Ĺ��߷�Ӧ�� ViewModel ��ҵ��״̬����ק��������С�������л��ȣ��й�����ʵ�ָ��û������������Ӿ���ʾ�������Ż���
    /// </summary>
    public class CursorManager
    {
        private readonly Control _control;
        private string _currentCursorType = "Default";
        private bool _isHoveringResizeEdge = false;
        private bool _isHoveringNote = false;
        private DateTime _lastStateChangeTime = DateTime.MinValue;
        private const double STATE_CHANGE_DEBOUNCE_MS = 50; // 50ms����

        /// <summary>
        /// ���캯������ʼ������������Ŀ��ؼ���
        /// �� MVVM �ܹ��н�����������ͼ�ؼ��󶨡�
        /// </summary>
        public CursorManager(Control control)
        {
            _control = control;
        }

        /// <summary>
        /// �Ƿ���ͣ������������Ե��
        /// </summary>
        public bool IsHoveringResizeEdge => _isHoveringResizeEdge;
        /// <summary>
        /// �Ƿ���ͣ�������ϡ�
        /// </summary>
        public bool IsHoveringNote => _isHoveringNote;
        /// <summary>
        /// ��ͣ״̬�Ƿ����仯��
        /// </summary>
        public bool HoveringStateChanged { get; private set; }

        /// <summary>
        /// ���ݵ�ǰָ��λ�ú� ViewModel ״̬���¹����ʽ��
        /// �� MVVM �ܹ��и���ҵ��״̬����ק��������С�������л��ȣ���̬���������ʽ�����û��ṩ�����Ľ�����ʾ��
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
                newCursorType = "SizeAll"; // ��קʱ��ʾ�ƶ���ͷ
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
                        newCursorType = "SizeWE"; // ������Сʱ��ʾ���Ҽ�ͷ
                        isHoveringResize = true;
                    }
                    else
                    {
                        newCursorType = "SizeAll"; // ��ͣ������ʱ��ʾ�ƶ���ͷ����קģʽ��
                    }
                }
                else
                {
                    newCursorType = "Default"; // �հ�����Ĭ�Ϲ��
                }
            }
            else if (viewModel.CurrentTool == EditorTool.Select)
            {
                var note = viewModel.GetNoteAtPosition(position);
                if (note != null)
                {
                    isHoveringNote = true;
                    newCursorType = "SizeAll"; // ѡ�񹤾���ͣ������ʱҲ��ʾ�ƶ���ͷ
                }
                else
                {
                    newCursorType = "Default"; // �հ�����Ĭ�Ϲ��
                }
            }

            // �����ͣ״̬�仯�����ӷ�������
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
        /// ���ù��״̬ΪĬ�ϡ�
        /// �� MVVM �����ڽ���������ؼ�ʧ��ʱ�ָ�Ĭ�Ϲ�ꡣ
        /// </summary>
        public void Reset()
        {
            _isHoveringResizeEdge = false;
            _isHoveringNote = false;
            UpdateCursor("Default");
        }

        /// <summary>
        /// ʵ�����ÿؼ��Ĺ�����͡�
        /// ���ڹ�����ͱ仯ʱ���£������ظ����á�
        /// </summary>
        private void UpdateCursor(string cursorType)
        {
            if (_currentCursorType == cursorType) return;

            _currentCursorType = cursorType;

            _control.Cursor = cursorType switch
            {
                "SizeWE" => new Cursor(StandardCursorType.SizeWestEast),     // ���Ҽ�ͷ
                "SizeAll" => new Cursor(StandardCursorType.SizeAll),        // �����ͷ
                "Hand" => new Cursor(StandardCursorType.Hand),              // ����
                _ => new Cursor(StandardCursorType.Arrow)                   // Ĭ�ϼ�ͷ
            };
        }
    }
}