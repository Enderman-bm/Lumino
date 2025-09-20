using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Components;
using Lumino.ViewModels.Editor.Enums;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel的键盘和鼠标交互功能
    /// </summary>
    public partial class PianoRollViewModel
    {
        #region 键盘快捷键命令
        /// <summary>
        /// 处理键盘按键事件
        /// </summary>
        public void HandleKeyDown(KeyEventArgs e)
        {
            if (e == null) return;

            var key = e.Key;
            var modifiers = e.KeyModifiers;

            // 处理修饰键组合
            if (modifiers.HasFlag(KeyModifiers.Control))
            {
                HandleControlKeyShortcuts(key);
                return;
            }

            if (modifiers.HasFlag(KeyModifiers.Alt))
            {
                HandleAltKeyShortcuts(key);
                return;
            }

            if (modifiers.HasFlag(KeyModifiers.Shift))
            {
                HandleShiftKeyShortcuts(key);
                return;
            }

            // 处理普通按键
            HandleRegularKeyShortcuts(key);
        }

        /// <summary>
        /// 处理键盘释放事件
        /// </summary>
        public void HandleKeyUp(KeyEventArgs e)
        {
            if (e == null) return;

            var key = e.Key;

            // 处理按键释放
            switch (key)
            {
                case Key.Space:
                    // 空格键释放时停止播放（如果正在播放）
                    if (IsPlaying)
                    {
                        Pause();
                    }
                    break;
            }
        }

        /// <summary>
        /// 处理Ctrl+快捷键
        /// </summary>
        private void HandleControlKeyShortcuts(Key key)
        {
            switch (key)
            {
                case Key.Z:
                    // Ctrl+Z: 撤销
                    Undo();
                    break;

                case Key.Y:
                    // Ctrl+Y: 重做
                    Redo();
                    break;

                case Key.A:
                    // Ctrl+A: 全选
                    SelectAll();
                    break;

                case Key.C:
                    // Ctrl+C: 复制
                    CopySelectedNotes();
                    break;

                case Key.X:
                    // Ctrl+X: 剪切
                    CutSelectedNotes();
                    break;

                case Key.V:
                    // Ctrl+V: 粘贴
                    PasteNotes();
                    break;

                case Key.D:
                    // Ctrl+D: 复制
                    _noteEditingService.DuplicateSelectedNotes();
                    break;

                case Key.S:
                    // Ctrl+S: 保存
                    SaveProject();
                    break;

                case Key.O:
                    // Ctrl+O: 打开
                    OpenProject();
                    break;

                case Key.N:
                    // Ctrl+N: 新建
                    CreateNewProject();
                    break;

                case Key.OemPlus:
                case Key.Add:
                    // Ctrl++: 放大
                    ZoomIn();
                    break;

                case Key.OemMinus:
                case Key.Subtract:
                    // Ctrl+-: 缩小
                    ZoomOut();
                    break;

                case Key.D0:
                case Key.NumPad0:
                    // Ctrl+0: 重置缩放
                    ResetZoom();
                    break;
            }
        }

        /// <summary>
        /// 处理Alt+快捷键
        /// </summary>
        private void HandleAltKeyShortcuts(Key key)
        {
            switch (key)
            {
                case Key.Left:
                    // Alt+左箭头: 向左滚动
                    ScrollLeft();
                    break;

                case Key.Right:
                    // Alt+右箭头: 向右滚动
                    ScrollRight();
                    break;

                case Key.Up:
                    // Alt+上箭头: 向上滚动
                    ScrollUp();
                    break;

                case Key.Down:
                    // Alt+下箭头: 向下滚动
                    ScrollDown();
                    break;
            }
        }

        /// <summary>
        /// 处理Shift+快捷键
        /// </summary>
        private void HandleShiftKeyShortcuts(Key key)
        {
            switch (key)
            {
                case Key.Left:
                    // Shift+左箭头: 向左移动选定音符
                    MoveSelectedNotes(-0.25, 0);
                    break;

                case Key.Right:
                    // Shift+右箭头: 向右移动选定音符
                    MoveSelectedNotes(0.25, 0);
                    break;

                case Key.Up:
                    // Shift+上箭头: 向上移动选定音符
                    MoveSelectedNotes(0, 1);
                    break;

                case Key.Down:
                    // Shift+下箭头: 向下移动选定音符
                    MoveSelectedNotes(0, -1);
                    break;

                case Key.Delete:
                    // Shift+Delete: 永久删除选定音符
                    DeleteSelectedNotes();
                    break;
            }
        }

        /// <summary>
        /// 处理普通按键快捷键
        /// </summary>
        private void HandleRegularKeyShortcuts(Key key)
        {
            switch (key)
            {
                case Key.Delete:
                    // Delete: 删除选定音符
                    DeleteSelectedNotes();
                    break;

                case Key.Escape:
                    // Escape: 取消选择
                    DeselectAllNotes();
                    break;

                case Key.Space:
                    // 空格键: 播放/暂停
                    if (IsPlaying)
                    {
                        Pause();
                    }
                    else
                    {
                        Play();
                    }
                    break;

                case Key.Left:
                    // 左箭头: 向左滚动
                    ScrollLeft();
                    break;

                case Key.Right:
                    // 右箭头: 向右滚动
                    ScrollRight();
                    break;

                case Key.Up:
                    // 上箭头: 向上滚动
                    ScrollUp();
                    break;

                case Key.Down:
                    // 下箭头: 向下滚动
                    ScrollDown();
                    break;

                case Key.PageUp:
                    // PageUp: 向上翻页
                    ScrollPageUp();
                    break;

                case Key.PageDown:
                    // PageDown: 向下翻页
                    ScrollPageDown();
                    break;

                case Key.Home:
                    // Home: 跳转到开始
                    JumpToStart();
                    break;

                case Key.End:
                    // End: 跳转到结束
                    JumpToEnd();
                    break;

                case Key.D1:
                case Key.NumPad1:
                    // 1: 选择工具
                    SetCurrentTool(EditorTool.Select);
                    break;

                case Key.D2:
                case Key.NumPad2:
                    // 2: 画笔工具
                    SetCurrentTool(EditorTool.Pencil);
                    break;

                case Key.D3:
                case Key.NumPad3:
                    // 3: 橡皮擦工具
                    SetCurrentTool(EditorTool.Eraser);
                    break;

                case Key.D4:
                case Key.NumPad4:
                    // 4: 力度编辑工具
                    SetCurrentTool(EditorTool.Cut);
                    break;
            }
        }
        #endregion

        #region 鼠标交互命令
        /// <summary>
        /// 处理鼠标按下事件
        /// </summary>
        public void HandleMouseDown(PointerEventArgs e, Point position)
        {
            if (e == null) return;

            var mouseButton = e.GetCurrentPoint(null).Properties.PointerUpdateKind;
            var modifiers = e.KeyModifiers;

            // 转换坐标到时间和音高
            var time = PixelToTime(position.X);
            var pitch = PixelToPitch(position.Y);

            // 处理鼠标按键
            switch (mouseButton)
            {
                case PointerUpdateKind.LeftButtonPressed:
                    HandleLeftMouseDown(position, time, pitch, modifiers);
                    break;

                case PointerUpdateKind.MiddleButtonPressed:
                    HandleMiddleMouseDown(position, time, pitch, modifiers);
                    break;

                case PointerUpdateKind.RightButtonPressed:
                    HandleRightMouseDown(position, time, pitch, modifiers);
                    break;
            }
        }

        /// <summary>
        /// 处理鼠标移动事件
        /// </summary>
        public void HandleMouseMove(PointerEventArgs e, Point position)
        {
            if (e == null) return;

            var mouseButton = e.GetCurrentPoint(null).Properties.PointerUpdateKind;
            var modifiers = e.KeyModifiers;

            // 转换坐标到时间和音高
            var time = PixelToTime(position.X);
            var pitch = PixelToPitch(position.Y);

            // 更新鼠标位置
            MousePosition = position;
            MouseTime = time;
            MousePitch = pitch;

            // 处理鼠标移动
            HandleMouseMove(position, time, pitch, modifiers);
        }

        /// <summary>
        /// 处理鼠标释放事件
        /// </summary>
        public void HandleMouseUp(PointerEventArgs e, Point position)
        {
            if (e == null) return;

            var mouseButton = e.GetCurrentPoint(null).Properties.PointerUpdateKind;
            var modifiers = e.KeyModifiers;

            // 转换坐标到时间和音高
            var time = PixelToTime(position.X);
            var pitch = PixelToPitch(position.Y);

            // 处理鼠标释放
            switch (mouseButton)
            {
                case PointerUpdateKind.LeftButtonReleased:
                    HandleLeftMouseUp(position, time, pitch, modifiers);
                    break;

                case PointerUpdateKind.MiddleButtonReleased:
                    HandleMiddleMouseUp(position, time, pitch, modifiers);
                    break;

                case PointerUpdateKind.RightButtonReleased:
                    HandleRightMouseUp(position, time, pitch, modifiers);
                    break;
            }
        }

        /// <summary>
        /// 处理鼠标滚轮事件
        /// </summary>
        public void HandleMouseWheel(PointerWheelEventArgs e, Point position)
        {
            if (e == null) return;

            var delta = e.Delta;
            var modifiers = e.KeyModifiers;

            // 处理垂直滚轮
            if (delta.Y != 0)
            {
                if (modifiers.HasFlag(KeyModifiers.Control))
                {
                    // Ctrl+滚轮: 缩放
                    if (delta.Y > 0)
                    {
                        ZoomIn();
                    }
                    else
                    {
                        ZoomOut();
                    }
                }
                else if (modifiers.HasFlag(KeyModifiers.Shift))
                {
                    // Shift+滚轮: 水平滚动
                    if (delta.Y > 0)
                    {
                        ScrollLeft();
                    }
                    else
                    {
                        ScrollRight();
                    }
                }
                else
                {
                    // 普通滚轮: 垂直滚动
                    if (delta.Y > 0)
                    {
                        ScrollUp();
                    }
                    else
                    {
                        ScrollDown();
                    }
                }
            }

            // 处理水平滚轮
            if (delta.X != 0)
            {
                if (delta.X > 0)
                {
                    ScrollRight();
                }
                else
                {
                    ScrollLeft();
                }
            }
        }

        /// <summary>
        /// 处理左键按下
        /// </summary>
        private void HandleLeftMouseDown(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            switch (CurrentTool)
            {
                case EditorTool.Select:
                    HandleSelectToolMouseDown(position, time, pitch, modifiers);
                    break;

                case EditorTool.Pencil:
                    HandlePenToolMouseDown(position, time, pitch, modifiers);
                    break;

                case EditorTool.Eraser:
                    HandleEraserToolMouseDown(position, time, pitch, modifiers);
                    break;

                case EditorTool.Cut:
                    HandleVelocityToolMouseDown(position, time, pitch, modifiers);
                    break;
            }
        }

        /// <summary>
        /// 处理中键按下
        /// </summary>
        private void HandleMiddleMouseDown(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            // 中键: 开始拖拽视图
            IsPanning = true;
            PanStartPosition = position;
            PanStartViewport = new Point(Viewport.CurrentScrollOffset, Viewport.VerticalScrollOffset);
        }

        /// <summary>
        /// 处理右键按下
        /// </summary>
        private void HandleRightMouseDown(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            // 右键: 显示上下文菜单
            ShowContextMenu(position, time, pitch);
        }

        /// <summary>
        /// 处理鼠标移动
        /// </summary>
        private void HandleMouseMove(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            if (IsPanning)
            {
                // 处理视图拖拽
                var deltaX = position.X - PanStartPosition.X;
                var deltaY = position.Y - PanStartPosition.Y;

                Viewport.CurrentScrollOffset = PanStartViewport.X - deltaX;
                Viewport.VerticalScrollOffset = PanStartViewport.Y - deltaY;

                ClampViewport();
                return;
            }

            switch (CurrentTool)
            {
                case EditorTool.Select:
                    HandleSelectToolMouseMove(position, time, pitch, modifiers);
                    break;

                case EditorTool.Pencil:
                    HandlePenToolMouseMove(position, time, pitch, modifiers);
                    break;

                case EditorTool.Eraser:
                    HandleEraserToolMouseMove(position, time, pitch, modifiers);
                    break;

                case EditorTool.Cut:
                    HandleVelocityToolMouseMove(position, time, pitch, modifiers);
                    break;
            }
        }

        /// <summary>
        /// 处理左键释放
        /// </summary>
        private void HandleLeftMouseUp(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            switch (CurrentTool)
            {
                case EditorTool.Select:
                    HandleSelectToolMouseUp(position, time, pitch, modifiers);
                    break;

                case EditorTool.Pencil:
                    HandlePenToolMouseUp(position, time, pitch, modifiers);
                    break;

                case EditorTool.Eraser:
                    HandleEraserToolMouseUp(position, time, pitch, modifiers);
                    break;

                case EditorTool.Cut:
                    HandleVelocityToolMouseUp(position, time, pitch, modifiers);
                    break;
            }
        }

        /// <summary>
        /// 处理中键释放
        /// </summary>
        private void HandleMiddleMouseUp(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            // 停止视图拖拽
            IsPanning = false;
        }

        /// <summary>
        /// 处理右键释放
        /// </summary>
        private void HandleRightMouseUp(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            // 隐藏上下文菜单
            HideContextMenu();
        }
        #endregion

        #region 鼠标交互属性
        /// <summary>
        /// 鼠标位置
        /// </summary>
        public Point MousePosition
        {
            get => _mousePosition;
            private set
            {
                if (SetProperty(ref _mousePosition, value))
                {
                    OnPropertyChanged(nameof(MouseTimeText));
                    OnPropertyChanged(nameof(MousePitchText));
                }
            }
        }
        private Point _mousePosition;

        /// <summary>
        /// 鼠标时间位置
        /// </summary>
        public double MouseTime
        {
            get => _mouseTime;
            private set
            {
                if (SetProperty(ref _mouseTime, value))
                {
                    OnPropertyChanged(nameof(MouseTimeText));
                }
            }
        }
        private double _mouseTime;

        /// <summary>
        /// 鼠标音高位置
        /// </summary>
        public int MousePitch
        {
            get => _mousePitch;
            private set
            {
                if (SetProperty(ref _mousePitch, value))
                {
                    OnPropertyChanged(nameof(MousePitchText));
                }
            }
        }
        private int _mousePitch;

        /// <summary>
        /// 是否正在拖拽视图
        /// </summary>
        public bool IsPanning
        {
            get => _isPanning;
            private set
            {
                if (SetProperty(ref _isPanning, value))
                {
                    OnPropertyChanged(nameof(Cursor));
                }
            }
        }
        private bool _isPanning;

        /// <summary>
        /// 拖拽开始位置
        /// </summary>
        private Point PanStartPosition { get; set; }

        /// <summary>
        /// 拖拽开始时的视口位置
        /// </summary>
        private Point PanStartViewport { get; set; }

        /// <summary>
        /// 鼠标时间文本
        /// </summary>
        public string MouseTimeText => $"{MouseTime:F2}s";


        /// <summary>
        /// 鼠标音高文本
        /// </summary>
        public string MousePitchText => GetPitchName(MousePitch);

        /// <summary>
        /// 当前光标形状
        /// </summary>
        public string Cursor => IsPanning ? "Hand" : GetToolCursor(CurrentTool);

        /// <summary>
        /// 获取工具对应的光标形状
        /// </summary>
        private string GetToolCursor(EditorTool tool)
        {
            switch (tool)
            {
                case EditorTool.Select:
                    return "Arrow";
                case EditorTool.Pencil:
                    return "Pen";
                case EditorTool.Eraser:
                    return "Cross";
                case EditorTool.Cut:
                    return "IBeam";
                default:
                    return "Arrow";
            }
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 向左滚动
        /// </summary>
        private void ScrollLeft()
        {
            ScrollToTime(ViewportLeftTime - 1.0);
        }

        /// <summary>
        /// 向右滚动
        /// </summary>
        private void ScrollRight()
        {
            ScrollToTime(ViewportLeftTime + 1.0);
        }

        /// <summary>
        /// 向上滚动
        /// </summary>
        private void ScrollUp()
        {
            ScrollToPitch((int)ViewportTopPitch + 1);
        }

        /// <summary>
        /// 向下滚动
        /// </summary>
        private void ScrollDown()
        {
            ScrollToPitch((int)ViewportTopPitch - 1);
        }

        /// <summary>
        /// 向上翻页
        /// </summary>
        private void ScrollPageUp()
        {
            var pageSize = (int)(VisiblePitchRange * 0.8);
            ScrollToPitch((int)ViewportTopPitch + pageSize);
        }

        /// <summary>
        /// 向下翻页
        /// </summary>
        private void ScrollPageDown()
        {
            var pageSize = (int)(VisiblePitchRange * 0.8);
            ScrollToPitch((int)ViewportTopPitch - pageSize);
        }

        /// <summary>
        /// 显示上下文菜单
        /// </summary>
        private void ShowContextMenu(Point position, double time, int pitch)
        {
            // TODO: 实现上下文菜单显示逻辑
            // 这里应该显示右键菜单，包含音符相关的操作选项
        }

        /// <summary>
        /// 隐藏上下文菜单
        /// </summary>
        private void HideContextMenu()
        {
            // TODO: 实现上下文菜单隐藏逻辑
        }

        /// <summary>
        /// 处理选择工具的鼠标事件
        /// </summary>
        private void HandleSelectToolMouseDown(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            // TODO: 实现选择工具的鼠标按下逻辑
        }

        private void HandleSelectToolMouseMove(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            // TODO: 实现选择工具的鼠标移动逻辑
        }

        private void HandleSelectToolMouseUp(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            // TODO: 实现选择工具的鼠标释放逻辑
        }

        /// <summary>
        /// 处理画笔工具的鼠标事件
        /// </summary>
        private void HandlePenToolMouseDown(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            // TODO: 实现画笔工具的鼠标按下逻辑
        }

        private void HandlePenToolMouseMove(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            // TODO: 实现画笔工具的鼠标移动逻辑
        }

        private void HandlePenToolMouseUp(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            // TODO: 实现画笔工具的鼠标释放逻辑
        }

        /// <summary>
        /// 处理橡皮擦工具的鼠标事件
        /// </summary>
        private void HandleEraserToolMouseDown(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            // TODO: 实现橡皮擦工具的鼠标按下逻辑
        }

        private void HandleEraserToolMouseMove(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            // TODO: 实现橡皮擦工具的鼠标移动逻辑
        }

        private void HandleEraserToolMouseUp(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            // TODO: 实现橡皮擦工具的鼠标释放逻辑
        }

        /// <summary>
        /// 处理力度编辑工具的鼠标事件
        /// </summary>
        private void HandleVelocityToolMouseDown(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            // TODO: 实现力度编辑工具的鼠标按下逻辑
        }

        private void HandleVelocityToolMouseMove(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            // TODO: 实现力度编辑工具的鼠标移动逻辑
        }

        private void HandleVelocityToolMouseUp(Point position, double time, int pitch, KeyModifiers modifiers)
        {
            // TODO: 实现力度编辑工具的鼠标释放逻辑
        }

        /// <summary>
        /// 获取音高名称
        /// </summary>
        private string GetPitchName(int pitch)
        {
            if (pitch < 0 || pitch > 127) return "Invalid";

            var noteNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            var octave = pitch / 12 - 1;
            var note = pitch % 12;

            return $"{noteNames[note]}{octave}";
        }
        #endregion
    }
}