using System;
using Avalonia;
using DominoNext.Services.Interfaces;
using DominoNext.Models.Music;
using DominoNext.ViewModels.Editor.State;
using DominoNext.ViewModels.Editor.Modules.Base;
using DominoNext.ViewModels.Editor.Services;
using System.Diagnostics;

namespace DominoNext.ViewModels.Editor.Modules
{
    /// <summary>
    /// 音符预览功能模块 - 基于分数的新实现
    /// 重构后使用基类和通用服务，减少重复代码
    /// </summary>
    public class NotePreviewModule : EditorModuleBase
    {
        public override string ModuleName => "NotePreview";

        public NoteViewModel? PreviewNote { get; private set; }

        public NotePreviewModule(ICoordinateService coordinateService) : base(coordinateService)
        {
        }

        /// <summary>
        /// 更新预览音符 - 使用基类的通用方法
        /// </summary>
        public void UpdatePreview(Point position)
        {
            if (_pianoRollViewModel == null) return;

            // 在创建音符时不显示通用预览
            if (_pianoRollViewModel.CreationModule.IsCreatingNote)
            {
                ClearPreview();
                return;
            }

            // 在调整大小时不显示通用预览
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
                // 悬停在音符时不显示预览音符（为了显示拖拽光标）
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

            // 使用基类的通用坐标转换和验证
            var pitch = GetPitchFromPosition(position);
            var timeValue = GetTimeFromPosition(position);

            if (EditorValidationService.IsValidNotePosition(pitch, timeValue))
            {
                // 使用基类的通用量化方法
                var quantizedPosition = GetQuantizedTimeFromPosition(position);

                // 只在预览音符实际改变时才更新，添加更准确的比较
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

        // 事件
        public event Action? OnPreviewUpdated;
    }
}