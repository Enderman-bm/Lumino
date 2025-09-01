using System;
using Avalonia;
using DominoNext.Services.Interfaces;
using DominoNext.Models.Music;
using DominoNext.ViewModels.Editor.State;
using System.Diagnostics;

namespace DominoNext.ViewModels.Editor.Modules
{
    /// <summary>
    /// 音符预览功能模块
    /// </summary>
    public class NotePreviewModule
    {
        private readonly ICoordinateService _coordinateService;
        private PianoRollViewModel? _pianoRollViewModel;

        public NoteViewModel? PreviewNote { get; private set; }

        public NotePreviewModule(ICoordinateService coordinateService)
        {
            _coordinateService = coordinateService;
        }

        public void SetPianoRollViewModel(PianoRollViewModel viewModel)
        {
            _pianoRollViewModel = viewModel;
        }

        /// <summary>
        /// 更新预览音符
        /// </summary>
        public void UpdatePreview(Point position)
        {
            if (_pianoRollViewModel == null) return;

            // 正在创建音符时不显示普通预览
            if (_pianoRollViewModel.CreationModule.IsCreatingNote)
            {
                ClearPreview();
                return;
            }

            // 正在调整大小时不显示普通预览
            if (_pianoRollViewModel.ResizeState.IsResizing)
            {
                ClearPreview();
                return;
            }

            if (_pianoRollViewModel.CurrentTool != EditorTool.Pencil)
            {
                ClearPreview();
                return;
            }

            // 检查是否悬停在音符上，使用支持滚动偏移量的方法
            var hoveredNote = _pianoRollViewModel.SelectionModule.GetNoteAtPosition(position, _pianoRollViewModel.Notes, 
                _pianoRollViewModel.TimeToPixelScale, _pianoRollViewModel.KeyHeight);
            if (hoveredNote != null)
            {
                // 悬停在音符时不显示预览（因为要显示拖拽光标）
                ClearPreview();
                return;
            }

            // 检查是否悬停在可调整大小的音符边缘上
            if (hoveredNote != null)
            {
                var handle = _pianoRollViewModel.GetResizeHandleAtPosition(position, hoveredNote);
                if (handle == ResizeHandle.StartEdge || handle == ResizeHandle.EndEdge)
                {
                    // 悬停在调整边缘上，不显示预览音符
                    ClearPreview();
                    return;
                }
            }

            // 使用支持滚动偏移量的坐标转换方法
            var pitch = _pianoRollViewModel.GetPitchFromScreenY(position.Y);
            var startTime = _pianoRollViewModel.GetTimeFromScreenX(position.X);

            if (IsValidNotePosition(pitch, startTime))
            {
                var quantizedStartTime = _pianoRollViewModel.SnapToGridTime(startTime);
                var quantizedPosition = MusicalFraction.FromTicks(quantizedStartTime, _pianoRollViewModel.TicksPerBeat);

                // 只在预览音符实际改变时才更新，并增加更精确的比较
                bool shouldUpdate = false;
                
                if (PreviewNote == null)
                {
                    shouldUpdate = true;
                }
                else if (PreviewNote.Pitch != pitch)
                {
                    shouldUpdate = true;
                }
                else if (!PreviewNote.StartPosition.Equals(quantizedPosition))
                {
                    shouldUpdate = true;
                }
                else if (!PreviewNote.Duration.Equals(_pianoRollViewModel.UserDefinedNoteDuration))
                {
                    shouldUpdate = true;
                }

                if (shouldUpdate)
                {
                    PreviewNote = new NoteViewModel
                    {
                        Pitch = pitch,
                        StartPosition = quantizedPosition,
                        Duration = _pianoRollViewModel.UserDefinedNoteDuration,
                        Velocity = 100,
                        IsPreview = true
                    };

                    OnPreviewUpdated?.Invoke();
                }
            }
            else
            {
                ClearPreview();
            }
        }

        /// <summary>
        /// 清除预览音符
        /// </summary>
        public void ClearPreview()
        {
            if (PreviewNote != null)
            {
                PreviewNote = null;
                OnPreviewUpdated?.Invoke();
            }
        }

        private bool IsValidNotePosition(int pitch, double startTime)
        {
            return pitch >= 0 && pitch <= 127 && startTime >= 0;
        }

        // 事件
        public event Action? OnPreviewUpdated;
    }
}