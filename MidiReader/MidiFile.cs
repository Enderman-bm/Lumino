using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MidiReader
{
    /// <summary>
    /// ������MIDI�ļ���ȡ��
    /// ֧�ִ��ļ��ĸ�Ч��������С�ڴ�ռ��
    /// </summary>
    public class MidiFile : IDisposable
    {
        private readonly ReadOnlyMemory<byte> _fileData;
        private readonly List<MidiTrack> _tracks = new();
        private bool _isDisposed;

        /// <summary>
        /// MIDI�ļ�ͷ��Ϣ
        /// </summary>
        public MidiFileHeader Header { get; private set; }

        /// <summary>
        /// ���й����ֻ���б�
        /// </summary>
        public IReadOnlyList<MidiTrack> Tracks => _tracks;

        /// <summary>
        /// �ļ���С���ֽڣ�
        /// </summary>
        public int FileSize => _fileData.Length;

        /// <summary>
        /// ���ļ�·������MIDI�ļ�
        /// </summary>
        public static MidiFile LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"MIDI file not found: {filePath}");

            var fileData = File.ReadAllBytes(filePath);
            return new MidiFile(fileData);
        }

        /// <summary>
        /// �첽����MIDI�ļ���֧�ֽ��Ȼص�
        /// </summary>
        /// <param name="filePath">�ļ�·��</param>
        /// <param name="progress">���Ȼص�</param>
        /// <param name="cancellationToken">ȡ������</param>
        /// <returns>MIDI�ļ�ʵ��</returns>
        public static async Task<MidiFile> LoadFromFileAsync(string filePath, IProgress<(double Progress, string Status)>? progress = null, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"MIDI file not found: {filePath}");

            progress?.Report((0, "���ڶ�ȡ�ļ�..."));

            // �첽��ȡ�ļ�
            byte[] fileData;
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
            {
                fileData = new byte[fileStream.Length];
                var totalBytes = fileStream.Length;
                var bytesRead = 0;

                while (bytesRead < totalBytes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var chunkSize = Math.Min(8192, (int)(totalBytes - bytesRead));
                    var currentRead = await fileStream.ReadAsync(fileData, bytesRead, chunkSize, cancellationToken);
                    
                    if (currentRead == 0)
                        break;

                    bytesRead += currentRead;
                    
                    var progressPercent = (double)bytesRead / totalBytes * 30; // �ļ���ȡռ30%
                    progress?.Report((progressPercent, $"���ڶ�ȡ�ļ�... {bytesRead}/{totalBytes} �ֽ�"));
                }
            }

            progress?.Report((30, "�ļ���ȡ��ɣ���ʼ����..."));

            // �ں�̨�߳��н���MIDI�ļ�
            return await Task.Run(() =>
            {
                return new MidiFile(fileData, progress, cancellationToken);
            }, cancellationToken);
        }

        /// <summary>
        /// ���ֽ����鴴��MIDI�ļ�
        /// </summary>
        public static MidiFile LoadFromBytes(byte[] data)
        {
            return new MidiFile(data);
        }

        /// <summary>
        /// ���ڴ洴��MIDI�ļ�
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

        private MidiFile(ReadOnlyMemory<byte> fileData, IProgress<(double Progress, string Status)>? progress, CancellationToken cancellationToken)
        {
            _fileData = fileData;
            ParseFileWithProgress(progress, cancellationToken);
        }

        private void ParseFile()
        {
            var reader = new MidiBinaryReader(_fileData.Span);

            // �����ļ�ͷ
            ParseHeader(ref reader);

            // �������й��
            for (int i = 0; i < Header.TrackCount; i++)
            {
                var track = ParseTrack(ref reader);
                _tracks.Add(track);
            }
        }

        private void ParseFileWithProgress(IProgress<(double Progress, string Status)>? progress, CancellationToken cancellationToken)
        {
            var reader = new MidiBinaryReader(_fileData.Span);

            progress?.Report((35, "���ڽ����ļ�ͷ..."));
            cancellationToken.ThrowIfCancellationRequested();

            // �����ļ�ͷ
            ParseHeader(ref reader);

            progress?.Report((40, "�ļ�ͷ�������"));

            // �ȸ�������е�������ݣ�Ȼ�����н���
            var trackDatas = new List<byte[]>(Header.TrackCount);
            for (int i = 0; i < Header.TrackCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // ��ȡ������ʶ�� "MTrk"
                var trackChunk = reader.ReadFixedLengthString(4);
                if (trackChunk != "MTrk")
                    throw new InvalidDataException($"Invalid track header: expected 'MTrk', got '{trackChunk}'");

                // ��ȡ������ݳ���
                uint trackLength = reader.ReadUInt32BigEndian();
                var trackData = reader.ReadBytes((int)trackLength);
                trackDatas.Add(trackData.ToArray());
            }

            progress?.Report((50, "数据读取完成"));

            // 根据CPU核心数动态设置并行度
            int optimalDegreeOfParallelism = Math.Min(
                Header.TrackCount,
                Environment.ProcessorCount * 2
            );

            // 使用多线程并行解析轨道
            var tracks = new MidiTrack[Header.TrackCount];
            System.Threading.Tasks.Parallel.For(0, Header.TrackCount, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = optimalDegreeOfParallelism }, i =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                tracks[i] = new MidiTrack(trackDatas[i]);
            });

            _tracks.AddRange(tracks);

            progress?.Report((100, "MIDI文件解析完成"));
        }

        private void ParseHeader(ref MidiBinaryReader reader)
        {
            // ��ȡͷ���ʶ�� "MThd"
            var headerChunk = reader.ReadFixedLengthString(4);
            if (headerChunk != "MThd")
                throw new InvalidDataException($"Invalid MIDI file header: expected 'MThd', got '{headerChunk}'");

            // ��ȡͷ�鳤�� (Ӧ����6)
            uint headerLength = reader.ReadUInt32BigEndian();
            if (headerLength != 6)
                throw new InvalidDataException($"Invalid header length: expected 6, got {headerLength}");

            // ��ȡ��ʽ����
            ushort format = reader.ReadUInt16BigEndian();
            if (format > 2)
                throw new InvalidDataException($"Unsupported MIDI format: {format}");

            // ��ȡ�������
            ushort trackCount = reader.ReadUInt16BigEndian();

            // ��ȡʱ��ֱ���
            ushort timeDivision = reader.ReadUInt16BigEndian();

            Header = new MidiFileHeader((MidiFileFormat)format, trackCount, timeDivision);
        }

        private MidiTrack ParseTrack(ref MidiBinaryReader reader)
        {
            // ��ȡ������ʶ�� "MTrk"
            var trackChunk = reader.ReadFixedLengthString(4);
            if (trackChunk != "MTrk")
                throw new InvalidDataException($"Invalid track header: expected 'MTrk', got '{trackChunk}'");

            // 读取轨道数据长度
            uint trackLength = reader.ReadUInt32BigEndian();

            // 读取轨道数据
            var trackData = reader.ReadBytes((int)trackLength);

            // 转换为 ReadOnlyMemory<byte> 并创建轨道
            return new MidiTrack(trackData.ToArray().AsMemory());
        }

        /// <summary>
        /// ��ȡ�ļ�������ͳ����Ϣ
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
        /// ��ȡ���й���е������¼�����ʱ������
        /// ʹ����ʽ�����Խ�ʡ�ڴ�
        /// </summary>
        public IEnumerable<(MidiEvent Event, int TrackIndex, uint AbsoluteTime)> GetAllNotesStreamable()
        {
            // ����MidiEventEnumerator��ref struct�����ܴ洢�ڷ��ͼ�����
            // �����ṩһ���򻯵�ʵ�֣�����������
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
        /// ��ȡ���й���е������¼�����ʱ�����루�����汾��
        /// ʹ��32�̲߳����н���tickת��
        /// </summary>
        public IEnumerable<(MidiEvent Event, int TrackIndex, uint AbsoluteTime)> GetAllNotesParallel()
        {
            var results = new System.Collections.Concurrent.ConcurrentBag<(MidiEvent Event, int TrackIndex, uint AbsoluteTime)>();

            System.Threading.Tasks.Parallel.ForEach(
                _tracks.Select((track, index) => (track, index)),
                new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 32 },
                tuple =>
                {
                    var (track, trackIndex) = tuple;
                    uint absoluteTime = 0;
                    foreach (var evt in track.GetEventEnumerator())
                    {
                        absoluteTime += evt.DeltaTime;
                        if (evt.EventType == MidiEventType.NoteOn || evt.EventType == MidiEventType.NoteOff)
                        {
                            results.Add((evt, trackIndex, absoluteTime));
                        }
                    }
                });

            // �������ʱ������
            return results.OrderBy(x => x.AbsoluteTime);
        }

        /// <summary>
        /// ��ȡָ������е����������¼�
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
    /// MIDI�ļ�ͳ����Ϣ
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
        /// ������ƵĲ���ʱ�����룩
        /// ��Ҫ�ļ��е�tempo��Ϣ����׼ȷ����
        /// </summary>
        public double EstimatedDurationSeconds(int microsecondsPerQuarterNote = 500000)
        {
            if (Header.IsSmpteFormat)
            {
                // SMPTE��ʽ��ʱ�����
                return (double)MaxTicks / (Header.SmpteFrameRate * Header.TicksPerFrame);
            }
            else
            {
                // ��׼��ʽ��ʱ�����
                double secondsPerTick = (double)microsecondsPerQuarterNote / (Header.TicksPerQuarterNote * 1_000_000.0);
                return MaxTicks * secondsPerTick;
            }
        }
    }
}