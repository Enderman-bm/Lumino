using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DominoNext.Models.Music;
using DominoNext.ViewModels.Editor.Enums;

namespace DominoNext.ViewModels.Editor.Components
{
    /// <summary>
    /// 工具栏ViewModel - 专门处理工具栏相关的状态和命令
    /// 遵循MVVM设计原则和单一职责原则
    /// </summary>
    public partial class ToolbarViewModel : ObservableObject
    {
        #region 私有字段
        private readonly PianoRollConfiguration _configuration;
        #endregion

        #region 事件
        /// <summary>
        /// 当需要切换事件视图时触发
        /// </summary>
        public event Action<bool>? EventViewToggleRequested;

        /// <summary>
        /// 当工具发生变化时触发
        /// </summary>
        public event Action<EditorTool>? ToolChanged;

        /// <summary>
        /// 当音符时值发生变化时触发
        /// </summary>
        public event Action<MusicalFraction>? NoteDurationChanged;

        /// <summary>
        /// 当网格量化发生变化时触发
        /// </summary>
        public event Action<MusicalFraction>? GridQuantizationChanged;
        #endregion

        #region 属性 - 委托给Configuration
        /// <summary>
        /// 当前选择的工具
        /// </summary>
        public EditorTool CurrentTool => _configuration.CurrentTool;

        /// <summary>
        /// 当前网格量化设置
        /// </summary>
        public MusicalFraction GridQuantization => _configuration.GridQuantization;

        /// <summary>
        /// 用户定义的音符时长
        /// </summary>
        public MusicalFraction UserDefinedNoteDuration => _configuration.UserDefinedNoteDuration;

        /// <summary>
        /// 是否显示事件视图
        /// </summary>
        public bool IsEventViewVisible => _configuration.IsEventViewVisible;

        /// <summary>
        /// 网格量化下拉框是否打开
        /// </summary>
        public bool IsNoteDurationDropDownOpen => _configuration.IsNoteDurationDropDownOpen;

        /// <summary>
        /// 自定义时值输入文本
        /// </summary>
        public string CustomFractionInput => _configuration.CustomFractionInput;

        /// <summary>
        /// 音符时值选项集合
        /// </summary>
        public ObservableCollection<NoteDurationOption> NoteDurationOptions => _configuration.NoteDurationOptions;

        /// <summary>
        /// 当前网格量化显示文本
        /// </summary>
        public string CurrentNoteDurationText => _configuration.CurrentNoteDurationText;

        /// <summary>
        /// 当前音符时值显示文本
        /// </summary>
        public string CurrentNoteTimeValueText => _configuration.CurrentNoteTimeValueText;
        #endregion

        #region 独立属性
        /// <summary>
        /// 当前Tempo值（BPM）
        /// </summary>
        [ObservableProperty]
        private int _currentTempo = 120;
        #endregion

        #region 构造函数
        /// <summary>
        /// 设计时构造函数
        /// </summary>
        public ToolbarViewModel() : this(new PianoRollConfiguration()) { }

        /// <summary>
        /// 运行时构造函数
        /// </summary>
        /// <param name="configuration">钢琴卷帘配置对象</param>
        public ToolbarViewModel(PianoRollConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            // 订阅配置变更事件
            _configuration.PropertyChanged += OnConfigurationPropertyChanged;
        }
        #endregion

        #region 事件处理
        /// <summary>
        /// 处理配置属性变更事件
        /// </summary>
        private void OnConfigurationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 将配置变更传播到工具栏ViewModel的属性通知
            switch (e.PropertyName)
            {
                case nameof(PianoRollConfiguration.CurrentTool):
                    OnPropertyChanged(nameof(CurrentTool));
                    ToolChanged?.Invoke(CurrentTool);
                    break;
                case nameof(PianoRollConfiguration.GridQuantization):
                    OnPropertyChanged(nameof(GridQuantization));
                    OnPropertyChanged(nameof(CurrentNoteDurationText));
                    GridQuantizationChanged?.Invoke(GridQuantization);
                    break;
                case nameof(PianoRollConfiguration.UserDefinedNoteDuration):
                    OnPropertyChanged(nameof(UserDefinedNoteDuration));
                    OnPropertyChanged(nameof(CurrentNoteTimeValueText));
                    NoteDurationChanged?.Invoke(UserDefinedNoteDuration);
                    break;
                case nameof(PianoRollConfiguration.IsEventViewVisible):
                    OnPropertyChanged(nameof(IsEventViewVisible));
                    EventViewToggleRequested?.Invoke(IsEventViewVisible);
                    break;
                case nameof(PianoRollConfiguration.IsNoteDurationDropDownOpen):
                    OnPropertyChanged(nameof(IsNoteDurationDropDownOpen));
                    break;
                case nameof(PianoRollConfiguration.CustomFractionInput):
                    OnPropertyChanged(nameof(CustomFractionInput));
                    break;
            }
        }
        #endregion

