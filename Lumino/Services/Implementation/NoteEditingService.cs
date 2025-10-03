using System;
using System.Linq;
using System.Collections.Generic;
using Avalonia;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor;
using Lumino.Models.Music;
using EnderDebugger;

namespace Lumino.Services.Implementation
{
    public class NoteEditingService : INoteEditingService
    {
        private readonly PianoRollViewModel _viewModel;
        private readonly ICoordinateService _coordinateService;
        private readonly EnderLogger _logger;

        private NoteViewModel? _draggingNote;
        private Point _dragStartPoint;
        private MusicalFraction _originalStartPosition;
        private int _originalPitch;

        public NoteEditingService(PianoRollViewModel viewModel, ICoordinateService coordinateService)
        {
            _viewModel = viewModel;
            _coordinateService = coordinateService;
            _logger = new EnderLogger("NoteEditingService");
            _logger.Info("NoteEditingService", "[EnderDebugger][2025-10-02 18:41:03.114][EnderLogger][NoteEditingService]音符编辑服务已创建");
        }

        public void CreateNoteAtPosition(Point position)
        {
            var pitch = _coordinateService.GetPitchFromY(position.Y, _viewModel.KeyHeight);
            var timeValue = _coordinateService.GetTimeFromX(position.X, _viewModel.TimeToPixelScale);
            _logger.Info("NoteEditingService", $"[EnderDebugger][2025-10-02 18:41:03.114][EnderLogger][NoteEditingService]尝试在位置({position.X},{position.Y})创建音符，音高:{pitch}, 时间:{timeValue}");
            if (IsValidNotePosition(pitch, timeValue))
            {
                var startPosition = MusicalFraction.FromDouble(timeValue);
                _viewModel.AddNote(pitch, startPosition);
                _logger.Info("NoteEditingService", $"[EnderDebugger][2025-10-02 18:41:03.114][EnderLogger][NoteEditingService]已添加音符，音高:{pitch}, 起始:{startPosition}");
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

                // 计算时间偏移（基于分数）
                var timeDelta = deltaX / _viewModel.BaseQuarterNoteWidth; // 以四分音符为单位
                var pitchDelta = -(int)(deltaY / _viewModel.KeyHeight);

                // 基于原始位置计算新位置，避免累积误差
                var originalTimeValue = _originalStartPosition.ToDouble();
                var newTimeValue = Math.Max(0, originalTimeValue + timeDelta);
                var newPitch = Math.Max(0, Math.Min(127, _originalPitch + pitchDelta));

                // 转换为分数并量化
                var newTimeFraction = MusicalFraction.FromDouble(newTimeValue);
                var quantizedPosition = _viewModel.SnapToGrid(newTimeFraction);

                // 更新音符位置
                _draggingNote.StartPosition = quantizedPosition;
                _draggingNote.Pitch = newPitch;
            }
            catch (Exception ex)
            {
                // 记录错误并恢复到原始位置
                _logger.LogException(ex, "UpdateNoteDrag", "拖拽更新错误");

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
                var noteRect = _coordinateService.GetNoteRect(note, _viewModel.TimeToPixelScale, _viewModel.KeyHeight);
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
                    // 使用基于分数的量化
                    var quantizedPosition = _viewModel.SnapToGrid(note.StartPosition);
                    note.StartPosition = quantizedPosition;
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "QuantizeNote", "量化音符错误");
                    // 发生错误时跳过该音符
                }
            }
        }

        public bool IsValidNotePosition(int pitch, double timeValue)
        {
            return pitch >= 0 && pitch <= 127 && timeValue >= 0 && !double.IsNaN(timeValue) && !double.IsInfinity(timeValue);
        }
    }
}