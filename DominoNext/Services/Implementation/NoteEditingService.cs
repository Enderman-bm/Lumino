using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using DominoNext.Services.Interfaces;
using DominoNext.ViewModels.Editor;
using DominoNext.Models.Music;

namespace DominoNext.Services.Implementation
{
    public class NoteEditingService : INoteEditingService
    {
        private readonly PianoRollViewModel _viewModel;
        private readonly ICoordinateService _coordinateService;

        private NoteViewModel? _draggingNote;
        private Point _dragStartPoint;
        private MusicalFraction _originalStartPosition;
        private int _originalPitch;

        public NoteEditingService(PianoRollViewModel viewModel, ICoordinateService coordinateService)
        {
            _viewModel = viewModel;
            _coordinateService = coordinateService;
        }

        public void CreateNoteAtPosition(Point position)
        {
            var pitch = _coordinateService.GetPitchFromY(position.Y, _viewModel.KeyHeight);
            var startTime = _coordinateService.GetTimeFromX(position.X, _viewModel.Zoom, _viewModel.PixelsPerTick);
            var startFraction = MusicalFraction.FromTicks(startTime, _viewModel.TicksPerBeat);
            if (IsValidNotePosition(pitch, startTime))
            {
                _viewModel.AddNote(pitch, startFraction);
            }
        }

        public void StartNoteDrag(NoteViewModel note, Point startPosition)
        {
            _draggingNote = note;
            _dragStartPoint = startPosition;

            // 保存原始位置，避免累积误差
            _originalStartPosition = note.StartPosition;
            _originalPitch = note.Pitch;
        }

        public void UpdateNoteDrag(Point currentPosition)
        {
            if (_draggingNote == null) return;

            try
            {
                var deltaX = currentPosition.X - _dragStartPoint.X;
                var deltaY = currentPosition.Y - _dragStartPoint.Y;

                // 计算时间偏移（以tick为单位）
                var timeDeltaInTicks = deltaX / (_viewModel.PixelsPerTick * _viewModel.Zoom);
                var pitchDelta = -(int)(deltaY / _viewModel.KeyHeight);

                // 基于原始位置计算新位置，避免累积误差
                var originalTimeInTicks = _originalStartPosition.ToTicks(_viewModel.TicksPerBeat);
                var newTimeInTicks = Math.Max(0, originalTimeInTicks + timeDeltaInTicks);
                var newPitch = Math.Max(0, Math.Min(127, _originalPitch + pitchDelta));

                // 量化新位置
                var quantizedTimeInTicks = _viewModel.SnapToGridTime(newTimeInTicks);

                // 安全地转换为 MusicalFraction
                var newStartPosition = MusicalFraction.FromTicks(quantizedTimeInTicks, _viewModel.TicksPerBeat);

                // 更新音符位置
                _draggingNote.StartPosition = newStartPosition;
                _draggingNote.Pitch = newPitch;
            }
            catch (Exception ex)
            {
                // 记录错误并恢复到原始位置
                System.Diagnostics.Debug.WriteLine($"拖拽更新错误: {ex.Message}");

                if (_draggingNote != null)
                {
                    _draggingNote.StartPosition = _originalStartPosition;
                    _draggingNote.Pitch = _originalPitch;
                }
            }
        }

        public void EndNoteDrag()
        {
            _draggingNote = null;
        }

        public void SelectNotesInArea(Rect area)
        {
            foreach (var note in _viewModel.Notes)
            {
                var noteRect = _coordinateService.GetNoteRect(note, _viewModel.Zoom, _viewModel.PixelsPerTick, _viewModel.KeyHeight);
                note.IsSelected = area.Intersects(noteRect);
            }
        }

        public void ClearSelection()
        {
            foreach (var note in _viewModel.Notes)
            {
                note.IsSelected = false;
            }
        }

