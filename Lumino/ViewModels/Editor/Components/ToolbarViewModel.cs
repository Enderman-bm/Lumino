using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Enums;

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
        /// �Ƿ�����װƤ
        /// </summary>
        public bool IsOnionSkinEnabled => _configuration.IsOnionSkinEnabled;

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
                case nameof(PianoRollConfiguration.IsNoteDurationDropDownOpen):
                    OnPropertyChanged(nameof(IsNoteDurationDropDownOpen));
                    break;
                case nameof(PianoRollConfiguration.CustomFractionInput):
                    OnPropertyChanged(nameof(CustomFractionInput));
                    break;
            }
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
        /// 清理工具栏状态
        /// </summary>
        public void Cleanup()
        {
            // 重置工具栏状态到默认值
            _configuration.CurrentTool = EditorTool.Pencil;
            _configuration.IsNoteDurationDropDownOpen = false;
            _configuration.CustomFractionInput = "1/4";
            CurrentTempo = 120;
        }

        /// <summary>
        /// 切换洋葱皮显示状态
        /// </summary>
        [RelayCommand]
        public void ToggleOnionSkin()
        {
            _configuration.IsOnionSkinEnabled = !_configuration.IsOnionSkinEnabled;
        }
        #endregion
    }
}