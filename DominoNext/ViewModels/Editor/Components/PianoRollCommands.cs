using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DominoNext.Models.Music;
using DominoNext.ViewModels.Editor;

namespace DominoNext.ViewModels.Editor.Components
{
    /// <summary>
    /// ���پ���������� - �������е��û���������
    /// ���ϵ�һְ��ԭ��רע�������߼�
    /// </summary>
    public partial class PianoRollCommands : ObservableObject
    {
        #region ����
        private readonly PianoRollConfiguration _configuration;
        private readonly PianoRollViewport _viewport;
        #endregion

        #region �¼�
        public event Action? SelectAllRequested;
        public event Action? ConfigurationChanged;
        public event Action? ViewportChanged;
        #endregion

        #region ���캯��
        public PianoRollCommands(PianoRollConfiguration configuration, PianoRollViewport viewport)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        }
        #endregion

        #region ����ѡ������
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

        #region ��������ʱֵ����
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
            
            // ����Ӧ�ø��������������������û����������ʱֵ
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

        #region ��ͼ����
        [RelayCommand]
        private void ToggleEventView()
        {
            _configuration.IsEventViewVisible = !_configuration.IsEventViewVisible;
            
            // �����ӿ�����Ӧ�Ĳ���
            _viewport.UpdateViewportForEventView(_configuration.IsEventViewVisible);
            
            OnConfigurationChanged();
            OnViewportChanged();
        }
        #endregion

        #region ѡ������
        [RelayCommand]
        private void SelectAll()
        {
            SelectAllRequested?.Invoke();
        }
        #endregion

        #region ��������
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
            _viewport.SetVerticalScrollOffset(newOffset, _viewport.GetEffectiveVerticalScrollMax(128 * 12.0));
            OnViewportChanged();
        }

        [RelayCommand]
        private void ScrollDown()
        {
            var newOffset = _viewport.VerticalScrollOffset + _viewport.VerticalViewportSize * 0.1;
            _viewport.SetVerticalScrollOffset(newOffset, _viewport.GetEffectiveVerticalScrollMax(128 * 12.0));
            OnViewportChanged();
        }
        #endregion

        #region �¼���������
        private void OnConfigurationChanged()
        {
            ConfigurationChanged?.Invoke();
        }

        private void OnViewportChanged()
        {
            ViewportChanged?.Invoke();
        }
        #endregion

        #region ״̬��ѯ����
        /// <summary>
        /// ��鵱ǰ�Ƿ�Ϊָ������
        /// </summary>
        public bool IsCurrentTool(EditorTool tool)
        {
            return _configuration.CurrentTool == tool;
        }

        /// <summary>
        /// ��ȡ��ǰ���ߵ���ʾ����
        /// </summary>
        public string GetCurrentToolDisplayName()
        {
            return _configuration.CurrentTool switch
            {
                EditorTool.Pencil => "Ǧ�ʹ���",
                EditorTool.Select => "ѡ�񹤾�",
                EditorTool.Eraser => "��Ƥ������",
                EditorTool.Cut => "�и��",
                _ => "δ֪����"
            };
        }
        #endregion
    }
}