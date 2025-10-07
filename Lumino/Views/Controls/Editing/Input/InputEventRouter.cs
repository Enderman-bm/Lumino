using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Lumino.ViewModels.Editor;
using Lumino.Views.Controls.Editing;

namespace Lumino.Views.Controls.Editing.Input
{
    /// <summary>
    /// 输入事件路由器（InputEventRouter）
    /// 该类在项目中作为钢琴卷帘等编辑控件的输入事件集中路由器，负责将视图层的鼠标、键盘等输入事件转发到 MVVM 的 ViewModel 层。
    /// 通过命令模式，将用户交互解耦到业务逻辑，提升可维护性和扩展性。
    /// 在 MVVM 架构中，InputEventRouter 充当 View 与 ViewModel 之间的桥梁，使输入处理更加模块化。
    /// </summary>
    public class InputEventRouter
    {
        /// <summary>
        /// 处理鼠标按下事件，将指针位置、工具等信息封装后通过命令传递到 ViewModel。
        /// 在 MVVM 中用于响应用户的点击操作，驱动编辑命令。
        /// </summary>
        public void HandlePointerPressed(PointerPressedEventArgs e, PianoRollViewModel? viewModel, Control control)
        {
            Debug.WriteLine("=== OnPointerPressed (MVVM) ===");

            if (viewModel?.EditorCommands == null)
            {
                Debug.WriteLine("错误: ViewModel或EditorCommands为空");
                return;
            }

            var position = e.GetPosition(control);
            var properties = e.GetCurrentPoint(control).Properties;

            Debug.WriteLine($"指针位置: {position}, 工具: {viewModel.CurrentTool}");

            if (properties.IsLeftButtonPressed)
            {
                var commandParameter = new EditorInteractionArgs
                {
                    Position = position,
                    Tool = viewModel.CurrentTool,
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
        /// 处理鼠标移动事件，将当前指针位置等信息通过命令传递到 ViewModel。
        /// 在 MVVM 中用于响应拖拽、移动等交互，驱动实时编辑逻辑。
        /// </summary>
        public void HandlePointerMoved(PointerEventArgs e, PianoRollViewModel? viewModel)
        {
            if (viewModel?.EditorCommands == null) return;

            var position = e.GetPosition((Visual)e.Source!);

            var commandParameter = new EditorInteractionArgs
            {
                Position = position,
                Tool = viewModel.CurrentTool,
                InteractionType = EditorInteractionType.Move
            };

            if (viewModel.EditorCommands.HandleInteractionCommand.CanExecute(commandParameter))
            {
                viewModel.EditorCommands.HandleInteractionCommand.Execute(commandParameter);
            }
        }

        /// <summary>
        /// 处理鼠标释放事件，将指针位置等信息通过命令传递到 ViewModel。
        /// 在 MVVM 中用于结束拖拽、创建等交互，驱动命令完成逻辑。
        /// </summary>
        public void HandlePointerReleased(PointerReleasedEventArgs e, PianoRollViewModel? viewModel)
        {
            if (viewModel?.EditorCommands == null) return;

            var position = e.GetPosition((Visual)e.Source!);

            var commandParameter = new EditorInteractionArgs
            {
                Position = position,
                Tool = viewModel.CurrentTool,
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
        /// 处理键盘按下事件，将按键和修饰键信息通过命令传递到 ViewModel。
        /// 在 MVVM 中用于响应快捷键、工具切换等操作，驱动命令逻辑。
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