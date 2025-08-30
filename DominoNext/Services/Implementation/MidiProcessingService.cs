using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using DominoNext.Services.Interfaces;
using DominoNext.ViewModels.Editor;
using DominoNext.Models.Music;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// MIDI文件处理服务 - 负责MIDI文件的加载、解析和转换
    /// </summary>
    public class MidiProcessingService
    {
        private readonly IMidiLoader _midiLoader;

        public MidiProcessingService(IMidiLoader? midiLoader = null)
        {
            _midiLoader = midiLoader ?? new MidiLoader();
        }

        /// <summary>
        /// 从MIDI文件加载轨道数据到PianoRollViewModel
        /// </summary>
        /// <param name="filePath">MIDI文件路径</param>
        /// <param name="pianoRoll">目标PianoRollViewModel</param>
        /// <returns>是否加载成功</returns>
        public async Task<bool> LoadMidiFileAsync(string filePath, PianoRollViewModel pianoRoll)
        {
            try
            {
                var midiFile = await Task.Run(() => _midiLoader.LoadMidi(filePath));
                return await ProcessMidiFileAsync(midiFile, pianoRoll);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载MIDI文件失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 处理MIDI文件数据
        /// </summary>
        private async Task<bool> ProcessMidiFileAsync(MidiFile midiFile, PianoRollViewModel pianoRoll)
        {
            try
            {
                // 获取MIDI文件的基本信息
                var tempoMap = midiFile.GetTempoMap();
                var timeDivision = midiFile.TimeDivision;
                int ticksPerBeat = MusicalFraction.QUARTER_NOTE_TICKS; // 默认值
                int beatsPerMeasure = 4; // 默认值

                if (timeDivision is TicksPerQuarterNoteTimeDivision tpq)
                {
                    ticksPerBeat = (int)tpq.TicksPerQuarterNote;
                    System.Diagnostics.Debug.WriteLine($"MIDI文件PPQ: {ticksPerBeat}");
                }

                // 读取拍号
                var timeSignature = tempoMap.GetTimeSignatureAtTime(new MidiTimeSpan(0));
                if (timeSignature != null)
                {
                    beatsPerMeasure = timeSignature.Numerator;
                    System.Diagnostics.Debug.WriteLine($"MIDI文件拍号: {timeSignature.Numerator}/{timeSignature.Denominator}");
                }

                // 更新PianoRoll的时间设置
                await UpdatePianoRollSettings(pianoRoll, ticksPerBeat, beatsPerMeasure);

                // 解析轨道
                var tracks = await ParseTracksAsync(midiFile, tempoMap, ticksPerBeat);
                
                // 计算总小节数
                var maxEndTick = tracks.SelectMany(t => t.Notes)
                    .DefaultIfEmpty()
                    .Max(note => note?.StartPosition.ToTicks(ticksPerBeat) + note?.Duration.ToTicks(ticksPerBeat) ?? 0);
                
                var totalMeasures = Math.Max(1, (int)(maxEndTick / (ticksPerBeat * beatsPerMeasure)) + 1);

                // 应用到PianoRoll
                await ApplyTracksToViewModel(pianoRoll, tracks, totalMeasures);

                System.Diagnostics.Debug.WriteLine($"成功加载MIDI文件，共{tracks.Count}个轨道，{totalMeasures}小节");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理MIDI文件失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 更新PianoRoll的时间设置
        /// </summary>
        private async Task UpdatePianoRollSettings(PianoRollViewModel pianoRoll, int ticksPerBeat, int beatsPerMeasure)
        {
            // 必须在UI线程上修改ObservableProperty
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                // 已经在UI线程上
                pianoRoll.TicksPerBeat = ticksPerBeat;
                pianoRoll.BeatsPerMeasure = beatsPerMeasure;
            }
            else
            {
                // 切换到UI线程
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    pianoRoll.TicksPerBeat = ticksPerBeat;
                    pianoRoll.BeatsPerMeasure = beatsPerMeasure;
                });
            }
        }

        /// <summary>
        /// 解析MIDI轨道 - 修复时间单位转换
        /// </summary>
        private async Task<List<TrackViewModel>> ParseTracksAsync(MidiFile midiFile, TempoMap tempoMap, int ticksPerBeat)
        {
            return await Task.Run(() =>
            {
                var tracks = new List<TrackViewModel>();

                foreach (var trackChunk in midiFile.GetTrackChunks())
                {
                    var trackVm = new TrackViewModel();
                    
                    // 尝试获取轨道名称
                    string? trackName = null;
                    var seqName = trackChunk.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(seqName?.Text)) 
                        trackName = seqName.Text;
                    
                    if (string.IsNullOrWhiteSpace(trackName))
                        trackName = $"轨道 {tracks.Count + 1}";
                    
                    trackVm.Name = trackName;
                    System.Diagnostics.Debug.WriteLine($"解析轨道: {trackName}");

                    // 解析音符 - 修复tick转换
                    var notes = trackChunk.GetNotes();
                    foreach (var note in notes)
                    {
                        // 关键修复：使用MIDI文件的原始tick值，而不是转换到标准96 PPQ
                        // 直接使用原始tick值计算分数
                        var startTicks = (double)note.Time;
                        var durationTicks = (double)note.Length;
                        
                        // 使用MIDI文件的实际PPQ进行转换
                        var startFraction = MusicalFraction.FromTicks(startTicks, ticksPerBeat);
                        var durationFraction = MusicalFraction.FromTicks(durationTicks, ticksPerBeat);

                        var noteVm = new NoteViewModel
                        {
                            Pitch = note.NoteNumber,
                            StartPosition = startFraction,
                            Duration = durationFraction,
                            Velocity = Math.Clamp((int)note.Velocity, 0, 127)
                        };
                        
                        trackVm.Notes.Add(noteVm);
                        
                        // 调试输出：验证转换
                        if (trackVm.Notes.Count <= 5) // 只输出前5个音符的调试信息
                        {
                            System.Diagnostics.Debug.WriteLine($"音符转换: MIDI Tick={note.Time}, 分数={startFraction}, 时长Tick={note.Length}, 分数={durationFraction}");
                        }
                    }

                    if (trackVm.Notes.Count > 0)
                    {
                        tracks.Add(trackVm);
                    }
                }

                return tracks;
            });
        }

        /// <summary>
        /// 将解析的轨道应用到ViewModel
        /// </summary>
        private async Task ApplyTracksToViewModel(PianoRollViewModel pianoRoll, List<TrackViewModel> tracks, int totalMeasures)
        {
            // 必须在UI线程上修改ObservableCollection
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                // 已经在UI线程上
                ApplyTracksToViewModelCore(pianoRoll, tracks, totalMeasures);
            }
            else
            {
                // 切换到UI线程
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ApplyTracksToViewModelCore(pianoRoll, tracks, totalMeasures);
                });
            }
        }

        /// <summary>
        /// 核心的轨道应用逻辑 - 必须在UI线程上执行
        /// </summary>
        private void ApplyTracksToViewModelCore(PianoRollViewModel pianoRoll, List<TrackViewModel> tracks, int totalMeasures)
        {
            // 清除现有数据
            pianoRoll.Tracks.Clear();
            pianoRoll.Notes.Clear();

            // 添加轨道
            foreach (var track in tracks)
            {
                pianoRoll.Tracks.Add(track);
            }

            // 设置总小节数
            pianoRoll.TotalMeasures = totalMeasures;

            // 如果有轨道，选择第一个作为当前轨道
            if (pianoRoll.Tracks.Count > 0)
            {
                pianoRoll.SelectedTrack = pianoRoll.Tracks[0];
            }

            // 修复：MIDI加载后设置最佳缩放
            SetOptimalZoomForLoadedContent(pianoRoll);
        }

        /// <summary>
        /// 修复：为加载的MIDI内容设置最佳缩放
        /// </summary>
        private void SetOptimalZoomForLoadedContent(PianoRollViewModel pianoRoll)
        {
            // 计算一个小节应该占据的像素宽度（目标：在1920px宽屏幕上显示约6-8个小节）
            var targetMeasureWidth = 240.0; // 目标小节宽度
            var measuresPerScreen = 8.0; // 目标每屏显示的小节数
            
            // 根据PPQ调整缩放策略
            var currentPPQ = pianoRoll.TicksPerBeat;
            var beatsPerMeasure = pianoRoll.BeatsPerMeasure;
            
            // 计算理想的PixelsPerTick值
            var idealPixelsPerTick = targetMeasureWidth / (currentPPQ * beatsPerMeasure);
            
            // 根据PPQ范围调整
            if (currentPPQ >= 960) // 超高精度MIDI
            {
                idealPixelsPerTick *= 0.3;
            }
            else if (currentPPQ >= 480) // 高精度MIDI (如1536)
            {
                idealPixelsPerTick *= 0.4;
            }
            else if (currentPPQ >= 240) // 中等精度
            {
                idealPixelsPerTick *= 0.6;
            }
            else if (currentPPQ < 96) // 低精度
            {
                idealPixelsPerTick *= 1.5;
            }
            
            // 计算最终缩放值（考虑基础缩放比例）
            var baseScale = 96.0 / currentPPQ;
            var optimalZoom = idealPixelsPerTick / baseScale;
            
            // 限制在合理范围内
            optimalZoom = Math.Max(0.1, Math.Min(5.0, optimalZoom));
            
            System.Diagnostics.Debug.WriteLine($"MIDI加载完成，设置最佳缩放:");
            System.Diagnostics.Debug.WriteLine($"  PPQ: {currentPPQ}");
            System.Diagnostics.Debug.WriteLine($"  拍数/小节: {beatsPerMeasure}");
            System.Diagnostics.Debug.WriteLine($"  目标小节宽度: {targetMeasureWidth}px");
            System.Diagnostics.Debug.WriteLine($"  计算出的理想PixelsPerTick: {idealPixelsPerTick}");
            System.Diagnostics.Debug.WriteLine($"  基础缩放比例: {baseScale}");
            System.Diagnostics.Debug.WriteLine($"  最终缩放值: {optimalZoom}");
            
            // 应用缩放
            pianoRoll.Zoom = optimalZoom;
            // 注意：需要确保ZoomSliderValue也同步更新
            // 这会在PianoRollViewModel的OnZoomChanged中自动处理
            
            // 重建音符索引以适应新的缩放
            pianoRoll.RebuildNoteIndex();
        }

        /// <summary>
        /// 验证MIDI文件格式
        /// </summary>
        public bool ValidateMidiFile(string filePath)
        {
            try
            {
                var midiFile = _midiLoader.LoadMidi(filePath);
                return midiFile != null && midiFile.GetTrackChunks().Any();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取MIDI文件信息
        /// </summary>
        public async Task<MidiFileInfo?> GetMidiFileInfoAsync(string filePath)
        {
            try
            {
                var midiFile = await Task.Run(() => _midiLoader.LoadMidi(filePath));
                var tempoMap = midiFile.GetTempoMap();
                
                var trackCount = midiFile.GetTrackChunks().Count();
                var totalNotes = midiFile.GetTrackChunks().SelectMany(t => t.GetNotes()).Count();
                
                var timeDivision = midiFile.TimeDivision;
                int ticksPerBeat = MusicalFraction.QUARTER_NOTE_TICKS;
                if (timeDivision is TicksPerQuarterNoteTimeDivision tpq)
                {
                    ticksPerBeat = (int)tpq.TicksPerQuarterNote;
                }

                var timeSignature = tempoMap.GetTimeSignatureAtTime(new MidiTimeSpan(0));
                
                return new MidiFileInfo
                {
                    TrackCount = trackCount,
                    NoteCount = totalNotes,
                    TicksPerBeat = ticksPerBeat,
                    TimeSignature = timeSignature != null ? $"{timeSignature.Numerator}/{timeSignature.Denominator}" : "4/4"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取MIDI文件信息失败: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// MIDI文件信息
    /// </summary>
    public class MidiFileInfo
    {
        public int TrackCount { get; set; }
        public int NoteCount { get; set; }
        public int TicksPerBeat { get; set; }
        public string TimeSignature { get; set; } = "4/4";
    }
}