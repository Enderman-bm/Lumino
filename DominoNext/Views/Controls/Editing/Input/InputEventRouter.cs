using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Lumino.ViewModels.Editor;
using Lumino.Views.Controls.Editing;

namespace Lumino.Views.Controls.Editing.Input
{
    /// <summary>
    /// �����¼�·������InputEventRouter��
    /// ��������Ŀ����Ϊ���پ����ȱ༭�ؼ��������¼�����·������������ͼ�����ꡢ���̵������¼�ת���� MVVM �� ViewModel �㡣
    /// ͨ������ģʽ�����û��������ҵ���߼���������ά���Ժ���չ�ԡ�
    /// �� MVVM �ܹ��У�InputEventRouter �䵱 View �� ViewModel ֮���������ʹ���봦������ģ�黯��
    /// </summary>
    public class InputEventRouter
    {
        /// <summary>
        /// ������갴���¼�����ָ��λ�á����ߵ���Ϣ��װ��ͨ������ݵ� ViewModel��
        /// �� MVVM ��������Ӧ�û��ĵ�������������༭���
        /// </summary>
        public void HandlePointerPressed(PointerPressedEventArgs e, PianoRollViewModel? viewModel, Control control)
        {
            Debug.WriteLine("=== OnPointerPressed (MVVM) ===");

            if (viewModel?.EditorCommands == null)
            {
                Debug.WriteLine("����: ViewModel��EditorCommandsΪ��");
                return;
            }

            var position = e.GetPosition(control);
            var properties = e.GetCurrentPoint(control).Properties;

            Debug.WriteLine($"ָ��λ��: {position}, ����: {viewModel.CurrentTool}");

            if (properties.IsLeftButtonPressed)
            {
                var commandParameter = new EditorInteractionArgs
                {
                    Position = position,
                    Tool = (EditorTool)viewModel.CurrentTool,
                    Modifiers = e.KeyModifiers,
                    InteractionType = EditorInteractionType.Press
                };

                if (viewModel.EditorCommands.HandleInteractionCommand.CanExecute(commandParameter))
                {
                    viewModel.EditorCommands.HandleInteractionCommand.Execute(commandParameter);

                    control.Focus();
                    e.Pointer.Capture(control);
                    e.Handled = true;
                }
            }

            Debug.WriteLine("=== OnPointerPressed End ===");
        }

        /// <summary>
        /// ��������ƶ��¼�������ǰָ��λ�õ���Ϣͨ������ݵ� ViewModel��
        /// �� MVVM ��������Ӧ��ק���ƶ��Ƚ���������ʵʱ�༭�߼���
        /// </summary>
        public void HandlePointerMoved(PointerEventArgs e, PianoRollViewModel? viewModel)
        {
            if (viewModel?.EditorCommands == null) return;

            var position = e.GetPosition((Visual)e.Source!);

            var commandParameter = new EditorInteractionArgs
            {
                Position = position,
                Tool = (EditorTool)viewModel.CurrentTool,
                InteractionType = EditorInteractionType.Move
            };

            if (viewModel.EditorCommands.HandleInteractionCommand.CanExecute(commandParameter))
            {
                viewModel.EditorCommands.HandleInteractionCommand.Execute(commandParameter);
            }
        }

        /// <summary>
        /// ��������ͷ��¼�����ָ��λ�õ���Ϣͨ������ݵ� ViewModel��
        /// �� MVVM �����ڽ�����ק�������Ƚ�����������������߼���
        /// </summary>
        public void HandlePointerReleased(PointerReleasedEventArgs e, PianoRollViewModel? viewModel)
        {
            if (viewModel?.EditorCommands == null) return;

            var position = e.GetPosition((Visual)e.Source!);

            var commandParameter = new EditorInteractionArgs
            {
                Position = position,
                Tool = (EditorTool)viewModel.CurrentTool,
                InteractionType = EditorInteractionType.Release
            };

            if (viewModel.EditorCommands.HandleInteractionCommand.CanExecute(commandParameter))
            {
                viewModel.EditorCommands.HandleInteractionCommand.Execute(commandParameter);
            }

            e.Pointer.Capture(null);
            e.Handled = true;
        }

        /// <summary>
        /// �������̰����¼��������������μ���Ϣͨ������ݵ� ViewModel��
        /// �� MVVM ��������Ӧ��ݼ��������л��Ȳ��������������߼���
        /// </summary>
        public void HandleKeyDown(KeyEventArgs e, PianoRollViewModel? viewModel)
        {
            if (viewModel?.EditorCommands == null) return;

            var keyCommandParameter = new KeyCommandArgs
            {
                Key = e.Key,
                Modifiers = e.KeyModifiers
            };

            if (viewModel.EditorCommands.HandleKeyCommand.CanExecute(keyCommandParameter))
            {
                viewModel.EditorCommands.HandleKeyCommand.Execute(keyCommandParameter);
                e.Handled = true;
            }
        }
    }
}