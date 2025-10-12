using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor;
using Lumino.ViewModels.Editor.Enums;

namespace Lumino.ViewModels.Editor.Components
{
    /// <summary>
    /// 钢琴卷帘配置管理器 - 管理工具、网格等配置
    /// 遵循单一职责原则，专注于非缩放相关的状态管理
    /// 缩放功能已迁移到 PianoRollZoomManager
    /// </summary>
    public partial class PianoRollConfiguration : ObservableObject
    {
        #region 工具和编辑配置
        [ObservableProperty] private EditorTool _currentTool = EditorTool.Pencil;
        // 网格量化和音符时长配置
        [ObservableProperty] private MusicalFraction _gridQuantization = new MusicalFraction(1, 16);
        [ObservableProperty] private MusicalFraction _userDefinedNoteDuration = new MusicalFraction(1, 4);
        #endregion

        #region UI状态
        [ObservableProperty] private bool _isEventViewVisible = true;
        [ObservableProperty] private bool _isNoteDurationDropDownOpen = false;
        [ObservableProperty] private string _customFractionInput = "1/4";
        [ObservableProperty] private bool _isOnionSkinEnabled = false;
        [ObservableProperty] private OnionSkinMode _onionSkinMode = OnionSkinMode.AllTracks;
        [ObservableProperty] private ObservableCollection<int> _selectedOnionTrackIndices = new();
        #endregion

        #region 时值选项
        public ObservableCollection<NoteDurationOption> NoteDurationOptions { get; } = new();
        #endregion

        #region 构造函数
        public PianoRollConfiguration()
        {
            InitializeNoteDurationOptions();
        }
        #endregion

        #region 初始化方法
        private void InitializeNoteDurationOptions()
        {
            // 初始化音符时值选项 - 使用直观构造器，预先计算常用值
            // 初始化音符时值选项 - 使用直接构造器，预先定义常用值
            NoteDurationOptions.Add(new NoteDurationOption("全音符 (1/1)", new MusicalFraction(1, 1), "????"));
            NoteDurationOptions.Add(new NoteDurationOption("二分音符 (1/2)", new MusicalFraction(1, 2), "??"));
            NoteDurationOptions.Add(new NoteDurationOption("三连音二分音符 (1/3)", new MusicalFraction(1, 3), "???"));
            NoteDurationOptions.Add(new NoteDurationOption("四分音符 (1/4)", new MusicalFraction(1, 4), "?"));
            NoteDurationOptions.Add(new NoteDurationOption("三连音四分音符 (1/6)", new MusicalFraction(1, 6), "???"));
            NoteDurationOptions.Add(new NoteDurationOption("八分音符 (1/8)", new MusicalFraction(1, 8), "?"));
            NoteDurationOptions.Add(new NoteDurationOption("三连音八分音符 (1/12)", new MusicalFraction(1, 12), "???"));
            NoteDurationOptions.Add(new NoteDurationOption("十六分音符 (1/16)", new MusicalFraction(1, 16), "?"));
            NoteDurationOptions.Add(new NoteDurationOption("三连音十六分音符 (1/24)", new MusicalFraction(1, 24), "???"));
            NoteDurationOptions.Add(new NoteDurationOption("三十二分音符 (1/32)", new MusicalFraction(1, 32), "??"));
            NoteDurationOptions.Add(new NoteDurationOption("三连音三十二分音符 (1/48)", new MusicalFraction(1, 48), "???"));
            NoteDurationOptions.Add(new NoteDurationOption("六十四分音符 (1/64)", new MusicalFraction(1, 64), "????"));
            NoteDurationOptions.Add(new NoteDurationOption("三连音六十四分音符 (1/96)", new MusicalFraction(1, 96), "?????"));
            NoteDurationOptions.Add(new NoteDurationOption("一百二十八分音符 (1/128)", new MusicalFraction(1, 128), "??????"));
            NoteDurationOptions.Add(new NoteDurationOption("三连音一百二十八分音符 (1/192)", new MusicalFraction(1, 192), "???????"));
            NoteDurationOptions.Add(new NoteDurationOption("二百五十六分音符 (1/256)", new MusicalFraction(1, 256), "????????"));
            NoteDurationOptions.Add(new NoteDurationOption("三连音二百五十六分音符 (1/384)", new MusicalFraction(1, 384), "?????????"));
            NoteDurationOptions.Add(new NoteDurationOption("五百一十二分音符 (1/512)", new MusicalFraction(1, 512), "??????????"));
            NoteDurationOptions.Add(new NoteDurationOption("三连音五百一十二分音符 (1/768)", new MusicalFraction(1, 768), "???????????"));
            NoteDurationOptions.Add(new NoteDurationOption("一千二十四分音符 (1/1024)", new MusicalFraction(1, 1024), "????????????"));
            NoteDurationOptions.Add(new NoteDurationOption("三连音一千二十四分音符 (1/1536)", new MusicalFraction(1, 1536), "?????????????"));
            NoteDurationOptions.Add(new NoteDurationOption("二千零四十八分音符 (1/2048)", new MusicalFraction(1, 2048), "??????????????"));
        }
        #endregion

        #region 计算属性
        public string CurrentNoteDurationText => GridQuantization.ToString();
        public string CurrentNoteTimeValueText => UserDefinedNoteDuration.ToString();
        #endregion

        #region 工具方法
        /// <summary>
        /// 将时间量化到网格，用于对齐功能
        /// </summary>
        public MusicalFraction SnapToGrid(MusicalFraction time)
        {
            return MusicalFraction.QuantizeToGrid(time, GridQuantization);
        }

        /// <summary>
        /// 将时间数值量化到网格，更简便的重载方法
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
    }
}