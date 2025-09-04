using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DominoNext.ViewModels.Editor;
using DominoNext.ViewModels.Editor.Models;

namespace DominoNext.ViewModels.Editor.Components
{
    /// <summary>
    /// 钢琴卷帘命令组件 - 负责所有的用户交互命令
    /// 符合单一职责原则，专注于命令处理逻辑
    /// </summary>
    public partial class PianoRollCommands : ObservableObject
    {
        #region 依赖
        private readonly PianoRollConfiguration _configuration;
        private readonly PianoRollViewport _viewport;
        #endregion

        #region 事件
        public event Action? SelectAllRequested;
        public event Action? ConfigurationChanged;
        public event Action? ViewportChanged;
        #endregion

        #region 构造函数
        public PianoRollCommands(PianoRollConfiguration configuration, PianoRollViewport viewport)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        }
        #endregion

        #region 工具选择命令
        [RelayCommand]
        private void SelectPencilTool()
        {
            _configuration.CurrentTool = EditorTool.Pencil;
            OnConfigurationChanged();
        }

        [RelayCommand]
        private void SelectSelectionTool()
        {
            _configuration.CurrentTool = EditorTool.Select;
            OnConfigurationChanged();
        }

        [RelayCommand]
        private void SelectEraserTool()
        {
            _configuration.CurrentTool = EditorTool.Eraser;
            OnConfigurationChanged();
        }

        [RelayCommand]
        private void SelectCutTool()
        {
            _configuration.CurrentTool = EditorTool.Cut;
            OnConfigurationChanged();
        }
        #endregion

        #region 量化和音符时值命令
        [RelayCommand]
        private void ToggleNoteDurationDropDown()
        {
            _configuration.IsNoteDurationDropDownOpen = !_configuration.IsNoteDurationDropDownOpen;
            OnConfigurationChanged();
        }

        [RelayCommand]
        private void SelectNoteDuration(NoteDurationOption option)
        {
            if (option == null) return;
            
            // 这里应该更改网格量化，而不是用户定义的音符时值
            _configuration.GridQuantization = option.Duration;
            _configuration.IsNoteDurationDropDownOpen = false;
            OnConfigurationChanged();
        }

        [RelayCommand]
        private void ApplyCustomFraction()
        {
            if (_configuration.TryParseCustomFraction(_configuration.CustomFractionInput, out var fraction))
            {
                _configuration.GridQuantization = fraction;
                _configuration.IsNoteDurationDropDownOpen = false;
                OnConfigurationChanged();
            }
        }
        #endregion

        #region 视图命令
        [RelayCommand]
        private void ToggleEventView()
        {
            _configuration.IsEventViewVisible = !_configuration.IsEventViewVisible;
            
            // 更新视口以适应新的布局
            _viewport.UpdateViewportForEventView(_configuration.IsEventViewVisible);
            
            OnConfigurationChanged();
            OnViewportChanged();
        }
        #endregion

        #region 选择命令
        [RelayCommand]
        private void SelectAll()
        {
            SelectAllRequested?.Invoke();
        }
        #endregion

        #region 缩放命令
        [RelayCommand]
        private void ZoomIn()
        {
            var newValue = Math.Min(100, _configuration.ZoomSliderValue + 10);
            _configuration.ZoomSliderValue = newValue;
            OnConfigurationChanged();
        }

        [RelayCommand]
        private void ZoomOut()
        {
            var newValue = Math.Max(0, _configuration.ZoomSliderValue - 10);
            _configuration.ZoomSliderValue = newValue;
            OnConfigurationChanged();
        }

        [RelayCommand]
        private void ResetZoom()
        {
            _configuration.ZoomSliderValue = 50.0; // 重置为1.0倍缩放
            OnConfigurationChanged();
        }

        [RelayCommand]
        private void VerticalZoomIn()
        {
            var newValue = Math.Min(100, _configuration.VerticalZoomSliderValue + 10);
            _configuration.VerticalZoomSliderValue = newValue;
            OnConfigurationChanged();
        }

        [RelayCommand]
        private void VerticalZoomOut()
        {
            var newValue = Math.Max(0, _configuration.VerticalZoomSliderValue - 10);
            _configuration.VerticalZoomSliderValue = newValue;
            OnConfigurationChanged();
        }

        [RelayCommand]
        private void ResetVerticalZoom()
        {
            _configuration.VerticalZoomSliderValue = 50.0; // 重置为1.0倍缩放
            OnConfigurationChanged();
        }
        #endregion

        #region 滚动命令
        [RelayCommand]
        private void ScrollToStart()
        {
            _viewport.SetHorizontalScrollOffset(0);
            OnViewportChanged();
        }

        [RelayCommand]
        private void ScrollToEnd()
        {
            _viewport.SetHorizontalScrollOffset(_viewport.MaxScrollExtent);
            OnViewportChanged();
        }

        [RelayCommand]
        private void ScrollLeft()
        {
            var newOffset = _viewport.CurrentScrollOffset - _viewport.ViewportWidth * 0.1;
            _viewport.SetHorizontalScrollOffset(newOffset);
            OnViewportChanged();
        }

        [RelayCommand]
        private void ScrollRight()
        {
            var newOffset = _viewport.CurrentScrollOffset + _viewport.ViewportWidth * 0.1;
            _viewport.SetHorizontalScrollOffset(newOffset);
            OnViewportChanged();
        }

        [RelayCommand]
        private void ScrollUp()
        {
            var newOffset = _viewport.VerticalScrollOffset - _viewport.VerticalViewportSize * 0.1;
            _viewport.SetVerticalScrollOffset(newOffset, 128 * 12.0); // 假设总高度
            OnViewportChanged();
        }

        [RelayCommand]
        private void ScrollDown()
        {
            var newOffset = _viewport.VerticalScrollOffset + _viewport.VerticalViewportSize * 0.1;
            _viewport.SetVerticalScrollOffset(newOffset, 128 * 12.0); // 假设总高度
            OnViewportChanged();
        }
        #endregion

        #region 事件触发方法
        private void OnConfigurationChanged()
        {
            ConfigurationChanged?.Invoke();
        }

        private void OnViewportChanged()
        {
            ViewportChanged?.Invoke();
        }
        #endregion

        #region 状态查询方法
        /// <summary>
        /// 检查当前是否为指定工具
        /// </summary>
        public bool IsCurrentTool(EditorTool tool)
        {
            return _configuration.CurrentTool == tool;
        }

        /// <summary>
        /// 获取当前工具的显示名称
        /// </summary>
        public string GetCurrentToolDisplayName()
        {
            return _configuration.CurrentTool switch
            {
                EditorTool.Pencil => "铅笔工具",
                EditorTool.Select => "选择工具",
                EditorTool.Eraser => "橡皮擦工具",
                EditorTool.Cut => "切割工具",
                _ => "未知工具"
            };
        }
        #endregion
    }
}