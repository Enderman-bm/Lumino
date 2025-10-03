using Lumino.Models.Music;
using Lumino.Services.Interfaces;
using MidiReader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// 项目存储服务实现类
    /// 负责项目的保存、加载和MIDI文件的导入导出
    /// </summary>
    public class ProjectStorageService : IProjectStorageService
    {
        private readonly EnderDebugger.EnderLogger _logger = EnderDebugger.EnderLogger.Instance;
        private const int DEFAULT_TICKS_PER_BEAT = 96;

        /// <summary>
        /// 保存项目到文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="notes">音符集合</param>
        /// <param name="metadata">项目元数据</param>
        /// <returns>是否保存成功</returns>
        public async Task<bool> SaveProjectAsync(string filePath, IEnumerable<Note> notes, ProjectMetadata metadata)
        {
            try
            {
                _logger.Info("ProjectStorageService", $"[EnderDebugger][{DateTime.Now}][EnderLogger][ProjectStorageService] 开始保存项目到文件: {filePath}");
                // TODO: 实现项目保存功能
                await Task.Delay(100); // 占位，实际实现时移除
                _logger.Info("ProjectStorageService", $"[EnderDebugger][{DateTime.Now}][EnderLogger][ProjectStorageService] 项目保存成功: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("ProjectStorageService", $"[EnderDebugger][{DateTime.Now}][EnderLogger][ProjectStorageService] 项目保存失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从文件加载项目
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>音符集合和项目元数据</returns>
        public async Task<(IEnumerable<Note> notes, ProjectMetadata metadata)> LoadProjectAsync(string filePath)
        {
            try
            {
                // TODO: 实现项目加载功能
                await Task.Delay(100); // 占位，实际实现时移除
                return (new List<Note>(), new ProjectMetadata());
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 导出MIDI文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="notes">音符集合</param>
        /// <returns>是否导出成功</returns>
        public async Task<bool> ExportMidiAsync(string filePath, IEnumerable<Note> notes)
        {
            try
            {
                await Task.Run(() =>
                {
                    // 按音轨分组音符
                    var notesByTrack = notes.GroupBy(n => n.TrackIndex).ToList();
                    int trackCount = Math.Max(1, notesByTrack.Count);

                    // 创建MIDI文件头
                    var header = new MidiFileHeader(MidiFileFormat.MultipleTracksParallel, (ushort)trackCount, (ushort)DEFAULT_TICKS_PER_BEAT);

                    // 创建轨道列表
                    var tracks = new List<List<MidiEvent>>();

                    // 为每个音轨创建MIDI事件
                    for (int trackIndex = 0; trackIndex < trackCount; trackIndex++)
                    {
                        var trackEvents = new List<MidiEvent>();

                        // 添加轨道名称元事件
                        var trackName = System.Text.Encoding.UTF8.GetBytes($"Track {trackIndex + 1}");
                        trackEvents.Add(new MidiEvent(0, MidiEventType.MetaEvent, 0, (byte)MetaEventType.TrackName, 0, trackName));

                        // 获取该音轨的音符
                        var trackNotes = notesByTrack.FirstOrDefault(g => g.Key == trackIndex)?.ToList() ?? new List<Note>();

                        // 转换音符为MIDI事件
                        var midiEvents = ConvertNotesToMidiEvents(trackNotes, DEFAULT_TICKS_PER_BEAT);

                        // 添加事件到轨道
                        trackEvents.AddRange(midiEvents);

                        // 添加轨道结束事件
                        trackEvents.Add(new MidiEvent(0, MidiEventType.MetaEvent, 0, (byte)MetaEventType.EndOfTrack));

                        tracks.Add(trackEvents);
                    }

                    // 写入MIDI文件
                    WriteMidiFile(filePath, header, tracks);
                });

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 将音符转换为MIDI事件
        /// </summary>
        /// <param name="notes">音符集合</param>
        /// <param name="ticksPerBeat">每拍的tick数</param>
        /// <returns>MIDI事件列表</returns>
        private List<MidiEvent> ConvertNotesToMidiEvents(List<Note> notes, int ticksPerBeat)
        {
            var events = new List<MidiEvent>();

            // 创建开始时间事件对列表 (startTime, noteOnEvent)
            var noteOnEvents = new List<(long time, MidiEvent evt)>();
            var noteOffEvents = new List<(long time, MidiEvent evt)>();

            foreach (var note in notes)
            {
                // 计算开始和结束时间（以tick为单位）
                long startTime = ConvertMusicalFractionToTicks(note.StartPosition, ticksPerBeat);
                long endTime = ConvertMusicalFractionToTicks(note.StartPosition + note.Duration, ticksPerBeat);
                long duration = endTime - startTime;

                // 确保持续时间大于0
                if (duration <= 0)
                    duration = 1;

                // 创建Note On事件
                var noteOn = new MidiEvent(0, MidiEventType.NoteOn, 0, (byte)note.Pitch, (byte)note.Velocity);
                noteOnEvents.Add((startTime, noteOn));

                // 创建Note Off事件
                var noteOff = new MidiEvent(0, MidiEventType.NoteOff, 0, (byte)note.Pitch, (byte)0);
                noteOffEvents.Add((endTime, noteOff));
            }

            // 对事件按时间排序
            noteOnEvents.Sort((a, b) => a.time.CompareTo(b.time));
            noteOffEvents.Sort((a, b) => a.time.CompareTo(b.time));

            // 合并所有事件并按时间排序
            var allEvents = new List<(long time, MidiEvent evt)>();
            allEvents.AddRange(noteOnEvents);
            allEvents.AddRange(noteOffEvents);
            allEvents.Sort((a, b) => a.time.CompareTo(b.time));

            // 计算delta time并创建最终事件列表
            long currentTime = 0;
            foreach (var (time, evt) in allEvents)
            {
                uint deltaTime = (uint)(time - currentTime);
                events.Add(new MidiEvent(deltaTime, evt.EventType, evt.Channel, evt.Data1, evt.Data2, evt.AdditionalData));
                currentTime = time;
            }

            return events;
        }

        /// <summary>
        /// 将MusicalFraction转换为tick值
        /// </summary>
        /// <param name="fraction">音乐分数</param>
        /// <param name="ticksPerBeat">每拍的tick数</param>
        /// <returns>tick值</returns>
        private long ConvertMusicalFractionToTicks(MusicalFraction fraction, int ticksPerBeat)
        {
            // 将以四分音符为单位的值转换为tick
            double quarterNotes = fraction.ToDouble();
            return (long)Math.Round(quarterNotes * ticksPerBeat);
        }

        /// <summary>
        /// 写入MIDI文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="header">MIDI文件头</param>
        /// <param name="tracks">轨道事件列表</param>
        private void WriteMidiFile(string filePath, MidiFileHeader header, List<List<MidiEvent>> tracks)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(fileStream))
            {
                // 写入文件头块
                writer.Write(System.Text.Encoding.ASCII.GetBytes("MThd"));
                writer.Write(ToBigEndian((uint)6)); // 头部长度
                writer.Write(ToBigEndian((ushort)header.Format));
                writer.Write(ToBigEndian((ushort)header.TrackCount));
                writer.Write(ToBigEndian((ushort)header.TimeDivision));

                // 写入每个轨道
                foreach (var trackEvents in tracks)
                {
                    using (var trackMemory = new MemoryStream())
                    using (var trackWriter = new BinaryWriter(trackMemory))
                    {
                        // 写入事件
                        foreach (var evt in trackEvents)
                        {
                            // 写入delta time（可变长度）
                            WriteVariableLength(trackWriter, evt.DeltaTime);

                            // 写入事件数据
                            if (evt.IsMetaEvent)
                            {
                                trackWriter.Write((byte)evt.EventType);
                                trackWriter.Write(evt.Data1); // Meta事件类型

                                // 写入额外数据长度和数据
                                if (evt.AdditionalData.Length > 0)
                                {
                                    WriteVariableLength(trackWriter, (uint)evt.AdditionalData.Length);
                                    trackWriter.Write(evt.AdditionalData.Span);
                                }
                                else
                                {
                                    WriteVariableLength(trackWriter, 0);
                                }
                            }
                            else if (evt.IsSystemEvent)
                            {
                                trackWriter.Write((byte)evt.EventType);
                                if (evt.AdditionalData.Length > 0)
                                {
                                    WriteVariableLength(trackWriter, (uint)evt.AdditionalData.Length);
                                    trackWriter.Write(evt.AdditionalData.Span);
                                }
                            }
                            else
                            {
                                // 通道事件
                                trackWriter.Write((byte)evt.EventType);
                                trackWriter.Write(evt.Data1);
                                trackWriter.Write(evt.Data2);
                            }
                        }

                        // 写入轨道块
                        writer.Write(System.Text.Encoding.ASCII.GetBytes("MTrk"));
                        writer.Write(ToBigEndian((uint)trackMemory.Length));
                        trackMemory.WriteTo(fileStream);
                    }
                }
            }
        }

        /// <summary>
        /// 将uint转换为大端字节序
        /// </summary>
        private byte[] ToBigEndian(uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return bytes;
        }

        /// <summary>
        /// 将ushort转换为大端字节序
        /// </summary>
        private byte[] ToBigEndian(ushort value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return bytes;
        }

        /// <summary>
        /// 写入可变长度值
        /// </summary>
        private void WriteVariableLength(BinaryWriter writer, uint value)
        {
            // 将值转换为可变长度格式
            if (value == 0)
            {
                writer.Write((byte)0);
                return;
            }

            var bytes = new List<byte>();
            uint val = value;

            while (val > 0)
            {
                byte b = (byte)(val & 0x7F);
                val >>= 7;

                if (bytes.Count > 0)
                    b |= 0x80;

                bytes.Add(b);
            }

            // 反转字节顺序
            bytes.Reverse();

            foreach (byte b in bytes)
                writer.Write(b);
        }

        /// <summary>
        /// 导入MIDI文件
        /// </summary>
        /// <param name="filePath">MIDI文件路径</param>
        /// <returns>导入的音符集合</returns>
        public async Task<IEnumerable<Note>> ImportMidiAsync(string filePath)
        {
            try
            {
                // 检查文件是否存在
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException("MIDI文件不存在", filePath);
                }

                // 读取MIDI文件
                var midiFile = await Task.Run(() => MidiFile.LoadFromFile(filePath));

                // 转换为应用的音符格式
                return await ConvertMidiToNotes(midiFile);
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 导入MIDI文件（带进度回调）
        /// </summary>
        /// <param name="filePath">MIDI文件路径</param>
        /// <param name="progress">进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>导入的音符集合</returns>
        public async Task<IEnumerable<Note>> ImportMidiWithProgressAsync(string filePath, 
            IProgress<(double Progress, string Status)>? progress = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                // 检查文件是否存在
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException("MIDI文件不存在", filePath);
                }

                progress?.Report((0, "开始导入MIDI文件..."));

                // 使用带进度回调的异步加载方法
                var midiFile = await MidiFile.LoadFromFileAsync(filePath, progress, cancellationToken);

                progress?.Report((100, "正在转换音符格式..."));

                // 转换为应用的音符格式
                var notes = await ConvertMidiToNotes(midiFile, progress, cancellationToken);

                progress?.Report((100, "MIDI导入完成"));
                return notes;
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 将MIDI文件转换为应用的音符格式
        /// </summary>
        /// <param name="midiFile">MIDI文件</param>
        /// <param name="progress">进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>转换后的音符集合</returns>
        private async Task<List<Note>> ConvertMidiToNotes(MidiFile midiFile, 
            IProgress<(double Progress, string Status)>? progress = null, 
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var notes = new List<Note>();
                
                // 使用MIDI文件实际的时间分辨率，如果没有则使用默认值
                // 这确保了能够保持原始MIDI文件的时间精度
                var ticksPerBeat = midiFile.Header.TicksPerQuarterNote > 0 ? midiFile.Header.TicksPerQuarterNote : DEFAULT_TICKS_PER_BEAT;
                
                // 记录实际使用的时间分辨率用于调试
                progress?.Report((10, $"使用时间分辨率: {ticksPerBeat} ticks/四分音符"));

                // 首先识别哪些轨道包含Conductor事件
                var conductorTrackIndices = new List<int>();
                for (int i = 0; i < midiFile.Tracks.Count; i++)
                {
                    if (HasConductorEvents(midiFile.Tracks[i]))
                    {
                        conductorTrackIndices.Add(i);
                    }
                }

                // 创建非Conductor轨道的映射表
                var regularTrackMapping = new Dictionary<int, int>(); // 原始索引 -> 新TrackIndex
                int newTrackIndex = 0; // 从0开始

                for (int originalIndex = 0; originalIndex < midiFile.Tracks.Count; originalIndex++)
                {
                    if (!conductorTrackIndices.Contains(originalIndex))
                    {
                        regularTrackMapping[originalIndex] = newTrackIndex;
                        newTrackIndex++;
                    }
                }

                // 遍历每个音轨，并记录当前音轨索引
                for (int trackIndex = 0; trackIndex < midiFile.Tracks.Count; trackIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var progressPercent = 100.0 + (double)trackIndex / midiFile.Tracks.Count * 100; // 从100%开始，因为文件加载已经占用了0-100%
                    progress?.Report((Math.Min(progressPercent, 200), $"正在处理音轨 {trackIndex + 1}/{midiFile.Tracks.Count}..."));

                    var track = midiFile.Tracks[trackIndex];
                    
                    // 如果是Conductor轨，跳过音符处理
                    if (conductorTrackIndices.Contains(trackIndex))
                    {
                        continue;
                    }
                    
                    // 获取映射后的轨道索引
                    if (!regularTrackMapping.ContainsKey(trackIndex))
                    {
                        continue; // 这不应该发生，但作为保护
                    }
                    
                    int mappedTrackIndex = regularTrackMapping[trackIndex];
                    
                    // 跟踪当前时间位置
                    long currentTime = 0;
                    
                    // 用于存储Note On事件，等待对应的Note Off事件
                    var activeNotes = new Dictionary<(int Channel, int Pitch), (long StartTime, int Velocity)>();

                    // 遍历音轨中的所有事件
                    foreach (var midiEvent in track.Events)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // 更新当前时间
                        currentTime += midiEvent.DeltaTime;

                        // 处理Note On事件
                        if (midiEvent.EventType == MidiEventType.NoteOn && midiEvent.Data2 > 0)
                        {
                            activeNotes[(midiEvent.Channel, midiEvent.Data1)] = (currentTime, midiEvent.Data2);
                        }
                        // 处理Note Off事件或Velocity为0的Note On事件（也是Note Off的一种表示）
                        else if ((midiEvent.EventType == MidiEventType.NoteOff || 
                                 (midiEvent.EventType == MidiEventType.NoteOn && midiEvent.Data2 == 0)) &&
                                 activeNotes.ContainsKey((midiEvent.Channel, midiEvent.Data1)))
                        {
                            var noteInfo = activeNotes[(midiEvent.Channel, midiEvent.Data1)];
                            activeNotes.Remove((midiEvent.Channel, midiEvent.Data1));

                            // 计算持续时间
                            long duration = currentTime - noteInfo.StartTime;

                            // 确保持续时间大于0
                            if (duration <= 0)
                                duration = 1;

                            // 创建音符模型，使用映射后的轨道索引
                            var note = new Note
                            {
                                Pitch = midiEvent.Data1, // Data1代表音高
                                StartPosition = ConvertTicksToMusicalFraction(noteInfo.StartTime, ticksPerBeat),
                                Duration = ConvertTicksToMusicalFraction(duration, ticksPerBeat),
                                Velocity = noteInfo.Velocity,
                                TrackIndex = mappedTrackIndex // 使用映射后的轨道索引
                            };

                            notes.Add(note);
                        }
                    }
                }

                // 分析转换后的最小音符时值，用于调试信息
                if (notes.Any())
                {
                    var minDuration = notes.Min(n => n.Duration.ToDouble());
                    var minStartPos = notes.Min(n => n.StartPosition.ToDouble());
                    var nonZeroMinStart = notes.Where(n => n.StartPosition.ToDouble() > 0).DefaultIfEmpty().Min(n => n?.StartPosition.ToDouble()) ?? 0;
                    
                    progress?.Report((200, $"音符转换完成，共处理 {notes.Count} 个音符。最小音符时值: {minDuration:F6} 四分音符，最小非零位置: {nonZeroMinStart:F6} 四分音符"));
                }
                else
                {
                    progress?.Report((200, "音符转换完成，未发现音符"));
                }
                
                return notes;
            }, cancellationToken);
        }

        /// <summary>
        /// 检查MIDI轨道是否包含Conductor相关的事件
        /// </summary>
        /// <param name="track">MIDI轨道</param>
        /// <returns>是否包含Conductor事件</returns>
        private bool HasConductorEvents(MidiTrack track)
        {
            foreach (var midiEvent in track.Events)
            {
                if (midiEvent.IsMetaEvent)
                {
                    switch (midiEvent.Data1)
                    {
                        case 0x51: // Tempo事件
                        case 0x58: // Time Signature事件
                        case 0x59: // Key Signature事件
                        case 0x54: // SMPTE Offset事件
                        case 0x7F: // Sequencer-Specific事件
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 将MIDI的tick值转换为MusicalFraction
        /// </summary>
        /// <param name="ticks">tick值</param>
        /// <param name="ticksPerBeat">每拍的tick数</param>
        /// <returns>转换后的MusicalFraction对象</returns>
        private MusicalFraction ConvertTicksToMusicalFraction(long ticks, int ticksPerBeat)
        {
            // 计算以四分音符为单位的值（1个四分音符 = 1拍）
            double quarterNotes = (double)ticks / ticksPerBeat;
            return MusicalFraction.FromDouble(quarterNotes);
        }
    }
}