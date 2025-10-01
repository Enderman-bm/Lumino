using System;
using System.Threading.Tasks;
using Avalonia;
using DominoNext.Services.Interfaces;
using DominoNext.Models.Music;
using DominoNext.ViewModels.Editor.Modules.Base;
using DominoNext.ViewModels.Editor.Services;
using System.Diagnostics;
using EnderWaveTableAccessingParty.Services;
using EnderWaveTableAccessingParty.Models;

namespace DominoNext.ViewModels.Editor.Modules
{
    /// <summary>
    /// ������������ģ�� - ���ڷ�������ʵ��
    /// �ع���ʹ�û����ͨ�÷��񣬼����ظ�����
    /// </summary>
    public class NoteCreationModule : EditorModuleBase
    {
        private readonly AntiShakeService _antiShakeService;
        private readonly IMidiPlaybackService _midiPlaybackService;

        public override string ModuleName => "NoteCreation";

        // ����״̬
        public bool IsCreatingNote { get; private set; }
        public NoteViewModel? CreatingNote { get; private set; }
        public Point CreatingStartPosition { get; private set; }
        
        // �򻯷�������ֻ���ʱ���ж�
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
            _midiPlaybackService = new MidiPlaybackService();
            
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
        /// ��ʼ�������� - ʹ�û����ͨ�÷���
        /// </summary>
        public void StartCreating(Point position)
        {
            if (_pianoRollViewModel == null) return;

            var pitch = GetPitchFromPosition(position);
            var timeValue = GetTimeFromPosition(position);

            Debug.WriteLine("=== StartCreatingNote ===");

            if (EditorValidationService.IsValidNotePosition(pitch, timeValue))
            {
                // ʹ�û����ͨ����������
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

                Debug.WriteLine($"��ʼ��������: Pitch={pitch}, Duration={CreatingNote.Duration}");
                OnCreationStarted?.Invoke();
            }
        }

        /// <summary>
        /// ���´����е��������� - ���ڷ�������ʵ��
        /// </summary>
        public void UpdateCreating(Point currentPosition)
        {
            if (!IsCreatingNote || CreatingNote == null || _pianoRollViewModel == null) return;

            var currentTimeValue = GetTimeFromPosition(currentPosition);
            var startValue = CreatingNote.StartPosition.ToDouble();

            // ���������ĳ���
            var minDuration = _pianoRollViewModel.GridQuantization.ToDouble();
            var actualDuration = Math.Max(minDuration, currentTimeValue - startValue);

            if (actualDuration > 0)
            {
                var startFraction = CreatingNote.StartPosition;
                var endValue = startValue + actualDuration;
                var endFraction = MusicalFraction.FromDouble(endValue);
                
                var duration = MusicalFraction.CalculateQuantizedDuration(startFraction, endFraction, _pianoRollViewModel.GridQuantization);

                // ֻ�ڳ��ȷ����ı�ʱ����
                if (!CreatingNote.Duration.Equals(duration))
                {
                    Debug.WriteLine($"ʵʱ������������: {CreatingNote.Duration} -> {duration}");
                    CreatingNote.Duration = duration;
                    SafeInvalidateNoteCache(CreatingNote);

                    OnCreationUpdated?.Invoke();
                }
            }
        }

        /// <summary>
        /// ��ɴ������� - ʹ��ͳһ�ķ�������
        /// </summary>
        public void FinishCreating()
        {
            if (IsCreatingNote && CreatingNote != null && _pianoRollViewModel != null)
            {
                MusicalFraction finalDuration;

                // ʹ�÷��������ж�
                if (_antiShakeService.IsShortPress(_creationStartTime))
                {
                    // �̰���ʹ���û�Ԥ����ʱֵ
                    finalDuration = _pianoRollViewModel.UserDefinedNoteDuration;
                    Debug.WriteLine($"�̰�����������ʹ��Ԥ��ʱֵ: {finalDuration}");
                }
                else
                {
                    // ������ʹ����ק�ĳ���
                    finalDuration = CreatingNote.Duration;
                    Debug.WriteLine($"��������������ʹ����קʱֵ: {finalDuration}");
                }

                // ������������
                var finalNote = new NoteViewModel
                {
                    Pitch = CreatingNote.Pitch,
                    StartPosition = CreatingNote.StartPosition,
                    Duration = finalDuration,
                    Velocity = CreatingNote.Velocity,
                    TrackIndex = _pianoRollViewModel.CurrentTrackIndex, // ����Ϊ��ǰ����
                    IsPreview = false
                };

                // ���ӵ��������ϣ��⽫�Զ�����UpdateMaxScrollExtent��
                _pianoRollViewModel.Notes.Add(finalNote);

                // ֻ�г���ʱ�Ÿ����û�Ԥ�賤��
                if (!_antiShakeService.IsShortPress(_creationStartTime))
                {
                    _pianoRollViewModel.SetUserDefinedNoteDuration(CreatingNote.Duration);
                    Debug.WriteLine($"�����û��Զ��峤��Ϊ: {CreatingNote.Duration}");
                }

                // ������������
                try
                {
                    if (_midiPlaybackService.IsInitialized)
                    {
                        _ = Task.Run(async () =>
                        {
                            await _midiPlaybackService.PlayNoteAsync(CreatingNote.Pitch, CreatingNote.Velocity, 200, 0);
                        });
                        Debug.WriteLine($"��������: Pitch={CreatingNote.Pitch}, Velocity={CreatingNote.Velocity}");
                    }
                    else
                    {
                        Debug.WriteLine($"MIDI播放服务未初始化，跳过音频播放");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"��������ʧ��: {ex.Message}");
                }

                Debug.WriteLine($"��ɴ�������: {finalNote.Duration}, TrackIndex: {finalNote.TrackIndex}");
            }

            ClearCreating();
            OnCreationCompleted?.Invoke();
        }

        /// <summary>
        /// ȡ����������
        /// </summary>
        public void CancelCreating()
        {
            if (IsCreatingNote)
            {
                Debug.WriteLine("ȡ����������");
            }

            ClearCreating();
            OnCreationCancelled?.Invoke();
        }

        private void ClearCreating()
        {
            IsCreatingNote = false;
            CreatingNote = null;
        }

        // �¼�
        public event Action? OnCreationStarted;
        public event Action? OnCreationUpdated;
        public event Action? OnCreationCompleted;
        public event Action? OnCreationCancelled;
    }
}