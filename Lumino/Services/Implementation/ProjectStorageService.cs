using Lumino.Models.Music;
using Lumino.Services.Interfaces;
using MidiReader;
using System;
using System.Collections.Concurrent;
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
        public async Task<bool> SaveProjectAsync(string filePath, IEnumerable<Note> notes, ProjectMetadata metadata, System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Info("ProjectStorageService", $"[EnderDebugger][{DateTime.Now}][EnderLogger][ProjectStorageService] 开始保存项目到文件: {filePath}");
                // 项目文件格式 (简单自定义格式)：
                // [4 bytes] - ASCII "LMPF"
                // [4 bytes] - int32 version
                // [4 bytes] - int32 metadataJsonLength
                // [N bytes] - metadataJson (UTF8)
                // [4 bytes] - int32 compressedNotesLength
                // [M bytes] - compressed notes JSON (GZip)

                // 在后台线程执行序列化与磁盘写入，避免阻塞调用线程（UI线程）
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = false };

                    var metadataJson = System.Text.Json.JsonSerializer.Serialize(metadata, options);
                    var metadataBytes = System.Text.Encoding.UTF8.GetBytes(metadataJson);

                    var notesJson = System.Text.Json.JsonSerializer.Serialize(notes, options);
                    byte[] notesBytes = System.Text.Encoding.UTF8.GetBytes(notesJson);

                    // 如果已请求取消，则提前退出
                    cancellationToken.ThrowIfCancellationRequested();

                    // GZip 压缩 notesBytes
                    byte[] compressedNotes;
                    using (var ms = new MemoryStream())
                    {
                        using (var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, true))
                        {
                            gz.Write(notesBytes, 0, notesBytes.Length);
                        }
                        compressedNotes = ms.ToArray();
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    // 为了实现原子保存：先写入临时文件（与目标同目录），写入完成并刷新后再替换目标文件。
                    var dir = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
                    var tempFileName = Path.Combine(dir, Path.GetFileName(filePath) + ".tmp." + Guid.NewGuid().ToString("N"));

                    try
                    {
                        using (var fs = new FileStream(tempFileName, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        using (var bw = new BinaryWriter(fs))
                        {
                            // 写入头部与元数据、压缩数据，分段写入以便在长文件时响应取消
                            bw.Write(System.Text.Encoding.ASCII.GetBytes("LMPF"));
                            bw.Write((int)1); // version

                            bw.Write((int)metadataBytes.Length);
                            // 分块写入metadataBytes
                            WriteBytesWithCancellation(bw, metadataBytes, cancellationToken);

                            bw.Write((int)compressedNotes.Length);
                            // 分块写入compressedNotes
                            WriteBytesWithCancellation(bw, compressedNotes, cancellationToken);

                            // 确保数据已刷新到磁盘
                            bw.Flush();
                            fs.Flush(true);
                        }

                        // 如果写入期间未被取消，使用原子替换替换目标文件（若目标存在则 Replace，否则 Move）
                        cancellationToken.ThrowIfCancellationRequested();

                        if (File.Exists(filePath))
                        {
                            // File.Replace 会用 temp 覆盖 dest，且支持在出错时指定备份路径（这里不需要备份）
                            File.Replace(tempFileName, filePath, null);
                        }
                        else
                        {
                            // 目标不存在，直接移动并允许覆盖（CreateNew 保证临时文件存在）
                            File.Move(tempFileName, filePath, true);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 取消：删除临时文件（如果存在），并向上抛出以便外层捕获处理
                        try { if (File.Exists(tempFileName)) File.Delete(tempFileName); } catch { }
                        throw;
                    }
                    catch (Exception)
                    {
                        // 其他异常也尽量清理临时文件，然后继续抛出以便外层捕获并记录日志
                        try { if (File.Exists(tempFileName)) File.Delete(tempFileName); } catch { }
                        throw;
                    }
                }, cancellationToken);

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
        public async Task<(IEnumerable<Note> notes, ProjectMetadata metadata)> LoadProjectAsync(string filePath, System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                // 在后台线程进行读取与解压，以便响应取消请求
                return await Task.Run(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var br = new BinaryReader(fs))
                    {
                        var header = br.ReadBytes(4);
                        var headerStr = System.Text.Encoding.ASCII.GetString(header);
                        if (headerStr != "LMPF")
                            throw new InvalidDataException("Not a LMPF project file");

                        var version = br.ReadInt32();
                        var metadataLen = br.ReadInt32();
                        var metadataBytes = br.ReadBytes(metadataLen);
                        var metadataJson = System.Text.Encoding.UTF8.GetString(metadataBytes);
                        var metadata = System.Text.Json.JsonSerializer.Deserialize<ProjectMetadata>(metadataJson) ?? new ProjectMetadata();

                        var compressedLen = br.ReadInt32();
                        var compressed = br.ReadBytes(compressedLen);

                        cancellationToken.ThrowIfCancellationRequested();

                        byte[] notesBytes;
                        using (var inMs = new MemoryStream(compressed))
                        using (var gz = new System.IO.Compression.GZipStream(inMs, System.IO.Compression.CompressionMode.Decompress))
                        using (var outMs = new MemoryStream())
                        {
                            await gz.CopyToAsync(outMs, cancellationToken);
                            notesBytes = outMs.ToArray();
                        }

                        var notesJson = System.Text.Encoding.UTF8.GetString(notesBytes);
                        var notes = System.Text.Json.JsonSerializer.Deserialize<List<Note>>(notesJson) ?? new List<Note>();

                        return (notes as IEnumerable<Note>, metadata);
                    }
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
                                    TrackIndex = mappedTrackIndex // 使用映射后的轨道索引
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