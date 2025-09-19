using Avalonia;
using Avalonia.Input;
using Lumino.Models.Music;

namespace Lumino.ViewModels.Editor.Commands.Handlers
{
    /// <summary>
    /// ���ȱ༭���ߴ�����
    /// </summary>
    public class VelocityToolHandler
    {
        private PianoRollViewModel? _pianoRollViewModel;

        public void SetPianoRollViewModel(PianoRollViewModel viewModel)
        {
            _pianoRollViewModel = viewModel;
        }

        public void HandlePress(Point position, NoteViewModel? clickedNote, KeyModifiers modifiers)
        {
            if (_pianoRollViewModel?.VelocityEditingModule == null) return;

            // ��ʼ���ȱ༭
            _pianoRollViewModel.VelocityEditingModule.StartEditing(position);
        }

        public void HandleMove(Point position)
        {
            if (_pianoRollViewModel?.VelocityEditingModule == null) return;

            // �������ȱ༭
            _pianoRollViewModel.VelocityEditingModule.UpdateEditing(position);
        }

        public void HandleRelease(Point position)
        {
            if (_pianoRollViewModel?.VelocityEditingModule == null) return;

            // �������ȱ༭
            _pianoRollViewModel.VelocityEditingModule.EndEditing();
        }
    }
}