using Lumino.Models.Settings;
using System;
using System.Threading.Tasks;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// ���÷���ӿ�
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// ��ǰ����
        /// </summary>
        SettingsModel Settings { get; }

        /// <summary>
        /// ���ñ���¼�
        /// </summary>
        event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

        /// <summary>
        /// ��������
        /// </summary>
        Task LoadSettingsAsync();

        /// <summary>
        /// ��������
        /// </summary>
        Task SaveSettingsAsync();

        /// <summary>
        /// ��������ΪĬ��ֵ
        /// </summary>
        Task ResetToDefaultsAsync();

        /// <summary>
        /// Ӧ����������
        /// </summary>
        void ApplyLanguageSettings();

        /// <summary>
        /// Ӧ����������
        /// </summary>
        void ApplyThemeSettings();
    }

    /// <summary>
    /// ���ñ���¼�����
    /// </summary>
    public class SettingsChangedEventArgs : EventArgs
    {
        public string PropertyName { get; set; } = string.Empty;
        public object? OldValue { get; set; }
        public object? NewValue { get; set; }
    }
}