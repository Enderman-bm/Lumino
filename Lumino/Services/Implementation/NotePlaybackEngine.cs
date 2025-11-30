using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnderDebugger;
using EnderWaveTableAccessingParty.Services;
using MidiReader;
using Lumino.Models.Music;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// 音符播放引擎 - 实时根据播放时间查询并演奏音符
    /// 集成KDMAPI实现高性能、低延迟的MIDI发声
    /// </summary>
    public class NotePlaybackEngine : IDisposable
    {
        private readonly EnderLogger _logger = EnderLogger.Instance;
        private readonly MidiPlaybackService _midiPlaybackService;
        private readonly PlaybackService _playbackService;

        // 活跃音符跟踪
        private readonly Dictionary<string, NotePlaybackState> _activeNotes; // Key: Note.Id
        private readonly object _notesLock = new object();

        // 演奏数据
        private List<Note>? _allNotes; // 所有音符
        private List<Note>? _sortedNotes; // 按开始位置排序的音符
        private int _nextNoteIndex = 0;
        private double _lastProcessedTime = 0.0;

        // MIDI时间参数
        private int _ticksPerQuarter = 480; // 默认480 TPQ
        private double _currentTempo = 500000.0; // 微秒/四分音符，默认120BPM

        private bool _isEnabled = true;
        private bool _disposed = false;

        public NotePlaybackEngine(MidiPlaybackService midiPlaybackService, PlaybackService playbackService)
        {
            _midiPlaybackService = midiPlaybackService ?? throw new ArgumentNullException(nameof(midiPlaybackService));
            _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
            _activeNotes = new Dictionary<string, NotePlaybackState>();

            // 订阅播放时间变化事件
            _playbackService.PlaybackTimeChanged += OnPlaybackTimeChanged;
            _playbackService.PlaybackStateChanged += OnPlaybackStateChanged;

            _logger.Info("NotePlaybackEngine", "音符播放引擎已初始化");
        }

        /// <summary>
        /// 加载音符列表用于演奏
        /// </summary>
        public void LoadNotes(List<Note> notes, int ticksPerQuarter = 480, double tempoInMicrosecondsPerQuarter = 500000.0)
        {
            lock (_notesLock)
            {
                _allNotes = notes ?? throw new ArgumentNullException(nameof(notes));
                _ticksPerQuarter = ticksPerQuarter;
                _currentTempo = tempoInMicrosecondsPerQuarter;

                // 按开始位置排序音符
                _sortedNotes = _allNotes
                    .OrderBy(n => ConvertFractionToSeconds(n.StartPosition))
                    .ToList();

                _nextNoteIndex = 0;
                _lastProcessedTime = 0.0;

                _logger.Info("NotePlaybackEngine", $"已加载{_sortedNotes.Count}个音符，TPQ={_ticksPerQuarter}, Tempo={_currentTempo}μs/quarter");
                
                // 输出前5个音符的时间信息用于调试
                for (int i = 0; i < Math.Min(5, _sortedNotes.Count); i++)
                {
                    var n = _sortedNotes[i];
                    double startSec = ConvertFractionToSeconds(n.StartPosition);
                    _logger.Debug("NotePlaybackEngine", $"  音符[{i}]: Pitch={n.Pitch}, StartPos={n.StartPosition}, StartSec={startSec:F3}s");
                }
            }
        }

        /// <summary>
        /// 启用/禁用引擎
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        /// <summary>
        /// MusicalFraction转换为秒
        /// </summary>
        private double ConvertFractionToSeconds(MusicalFraction fraction)
        {
            // MusicalFraction.ToDouble() 返回以四分音符为单位的值
            double totalQuarters = fraction.ToDouble();
            
            // 将四分音符数转换为秒
            // 秒 = 四分音符数 × (微秒/四分音符) / 1,000,000
            double seconds = (totalQuarters * _currentTempo) / 1_000_000.0;
            
            return seconds;
        }

        /// <summary>
        /// 计算音符的持续时间（秒）
        /// </summary>
        private double GetNoteDurationSeconds(Note note)
        {
            double totalQuarters = note.Duration.ToDouble();
            return (totalQuarters * _currentTempo) / 1_000_000.0;
        }

        /// <summary>
        /// 播放时间变化回调 - 实时检查要演奏的音符
        /// </summary>
        private void OnPlaybackTimeChanged(object? sender, PlaybackTimeChangedEventArgs e)
        {
            if (!_isEnabled || _sortedNotes == null || _sortedNotes.Count == 0)
                return;

            try
            {
                double currentTime = e.CurrentTime;
                
                // 每秒输出一次调试信息
                if ((int)(currentTime * 2) > (int)(_lastProcessedTime * 2))
                {
                    _logger.Debug("NotePlaybackEngine", $"播放时间: {currentTime:F2}s, 下一个音符索引: {_nextNoteIndex}/{_sortedNotes.Count}");
                }

                // 如果时间倒退（Seek操作），重置状态
                if (currentTime < _lastProcessedTime - 0.05) // 允许50ms的误差
                {
                    lock (_notesLock)
                    {
                        // 停止所有活跃音符
                        StopAllNotesInternal();
                        _nextNoteIndex = 0;

                        // 重新查找应该开始播放的音符
                        for (int i = 0; i < _sortedNotes.Count; i++)
                        {
                            if (ConvertFractionToSeconds(_sortedNotes[i].StartPosition) > currentTime)
                            {
                                _nextNoteIndex = i;
                                break;
                            }
                            _nextNoteIndex = i + 1;
                        }
                    }
                }

                _lastProcessedTime = currentTime;

                // 处理新的音符开始事件
                ProcessNoteOn(currentTime);

                // 处理音符结束事件
                ProcessNoteOff(currentTime);
            }
            catch (Exception ex)
            {
                _logger.Error("NotePlaybackEngine", $"播放时间处理错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 播放状态变化回调
        /// </summary>
        private void OnPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
        {
            if (e.State == PlaybackState.Stopped || e.State == PlaybackState.Paused)
            {
                // 暂停或停止时，停止所有活跃音符
                lock (_notesLock)
                {
                    StopAllNotesInternal();
                }
            }
        }

        /// <summary>
        /// 处理音符开始 - 查询当前时间应该开始的所有音符
        /// </summary>
        private void ProcessNoteOn(double currentTime)
        {
            if (_sortedNotes == null)
                return;

            lock (_notesLock)
            {
                // 处理所有应该现在开始的音符
                while (_nextNoteIndex < _sortedNotes.Count)
                {
                    var note = _sortedNotes[_nextNoteIndex];
                    double noteStartTime = ConvertFractionToSeconds(note.StartPosition);

                    // 检查音符是否应该开始（允许50ms提前量）
                    if (noteStartTime > currentTime + 0.05)
                        break; // 音符还未到达，退出循环

                    // 音符时间已到或即将到达，发送Note On事件
                    SendNoteOn(note);
                    _activeNotes[note.Id.ToString()] = new NotePlaybackState
                    {
                        Note = note,
                        StartTime = noteStartTime,
                        DurationSeconds = GetNoteDurationSeconds(note)
                    };

                    _nextNoteIndex++;
                }
            }
        }

        /// <summary>
        /// 处理音符结束 - 停止已到期的活跃音符
        /// </summary>
        private void ProcessNoteOff(double currentTime)
        {
            lock (_notesLock)
            {
                var notesToStop = _activeNotes
                    .Where(kvp => kvp.Value.StartTime + kvp.Value.DurationSeconds <= currentTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in notesToStop)
                {
                    if (_activeNotes.TryGetValue(key, out var state))
                    {
                        SendNoteOff(state.Note);
                        _activeNotes.Remove(key);
                    }
                }
            }
        }

        /// <summary>
        /// 停止所有活跃音符（内部方法，需要调用方持有锁）
        /// </summary>
        private void StopAllNotesInternal()
        {
            var activeNotesCopy = _activeNotes.Values.ToList();
            foreach (var state in activeNotesCopy)
            {
                SendNoteOff(state.Note);
            }
            _activeNotes.Clear();
        }

        /// <summary>
        /// 停止所有活跃音符（公开方法）
        /// </summary>
        public void StopAllNotes()
        {
            lock (_notesLock)
            {
                StopAllNotesInternal();
            }
        }

        /// <summary>
        /// 发送Note On事件到KDMAPI
        /// </summary>
        private void SendNoteOn(Note note)
        {
            try
            {
                // 构建MIDI消息：Note On, Channel, Pitch, Velocity
                // MIDI消息格式: [Status][Pitch][Velocity]
                // Status = 0x90 | Channel (Note On, Channel 1-16)
                // 注意：音轨索引需要映射到MIDI通道（0-15）
                int midiChannel = (note.TrackIndex) % 16; // 循环使用16个通道

                uint status = (uint)(0x90 | (midiChannel & 0x0F));
                uint pitch = (uint)(note.Pitch & 0x7F);
                uint velocity = (uint)(note.Velocity & 0x7F);

                // 组合成32位MIDI消息 (dwData)
                uint midiMessage = status | (pitch << 8) | (velocity << 16);

                // 通过KDMAPI发送
                _midiPlaybackService.SendMidiMessage(midiMessage);

                // 调试日志
                _logger.Debug("NotePlaybackEngine", 
                    $"✓ Note On: Track={note.TrackIndex} Channel={midiChannel} Pitch={note.Pitch} Velocity={note.Velocity} Message=0x{midiMessage:X8}");
            }
            catch (Exception ex)
            {
                _logger.Warn("NotePlaybackEngine", $"✗ 发送 Note On 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送Note Off事件到KDMAPI
        /// </summary>
        private void SendNoteOff(Note note)
        {
            try
            {
                // 构建MIDI消息：Note Off, Channel, Pitch, Velocity
                // Status = 0x80 | Channel (Note Off, Channel 1-16)
                int midiChannel = (note.TrackIndex) % 16;

                uint status = (uint)(0x80 | (midiChannel & 0x0F));
                uint pitch = (uint)(note.Pitch & 0x7F);
                uint velocity = (uint)(note.Velocity & 0x7F);

                // 组合成32位MIDI消息
                uint midiMessage = status | (pitch << 8) | (velocity << 16);

                // 通过KDMAPI发送
                _midiPlaybackService.SendMidiMessage(midiMessage);

                // 调试日志
                _logger.Debug("NotePlaybackEngine", 
                    $"Note Off: Track={note.TrackIndex} Ch={midiChannel} Pitch={note.Pitch}");
            }
            catch (Exception ex)
            {
                _logger.Warn("NotePlaybackEngine", $"发送Note Off失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前活跃音符数量
        /// </summary>
        public int GetActiveNoteCount()
        {
            lock (_notesLock)
            {
                return _activeNotes.Count;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _playbackService.PlaybackTimeChanged -= OnPlaybackTimeChanged;
            _playbackService.PlaybackStateChanged -= OnPlaybackStateChanged;

            StopAllNotes();
            _disposed = true;
        }

        /// <summary>
        /// 音符播放状态
        /// </summary>
        private class NotePlaybackState
        {
            public Note Note { get; set; } = null!;
            public double StartTime { get; set; }
            public double DurationSeconds { get; set; }
        }
    }
}
