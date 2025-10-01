using System;
using DominoNext.Models.Music;
using DominoNext.Services.Interfaces;
using DominoNext.ViewModels.Editor;
using DominoNext.ViewModels.Settings;
using EnderWaveTableAccessingParty.Services;
using CommunityToolkit.Mvvm.Messaging;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// ViewModel工厂实现 - 负责创建各种ViewModel实例
    /// 遵循依赖注入原则，确保所有实例都能正确初始化
    /// 遵循MVVM架构的最佳实践和依赖注入原则
    /// </summary>
    public class ViewModelFactory : IViewModelFactory
    {
        #region 依赖项
        private readonly ICoordinateService _coordinateService;
        private readonly ISettingsService _settingsService;
        private readonly IMidiConversionService _midiConversionService;
        private readonly IMidiPlaybackService _midiPlaybackService;
        private readonly IMessenger _messenger;
        #endregion

        #region 构造函数
        /// <summary>
        /// 初始化ViewModel工厂
        /// </summary>
        /// <param name="coordinateService">坐标转换服务</param>
        /// <param name="settingsService">设置服务</param>
        /// <param name="midiConversionService">MIDI转换服务</param>
        /// <param name="midiPlaybackService">MIDI播放服务</param>
        /// <param name="messenger">消息传递服务</param>
        public ViewModelFactory(
            ICoordinateService coordinateService, 
            ISettingsService settingsService,
            IMidiConversionService midiConversionService,
            IMidiPlaybackService midiPlaybackService,
            IMessenger messenger)
        {
            _coordinateService = coordinateService ?? throw new ArgumentNullException(nameof(coordinateService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _midiConversionService = midiConversionService ?? throw new ArgumentNullException(nameof(midiConversionService));
            _midiPlaybackService = midiPlaybackService ?? throw new ArgumentNullException(nameof(midiPlaybackService));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        }

        /// <summary>
        /// 兼容构造函数 - 支持不传入MidiConversionService参数的情况
        /// 当MidiConversionService为null时将创建默认实现
        /// </summary>
        /// <param name="coordinateService">坐标转换服务</param>
        /// <param name="settingsService">设置服务</param>
        /// <param name="midiPlaybackService">MIDI播放服务</param>
        /// <param name="messenger">消息传递服务</param>
        public ViewModelFactory(
            ICoordinateService coordinateService, 
            ISettingsService settingsService,
            IMidiPlaybackService midiPlaybackService,
            IMessenger messenger)
            : this(coordinateService, settingsService, new MidiConversionService(), midiPlaybackService, messenger)
        {
        }
        #endregion

        #region IViewModelFactory 实现
        /// <summary>
        /// 创建PianoRollViewModel实例并注入所有依赖项
        /// </summary>
        public PianoRollViewModel CreatePianoRollViewModel()
        {
            return new PianoRollViewModel(_coordinateService, null, _midiPlaybackService, _messenger, _midiConversionService);
        }

        /// <summary>
        /// 创建SettingsWindowViewModel实例并注入设置服务
        /// </summary>
        public SettingsWindowViewModel CreateSettingsWindowViewModel()
        {
            return new SettingsWindowViewModel(_settingsService);
        }

        /// <summary>
        /// 创建NoteViewModel实例并注入MIDI转换服务
        /// </summary>
        /// <param name="note">音符数据模型，如果为null则创建默认实例</param>
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