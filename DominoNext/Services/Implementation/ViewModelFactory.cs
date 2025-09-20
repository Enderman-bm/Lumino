using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor;
using Lumino.ViewModels.Settings;
using Lumino.Models.Music;
using System;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// ViewModel工厂实现 - 负责创建各类ViewModel实例
    /// 通过依赖注入容器获取服务，确保所有依赖正确注入
    /// 遵循MVVM最佳实践的依赖注入原则
    /// </summary>
    public class ViewModelFactory : IViewModelFactory
    {
        #region 依赖服务
        private readonly ICoordinateService _coordinateService;
        private readonly ISettingsService _settingsService;
        private readonly IMidiConversionService _midiConversionService;
        private readonly INoteEditingService _noteEditingService;
        private readonly IEventCurveCalculationService _eventCurveCalculationService;
        #endregion

        #region 构造函数
        /// <summary>
        /// 初始化ViewModel工厂
        /// </summary>
        /// <param name="coordinateService">坐标转换服务</param>
        /// <param name="settingsService">设置服务</param>
        /// <param name="midiConversionService">MIDI转换服务</param>
        /// <param name="noteEditingService">音符编辑服务</param>
        /// <param name="eventCurveCalculationService">事件曲线计算服务</param>
        public ViewModelFactory(
            ICoordinateService coordinateService, 
            ISettingsService settingsService,
            IMidiConversionService midiConversionService,
            INoteEditingService noteEditingService,
            IEventCurveCalculationService eventCurveCalculationService)
        {
            _coordinateService = coordinateService ?? throw new ArgumentNullException(nameof(coordinateService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _midiConversionService = midiConversionService ?? throw new ArgumentNullException(nameof(midiConversionService));
            _noteEditingService = noteEditingService ?? throw new ArgumentNullException(nameof(noteEditingService));
            _eventCurveCalculationService = eventCurveCalculationService ?? throw new ArgumentNullException(nameof(eventCurveCalculationService));
        }

        /// <summary>
        /// 简化构造函数 - 支持部分服务注入
        /// 当某些服务为null时会创建默认实现
        /// </summary>
        /// <param name="coordinateService">坐标转换服务</param>
        /// <param name="settingsService">设置服务</param>
        public ViewModelFactory(ICoordinateService coordinateService, ISettingsService settingsService)
            : this(coordinateService, settingsService, new MidiConversionService(), new NoteEditingService(null, coordinateService), new EventCurveCalculationService())
        {
        }
        #endregion

        #region IViewModelFactory ʵ��
        /// <summary>
        /// 创建PianoRollViewModel实例并注入所有必要服务
        /// </summary>
        public PianoRollViewModel CreatePianoRollViewModel()
        {
            return new PianoRollViewModel(_coordinateService, _eventCurveCalculationService, _noteEditingService);
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