using Lumino.Models.Music;
using Lumino.Services.Interfaces;
using MidiReader;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
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
        public async Task<bool> SaveProjectAsync(string filePath, ProjectSnapshot snapshot, ProjectMetadata metadata, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Info("ProjectStorageService", $"[EnderDebugger][{DateTime.Now}][EnderLogger][ProjectStorageService] 开始保存项目到文件: {filePath}");
                snapshot ??= new ProjectSnapshot();
                snapshot.Notes ??= new List<Note>();
                snapshot.ControllerEvents ??= new List<ControllerEvent>();

                await Task.Run(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var options = new JsonSerializerOptions { WriteIndented = false };
                    metadata ??= new ProjectMetadata();
                    metadata.Tracks ??= new List<TrackMetadata>();

                    var metadataJson = JsonSerializer.Serialize(metadata, options);
                    var metadataBytes = Encoding.UTF8.GetBytes(metadataJson);

                    var directory = Path.GetDirectoryName(filePath);
                    if (string.IsNullOrWhiteSpace(directory))
                    {
                        directory = Directory.GetCurrentDirectory();
                    }

                    Directory.CreateDirectory(directory);

                    var tempFileName = Path.Combine(directory, Path.GetFileName(filePath) + ".tmp." + Guid.NewGuid().ToString("N"));

                    try
                    {
                        await using var fs = new FileStream(tempFileName, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                        using var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: true);

                        bw.Write(Encoding.ASCII.GetBytes("LMPF"));
                        bw.Write(1); // version

                        bw.Write(metadataBytes.Length);
                        WriteBytesWithCancellation(bw, metadataBytes, cancellationToken);
                        bw.Flush();

                        var compressedLengthPosition = fs.Position;
                        bw.Write(0); // placeholder for compressed payload length
                        bw.Flush();

                        var dataStartPosition = fs.Position;

                        await using (var gzip = new GZipStream(fs, CompressionLevel.Optimal, leaveOpen: true))
                        {
                            await JsonSerializer.SerializeAsync(gzip, snapshot, options, cancellationToken);
                            await gzip.FlushAsync(cancellationToken);
                        }

                        cancellationToken.ThrowIfCancellationRequested();

                        var dataEndPosition = fs.Position;
                        var compressedLength = checked((int)(dataEndPosition - dataStartPosition));

                        fs.Position = compressedLengthPosition;
                        bw.Write(compressedLength);
                        bw.Flush();

                        fs.Position = dataEndPosition;
                        await fs.FlushAsync(cancellationToken);
                        fs.Flush(true);
                    }
                    catch (OperationCanceledException)
                    {
                        // 尝试清理临时文件
                        try
                        {
                            if (File.Exists(tempFileName))
                            {
                                File.Delete(tempFileName);
                            }
                        }
                        catch
                        {
                            // 忽略清理异常
                        }
                        throw;
                    }
                    catch (Exception)
                    {
                        try
                        {
                            if (File.Exists(tempFileName))
                            {
                                File.Delete(tempFileName);
                            }
                        }
                        catch
                        {
                        }
                        throw;
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    if (File.Exists(filePath))
                    {
                        File.Replace(tempFileName, filePath, null);
                    }
                    else
                    {
                        File.Move(tempFileName, filePath, true);
                    }
                }, cancellationToken);

                _logger.Info("ProjectStorageService", $"[EnderDebugger][{DateTime.Now}][EnderLogger][ProjectStorageService] 项目保存成功: {filePath}");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("ProjectStorageService", $"[EnderDebugger][{DateTime.Now}][EnderLogger][ProjectStorageService] 项目保存被取消: {filePath}");
                return false;
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
        public async Task<(ProjectSnapshot snapshot, ProjectMetadata metadata)> LoadProjectAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                // 在后台线程进行读取与解压，以便响应取消请求
                return await Task.Run(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);

                    var header = br.ReadBytes(4);
                    var headerStr = Encoding.ASCII.GetString(header);
                    if (headerStr != "LMPF")
                        throw new InvalidDataException("Not a LMPF project file");

                    var version = br.ReadInt32();
                    var metadataLen = br.ReadInt32();
                    var metadataBytes = br.ReadBytes(metadataLen);
                    var metadataJson = Encoding.UTF8.GetString(metadataBytes);
                    var options = new JsonSerializerOptions { WriteIndented = false };
                    var metadata = JsonSerializer.Deserialize<ProjectMetadata>(metadataJson, options) ?? new ProjectMetadata();
                    metadata.Tracks ??= new List<TrackMetadata>();

                    var compressedLen = br.ReadInt32();
                    var compressed = br.ReadBytes(compressedLen);

                    cancellationToken.ThrowIfCancellationRequested();

                    using var compressedStream = new MemoryStream(compressed, writable: false);
                    using var gzip = new GZipStream(compressedStream, CompressionMode.Decompress);
                    using var decompressedStream = new MemoryStream();
                    await gzip.CopyToAsync(decompressedStream, cancellationToken);
                    var payloadBytes = decompressedStream.ToArray();
                    var payloadJson = Encoding.UTF8.GetString(payloadBytes);

                    ProjectSnapshot snapshot;
                    try
                    {
                        snapshot = JsonSerializer.Deserialize<ProjectSnapshot>(payloadJson, options) ?? new ProjectSnapshot();
                        snapshot.Notes ??= new List<Note>();
                        snapshot.ControllerEvents ??= new List<ControllerEvent>();
                    }
                    catch (JsonException)
                    {
                        var legacyNotes = JsonSerializer.Deserialize<List<Note>>(payloadJson, options) ?? new List<Note>();
                        snapshot = ProjectSnapshot.FromNotesOnly(legacyNotes);
                    }

                    return (snapshot, metadata);
                }, cancellationToken);
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
        public async Task<bool> ExportMidiAsync(string filePath, ProjectSnapshot snapshot)
        {
            try
            {
                await Task.Run(() =>
                {
                    snapshot ??= new ProjectSnapshot();
                    var notes = snapshot.Notes ?? new List<Note>();
                    var controllers = snapshot.ControllerEvents ?? new List<ControllerEvent>();

                    var trackIndices = notes.Select(n => n.TrackIndex)
                        .Concat(controllers.Select(c => c.TrackIndex))
                        .Distinct()
                        .OrderBy(i => i)
                        .ToList();

                    if (trackIndices.Count == 0)
                    {
                        trackIndices.Add(0);
                    }

                    int trackCount = trackIndices.Count;

                    // 创建MIDI文件头
                    var header = new MidiFileHeader(MidiFileFormat.MultipleTracksParallel, (ushort)trackCount, (ushort)DEFAULT_TICKS_PER_BEAT);

                    // 创建轨道列表
                    var tracks = new List<List<MidiEvent>>();

                    // 为每个音轨创建MIDI事件
                    for (int trackPosition = 0; trackPosition < trackCount; trackPosition++)
                    {
                        var trackEvents = new List<MidiEvent>();
                        var trackIndex = trackIndices[trackPosition];

                        // 添加轨道名称元事件
                        var trackName = Encoding.UTF8.GetBytes($"Track {trackPosition + 1}");
                        trackEvents.Add(new MidiEvent(0, MidiEventType.MetaEvent, 0, (byte)MetaEventType.TrackName, 0, trackName));

                        var trackNotes = notes.Where(n => n.TrackIndex == trackIndex).ToList();
                        var trackControllers = controllers.Where(c => c.TrackIndex == trackIndex).ToList();

                        var midiEvents = BuildMidiEventsForTrack(trackNotes, trackControllers, DEFAULT_TICKS_PER_BEAT);

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
        private List<MidiEvent> BuildMidiEventsForTrack(List<Note> notes, List<ControllerEvent> controllers, int ticksPerBeat)
        {
            var scheduled = new List<ScheduledMidiEvent>();

            foreach (var note in notes)
            {
                long startTime = ConvertMusicalFractionToTicks(note.StartPosition, ticksPerBeat);
                long endTime = ConvertMusicalFractionToTicks(note.StartPosition + note.Duration, ticksPerBeat);
                if (endTime <= startTime)
                {
                    endTime = startTime + 1;
                }

                var velocity = (byte)Math.Clamp(note.Velocity, 0, 127);

                scheduled.Add(new ScheduledMidiEvent(startTime, MidiEventType.NoteOn, 0, (byte)note.Pitch, velocity));
                scheduled.Add(new ScheduledMidiEvent(endTime, MidiEventType.NoteOff, 0, (byte)note.Pitch, 0));
            }

            foreach (var controller in controllers)
            {
                long time = ConvertMusicalFractionToTicks(controller.Time, ticksPerBeat);
                var controllerNumber = (byte)Math.Clamp(controller.ControllerNumber, 0, 127);
                var value = (byte)Math.Clamp(controller.Value, 0, 127);

                scheduled.Add(new ScheduledMidiEvent(time, MidiEventType.ControlChange, 0, controllerNumber, value));
            }

            scheduled.Sort(static (a, b) =>
            {
                var timeCompare = a.Time.CompareTo(b.Time);
                if (timeCompare != 0)
                {
                    return timeCompare;
                }

                return ((byte)a.EventType).CompareTo((byte)b.EventType);
            });

            var events = new List<MidiEvent>();
            long currentTime = 0;
            foreach (var item in scheduled)
            {
                uint deltaTime = (uint)Math.Max(0, item.Time - currentTime);
                events.Add(new MidiEvent(deltaTime, item.EventType, item.Channel, item.Data1, item.Data2));
                currentTime = item.Time;
            }

            return events;
        }

        private readonly struct ScheduledMidiEvent
        {
            public ScheduledMidiEvent(long time, MidiEventType eventType, byte channel, byte data1, byte data2)
            {
                Time = time;
                EventType = eventType;
                Channel = channel;
                Data1 = data1;
                Data2 = data2;
            }

            public long Time { get; }
            public MidiEventType EventType { get; }
            public byte Channel { get; }
            public byte Data1 { get; }
            public byte Data2 { get; }
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
                var notes = new System.Collections.Concurrent.ConcurrentBag<Note>();
                
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

                // 并行处理每个音轨
                System.Threading.Tasks.Parallel.ForEach(
                    midiFile.Tracks.Select((track, index) => (track, index)),
                    new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 32, CancellationToken = cancellationToken },
                    tuple =>
                    {
                        var (track, trackIndex) = tuple;
                        
                        // 如果是Conductor轨，跳过音符处理
                        if (conductorTrackIndices.Contains(trackIndex))
                        {
                            return;
                        }
                        
                        // 获取映射后的轨道索引
                        if (!regularTrackMapping.ContainsKey(trackIndex))
                        {
                            return; // 这不应该发生，但作为保护
                        }
                        
                        int mappedTrackIndex = regularTrackMapping[trackIndex];
                        
                        // 跟踪当前时间位置
                        long currentTime = 0;
                        
                        // 用于存储Note On事件，等待对应的Note Off事件
                        var activeNotes = new Dictionary<(int Channel, int Pitch), (long StartTime, int Velocity)>();

                        // 遍历音轨中的所有事件
                        foreach (var midiEvent in track.Events)
                        {
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
                                    TrackIndex = mappedTrackIndex, // 使用映射后的轨道索引
                                    MidiChannel = midiEvent.Channel // 保存原始MIDI通道 (0-15)
                                };

                                notes.Add(note);
                            }
                        }
                    });

                // 分析转换后的最小音符时值，用于调试信息
                var notesList = notes.ToList();
                if (notesList.Any())
                {
                    var minDuration = notesList.Min(n => n.Duration.ToDouble());
                    var minStartPos = notesList.Min(n => n.StartPosition.ToDouble());
                    var nonZeroMinStart = notesList.Where(n => n.StartPosition.ToDouble() > 0).DefaultIfEmpty().Min(n => n?.StartPosition.ToDouble()) ?? 0;
                    
                    progress?.Report((200, $"音符转换完成，共处理 {notesList.Count} 个音符。最小音符时值: {minDuration:F6} 四分音符，最小非零位置: {nonZeroMinStart:F6} 四分音符"));
                }
                else
                {
                    progress?.Report((200, "音符转换完成，未发现音符"));
                }
                
                return notesList;
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

        /// <summary>
        /// 分段写入字节数组到 BinaryWriter，并在每个分段后检查取消令牌以响应取消请求。
        /// </summary>
        private static void WriteBytesWithCancellation(BinaryWriter bw, byte[] data, CancellationToken cancellationToken, int chunkSize = 81920)
        {
            int offset = 0;
            while (offset < data.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int toWrite = Math.Min(chunkSize, data.Length - offset);
                bw.Write(data, offset, toWrite);
                offset += toWrite;
            }
        }
    }
}