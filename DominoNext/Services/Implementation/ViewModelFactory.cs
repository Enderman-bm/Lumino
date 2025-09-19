using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor;
using Lumino.ViewModels.Settings;
using Lumino.Models.Music;
using System;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// ViewModel��������ʵ�� - ���𴴽�������ViewModelʵ��
    /// ���й���ViewModel������ע�룬ȷ������ʵ������ȷ����
    /// ����MVVM���ʵ��������ע��ԭ��
    /// </summary>
    public class ViewModelFactory : IViewModelFactory
    {
        #region ��������
        private readonly ICoordinateService _coordinateService;
        private readonly ISettingsService _settingsService;
        private readonly IMidiConversionService _midiConversionService;
        #endregion

        #region ���캯��
        /// <summary>
        /// ��ʼ��ViewModel����
        /// </summary>
        /// <param name="coordinateService">����ת������</param>
        /// <param name="settingsService">���÷���</param>
        /// <param name="midiConversionService">MIDIת������</param>
        public ViewModelFactory(
            ICoordinateService coordinateService, 
            ISettingsService settingsService,
            IMidiConversionService midiConversionService)
        {
            _coordinateService = coordinateService ?? throw new ArgumentNullException(nameof(coordinateService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _midiConversionService = midiConversionService ?? throw new ArgumentNullException(nameof(midiConversionService));
        }

        /// <summary>
        /// �����Թ��캯�� - ֧�ֲ�����MidiConversionService�����
        /// ��MidiConversionServiceΪnullʱ���ᴴ��Ĭ��ʵ��
        /// </summary>
        /// <param name="coordinateService">����ת������</param>
        /// <param name="settingsService">���÷���</param>
        public ViewModelFactory(ICoordinateService coordinateService, ISettingsService settingsService)
            : this(coordinateService, settingsService, new MidiConversionService())
        {
        }
        #endregion

        #region IViewModelFactory ʵ��
        /// <summary>
        /// ����PianoRollViewModelʵ����ע���������������
        /// </summary>
        public PianoRollViewModel CreatePianoRollViewModel()
        {
            return new PianoRollViewModel(_coordinateService);
        }

        /// <summary>
        /// ����SettingsWindowViewModelʵ����ע�����÷���
        /// </summary>
        public SettingsWindowViewModel CreateSettingsWindowViewModel()
        {
            return new SettingsWindowViewModel(_settingsService);
        }

        /// <summary>
        /// ����NoteViewModelʵ����ע��MIDIת������
        /// </summary>
        /// <param name="note">��������ģ�ͣ����Ϊnull�򴴽�Ĭ������</param>
        public NoteViewModel CreateNoteViewModel(Note? note = null)
        {
            if (note == null)
            {
                return new NoteViewModel(_midiConversionService);
            }
            return new NoteViewModel(note, _midiConversionService);
        }
        #endregion
    }
}