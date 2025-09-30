using System;
using Avalonia;
using DominoNext.Services.Interfaces;
using DominoNext.Models.Music;
using DominoNext.ViewModels.Editor.Modules.Base;
using DominoNext.ViewModels.Editor.Services;
using System.Diagnostics;

namespace DominoNext.ViewModels.Editor.Modules
{
    /// <summary>
    /// 音符创建功能模块 - 基于分数的新实现
    /// 重构后使用基类和通用服务，减少重复代码
    /// </summary>
    public class NoteCreationModule : EditorModuleBase
    {
        private readonly AntiShakeService _antiShakeService;

        public override string ModuleName => "NoteCreation";

        // 创建状态
        public bool IsCreatingNote { get; private set; }
        public NoteViewModel? CreatingNote { get; private set; }
        public Point CreatingStartPosition { get; private set; }
        
        // 简化防抖动：只检查时间判断
        private DateTime _creationStartTime;

        public NoteCreationModule(ICoordinateService coordinateService) : base(coordinateService)
        {
            // 使用时间防抖配置，适合音符创建的短按/长按区分
            _antiShakeService = new AntiShakeService(new AntiShakeConfig
            {
                PixelThreshold = 2.0,
                TimeThresholdMs = 100.0,
                EnablePixelAntiShake = false, // 音符创建主要依赖时间防抖
                EnableTimeAntiShake = true
            });
        }

        /// <summary>
        /// 开始创建音符 - 使用基类的通用方法
        /// </summary>
        public void StartCreating(Point position)
        {
            if (_pianoRollViewModel == null) return;

            var pitch = GetPitchFromPosition(position);
            var timeValue = GetTimeFromPosition(position);

            Debug.WriteLine("=== StartCreatingNote ===");

            if (EditorValidationService.IsValidNotePosition(pitch, timeValue))
            {
                // 使用基类的通用量化方法
                var quantizedPosition = GetQuantizedTimeFromPosition(position);

                CreatingNote = new NoteViewModel
                {
                    Pitch = pitch,
                    StartPosition = quantizedPosition,
                    Duration = _pianoRollViewModel.UserDefinedNoteDuration,
                    Velocity = 100,
                    IsPreview = true
                };

                CreatingStartPosition = position;
                IsCreatingNote = true;
                _creationStartTime = DateTime.Now;

                Debug.WriteLine($"开始创建音符: Pitch={pitch}, Duration={CreatingNote.Duration}");
                OnCreationStarted?.Invoke();
            }
        }

        /// <summary>
        /// 更新创建中的音符长度 - 基于分数的新实现
        /// </summary>
        public void UpdateCreating(Point currentPosition)
        {
            if (!IsCreatingNote || CreatingNote == null || _pianoRollViewModel == null) return;

            var currentTimeValue = GetTimeFromPosition(currentPosition);
            var startValue = CreatingNote.StartPosition.ToDouble();

            // 计算音符的长度
            var minDuration = _pianoRollViewModel.GridQuantization.ToDouble();
            var actualDuration = Math.Max(minDuration, currentTimeValue - startValue);

            if (actualDuration > 0)
            {
                var startFraction = CreatingNote.StartPosition;
                var endValue = startValue + actualDuration;
                var endFraction = MusicalFraction.FromDouble(endValue);
                
                var duration = MusicalFraction.CalculateQuantizedDuration(startFraction, endFraction, _pianoRollViewModel.GridQuantization);

                // 只在长度发生改变时更新
                if (!CreatingNote.Duration.Equals(duration))
                {
                    Debug.WriteLine($"实时调整音符长度: {CreatingNote.Duration} -> {duration}");
                    CreatingNote.Duration = duration;
                    SafeInvalidateNoteCache(CreatingNote);

                    OnCreationUpdated?.Invoke();
                }
            }
        }

        /// <summary>
        /// 完成创建音符 - 使用统一的防抖方法
        /// </summary>
        public void FinishCreating()
        {
            if (IsCreatingNote && CreatingNote != null && _pianoRollViewModel != null)
            {
                MusicalFraction finalDuration;

                // 使用防抖服务判断
                if (_antiShakeService.IsShortPress(_creationStartTime))
                {
                    // 短按：使用用户预定义时值
                    finalDuration = _pianoRollViewModel.UserDefinedNoteDuration;
                    Debug.WriteLine($"短按，创建音符使用预设时值: {finalDuration}");
                }
                else
                {
                    // 长按：使用拖拽的长度
                    finalDuration = CreatingNote.Duration;
                    Debug.WriteLine($"长按，创建音符使用拖拽时值: {finalDuration}");
                }

                // 创建最终音符
                var finalNote = new NoteViewModel
                {
                    Pitch = CreatingNote.Pitch,
                    StartPosition = CreatingNote.StartPosition,
                    Duration = finalDuration,
                    Velocity = CreatingNote.Velocity,
                    TrackIndex = _pianoRollViewModel.CurrentTrackIndex, // 设置为当前音轨
                    IsPreview = false
                };

                // 添加到音符集合，这将自动调用UpdateMaxScrollExtent等
                _pianoRollViewModel.Notes.Add(finalNote);

                // 只有长按时才更新用户预设长度
                if (!_antiShakeService.IsShortPress(_creationStartTime))
                {
                    _pianoRollViewModel.SetUserDefinedNoteDuration(CreatingNote.Duration);
                    Debug.WriteLine($"更新用户自定义长度为: {CreatingNote.Duration}");
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
    }
}