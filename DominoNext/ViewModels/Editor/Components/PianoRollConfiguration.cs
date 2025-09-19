using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor;

namespace Lumino.ViewModels.Editor.Components
{
    /// <summary>
    /// ���پ������ù����� - �������ߡ����������
    /// ��ѭ��һְ��ԭ��רע�ڷ�������ص�״̬����
    /// ���Ź�����Ǩ�Ƶ� PianoRollZoomManager
    /// </summary>
    public partial class PianoRollConfiguration : ObservableObject
    {
        #region ���ߺͱ༭����
        [ObservableProperty] private EditorTool _currentTool = EditorTool.Pencil;
        // ��������������ʱ������
        [ObservableProperty] private MusicalFraction _gridQuantization = new MusicalFraction(1, 16);
        [ObservableProperty] private MusicalFraction _userDefinedNoteDuration = new MusicalFraction(1, 4);
        #endregion

        #region UI״̬
        [ObservableProperty] private bool _isEventViewVisible = true;
        [ObservableProperty] private bool _isNoteDurationDropDownOpen = false;
        [ObservableProperty] private string _customFractionInput = "1/4";
        #endregion

        #region ʱֵѡ��
        public ObservableCollection<NoteDurationOption> NoteDurationOptions { get; } = new();
        #endregion

        #region ���캯��
        public PianoRollConfiguration()
        {
            InitializeNoteDurationOptions();
        }
        #endregion

        #region ��ʼ������
        private void InitializeNoteDurationOptions()
        {
            // ��ʼ������ʱֵѡ�� - ʹ��ֱ�۹�������Ԥ�ȼ��㳣��ֵ
            // ��ʼ������ʱֵѡ�� - ʹ��ֱ�ӹ�������Ԥ�ȶ��峣��ֵ
            NoteDurationOptions.Add(new NoteDurationOption("ȫ���� (1/1)", new MusicalFraction(1, 1), "????"));
            NoteDurationOptions.Add(new NoteDurationOption("�������� (1/2)", new MusicalFraction(1, 2), "??"));
            NoteDurationOptions.Add(new NoteDurationOption("�������������� (1/3)", new MusicalFraction(1, 3), "???"));
            NoteDurationOptions.Add(new NoteDurationOption("�ķ����� (1/4)", new MusicalFraction(1, 4), "?"));
            NoteDurationOptions.Add(new NoteDurationOption("�������ķ����� (1/6)", new MusicalFraction(1, 6), "???"));
            NoteDurationOptions.Add(new NoteDurationOption("�˷����� (1/8)", new MusicalFraction(1, 8), "?"));
            NoteDurationOptions.Add(new NoteDurationOption("�������˷����� (1/12)", new MusicalFraction(1, 12), "???"));
            NoteDurationOptions.Add(new NoteDurationOption("ʮ�������� (1/16)", new MusicalFraction(1, 16), "?"));
            NoteDurationOptions.Add(new NoteDurationOption("������ʮ�������� (1/24)", new MusicalFraction(1, 24), "???"));
            NoteDurationOptions.Add(new NoteDurationOption("��ʮ�������� (1/32)", new MusicalFraction(1, 32), "??"));
            NoteDurationOptions.Add(new NoteDurationOption("��������ʮ�������� (1/48)", new MusicalFraction(1, 48), "???"));
            NoteDurationOptions.Add(new NoteDurationOption("��ʮ�ķ����� (1/64)", new MusicalFraction(1, 64), "????"));
        }
        #endregion

        #region ��������
        public string CurrentNoteDurationText => GridQuantization.ToString();
        public string CurrentNoteTimeValueText => UserDefinedNoteDuration.ToString();
        #endregion

        #region ���߷���
        /// <summary>
        /// ��ʱ���������������ڶ��빦��
        /// </summary>
        public MusicalFraction SnapToGrid(MusicalFraction time)
        {
            return MusicalFraction.QuantizeToGrid(time, GridQuantization);
        }

        /// <summary>
        /// ��ʱ����ֵ���������񣬸��������ط���
        /// </summary>
        public double SnapToGridTime(double timeValue)
        {
            var timeFraction = MusicalFraction.FromDouble(timeValue);
            var quantized = SnapToGrid(timeFraction);
            return quantized.ToDouble();
        }

        /// <summary>
        /// ���Խ����Զ�������ַ���
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
                // ����ʧ�ܣ�����false
            }
            
            return false;
        }
        #endregion
    }
}