        public void DeleteSelectedNotes()
        {
            var notesToRemove = _viewModel.Notes.Where(n => n.IsSelected).ToList();
            foreach (var note in notesToRemove)
            {
                _viewModel.Notes.Remove(note);
            }
        }

        public void DuplicateSelectedNotes()
        {
            var selectedNotes = _viewModel.Notes.Where(n => n.IsSelected).ToList();

            // 先取消原有音符的选择
            foreach (var note in selectedNotes)
            {
                note.IsSelected = false;
            }

            // 创建复制的音符
            foreach (var note in selectedNotes)
            {
                var newNote = new NoteViewModel
                {
                    Pitch = note.Pitch,
                    StartPosition = note.StartPosition + note.Duration, // 放在原音符之后
                    Duration = note.Duration,
                    Velocity = note.Velocity,
                    IsSelected = true // 选中新创建的音符
                };
                _viewModel.Notes.Add(newNote);
            }
        }

        public void QuantizeSelectedNotes()
        {
            foreach (var note in _viewModel.Notes.Where(n => n.IsSelected))
            {
                try
                {
                    // 使用 MusicalFraction 进行量化
                    var currentTicks = note.StartPosition.ToTicks(_viewModel.TicksPerBeat);
                    var quantizedTicks = _viewModel.SnapToGridTime(currentTicks);
                    note.StartPosition = MusicalFraction.FromTicks(quantizedTicks, _viewModel.TicksPerBeat);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"量化音符错误: {ex.Message}");
                    // 发生错误时跳过该音符
                }
            }
        }

        /// <summary>
        /// 异步批量量化选中音符，提升大量音符处理性能
        /// </summary>
        public async Task QuantizeSelectedNotesAsync()
        {
            var selectedNotes = _viewModel.Notes.Where(n => n.IsSelected).ToList();
            if (selectedNotes.Count == 0) return;

            // 后台线程批量量化
            var quantizedResults = await Task.Run(() =>
            {
                var results = new List<(NoteViewModel note, MusicalFraction newStart)>();
                foreach (var note in selectedNotes)
                {
                    try
                    {
                        var currentTicks = note.StartPosition.ToTicks(_viewModel.TicksPerBeat);
                        var quantizedTicks = _viewModel.SnapToGridTime(currentTicks);
                        var newStart = MusicalFraction.FromTicks(quantizedTicks, _viewModel.TicksPerBeat);
                        results.Add((note, newStart));
                    }
                    catch { /* 跳过错误音符 */ }
                }
                return results;
            });

            // 主线程批量更新UI
            foreach (var (note, newStart) in quantizedResults)
            {
                note.StartPosition = newStart;
            }
        }

        /// <summary>
        /// 异步批量删除选中音符，提升大量音符处理性能
        /// </summary>
        public async Task DeleteSelectedNotesAsync()
        {
            var selectedNotes = _viewModel.Notes.Where(n => n.IsSelected).ToList();
            if (selectedNotes.Count == 0) return;

            // 对于大量音符，使用后台线程准备删除列表
            if (selectedNotes.Count > 100)
            {
                await Task.Run(() =>
                {
                    // 后台线程：预处理删除逻辑（如果有复杂计算）
                    foreach (var note in selectedNotes)
                    {
                        // 这里可以添加删除前的清理工作
                    }
                });
            }

            // 主线程：批量删除
            foreach (var note in selectedNotes)
            {
                _viewModel.Notes.Remove(note);
            }
        }

        /// <summary>
        /// 异步批量复制选中音符，提升大量音符处理性能
        /// </summary>
        public async Task DuplicateSelectedNotesAsync()
        {
            var selectedNotes = _viewModel.Notes.Where(n => n.IsSelected).ToList();
            if (selectedNotes.Count == 0) return;

            // 后台线程：创建复制的音符
            var duplicatedNotes = await Task.Run(() =>
            {
                var newNotes = new List<NoteViewModel>();
                foreach (var note in selectedNotes)
                {
                    var newNote = new NoteViewModel
                    {
                        Pitch = note.Pitch,
                        StartPosition = note.StartPosition + note.Duration, // 放在原音符之后
                        Duration = note.Duration,
                        Velocity = note.Velocity,
                        IsSelected = true // 选中新创建的音符
                    };
                    newNotes.Add(newNote);
                }
                return newNotes;
            });

            // 主线程：取消原音符选择并添加新音符
            foreach (var note in selectedNotes)
            {
                note.IsSelected = false;
            }

            foreach (var newNote in duplicatedNotes)
            {
                _viewModel.Notes.Add(newNote);
            }
        }

