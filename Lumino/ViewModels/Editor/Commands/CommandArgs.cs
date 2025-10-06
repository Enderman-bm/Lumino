using System;
using Avalonia;
using Avalonia.Input;
using Lumino.ViewModels.Editor;

namespace Lumino.ViewModels.Editor.Commands
{
    /// <summary>
    /// 编辑器交互类型枚举
    /// </summary>
    public enum EditorInteractionType
    {
        Press,
        Move,
        Release
    }

    /// <summary>
    /// 编辑器交互参数基类
    /// </summary>
    public class EditorInteractionArgs : EventArgs
    {
        public bool Handled { get; set; }
        public DateTime Timestamp { get; } = DateTime.Now;
        public Point Position { get; set; }
        public EditorTool Tool { get; set; }
        public KeyModifiers Modifiers { get; set; }
        public EditorInteractionType InteractionType { get; set; }
    }

    /// <summary>
    /// 键盘命令参数
    /// </summary>
    public class KeyCommandArgs : EditorInteractionArgs
    {
        public Key Key { get; set; }
        public bool IsKeyDown { get; set; }
        public string? Text { get; set; }
        public bool IsRepeat { get; set; }

        public KeyCommandArgs(Key key, KeyModifiers modifiers, bool isKeyDown, string? text = null, bool isRepeat = false)
        {
            Key = key;
            Modifiers = modifiers;
            IsKeyDown = isKeyDown;
            Text = text;
            IsRepeat = isRepeat;
        }

        public bool Matches(Key key, KeyModifiers modifiers)
        {
            return Key == key && Modifiers == modifiers;
        }

        public override string ToString()
        {
            var modifierText = Modifiers != KeyModifiers.None ? $"{Modifiers}+" : "";
            return $"{modifierText}{Key} ({(IsKeyDown ? "Down" : "Up")})";
        }
    }

    /// <summary>
    /// 鼠标命令参数
    /// </summary>
    public class MouseCommandArgs : EditorInteractionArgs
    {
        public double X { get; set; }
        public double Y { get; set; }
        public MouseButton Button { get; set; }
        public bool IsButtonDown { get; set; }
        public int ClickCount { get; set; }

        public MouseCommandArgs(double x, double y, MouseButton button, bool isButtonDown, KeyModifiers modifiers, int clickCount = 1)
        {
            X = x;
            Y = y;
            Button = button;
            IsButtonDown = isButtonDown;
            Modifiers = modifiers;
            ClickCount = clickCount;
        }
    }

    /// <summary>
    /// 滚轮命令参数
    /// </summary>
    public class WheelCommandArgs : EditorInteractionArgs
    {
        public double DeltaX { get; set; }
        public double DeltaY { get; set; }

        public WheelCommandArgs(double deltaX, double deltaY, KeyModifiers modifiers)
        {
            DeltaX = deltaX;
            DeltaY = deltaY;
            Modifiers = modifiers;
        }
    }
}