using DominoNext.Models.Music;
using DominoNext.Services.Interfaces;
using MidiReader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// 项目存储服务实现类
    /// 负责项目的保存、加载和MIDI文件的导入导出
    /// </summary>
    public class ProjectStorageService : IProjectStorageService
    {
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
                // TODO: 实现项目保存功能
                await Task.Delay(100); // 占位，实际实现时移除
                return true;
            }
            catch (Exception)
            {
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
                // TODO: 实现MIDI导出功能
                await Task.Delay(100); // 占位，实际实现时移除
                return true;
            }
            catch (Exception)
            {
                return false;
            }
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
                var ticksPerBeat = midiFile.Header.TicksPerQuarterNote > 0 ? midiFile.Header.TicksPerQuarterNote : DEFAULT_TICKS_PER_BEAT;

                // 遍历每个音轨，并记录当前音轨索引
                for (int trackIndex = 0; trackIndex < midiFile.Tracks.Count; trackIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var progressPercent = 100.0 + (double)trackIndex / midiFile.Tracks.Count * 100; // 从100%开始，因为文件加载已经占用了0-100%
                    progress?.Report((Math.Min(progressPercent, 200), $"正在处理音轨 {trackIndex + 1}/{midiFile.Tracks.Count}..."));

                    var track = midiFile.Tracks[trackIndex];
                    
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

                            // 创建音符模型，并设置音轨索引
                            var note = new Note
                            {
                                Pitch = midiEvent.Data1, // Data1代表音高
                                StartPosition = ConvertTicksToMusicalFraction(noteInfo.StartTime, ticksPerBeat),
                                Duration = ConvertTicksToMusicalFraction(duration, ticksPerBeat),
                                Velocity = noteInfo.Velocity,
                                TrackIndex = trackIndex // 设置音符所属的音轨索引
                            };

                            notes.Add(note);
                        }
                    }
                }

                progress?.Report((200, $"音符转换完成，共处理 {notes.Count} 个音符"));
                return notes;
            }, cancellationToken);
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