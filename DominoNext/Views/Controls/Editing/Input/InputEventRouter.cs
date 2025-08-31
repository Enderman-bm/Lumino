using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using DominoNext.ViewModels.Editor;
using DominoNext.Views.Controls.Editing;

namespace DominoNext.Views.Controls.Editing.Input
{
    /// <summary>
    /// �����¼�·���� - �򻯰汾
    /// </summary>
    public class InputEventRouter
    {
        public void HandlePointerPressed(PointerPressedEventArgs e, PianoRollViewModel? viewModel, Control control)
        {
            Debug.WriteLine("=== InputEventRouter.HandlePointerPressed ===");
            Debug.WriteLine($"ViewModel exists: {viewModel != null}");
            Debug.WriteLine($"EditorCommands exists: {viewModel?.EditorCommands != null}");
            Debug.WriteLine($"CurrentTool: {viewModel?.CurrentTool}");
            Debug.WriteLine($"Pressed button: {e.GetCurrentPoint(control).Properties.PointerUpdateKind}");

            if (viewModel?.EditorCommands == null)
            {
                Debug.WriteLine("ERROR: ViewModel or EditorCommands is null");
                return;
            }

            var position = e.GetPosition(control);
            var properties = e.GetCurrentPoint(control).Properties;

            Debug.WriteLine($"Pointer position: {position}, Tool: {viewModel.CurrentTool}");

            if (properties.IsLeftButtonPressed)
            {
                var commandParameter = new EditorInteractionArgs
            {
                Position = position,
                Tool = viewModel.CurrentTool,
                Modifiers = e.KeyModifiers,
                InteractionType = EditorInteractionType.Press,
                MouseButtons = MouseButtons.Left
            };

                if (viewModel.EditorCommands.HandleInteractionCommand.CanExecute(commandParameter))
                {
                    viewModel.EditorCommands.HandleInteractionCommand.Execute(commandParameter);
                    Debug.WriteLine("HandleInteractionCommand executed successfully");
                    
                    control.Focus();
                    e.Pointer.Capture(control);
                    e.Handled = true;
                }
                else
                {
                    Debug.WriteLine("ERROR: HandleInteractionCommand cannot execute");
                }
            }
            else
            {
                Debug.WriteLine($"Ignoring non-left button press: {properties.PointerUpdateKind}");
            }

            Debug.WriteLine("=== InputEventRouter.HandlePointerPressed End ===");
        }

        public void HandlePointerMoved(PointerEventArgs e, PianoRollViewModel? viewModel)
        {
            if (viewModel?.EditorCommands == null) return;

            var position = e.GetPosition((Visual)e.Source!);
            var properties = e.GetCurrentPoint((Visual)e.Source!).Properties;

            var commandParameter = new EditorInteractionArgs
            {
                Position = position,
                Tool = viewModel.CurrentTool,
                InteractionType = EditorInteractionType.Move,
                MouseButtons = properties.IsLeftButtonPressed ? MouseButtons.Left : MouseButtons.None
            };

            if (viewModel.EditorCommands.HandleInteractionCommand.CanExecute(commandParameter))
            {
                viewModel.EditorCommands.HandleInteractionCommand.Execute(commandParameter);
            }
        }

        public void HandlePointerReleased(PointerReleasedEventArgs e, PianoRollViewModel? viewModel)
        {
            if (viewModel?.EditorCommands == null) return;

            var position = e.GetPosition((Visual)e.Source!);

            var commandParameter = new EditorInteractionArgs
            {
                Position = position,
                Tool = viewModel.CurrentTool,
                InteractionType = EditorInteractionType.Release,
                MouseButtons = MouseButtons.None
            };

            if (viewModel.EditorCommands.HandleInteractionCommand.CanExecute(commandParameter))
            {
                viewModel.EditorCommands.HandleInteractionCommand.Execute(commandParameter);
            }

            e.Pointer.Capture(null);
            e.Handled = true;
        }

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

        // 移除旧的HandlePointerPressed方法，只保留新的版本
    }
}