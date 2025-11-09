using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Enums;
using Lumino.ViewModels.Dialogs;
using Lumino.Views.Dialogs;

namespace Lumino.ViewModels.Editor.Components
{
    /// <summary>
    /// ������ViewModel - ר�Ŵ�����������ص�״̬������
    /// ��ѭMVVM���ԭ��͵�һְ��ԭ��
    /// </summary>
    public partial class ToolbarViewModel : ObservableObject
    {
        #region ˽���ֶ�
        private readonly PianoRollConfiguration _configuration;
        private TrackSelectorViewModel? _trackSelector;
        #endregion

        #region �¼�
        /// <summary>
        /// ����Ҫ�л��¼���ͼʱ����
        /// </summary>
        public event Action<bool>? EventViewToggleRequested;

        /// <summary>
        /// �����߷����仯ʱ����
        /// </summary>
        public event Action<EditorTool>? ToolChanged;

        /// <summary>
        /// ������ʱֵ�����仯ʱ����
        /// </summary>
        public event Action<MusicalFraction>? NoteDurationChanged;

        /// <summary>
        /// ���������������仯ʱ����
        /// </summary>
        public event Action<MusicalFraction>? GridQuantizationChanged;

        /// <summary>
        /// 洋葱皮开关状态改变时触发
        /// </summary>
        public event Action<bool>? OnionSkinToggleRequested;

        /// <summary>
        /// 洋葱皮模式改变时触发
        /// </summary>
        public event Action<OnionSkinMode>? OnionSkinModeChanged;
        #endregion

        #region ���� - ί�и�Configuration
        /// <summary>
        /// ��ǰѡ��Ĺ���
        /// </summary>
        public EditorTool CurrentTool => _configuration.CurrentTool;

        /// <summary>
        /// ��ǰ������������
        /// </summary>
        public MusicalFraction GridQuantization => _configuration.GridQuantization;

        /// <summary>
        /// �û����������ʱ��
        /// </summary>
        public MusicalFraction UserDefinedNoteDuration => _configuration.UserDefinedNoteDuration;

        /// <summary>
        /// �Ƿ���ʾ�¼���ͼ
        /// </summary>
        public bool IsEventViewVisible => _configuration.IsEventViewVisible;

        /// <summary>
        /// �Ƿ�����װƤ - 基于是否有音轨开启洋葱皮
        /// </summary>
        public bool IsOnionSkinEnabled => _trackSelector != null &&
            _trackSelector.Tracks.Any(t => !t.IsConductorTrack && t.IsOnionSkinEnabled);

        /// <summary>
        /// 洋葱皮显示模式
        /// </summary>
        public OnionSkinMode OnionSkinMode => _configuration.OnionSkinMode;

        /// <summary>
        /// 选中的洋葱皮音轨索引
        /// </summary>
        public ObservableCollection<int> SelectedOnionTrackIndices => _configuration.SelectedOnionTrackIndices;

        /// <summary>
        /// 当前选中的洋葱皮模式选项
        /// </summary>
        public OnionSkinModeOption? SelectedOnionSkinModeOption
        {
            get => OnionSkinModeOptions.FirstOrDefault(o => o.Mode == OnionSkinMode);
            set
            {
                if (value != null)
                {
                    _configuration.OnionSkinMode = value.Mode;
                }
            }
        }

        /// <summary>
        /// ���������������Ƿ��
        /// </summary>
        public bool IsNoteDurationDropDownOpen => _configuration.IsNoteDurationDropDownOpen;

        /// <summary>
        /// �Զ���ʱֵ�����ı�
        /// </summary>
        public string CustomFractionInput => _configuration.CustomFractionInput;

        /// <summary>
        /// ����ʱֵѡ���
        /// </summary>
        public ObservableCollection<NoteDurationOption> NoteDurationOptions => _configuration.NoteDurationOptions;

        /// <summary>
        /// ��ǰ����������ʾ�ı�
        /// </summary>
        public string CurrentNoteDurationText => _configuration.CurrentNoteDurationText;

        /// <summary>
        /// ��ǰ����ʱֵ��ʾ�ı�
        /// </summary>
        public string CurrentNoteTimeValueText => _configuration.CurrentNoteTimeValueText;

        /// <summary>
        /// 洋葱皮模式选项
        /// </summary>
        public ObservableCollection<OnionSkinModeOption> OnionSkinModeOptions { get; } = new();
        #endregion

        #region ��������
        /// <summary>
        /// ��ǰTempoֵ��BPM��
        /// </summary>
        [ObservableProperty]
        private int _currentTempo = 120;
        #endregion

