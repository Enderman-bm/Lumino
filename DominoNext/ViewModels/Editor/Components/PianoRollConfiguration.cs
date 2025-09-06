using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DominoNext.Models.Music;
using DominoNext.ViewModels.Editor;
using DominoNext.ViewModels.Editor.Models;

namespace DominoNext.ViewModels.Editor.Components
{
    /// <summary>
    /// 钢琴卷帘配置管理组件 - 负责缩放、量化、工具等配置
    /// 符合单一职责原则，专注于配置相关的状态管理
    /// </summary>
    public partial class PianoRollConfiguration : ObservableObject
    {
        #region 缩放配置
        [ObservableProperty] private double _zoom = 1.0;
        [ObservableProperty] private double _verticalZoom = 1.0;
        [ObservableProperty] private double _zoomSliderValue = 50.0;
        [ObservableProperty] private double _verticalZoomSliderValue = 50.0;
        #endregion

        #region 工具和量化配置
        [ObservableProperty] private EditorTool _currentTool = EditorTool.Pencil;
        // 网格和音符时长相关属性
        [ObservableProperty] private MusicalFraction _gridQuantization = new MusicalFraction(1, 16);
        [ObservableProperty] private MusicalFraction _userDefinedNoteDuration = new MusicalFraction(1, 4);
        #endregion

        #region UI配置
        [ObservableProperty] private bool _isEventViewVisible = true;
        [ObservableProperty] private bool _isNoteDurationDropDownOpen = false;
        [ObservableProperty] private string _customFractionInput = "1/4";
        #endregion

        #region 量化选项
        public ObservableCollection<NoteDurationOption> NoteDurationOptions { get; } = new();
        #endregion

        #region 构造函数
        public PianoRollConfiguration()
        {
            InitializeNoteDurationOptions();
            
            // 监听缩放滑块变化
            PropertyChanged += OnPropertyChanged;
        }
        #endregion

        #region 初始化方法
        private void InitializeNoteDurationOptions()
        {
            // 网格量化选项 - 控制音符可以放置在多细的网格上
            // 初始化音符时长选项 - 使用直接构造替代预定义常量
            NoteDurationOptions.Add(new NoteDurationOption("全音符网格 (1/1)", new MusicalFraction(1, 1), "??"));
            NoteDurationOptions.Add(new NoteDurationOption("二分音符网格 (1/2)", new MusicalFraction(1, 2), "????"));
            NoteDurationOptions.Add(new NoteDurationOption("三连二分音符网格 (1/3)", new MusicalFraction(1, 3), "?????"));
            NoteDurationOptions.Add(new NoteDurationOption("四分音符网格 (1/4)", new MusicalFraction(1, 4), "????"));
            NoteDurationOptions.Add(new NoteDurationOption("三连四分音符网格 (1/6)", new MusicalFraction(1, 6), "?????"));
            NoteDurationOptions.Add(new NoteDurationOption("八分音符网格 (1/8)", new MusicalFraction(1, 8), "??????"));
            NoteDurationOptions.Add(new NoteDurationOption("三连八分音符网格 (1/12)", new MusicalFraction(1, 12), "???????"));
            NoteDurationOptions.Add(new NoteDurationOption("十六分音符网格 (1/16)", new MusicalFraction(1, 16), "??????"));
            NoteDurationOptions.Add(new NoteDurationOption("三连十六分音符网格 (1/24)", new MusicalFraction(1, 24), "???????"));
            NoteDurationOptions.Add(new NoteDurationOption("三十二分音符网格 (1/32)", new MusicalFraction(1, 32), "??????"));
            NoteDurationOptions.Add(new NoteDurationOption("三连三十二分音符网格 (1/48)", new MusicalFraction(1, 48), "???????"));
            NoteDurationOptions.Add(new NoteDurationOption("六十四分音符网格 (1/64)", new MusicalFraction(1, 64), "??????"));
        }
        #endregion

        #region 计算属性
        public string CurrentNoteDurationText => GridQuantization.ToString();
        public string CurrentNoteTimeValueText => UserDefinedNoteDuration.ToString();
        #endregion

        #region 缩放转换方法
        public double ConvertSliderValueToZoom(double sliderValue)
        {
            // 确保滑块值在有效范围内
            sliderValue = Math.Max(0, Math.Min(100, sliderValue));
            
            // 水平缩放：0-100 -> 0.1-5.0
            if (sliderValue <= 50)
            {
                // 0-50对应0.1-1.0
                return 0.1 + (sliderValue / 50.0) * 0.9;
            }
            else
            {
                // 50-100对应1.0-5.0
                return 1.0 + ((sliderValue - 50) / 50.0) * 4.0;
            }
        }

        public double ConvertSliderValueToVerticalZoom(double sliderValue)
        {
            // 确保滑块值在有效范围内
            sliderValue = Math.Max(0, Math.Min(100, sliderValue));
            
            // 垂直缩放：0-100 -> 0.5-3.0
            if (sliderValue <= 50)
            {
                // 0-50对应0.5-1.0
                return 0.5 + (sliderValue / 50.0) * 0.5;
            }
            else
            {
                // 50-100对应1.0-3.0
                return 1.0 + ((sliderValue - 50) / 50.0) * 2.0;
            }
        }
        #endregion

        #region 属性变更处理
        private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ZoomSliderValue):
                    Zoom = ConvertSliderValueToZoom(ZoomSliderValue);
                    break;
                case nameof(VerticalZoomSliderValue):
                    VerticalZoom = ConvertSliderValueToVerticalZoom(VerticalZoomSliderValue);
                    break;
            }
        }
        #endregion

        #region 工具方法
        /// <summary>
        /// 对时间进行网格量化（基于分数）
        /// </summary>
        public MusicalFraction SnapToGrid(MusicalFraction time)
        {
            return MusicalFraction.QuantizeToGrid(time, GridQuantization);
        }

        /// <summary>
        /// 对时间值进行网格量化（兼容性方法）
        /// </summary>
        public double SnapToGridTime(double timeValue)
        {
            var timeFraction = MusicalFraction.FromDouble(timeValue);
            var quantized = SnapToGrid(timeFraction);
            return quantized.ToDouble();
        }

        /// <summary>
        /// 尝试解析自定义分数字符串
        /// </summary>
        public bool TryParseCustomFraction(string input, out MusicalFraction fraction)
        {
            fraction = new MusicalFraction(1, 4);
            
            try
            {
                var parts = input.Split('/');
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out int numerator) &&
                    int.TryParse(parts[1], out int denominator) &&
                    numerator > 0 && denominator > 0)
                {
                    fraction = new MusicalFraction(numerator, denominator);
                    return true;
                }
            }
            catch
            {
                // 解析失败，返回false
            }
            
            return false;
        }
        #endregion
    }}