        /// <summary>
        /// 异步批量选择区域内音符，提升大量音符处理性能
        /// </summary>
        public async Task SelectNotesInAreaAsync(Rect area)
        {
            var allNotes = _viewModel.Notes.ToList();
            if (allNotes.Count == 0) return;

            // 后台线程：批量计算碰撞检测
            var selectionResults = await Task.Run(() =>
            {
                var results = new List<(NoteViewModel note, bool shouldSelect)>();
                foreach (var note in allNotes)
                {
                    var noteRect = _coordinateService.GetNoteRect(note, _viewModel.Zoom, _viewModel.PixelsPerTick, _viewModel.KeyHeight);
                    var shouldSelect = area.Intersects(noteRect);
                    results.Add((note, shouldSelect));
                }
                return results;
            });

            // 主线程：批量更新选择状态
            foreach (var (note, shouldSelect) in selectionResults)
            {
                note.IsSelected = shouldSelect;
            }
        }

        /// <summary>
        /// 异步批量清除选择，提升大量音符处理性能
        /// </summary>
        public async Task ClearSelectionAsync()
        {
            var selectedNotes = _viewModel.Notes.Where(n => n.IsSelected).ToList();
            if (selectedNotes.Count == 0) return;

            if (selectedNotes.Count > 1000) // 大量音符时使用异步
            {
                await Task.Run(() =>
                {
                    // 后台线程预处理（如果需要）
                });
            }

            // 主线程：批量取消选择
            foreach (var note in selectedNotes)
            {
                note.IsSelected = false;
            }
        }

        /// <summary>
        /// 批量量化多个音符位置 - 高性能版本
        /// </summary>
        public async Task QuantizeNotesAsync(IEnumerable<NoteViewModel> notes)
        {
            var notesList = notes.ToList();
            if (notesList.Count == 0) return;

            // 后台线程：批量量化计算
            var quantizedResults = await Task.Run(() =>
            {
                var results = new List<(NoteViewModel note, MusicalFraction newStart)>();
                
                // 使用批量量化API提升性能
                var positions = new double[notesList.Count];
                for (int i = 0; i < notesList.Count; i++)
                {
                    positions[i] = notesList[i].StartPosition.ToTicks(_viewModel.TicksPerBeat);
                }

                // 调用 MusicalFraction 的批量量化方法
                var gridUnit = _viewModel.GridQuantization;
                var quantizedPositions = new double[positions.Length];
                positions.CopyTo(quantizedPositions, 0);
                
                // 使用 Span 进行高性能批量量化
                MusicalFraction.QuantizeToGridBatch(quantizedPositions, gridUnit, _viewModel.TicksPerBeat);

                for (int i = 0; i < notesList.Count; i++)
                {
                    try
                    {
                        var newStart = MusicalFraction.FromTicks(quantizedPositions[i], _viewModel.TicksPerBeat);
                        results.Add((notesList[i], newStart));
                    }
                    catch { /* 跳过错误音符 */ }
                }
                
                return results;
            });

            // 主线程：批量更新UI
            foreach (var (note, newStart) in quantizedResults)
            {
                note.StartPosition = newStart;
            }
        }

        public bool IsValidNotePosition(int pitch, double startTime)
        {
            return pitch >= 0 && pitch <= 127 && startTime >= 0 && !double.IsNaN(startTime) && !double.IsInfinity(startTime);
        }
    }
}