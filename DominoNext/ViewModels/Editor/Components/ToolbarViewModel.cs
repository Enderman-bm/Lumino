using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DominoNext.Models.Music;
using DominoNext.ViewModels.Editor.Enums;

namespace DominoNext.ViewModels.Editor.Components
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
        /// ѡ��ѡ�񹤾�
        /// </summary>
        [RelayCommand]
        public void SelectSelectionTool()
        {
            _configuration.CurrentTool = EditorTool.Select;
        }

        /// <summary>
        /// ѡ����Ƥ����
        /// </summary>
        [RelayCommand]
        public void SelectEraserTool()
        {
            _configuration.CurrentTool = EditorTool.Eraser;
        }

        /// <summary>
        /// ѡ���и��
        /// </summary>
        [RelayCommand]
        public void SelectCutTool()
        {
            _configuration.CurrentTool = EditorTool.Cut;
        }
        #endregion

        #region ����ʱֵ�������
        /// <summary>
        /// �л�����ʱֵ��������ʾ״̬
        /// </summary>
        [RelayCommand]
        public void ToggleNoteDurationDropDown()
        {
            _configuration.IsNoteDurationDropDownOpen = !_configuration.IsNoteDurationDropDownOpen;
        }

        /// <summary>
        /// ѡ������ʱֵѡ��
        /// </summary>
        /// <param name="option">����ʱֵѡ��</param>
        [RelayCommand]
        public void SelectNoteDuration(NoteDurationOption option)
        {
            if (option == null) return;
            
            _configuration.GridQuantization = option.Duration;
            _configuration.IsNoteDurationDropDownOpen = false;
        }

        /// <summary>
        /// Ӧ���Զ���ʱֵ
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

        #region ��ͼ��������
        /// <summary>
        /// �л��¼���ͼ��ʾ״̬
        /// </summary>
        [RelayCommand]
        public void ToggleEventView()
        {
            _configuration.IsEventViewVisible = !_configuration.IsEventViewVisible;
        }
        #endregion

        #region ��������
        /// <summary>
        /// ���õ�ǰ����
        /// </summary>
        /// <param name="tool">Ҫ���õĹ���</param>
        public void SetCurrentTool(EditorTool tool)
        {
            _configuration.CurrentTool = tool;
        }

        /// <summary>
        /// ������������
        /// </summary>
        /// <param name="quantization">��������ֵ</param>
        public void SetGridQuantization(MusicalFraction quantization)
        {
            _configuration.GridQuantization = quantization;
        }

        /// <summary>
        /// �����û����������ʱ��
        /// </summary>
        /// <param name="duration">����ʱ��</param>
        public void SetUserDefinedNoteDuration(MusicalFraction duration)
        {
            _configuration.UserDefinedNoteDuration = duration;
        }

        /// <summary>
        /// �����¼���ͼ�ɼ���
        /// </summary>
        /// <param name="isVisible">�Ƿ�ɼ�</param>
        public void SetEventViewVisible(bool isVisible)
        {
            _configuration.IsEventViewVisible = isVisible;
        }

        /// <summary>
        /// ���õ�ǰTempoֵ
        /// </summary>
        /// <param name="bpm">BPMֵ</param>
        public void SetCurrentTempo(int bpm)
        {
            if (bpm >= 20 && bpm <= 300)
            {
                CurrentTempo = bpm;
            }
        }

        /// <summary>
        /// �����Զ���ʱֵ�����ı�
        /// </summary>
        /// <param name="input">�����ı�</param>
        public void SetCustomFractionInput(string input)
        {
            _configuration.CustomFractionInput = input;
        }
        #endregion

        #region ���߷���
        /// <summary>
        /// ��ʱ������������
        /// </summary>
        /// <param name="time">ԭʼʱ��</param>
        /// <returns>�������ʱ��</returns>
        public MusicalFraction SnapToGrid(MusicalFraction time)
        {
            return _configuration.SnapToGrid(time);
        }

        /// <summary>
        /// ��ʱ��ֵ����������
        /// </summary>
        /// <param name="timeValue">ԭʼʱ��ֵ</param>
        /// <returns>�������ʱ��ֵ</returns>
        public double SnapToGridTime(double timeValue)
        {
            return _configuration.SnapToGridTime(timeValue);
        }

        /// <summary>
        /// ���Խ����Զ�������ַ���
        /// </summary>
        /// <param name="input">�����ַ���</param>
        /// <param name="fraction">�������</param>
        /// <returns>�Ƿ�����ɹ�</returns>
        public bool TryParseCustomFraction(string input, out MusicalFraction fraction)
        {
            return _configuration.TryParseCustomFraction(input, out fraction);
        }
        #endregion

        #region ����
        /// <summary>
        /// ������Դ
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