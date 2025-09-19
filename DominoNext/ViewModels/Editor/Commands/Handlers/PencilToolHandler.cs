using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Lumino.ViewModels.Editor.State;

namespace Lumino.ViewModels.Editor.Commands
{
    /// <summary>
    /// Ǧ�ʹ��ߴ�����
    /// </summary>
    public class PencilToolHandler
    {
        private PianoRollViewModel? _pianoRollViewModel;

        public void SetPianoRollViewModel(PianoRollViewModel viewModel)
        {
            _pianoRollViewModel = viewModel;
        }

        public void HandlePress(Point position, NoteViewModel? clickedNote, KeyModifiers modifiers)
        {
            if (_pianoRollViewModel == null) return;

            if (clickedNote == null)
            {
                // ��ʼ��ק����������
                Debug.WriteLine("Ǧ�ʹ���: ��ʼ��ק����������");
                _pianoRollViewModel.CreationModule.StartCreating(position);
            }
            else
            {
                // ����Ƿ�����������Ե�Ե�������
                var resizeHandle = _pianoRollViewModel.ResizeModule.GetResizeHandleAtPosition(position, clickedNote);

                if (resizeHandle != ResizeHandle.None)
                {
                    // ��ʼ�������ȵ���
                    Debug.WriteLine($"Ǧ�ʹ���: ��ʼ������������ - {resizeHandle}");

                    // ������ѡ������С
                    if (modifiers.HasFlag(KeyModifiers.Control))
                    {
                        clickedNote.IsSelected = !clickedNote.IsSelected;
                    }
                    else
                    {
                        if (!clickedNote.IsSelected)
                        {
                            _pianoRollViewModel.SelectionModule.ClearSelection(_pianoRollViewModel.Notes);
                            clickedNote.IsSelected = true;
                        }
                    }

                    _pianoRollViewModel.ResizeModule.StartResize(position, clickedNote, resizeHandle);
                }
                else
                {
                    // ѡ�񲢿�ʼ��ק��������
                    Debug.WriteLine("Ǧ�ʹ���: ��ʼ��ק��������");
                    
                    // ������ѡ�߼�
                    if (modifiers.HasFlag(KeyModifiers.Control))
                    {
                        clickedNote.IsSelected = !clickedNote.IsSelected;
                    }
                    else
                    {
                        bool wasAlreadySelected = clickedNote.IsSelected;
                        bool hasMultipleSelected = _pianoRollViewModel.Notes.Count(n => n.IsSelected) > 1;
                        
                        if (!wasAlreadySelected || !hasMultipleSelected)
                        {
                            _pianoRollViewModel.SelectionModule.ClearSelection(_pianoRollViewModel.Notes);
                            clickedNote.IsSelected = true;
                        }
                    }
                    
                    if (clickedNote.IsSelected)
                    {
                        _pianoRollViewModel.DragModule.StartDrag(clickedNote, position);
                    }
                }
            }
        }
    }
}