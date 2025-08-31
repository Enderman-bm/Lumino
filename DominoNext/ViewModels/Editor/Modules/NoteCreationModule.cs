using System;
using Avalonia;
using DominoNext.Services.Interfaces;
using DominoNext.Models.Music;
using System.Diagnostics;

namespace DominoNext.ViewModels.Editor.Modules
{
    /// <summary>
    /// 创建音符模块 - 通过鼠标创建音符
    /// </summary>
    public class NoteCreationModule
    {
        private readonly ICoordinateService _coordinateService;
        private PianoRollViewModel? _pianoRollViewModel;

        // 创建状态
        public bool IsCreatingNote { get; private set; }
        public NoteViewModel? CreatingNote { get; private set; }
        public Point CreatingStartPosition { get; private set; }
        
        // 创建音符的时间戳，仅在创建时记录
        private DateTime _creationStartTime;
        
        // 防抖动的阈值，单位为毫秒
        // 如果需要修改防抖动阈值，需要修改这里的值
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
        /// 开始创建音符
        /// </summary>
        public void StartCreating(Point position)
        {
            Debug.WriteLine($"=== NoteCreationModule.StartCreating ===");
            Debug.WriteLine($"Input position: {position}");
            Debug.WriteLine($"_coordinateService exists: {_coordinateService != null}");
            Debug.WriteLine($"_pianoRollViewModel exists: {_pianoRollViewModel != null}");

            if (_pianoRollViewModel == null) 
            {
                Debug.WriteLine("ERROR: _pianoRollViewModel is null");
                return;
            }

            if (_coordinateService == null)
            {
                Debug.WriteLine("ERROR: _coordinateService is null");
                return;
            }

            // 检查位置是否在有效范围内
            if (position.X < 0 || position.Y < 0)
            {
                Debug.WriteLine($"Invalid position: X={position.X}, Y={position.Y}");
                return;
            }

            try
            {
                var pitch = _coordinateService.GetPitchFromY(position.Y, _pianoRollViewModel.KeyHeight);
                var startTime = _coordinateService.GetTimeFromX(position.X, _pianoRollViewModel.Zoom, _pianoRollViewModel.PixelsPerTick);

                Debug.WriteLine($"Calculated pitch: {pitch}, startTime: {startTime}");

                if (!IsValidNotePosition(pitch, startTime))
                {
                    Debug.WriteLine($"Invalid note position: pitch={pitch}, startTime={startTime}");
                    return;
                }

                // FL Studio风格：音高对齐到网格
                var snappedPitch = _pianoRollViewModel.SnapToGridPitch(pitch);
                var quantizedStartTime = _pianoRollViewModel.SnapToGridTime(startTime);
                var quantizedPosition = MusicalFraction.FromTicks(quantizedStartTime, _pianoRollViewModel.TicksPerBeat);

                CreatingNote = new NoteViewModel
                {
                    Pitch = snappedPitch,
                    StartPosition = quantizedPosition,
                    Duration = _pianoRollViewModel.UserDefinedNoteDuration,
                    Velocity = 100,
                    IsPreview = true
                };

                CreatingStartPosition = position;
                IsCreatingNote = true;
                _creationStartTime = DateTime.Now;

                Debug.WriteLine($"开始创建音符: Pitch={snappedPitch}, Start={quantizedPosition}, Duration={CreatingNote.Duration}");
                OnCreationStarted?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in StartCreating: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 快速创建单个音符（FL Studio单击模式）
        /// </summary>
        public void QuickCreateNote(Point position)
        {
            Debug.WriteLine($"=== NoteCreationModule.QuickCreateNote ===");
            
            if (_pianoRollViewModel == null || _coordinateService == null)
            {
                Debug.WriteLine("ERROR: Required services not available");
                return;
            }

            try
            {
                var pitch = _coordinateService.GetPitchFromY(position.Y, _pianoRollViewModel.KeyHeight);
                var startTime = _coordinateService.GetTimeFromX(position.X, _pianoRollViewModel.Zoom, _pianoRollViewModel.PixelsPerTick);

                if (!IsValidNotePosition(pitch, startTime))
                {
                    Debug.WriteLine($"Invalid note position: pitch={pitch}, startTime={startTime}");
                    return;
                }

                // FL Studio风格：立即创建音符
                var snappedPitch = _pianoRollViewModel.SnapToGridPitch(pitch);
                var quantizedStartTime = _pianoRollViewModel.SnapToGridTime(startTime);
                var quantizedPosition = MusicalFraction.FromTicks(quantizedStartTime, _pianoRollViewModel.TicksPerBeat);

                var newNote = new NoteViewModel
                {
                    Pitch = snappedPitch,
                    StartPosition = quantizedPosition,
                    Duration = _pianoRollViewModel.UserDefinedNoteDuration,
                    Velocity = 100,
                    IsPreview = false
                };

                _pianoRollViewModel.Notes.Add(newNote);
                _pianoRollViewModel?.SubscribeToNoteEvents(newNote);

                Debug.WriteLine($"快速创建音符: Pitch={snappedPitch}, Start={quantizedPosition}");
                OnQuickNoteCreated?.Invoke(newNote);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in QuickCreateNote: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新正在创建的音符
        /// </summary>
        public void UpdateCreating(Point currentPosition)
        {
            if (!IsCreatingNote || CreatingNote == null || _pianoRollViewModel == null) return;

            var currentTime = _pianoRollViewModel.GetTimeFromX(currentPosition.X);
            var startTime = CreatingNote.StartPosition.ToTicks(_pianoRollViewModel.TicksPerBeat);

            // 计算音符的最小长度
            var minDuration = _pianoRollViewModel.GridQuantization.ToTicks(_pianoRollViewModel.TicksPerBeat);
            var actualDuration = Math.Max(minDuration, currentTime - startTime);

            if (actualDuration > 0)
            {
                var duration = MusicalFraction.CalculateQuantizedDuration(
                    startTime, startTime + actualDuration, _pianoRollViewModel.GridQuantization, _pianoRollViewModel.TicksPerBeat);

                // 仅在音符长度发生变化时更新
                if (!CreatingNote.Duration.Equals(duration))
                {
                    Debug.WriteLine($"实时更新音符长度: {CreatingNote.Duration} -> {duration}");
                    CreatingNote.Duration = duration;
                    CreatingNote.InvalidateCache();

                    OnCreationUpdated?.Invoke();
                }
            }
        }

        /// <summary>
        /// 完成创建音符 - 通过鼠标创建音符
        /// </summary>
        public void FinishCreating()
        {
            if (IsCreatingNote && CreatingNote != null && _pianoRollViewModel != null)
            {
                var holdTimeMs = (DateTime.Now - _creationStartTime).TotalMilliseconds;
                
                MusicalFraction finalDuration;

                // 防抖动判断，仅在抖动时使用
                if (holdTimeMs < ANTI_SHAKE_THRESHOLD_MS)
                {
                    // 默认使用用户预设的长度
                    finalDuration = _pianoRollViewModel.UserDefinedNoteDuration;
                    Debug.WriteLine($"防抖动判断 ({holdTimeMs:F0}ms < {ANTI_SHAKE_THRESHOLD_MS}ms) 使用预设长度: {finalDuration}");
                }
                else
                {
                    // 否则使用拖拽的长度
                    finalDuration = CreatingNote.Duration;
                    Debug.WriteLine($"防抖动判断 ({holdTimeMs:F0}ms >= {ANTI_SHAKE_THRESHOLD_MS}ms) 使用拖拽长度: {finalDuration}");
                }

                // 创建最终的音符
                var finalNote = new NoteViewModel
                {
                    Pitch = CreatingNote.Pitch,
                    StartPosition = CreatingNote.StartPosition,
                    Duration = finalDuration,
                    Velocity = CreatingNote.Velocity,
                    IsPreview = false
                };

                _pianoRollViewModel.Notes.Add(finalNote);
                
                // 订阅新创建音符的事件
                _pianoRollViewModel?.SubscribeToNoteEvents(finalNote);

                // 只在拖拽时间足够时才更新用户预设
                if (holdTimeMs >= ANTI_SHAKE_THRESHOLD_MS)
                {
                    _pianoRollViewModel.UserDefinedNoteDuration = CreatingNote.Duration;
                    Debug.WriteLine($"更新用户预设为: {_pianoRollViewModel.UserDefinedNoteDuration}");
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

        private bool IsValidNotePosition(int pitch, double startTime)
        {
            return pitch >= 0 && pitch <= 127 && startTime >= 0;
        }

        // 事件
        public event Action? OnCreationStarted;
        public event Action? OnCreationUpdated;
        public event Action? OnCreationCompleted;
        public event Action<NoteViewModel>? OnQuickNoteCreated;
        public event Action? OnCreationCancelled;
    }
}