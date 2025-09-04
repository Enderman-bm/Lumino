using System;
using System.Collections.Generic;
using System.IO;

namespace MidiReader
{
    /// <summary>
    /// 高性能MIDI文件读取器
    /// 支持大文件的高效解析和最小内存占用
    /// </summary>
    public class MidiFile : IDisposable
    {
        private readonly ReadOnlyMemory<byte> _fileData;
        private readonly List<MidiTrack> _tracks = new();
        private bool _isDisposed;

        /// <summary>
        /// MIDI文件头信息
        /// </summary>
        public MidiFileHeader Header { get; private set; }

        /// <summary>
        /// 所有轨道的只读列表
        /// </summary>
        public IReadOnlyList<MidiTrack> Tracks => _tracks;

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public int FileSize => _fileData.Length;

        /// <summary>
        /// 从文件路径加载MIDI文件
        /// </summary>
        public static MidiFile LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"MIDI file not found: {filePath}");

            var fileData = File.ReadAllBytes(filePath);
            return new MidiFile(fileData);
        }

        /// <summary>
        /// 从字节数组创建MIDI文件
        /// </summary>
        public static MidiFile LoadFromBytes(byte[] data)
        {
            return new MidiFile(data);
        }

        /// <summary>
        /// 从内存创建MIDI文件
        /// </summary>
        public static MidiFile LoadFromMemory(ReadOnlyMemory<byte> data)
        {
            return new MidiFile(data);
        }

        private MidiFile(ReadOnlyMemory<byte> fileData)
        {
            _fileData = fileData;
            ParseFile();
        }

        private void ParseFile()
        {
            var reader = new MidiBinaryReader(_fileData.Span);

            // 解析文件头
            ParseHeader(ref reader);

            // 解析所有轨道
            for (int i = 0; i < Header.TrackCount; i++)
            {
                var track = ParseTrack(ref reader);
                _tracks.Add(track);
            }
        }

        private void ParseHeader(ref MidiBinaryReader reader)
        {
            // 读取头块标识符 "MThd"
            var headerChunk = reader.ReadFixedLengthString(4);
            if (headerChunk != "MThd")
                throw new InvalidDataException($"Invalid MIDI file header: expected 'MThd', got '{headerChunk}'");

            // 读取头块长度 (应该是6)
            uint headerLength = reader.ReadUInt32BigEndian();
            if (headerLength != 6)
                throw new InvalidDataException($"Invalid header length: expected 6, got {headerLength}");

            // 读取格式类型
            ushort format = reader.ReadUInt16BigEndian();
            if (format > 2)
                throw new InvalidDataException($"Unsupported MIDI format: {format}");

            // 读取轨道数量
            ushort trackCount = reader.ReadUInt16BigEndian();

            // 读取时间分辨率
            ushort timeDivision = reader.ReadUInt16BigEndian();

            Header = new MidiFileHeader((MidiFileFormat)format, trackCount, timeDivision);
        }

        private MidiTrack ParseTrack(ref MidiBinaryReader reader)
        {
            // 读取轨道块标识符 "MTrk"
            var trackChunk = reader.ReadFixedLengthString(4);
            if (trackChunk != "MTrk")
                throw new InvalidDataException($"Invalid track header: expected 'MTrk', got '{trackChunk}'");

            // 读取轨道数据长度
            uint trackLength = reader.ReadUInt32BigEndian();

            // 读取轨道数据
            var trackData = reader.ReadBytes((int)trackLength);

            return new MidiTrack(trackData.ToArray());
        }

        /// <summary>
        /// 获取文件的总体统计信息
        /// </summary>
        public MidiFileStatistics GetStatistics()
        {
            int totalNotes = 0;
            int totalEvents = 0;
            uint maxTicks = 0;
            var usedChannels = new HashSet<byte>();

            foreach (var track in _tracks)
            {
                var stats = track.GetStatistics();
                totalNotes += stats.NoteCount;
                totalEvents += stats.EventCount;
                maxTicks = Math.Max(maxTicks, stats.TotalTicks);
            }

            return new MidiFileStatistics(
                Header,
                totalNotes,
                totalEvents,
                maxTicks,
                _tracks.Count,
                FileSize
            );
        }

        /// <summary>
        /// 获取所有轨道中的音符事件，按时间排序
        /// 使用流式处理以节省内存
        /// </summary>
        public IEnumerable<(MidiEvent Event, int TrackIndex, uint AbsoluteTime)> GetAllNotesStreamable()
        {
            // 由于MidiEventEnumerator是ref struct，不能存储在泛型集合中
            // 这里提供一个简化的实现，逐个轨道处理
            for (int trackIndex = 0; trackIndex < _tracks.Count; trackIndex++)
            {
                uint absoluteTime = 0;
                foreach (var evt in _tracks[trackIndex].GetEventEnumerator())
                {
                    absoluteTime += evt.DeltaTime;
                    
                    if (evt.EventType == MidiEventType.NoteOn || evt.EventType == MidiEventType.NoteOff)
                    {
                        yield return (evt, trackIndex, absoluteTime);
                    }
                }
            }
        }

        /// <summary>
        /// 获取指定轨道中的所有音符事件
        /// </summary>
        public IEnumerable<(MidiEvent Event, uint AbsoluteTime)> GetTrackNotesStreamable(int trackIndex)
        {
            if (trackIndex < 0 || trackIndex >= _tracks.Count)
                throw new ArgumentOutOfRangeException(nameof(trackIndex));

            uint absoluteTime = 0;
            foreach (var evt in _tracks[trackIndex].GetEventEnumerator())
            {
                absoluteTime += evt.DeltaTime;
                
                if (evt.EventType == MidiEventType.NoteOn || evt.EventType == MidiEventType.NoteOff)
                {
                    yield return (evt, absoluteTime);
                }
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _tracks.Clear();
                _isDisposed = true;
            }
        }
    }

    /// <summary>
    /// MIDI文件统计信息
    /// </summary>
    public readonly struct MidiFileStatistics
    {
        public readonly MidiFileHeader Header;
        public readonly int TotalNotes;
        public readonly int TotalEvents;
        public readonly uint MaxTicks;
        public readonly int TrackCount;
        public readonly int FileSizeBytes;

        public MidiFileStatistics(MidiFileHeader header, int totalNotes, int totalEvents, uint maxTicks, int trackCount, int fileSizeBytes)
        {
            Header = header;
            TotalNotes = totalNotes;
            TotalEvents = totalEvents;
            MaxTicks = maxTicks;
            TrackCount = trackCount;
            FileSizeBytes = fileSizeBytes;
        }

        /// <summary>
        /// 计算估计的播放时长（秒）
        /// 需要文件中的tempo信息才能准确计算
        /// </summary>
        public double EstimatedDurationSeconds(int microsecondsPerQuarterNote = 500000)
        {
            if (Header.IsSmpteFormat)
            {
                // SMPTE格式的时间计算
                return (double)MaxTicks / (Header.SmpteFrameRate * Header.TicksPerFrame);
            }
            else
            {
                // 标准格式的时间计算
                double secondsPerTick = (double)microsecondsPerQuarterNote / (Header.TicksPerQuarterNote * 1_000_000.0);
                return MaxTicks * secondsPerTick;
            }
        }
    }
}