        #region 工具选择命令
        /// <summary>
        /// 选择铅笔工具
        /// </summary>
        [RelayCommand]
        public void SelectPencilTool()
        {
            _configuration.CurrentTool = EditorTool.Pencil;
        }

        /// <summary>
        /// 选择选择工具
        /// </summary>
        [RelayCommand]
        public void SelectSelectionTool()
        {
            _configuration.CurrentTool = EditorTool.Select;
        }

        /// <summary>
        /// 选择橡皮工具
        /// </summary>
        [RelayCommand]
        public void SelectEraserTool()
        {
            _configuration.CurrentTool = EditorTool.Eraser;
        }

        /// <summary>
        /// 选择切割工具
        /// </summary>
        [RelayCommand]
        public void SelectCutTool()
        {
            _configuration.CurrentTool = EditorTool.Cut;
        }
        #endregion

        #region 音符时值相关命令
        /// <summary>
        /// 切换音符时值下拉框显示状态
        /// </summary>
        [RelayCommand]
        public void ToggleNoteDurationDropDown()
        {
            _configuration.IsNoteDurationDropDownOpen = !_configuration.IsNoteDurationDropDownOpen;
        }

        /// <summary>
        /// 选择音符时值选项
        /// </summary>
        /// <param name="option">音符时值选项</param>
        [RelayCommand]
        public void SelectNoteDuration(NoteDurationOption option)
        {
            if (option == null) return;
            
            _configuration.GridQuantization = option.Duration;
            _configuration.IsNoteDurationDropDownOpen = false;
        }

        /// <summary>
        /// 应用自定义时值
        /// </summary>
        [RelayCommand]
        public void ApplyCustomFraction()
        {
            if (_configuration.TryParseCustomFraction(_configuration.CustomFractionInput, out var fraction))
            {
                _configuration.GridQuantization = fraction;
                _configuration.IsNoteDurationDropDownOpen = false;
            }
        }
        #endregion

        #region 视图控制命令
        /// <summary>
        /// 切换事件视图显示状态
        /// </summary>
        [RelayCommand]
        public void ToggleEventView()
        {
            _configuration.IsEventViewVisible = !_configuration.IsEventViewVisible;
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 设置当前工具
        /// </summary>
        /// <param name="tool">要设置的工具</param>
        public void SetCurrentTool(EditorTool tool)
        {
            _configuration.CurrentTool = tool;
        }

        /// <summary>
        /// 设置网格量化
        /// </summary>
        /// <param name="quantization">网格量化值</param>
        public void SetGridQuantization(MusicalFraction quantization)
        {
            _configuration.GridQuantization = quantization;
        }

        /// <summary>
        /// 设置用户定义的音符时长
        /// </summary>
        /// <param name="duration">音符时长</param>
        public void SetUserDefinedNoteDuration(MusicalFraction duration)
        {
            _configuration.UserDefinedNoteDuration = duration;
        }

        /// <summary>
        /// 设置事件视图可见性
        /// </summary>
        /// <param name="isVisible">是否可见</param>
        public void SetEventViewVisible(bool isVisible)
        {
            _configuration.IsEventViewVisible = isVisible;
        }

        /// <summary>
        /// 设置当前Tempo值
        /// </summary>
        /// <param name="bpm">BPM值</param>
        public void SetCurrentTempo(int bpm)
        {
            if (bpm >= 20 && bpm <= 300)
            {
                _currentTempo = bpm;
            }
        }

        /// <summary>
        /// 设置自定义时值输入文本
        /// </summary>
        /// <param name="input">输入文本</param>
        public void SetCustomFractionInput(string input)
        {
            _configuration.CustomFractionInput = input;
        }
        #endregion

        #region 工具方法
        /// <summary>
        /// 将时间吸附到网格
        /// </summary>
        /// <param name="time">原始时间</param>
        /// <returns>吸附后的时间</returns>
        public MusicalFraction SnapToGrid(MusicalFraction time)
        {
            return _configuration.SnapToGrid(time);
        }

        /// <summary>
        /// 将时间值吸附到网格
        /// </summary>
        /// <param name="timeValue">原始时间值</param>
        /// <returns>吸附后的时间值</returns>
        public double SnapToGridTime(double timeValue)
        {
            return _configuration.SnapToGridTime(timeValue);
        }

        /// <summary>
        /// 尝试解析自定义分数字符串
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <param name="fraction">解析结果</param>
        /// <returns>是否解析成功</returns>
        public bool TryParseCustomFraction(string input, out MusicalFraction fraction)
        {
            return _configuration.TryParseCustomFraction(input, out fraction);
        }
        #endregion

        #region 清理
        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            if (_configuration != null)
            {
                _configuration.PropertyChanged -= OnConfigurationPropertyChanged;
            }
        }
        #endregion
    }
}