        #region ���캯��
        /// <summary>
        /// ���ʱ���캯��
        /// </summary>
        public ToolbarViewModel() : this(new PianoRollConfiguration()) { }

        /// <summary>
        /// ����ʱ���캯��
        /// </summary>
        /// <param name="configuration">���پ������ö���</param>
        public ToolbarViewModel(PianoRollConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            // 初始化洋葱皮模式选项
            InitializeOnionSkinModeOptions();
            
            // �������ñ���¼�
            _configuration.PropertyChanged += OnConfigurationPropertyChanged;
        }
        #endregion

        #region �¼�����
        /// <summary>
        /// �����������Ա���¼�
        /// </summary>
        private void OnConfigurationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // �����ñ��������������ViewModel������֪ͨ
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
                case nameof(PianoRollConfiguration.IsOnionSkinEnabled):
                    OnPropertyChanged(nameof(IsOnionSkinEnabled));
                    OnionSkinToggleRequested?.Invoke(IsOnionSkinEnabled);
                    break;
                case nameof(PianoRollConfiguration.OnionSkinMode):
                    OnPropertyChanged(nameof(OnionSkinMode));
                    OnionSkinModeChanged?.Invoke(OnionSkinMode);
                    break;
                case nameof(PianoRollConfiguration.SelectedOnionTrackIndices):
                    OnPropertyChanged(nameof(SelectedOnionTrackIndices));
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

        #region 初始化方法
        private void InitializeOnionSkinModeOptions()
        {
            OnionSkinModeOptions.Add(new OnionSkinModeOption("只显示上一个音轨（动态切换）", OnionSkinMode.PreviousTrack));
            OnionSkinModeOptions.Add(new OnionSkinModeOption("显示下一个音轨（动态切换）", OnionSkinMode.NextTrack));
            OnionSkinModeOptions.Add(new OnionSkinModeOption("显示全部音轨", OnionSkinMode.AllTracks));
        }
        #endregion

