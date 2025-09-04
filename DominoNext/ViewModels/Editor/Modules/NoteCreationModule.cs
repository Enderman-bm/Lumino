using System;
using Avalonia;
using DominoNext.Services.Interfaces;
using DominoNext.Models.Music;
using System.Diagnostics;

namespace DominoNext.ViewModels.Editor.Modules
{
    /// <summary>
    /// 音符创建功能模块 - 基于分数的新实现
    /// </summary>
    public class NoteCreationModule
    {
        private readonly ICoordinateService _coordinateService;
        private PianoRollViewModel? _pianoRollViewModel;

        // 创建状态
        public bool IsCreatingNote { get; private set; }
        public NoteViewModel? CreatingNote { get; private set; }
        public Point CreatingStartPosition { get; private set; }
        
        // 简化防抖动：只检查时间判断
        private DateTime _creationStartTime;
        
        // 可调整的防抖动时间阈值（毫秒）
        // 如果需要修改防抖动时间，请修改这个常量
        private const double ANTI_SHAKE_THRESHOLD_MS = 100.0;

        public NoteCreationModule(ICoordinateService coordinateService)
        {
            _coordinateService = coordinateService;
        }

        public void SetPianoRollViewModel(PianoRollViewModel viewModel)
        {
            _pianoRollViewModel = viewModel;
        }

        /// <summary>
        /// 开始创建音符 - 基于分数的新实现
        /// </summary>
        public void StartCreating(Point position)
        {
            if (_pianoRollViewModel == null) return;

            // 使用支持滚动偏移量的坐标转换方法
            var pitch = _pianoRollViewModel.GetPitchFromScreenY(position.Y);
            var timeValue = _pianoRollViewModel.GetTimeFromScreenX(position.X);

            Debug.WriteLine("=== StartCreatingNote ===");

            if (IsValidNotePosition(pitch, timeValue))
            {
                // 转换为分数并量化
                var timeFraction = MusicalFraction.FromDouble(timeValue);
                var quantizedPosition = _pianoRollViewModel.SnapToGrid(timeFraction);

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

            // 使用支持滚动偏移量的坐标转换方法
            var currentTimeValue = _pianoRollViewModel.GetTimeFromScreenX(currentPosition.X);
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
                    CreatingNote.InvalidateCache();

                    OnCreationUpdated?.Invoke();
                }
            }
        }

        /// <summary>
        /// 完成创建音符 - 简化防抖版本
        /// </summary>
        public void FinishCreating()
        {
            if (IsCreatingNote && CreatingNote != null && _pianoRollViewModel != null)
            {
                var holdTimeMs = (DateTime.Now - _creationStartTime).TotalMilliseconds;
                
                MusicalFraction finalDuration;

                // 防抖判断：只检查按住时间
                if (holdTimeMs < ANTI_SHAKE_THRESHOLD_MS)
                {
                    // 短按：使用用户预定义时值
                    finalDuration = _pianoRollViewModel.UserDefinedNoteDuration;
                    Debug.WriteLine($"短按创建音符 ({holdTimeMs:F0}ms < {ANTI_SHAKE_THRESHOLD_MS}ms)，使用预定时值: {finalDuration}");
                }
                else
                {
                    // 长按：使用拖拽的长度
                    finalDuration = CreatingNote.Duration;
                    Debug.WriteLine($"长按创建音符 ({holdTimeMs:F0}ms >= {ANTI_SHAKE_THRESHOLD_MS}ms)，使用拖拽时值: {finalDuration}");
                }

                // 创建最终音符
                var finalNote = new NoteViewModel
                {
                    Pitch = CreatingNote.Pitch,
                    StartPosition = CreatingNote.StartPosition,
                    Duration = finalDuration,
                    Velocity = CreatingNote.Velocity,
                    IsPreview = false
                };

                _pianoRollViewModel.Notes.Add(finalNote);

                // 只有长拖拽时才更新用户预设长度
                if (holdTimeMs >= ANTI_SHAKE_THRESHOLD_MS)
                {
                    _pianoRollViewModel.SetUserDefinedNoteDuration(CreatingNote.Duration);
                    Debug.WriteLine($"更新用户自定义长度为: {CreatingNote.Duration}");
                }

                Debug.WriteLine($"完成创建音符: {finalNote.Duration}");
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

        private bool IsValidNotePosition(int pitch, double timeValue)
        {
            return pitch >= 0 && pitch <= 127 && timeValue >= 0;
        }

        // 事件
        public event Action? OnCreationStarted;
        public event Action? OnCreationUpdated;
        public event Action? OnCreationCompleted;
        public event Action? OnCreationCancelled;
    }
}