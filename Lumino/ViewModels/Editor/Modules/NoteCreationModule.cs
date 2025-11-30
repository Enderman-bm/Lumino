using System;
using System.Threading.Tasks;
using Avalonia;
using Lumino.Services.Interfaces;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Modules.Base;
using Lumino.ViewModels.Editor.Services;
using System.Diagnostics;
using EnderWaveTableAccessingParty.Services;
using EnderWaveTableAccessingParty.Models;
using EnderDebugger;

namespace Lumino.ViewModels.Editor.Modules
{
    /// <summary>
    /// 音符创建模块 - 用于实现音符创建功能
    /// 回放使用用户监听，兼顾重复创建
    /// </summary>
    public class NoteCreationModule : EditorModuleBase
    {
        private readonly AntiShakeService _antiShakeService;
        private readonly IMidiPlaybackService _midiPlaybackService;
        private readonly EnderLogger _logger;

        public override string ModuleName => "NoteCreation";

        // 创建状态
        public bool IsCreatingNote { get; private set; }
        public NoteViewModel? CreatingNote { get; private set; }
        public Point CreatingStartPosition { get; private set; }

        // 简化创建防抖，只按时间间隔判断
        private DateTime _creationStartTime;

        public NoteCreationModule(ICoordinateService coordinateService) : base(coordinateService)
        {
            // 使用时间间隔判断，适合短时间内的重复创建
            _antiShakeService = new AntiShakeService(new AntiShakeConfig
            {
                PixelThreshold = 2.0,
                TimeThresholdMs = 100.0,
                EnablePixelAntiShake = false, // 创建时不需要基于像素防抖
                EnableTimeAntiShake = true
            });

            // 初始化MIDI播放服务
            _logger = new EnderLogger("NoteCreationModule");
            _midiPlaybackService = new MidiPlaybackService(_logger);

            // 异步初始化MIDI播放服务
            _ = Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine("开始初始化MIDI播放服务...");
                    await _midiPlaybackService.InitializeAsync();

                    // 获取可用设备列表
                    var devices = await _midiPlaybackService.GetMidiDevicesAsync();
                    Debug.WriteLine($"找到 {devices.Count} 个MIDI设备");
                    foreach (var device in devices)
                    {
                        Debug.WriteLine($"MIDI设备: {device.Name} (ID: {device.DeviceId})");
                    }

                    // 获取可用播表列表
                    var waveTables = await _midiPlaybackService.GetWaveTablesAsync();
                    Debug.WriteLine($"找到 {waveTables.Count} 个播表");
                    foreach (var waveTable in waveTables)
                    {
                        Debug.WriteLine($"播表: {waveTable.Name} (ID: {waveTable.Id})");
                    }

                    // 设置默认播表为钢琴音色
                    await _midiPlaybackService.SetWaveTableAsync("default");
                    Debug.WriteLine("播表设置完成：default");

                    // 设置默认乐器为钢琴（程序0）
                    await _midiPlaybackService.ChangeInstrumentAsync(0, 0);
                    Debug.WriteLine("乐器设置完成：钢琴 (程序0)");

                    Debug.WriteLine("MIDI播放服务初始化成功，播表和乐器设置完成");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"MIDI播放服务初始化失败: {ex.Message}");
                    Debug.WriteLine($"详细错误: {ex.StackTrace}");
                }
            });
        }

        /// <summary>
        /// 开始创建音符 - 使用用户监听
        /// </summary>
        public void StartCreating(Point position)
        {
            if (_pianoRollViewModel == null) return;

            // 检查当前轨道是否为Conductor轨，如果是则禁止创建音符
            if (_pianoRollViewModel.IsCurrentTrackConductor)
            {
                _logger.Debug("NoteCreationModule", "禁止在Conductor轨上创建音符");
                return;
            }

            var pitch = GetPitchFromPosition(position);
            var timeValue = GetTimeFromPosition(position);

            Debug.WriteLine("=== StartCreatingNote ===");

            if (EditorValidationService.IsValidNotePosition(pitch, timeValue))
            {
                // 使用用户监听的音符位置
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

                // 在开始创建时立即播放音频反馈
                try
                {
                    if (_midiPlaybackService.IsInitialized)
                    {
                        // 捕获局部变量，避免闭包捕获字段引用导致空引用
                        var notePitch = CreatingNote.Pitch;
                        var noteVelocity = CreatingNote.Velocity;
                        
                        _ = Task.Run(async () =>
                        {
                            await _midiPlaybackService.PlayNoteAsync(notePitch, noteVelocity, 200, 0);
                        });
                        Debug.WriteLine($"播放音符反馈: Pitch={notePitch}, Velocity={noteVelocity}");
                    }
                    else
                    {
                        Debug.WriteLine($"MIDI播放服务未初始化，跳过音频反馈播放");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"播放音符反馈失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 更新正在创建的音符 - 使用用户监听
        /// </summary>
        public void UpdateCreating(Point currentPosition)
        {
            if (!IsCreatingNote || CreatingNote == null || _pianoRollViewModel == null) return;

            // 检查当前轨道是否为Conductor轨，如果是则禁止创建音符
            if (_pianoRollViewModel.IsCurrentTrackConductor)
            {
                _logger.Debug("NoteCreationModule", "禁止在Conductor轨上创建音符");
                return;
            }

            var currentTimeValue = GetTimeFromPosition(currentPosition);
            var startValue = CreatingNote.StartPosition.ToDouble();

            // 计算音符长度
            var minDuration = _pianoRollViewModel.GridQuantization.ToDouble();
            var actualDuration = Math.Max(minDuration, currentTimeValue - startValue);

            if (actualDuration > 0)
            {
                var startFraction = CreatingNote.StartPosition;
                var endValue = startValue + actualDuration;
                var endFraction = MusicalFraction.FromDouble(endValue);

                var duration = MusicalFraction.CalculateQuantizedDuration(startFraction, endFraction, _pianoRollViewModel.GridQuantization);

                // 只有在精确变化时更新
                if (!CreatingNote.Duration.Equals(duration))
                {
                    Debug.WriteLine($"实时更新音符长度: {CreatingNote.Duration} -> {duration}");
                    CreatingNote.Duration = duration;
                    SafeInvalidateNoteCache(CreatingNote);

                    OnCreationUpdated?.Invoke();
                }
            }
        }

        /// <summary>
        /// 完成创建音符 - 使用系统默认的音符长度
        /// </summary>
        public void FinishCreating()
        {
            if (IsCreatingNote && CreatingNote != null && _pianoRollViewModel != null)
            {
                // 检查当前轨道是否为Conductor轨，如果是则禁止创建音符
                if (_pianoRollViewModel.IsCurrentTrackConductor)
                {
                    _logger.Debug("NoteCreationModule", "禁止在Conductor轨上创建音符");
                    ClearCreating();
                    OnCreationCompleted?.Invoke();
                    return;
                }

                MusicalFraction finalDuration;

                // 使用防抖判断
                if (_antiShakeService.IsShortPress(_creationStartTime))
                {
                    // 短按使用用户预设长度
                    finalDuration = _pianoRollViewModel.UserDefinedNoteDuration;
                    Debug.WriteLine($"短按创建音符使用预设长度: {finalDuration}");
                }
                else
                {
                    // 长按使用计算长度
                    finalDuration = CreatingNote.Duration;
                    Debug.WriteLine($"长按创建音符使用计算长度: {finalDuration}");
                }

                // 创建最终音符
                var finalNote = new NoteViewModel
                {
                    Pitch = CreatingNote.Pitch,
                    StartPosition = CreatingNote.StartPosition,
                    Duration = finalDuration,
                    Velocity = CreatingNote.Velocity,
                    TrackIndex = _pianoRollViewModel.CurrentTrackIndex, // 设置为当前轨道
                    IsPreview = false
                };

                // 添加到音轨列表，自动触发UpdateMaxScrollExtent
                var addOperation = new Lumino.Services.Implementation.AddNoteOperation(_pianoRollViewModel, finalNote);
                _pianoRollViewModel.UndoRedoService.ExecuteAndRecord(addOperation);

                // 只有长按时更新用户预设长度
                if (!_antiShakeService.IsShortPress(_creationStartTime))
                {
                    _pianoRollViewModel.SetUserDefinedNoteDuration(CreatingNote.Duration);
                    Debug.WriteLine($"用户预设长度自动更新为: {CreatingNote.Duration}");
                }

                // 注意：不在此处播放音符反馈，因为已在 StartCreating() 中播放过了
                // 避免按下和松开时重复发声

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