        #region ����ѡ������
        /// <summary>
        /// ѡ��Ǧ�ʹ���
        /// </summary>
        [RelayCommand]
        public void SelectPencilTool()
        {
            _configuration.CurrentTool = EditorTool.Pencil;
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
        /// 选择选择工具
        /// </summary>
        [RelayCommand]
        public void SelectSelectionTool()
        {
            _configuration.CurrentTool = EditorTool.Select;
        }

        /// <summary>
        /// 选择剪切工具
        /// </summary>
        [RelayCommand]
        public void SelectCutTool()
        {
            _configuration.CurrentTool = EditorTool.Cut;
        }

        /// <summary>
        /// 选择 CC 点工具（单击添加/选择控制点）
        /// </summary>
        [RelayCommand]
        public void SelectCCPointTool()
        {
            _configuration.CurrentTool = EditorTool.CCPoint;
        }

        /// <summary>
        /// 选择 CC 直线工具（绘制直线段）
        /// </summary>
        [RelayCommand]
        public void SelectCCLineTool()
        {
            _configuration.CurrentTool = EditorTool.CCLine;
        }

        /// <summary>
        /// 选择 CC 曲线工具（绘制光滑曲线段）
        /// </summary>
        [RelayCommand]
        public void SelectCCCurveTool()
        {
            _configuration.CurrentTool = EditorTool.CCCurve;
        }

        /// <summary>
        /// 切换音符时值下拉菜单显示状态
        /// </summary>
        [RelayCommand]
        public void ToggleNoteDurationDropDown()
        {
            _configuration.IsNoteDurationDropDownOpen = !_configuration.IsNoteDurationDropDownOpen;
        }

        /// <summary>
        /// 选择音符时值
        /// </summary>
        [RelayCommand]
        public void SelectNoteDuration(NoteDurationOption option)
        {
            if (option is null) return;
            
            _configuration.GridQuantization = option.Duration;
            _configuration.IsNoteDurationDropDownOpen = false;
            OnPropertyChanged(nameof(CurrentNoteDurationText));
            OnPropertyChanged(nameof(CurrentNoteTimeValueText));
        }

        /// <summary>
        /// 应用自定义分数
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

        /// <summary>
        /// 设置当前工具
        /// </summary>
        public void SetCurrentTool(EditorTool tool)
        {
            _configuration.CurrentTool = tool;
        }

        /// <summary>
        /// 设置用户定义的音符时长
        /// </summary>
        public void SetUserDefinedNoteDuration(MusicalFraction duration)
        {
            _configuration.UserDefinedNoteDuration = duration;
        }

        /// <summary>
        /// 设置当前Tempo
        /// </summary>
        public void SetCurrentTempo(int bpm)
        {
            CurrentTempo = bpm;
        }

        /// <summary>
        /// 将时间量化到网格
        /// </summary>
        public MusicalFraction SnapToGrid(MusicalFraction time)
        {
            return _configuration.SnapToGrid(time);
        }

        /// <summary>
        /// 将时间数值量化到网格
        /// </summary>
        public double SnapToGridTime(double timeValue)
        {
            return _configuration.SnapToGridTime(timeValue);
        }

        /// <summary>
        /// 切换事件视图显示状态
        /// </summary>
        [RelayCommand]
        public void ToggleEventView()
        {
            _configuration.IsEventViewVisible = !_configuration.IsEventViewVisible;
        }

        /// <summary>
        /// 设置音轨选择器
        /// </summary>
        public void SetTrackSelector(TrackSelectorViewModel trackSelector)
        {
            _trackSelector = trackSelector;
            if (_trackSelector != null)
            {
                // 订阅音轨洋葱皮状态变化事件
                _trackSelector.OnionSkinTrackStateChanged += OnOnionSkinTrackStateChanged;
            }
        }

        /// <summary>
        /// 当音轨洋葱皮状态改变时，更新全局开关状态
        /// </summary>
        private void OnOnionSkinTrackStateChanged()
        {
            OnPropertyChanged(nameof(IsOnionSkinEnabled));
        }

        /// <summary>
        /// 切换洋葱皮显示状态
        /// </summary>
        [RelayCommand]
        public void ToggleOnionSkin()
        {
            if (_trackSelector == null) return;

            // 检查当前是否有音轨开启了洋葱皮
            bool hasAnyTrackEnabled = _trackSelector.Tracks.Any(t => !t.IsConductorTrack && t.IsOnionSkinEnabled);

            if (hasAnyTrackEnabled)
            {
                // 如果有音轨开启，则关闭所有音轨
                foreach (var track in _trackSelector.Tracks)
                {
                    if (!track.IsConductorTrack)
                    {
                        track.IsOnionSkinEnabled = false;
                    }
                }
            }
            else
            {
                // 如果没有音轨开启，则开启所有音轨
                foreach (var track in _trackSelector.Tracks)
                {
                    if (!track.IsConductorTrack)
                    {
                        track.IsOnionSkinEnabled = true;
                    }
                }
            }

            // 通知UI更新全局开关状态
            OnPropertyChanged(nameof(IsOnionSkinEnabled));
        }

        /// <summary>
        /// 直接设置洋葱皮状态，不触发同步逻辑
        /// </summary>
        public void SetOnionSkinEnabled(bool enabled)
        {
            _configuration.IsOnionSkinEnabled = enabled;
        }

        /// <summary>
        /// 选择洋葱皮模式
        /// </summary>
        [RelayCommand]
        public void SelectOnionSkinMode(OnionSkinMode mode)
        {
            _configuration.OnionSkinMode = mode;

            // 模式选择不再自动改变音轨开关状态，由用户手动控制
            if (mode == OnionSkinMode.SpecifiedTracks)
            {
                // 指定音轨模式（已废弃）
                OpenTrackSelectionDialog();
            }
        }

        /// <summary>
        /// 打开音轨选择弹窗
        /// </summary>
        [RelayCommand]
        public Task OpenTrackSelectionDialog()
        {
            if (_trackSelector == null) return Task.CompletedTask;

            var dialogViewModel = new TrackSelectionDialogViewModel(_trackSelector);
            var dialog = new TrackSelectionDialog(dialogViewModel);

            // TODO: 获取当前窗口作为父窗口
            // var result = await dialog.ShowDialog<bool>(parentWindow);

            // 暂时直接显示
            dialog.Show();

            // 如果用户确认，将选中的索引应用到配置
            // if (result)
            // {
            //     _configuration.SelectedOnionTrackIndices.Clear();
            //     foreach (var index in dialogViewModel.SelectedTrackIndices)
            //     {
            //         _configuration.SelectedOnionTrackIndices.Add(index);
            //     }
            // }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 清理工具栏状态
        /// </summary>
        public void Cleanup()
        {
            // 重置工具栏状态到默认值
            _configuration.CurrentTool = EditorTool.Pencil;
            _configuration.IsNoteDurationDropDownOpen = false;
            _configuration.CustomFractionInput = "1/4";
        }
        #endregion
    }
}