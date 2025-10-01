using System;
using System.Diagnostics;
using Avalonia;
using CommunityToolkit.Mvvm.Messaging;
using DominoNext.Models.Music;
using DominoNext.Services.Interfaces;
using DominoNext.ViewModels.Editor.Services;
using EnderDebugger;
using EnderWaveTableAccessingParty.Services;
using EnderWaveTableAccessingParty.Models;

namespace DominoNext.ViewModels.Editor.Modules
{
    /// <summary>
    /// 音符创建模块 - 处理音符创建的核心逻辑
    /// 支持拖拽创建、短按创建等多种交互方式
    /// </summary>
    public class NoteCreationModule
    {
        #region 依赖服务
        private readonly IMidiPlaybackService _midiPlaybackService;
        private readonly AntiShakeService _antiShakeService;
        private readonly IMessenger _messenger;
        #endregion

        #region 状态字段
        private PianoRollViewModel? _pianoRollViewModel;
        private Point _creationStartPoint;
        private DateTime _creationStartTime;
        #endregion

        #region 可观察属性
        public bool IsCreatingNote { get; private set; }
        public NoteViewModel? CreatingNote { get; private set; }
        #endregion

        #region 构造函数
        public NoteCreationModule(IMidiPlaybackService midiPlaybackService, AntiShakeService antiShakeService, IMessenger messenger)
        {
            _midiPlaybackService = midiPlaybackService ?? throw new ArgumentNullException(nameof(midiPlaybackService));
            _antiShakeService = antiShakeService ?? throw new ArgumentNullException(nameof(antiShakeService));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 初始化模块 - 绑定到钢琴卷帘视图模型
        /// </summary>
        public void Initialize(PianoRollViewModel pianoRollViewModel)
        {
            _pianoRollViewModel = pianoRollViewModel;
        }

        /// <summary>
        /// 开始创建音符 - 统一的入口方法
        /// </summary>
        public void StartCreating(Point startPoint)
        {
            if (_pianoRollViewModel == null) return;

            // 检查当前轨道是否为Conductor轨道，如果是则不允许创建音符
            if (_pianoRollViewModel.IsCurrentTrackConductor || _pianoRollViewModel.CurrentTrackIndex == -1)
            {
                Debug.WriteLine("无法在Conductor轨道上创建音符");
                return;
            }

            IsCreatingNote = true;
            _creationStartPoint = startPoint;
            _creationStartTime = DateTime.Now;

            // 计算音高和起始位置（使用与预览音符相同的量化方式）
            var pitch = GetPitchFromPosition(startPoint);
            var quantizedStartTime = GetQuantizedTimeFromPosition(startPoint);

            // 创建预览音符
            CreatingNote = new NoteViewModel(new Note
            {
                Pitch = pitch,
                StartPosition = quantizedStartTime,
                Duration = _pianoRollViewModel.UserDefinedNoteDuration,
                Velocity = 100, // 默认力度
                TrackIndex = _pianoRollViewModel.CurrentTrackIndex
            }, _pianoRollViewModel.MidiConverter)
            {
                IsPreview = true
            };

            OnCreationStarted?.Invoke();
            Debug.WriteLine($"开始创建音符: Pitch={pitch}, StartTime={quantizedStartTime}, TrackIndex={_pianoRollViewModel.CurrentTrackIndex}");
        }

        /// <summary>
        /// 通用音高转换 - 从位置获取音高
        /// </summary>
        private int GetPitchFromPosition(Point position)
        {
            if (_pianoRollViewModel == null) return 0;
            return _pianoRollViewModel.GetPitchFromScreenY(position.Y);
        }

        /// <summary>
        /// 通用时间转换 - 从位置获取时间
        /// </summary>
        private MusicalFraction GetQuantizedTimeFromPosition(Point position)
        {
            if (_pianoRollViewModel == null) return new MusicalFraction(0, 1);
            
            var timeValue = _pianoRollViewModel.GetTimeFromScreenX(position.X);
            var timeFraction = MusicalFraction.FromDouble(timeValue);
            return _pianoRollViewModel.SnapToGrid(timeFraction);
        }

        /// <summary>
        /// 更新创建过程 - 统一的更新方法
        /// </summary>
        public void UpdateCreating(Point currentPoint)
        {
            if (!IsCreatingNote || CreatingNote == null || _pianoRollViewModel == null) return;

            // 计算持续时间
            var endTime = MusicalFraction.FromDouble(_pianoRollViewModel.GetTimeFromScreenX(currentPoint.X));
            var duration = endTime - CreatingNote.StartPosition;

            // 确保持续时间为正数
            if (duration.ToDouble() > 0)
            {
                // 应用网格对齐
                var quantizedEnd = _pianoRollViewModel.SnapToGrid(CreatingNote.StartPosition + duration);
                var quantizedDuration = quantizedEnd - CreatingNote.StartPosition;

                if (quantizedDuration.ToDouble() > 0)
                {
                    CreatingNote.Duration = quantizedDuration;
                    SafeInvalidateNoteCache(CreatingNote);

                    OnCreationUpdated?.Invoke();
                }
            }
        }

        /// <summary>
        /// 完成创建音符 - 使用统一的创建逻辑
        /// </summary>
        public void FinishCreating()
        {
            if (IsCreatingNote && CreatingNote != null && _pianoRollViewModel != null)
            {
                MusicalFraction finalDuration;

                // 使用防抖机制判断
                if (_antiShakeService.IsShortPress(_creationStartTime))
                {
                    // 短按则使用用户预定义时长
                    finalDuration = _pianoRollViewModel.UserDefinedNoteDuration;
                    Debug.WriteLine($"短按创建音符，使用预定义时长: {finalDuration}");
                }
                else
                {
                    // 长按则使用拖拽的长度
                    finalDuration = CreatingNote.Duration;
                    Debug.WriteLine($"长按创建音符，使用拖拽时长: {finalDuration}");
                }

                // 创建最终的音符
                var finalNote = new NoteViewModel
                {
                    Pitch = CreatingNote.Pitch,
                    StartPosition = CreatingNote.StartPosition,
                    Duration = finalDuration,
                    Velocity = CreatingNote.Velocity,
                    TrackIndex = _pianoRollViewModel.CurrentTrackIndex, // 设置为当前轨道
                    IsPreview = false
                };

                // 添加到音符集合，将自动触发UpdateMaxScrollExtent等
                _pianoRollViewModel.Notes.Add(finalNote);

                // 只在长按时更新用户预定义时长
                if (!_antiShakeService.IsShortPress(_creationStartTime))
                {
                    _pianoRollViewModel.SetUserDefinedNoteDuration(CreatingNote.Duration);
                    Debug.WriteLine($"更新用户自定义时长为: {CreatingNote.Duration}");
                }

                Debug.WriteLine($"完成创建音符: {finalNote.Duration}, TrackIndex: {finalNote.TrackIndex}");
            }

            ClearCreating();
            OnCreationCompleted?.Invoke();
        }

        /// <summary>
        /// 取消创建音符
        /// </summary>
        public void CancelCreating()
        {
            if (IsCreatingNote)
            {
                Debug.WriteLine("取消创建音符");
            }

            ClearCreating();
            OnCreationCancelled?.Invoke();
        }

        private void ClearCreating()
        {
            IsCreatingNote = false;
            CreatingNote = null;
        }

        // 事件
        public event Action? OnCreationStarted;
        public event Action? OnCreationUpdated;
        public event Action? OnCreationCompleted;
        public event Action? OnCreationCancelled;
        #endregion

        #region 私有辅助方法
        /// <summary>
        /// 安全地使音符缓存失效 - 避免空引用异常
        /// </summary>
        private void SafeInvalidateNoteCache(NoteViewModel note)
        {
            try
            {
                note.InvalidateCache();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"使音符缓存失效时发生错误: {ex.Message}");
            }
        }
        #endregion
    }
}