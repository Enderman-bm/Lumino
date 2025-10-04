using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor;
using Lumino.ViewModels.Settings;
using Lumino.Models.Music;
using System;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// ViewModel工厂服务实现 - 负责创建和配置ViewModel实例
    /// 集中管理ViewModel的依赖注入，确保所有实例都正确配置
    /// 符合MVVM最佳实践和依赖注入原则
    /// </summary>
    public class ViewModelFactory : IViewModelFactory
    {
        #region 服务依赖
        private readonly ICoordinateService _coordinateService;
        private readonly ISettingsService _settingsService;
        private readonly IMidiConversionService _midiConversionService;
        #endregion

        #region 构造函数
        /// <summary>
        /// 初始化ViewModel工厂
        /// </summary>
        /// <param name="coordinateService">坐标转换服务</param>
        /// <param name="settingsService">设置服务</param>
        /// <param name="midiConversionService">MIDI转换服务</param>
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
        /// 兼容性构造函数 - 支持不传入MidiConversionService的情况
        /// 当MidiConversionService为null时，会创建默认实例
        /// </summary>
        /// <param name="coordinateService">坐标转换服务</param>
        /// <param name="settingsService">设置服务</param>
        public ViewModelFactory(ICoordinateService coordinateService, ISettingsService settingsService)
            : this(coordinateService, settingsService, new MidiConversionService())
        {
        }
        #endregion

        #region IViewModelFactory 实现
        /// <summary>
        /// 创建PianoRollViewModel实例，注入所需的依赖服务
        /// </summary>
        public PianoRollViewModel CreatePianoRollViewModel()
        {
            var undoRedoService = new UndoRedoService();
            return new PianoRollViewModel(_coordinateService, midiConversionService: _midiConversionService, undoRedoService: undoRedoService);
        }

        /// <summary>
        /// 创建SettingsWindowViewModel实例，注入设置服务
        /// </summary>
        public SettingsWindowViewModel CreateSettingsWindowViewModel()
        {
            return new SettingsWindowViewModel(_settingsService);
        }

        /// <summary>
        /// 创建NoteViewModel实例，注入MIDI转换服务
        /// </summary>
        /// <param name="note">音符数据模型，如果为null则创建默认音符</param>
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