using System;
using Avalonia;
using Lumino.Services.Interfaces;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.State;
using Lumino.ViewModels.Editor.Modules.Base;
using Lumino.ViewModels.Editor.Services;
using System.Diagnostics;
using Lumino.Services.Implementation;
using EnderDebugger;

namespace Lumino.ViewModels.Editor.Modules
{
    /// <summary>
    /// 音符预览处理模块 - 用于处理音符预览
    /// 优化用户体验：使用防抖机制，避免重复创建
    /// </summary>
    public class NotePreviewModule : EditorModuleBase
    {
        public override string ModuleName => "NotePreview";
        
        private readonly WaveTableManager _waveTableManager;
        private readonly EnderLogger _logger;

        public NoteViewModel? PreviewNote { get; private set; }

        public NotePreviewModule(ICoordinateService coordinateService) : base(coordinateService)
        {
            _logger = EnderLogger.Instance;
            // 初始化播表管理器
            _waveTableManager = new WaveTableManager();
        }

        /// <summary>
        /// 更新预览音符 - 使用用户监听和防抖机制
        /// </summary>
        public void UpdatePreview(Point position)
        {
            _logger.Info("NotePreviewModule", $"UpdatePreview called, position={position}");
            
            if (_pianoRollViewModel == null)
            {
                _logger.Info("NotePreviewModule", "_pianoRollViewModel is null");
                return;
            }

            // 在创建音符时不要显示通用预览
            if (_pianoRollViewModel.CreationModule.IsCreatingNote)
            {
                _logger.Info("NotePreviewModule", "IsCreatingNote=true, clearing preview");
                ClearPreview();
                return;
            }

            // 在调整大小时不要显示通用预览
            if (_pianoRollViewModel.ResizeState.IsResizing)
            {
                _logger.Info("NotePreviewModule", "IsResizing=true, clearing preview");
                ClearPreview();
                return;
            }

            var currentTool = _pianoRollViewModel.CurrentTool;
            _logger.Info("NotePreviewModule", $"CurrentTool={currentTool}");
            
            if (currentTool != EditorTool.Pencil)
            {
                _logger.Info("NotePreviewModule", $"CurrentTool is not Pencil ({currentTool}), clearing preview");
                ClearPreview();
                return;
            }

            // 检查是否悬停在音符上，如果是则不显示预览（显示拖拽光标）
            var hoveredNote = _pianoRollViewModel.SelectionModule.GetNoteAtPosition(position, _pianoRollViewModel.Notes, 
                _pianoRollViewModel.TimeToPixelScale, _pianoRollViewModel.KeyHeight);
            if (hoveredNote != null)
            {
                // 悬停在音符上时清除预览
                ClearPreview();
                return;
            }

            // 检查是否悬停在可调整大小的音符边缘上
            if (hoveredNote != null)
            {
                var handle = _pianoRollViewModel.GetResizeHandleAtPosition(position, hoveredNote);
                if (handle == ResizeHandle.StartEdge || handle == ResizeHandle.EndEdge)
                {
                    // 悬停在调整边缘上时清除预览
                    ClearPreview();
                    return;
                }
            }

            // 使用用户监听坐标系统转换音高和时间值以确保准确性
            var pitch = GetPitchFromPosition(position);
            var timeValue = GetTimeFromPosition(position);

            if (EditorValidationService.IsValidNotePosition(pitch, timeValue))
            {
                // 使用用户监听时间位置进行量化
                var quantizedPosition = GetQuantizedTimeFromPosition(position);

                // 只有在预览音符实际改变时才更新，以减少不必要的比较
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

                    // 移除音频反馈功能
                    // PlayAudioFeedback(pitch);
                    
                    OnPreviewUpdated?.Invoke();
                }
            }
            else
            {
                ClearPreview();
            }
        }

        /// <summary>
        /// 播放音频反馈 - 已移除功能
        /// </summary>
        /// <param name="pitch">音高</param>
        private void PlayAudioFeedback(int pitch)
        {
            // 已移除预览时的音频反馈功能
            // 根据用户需求，仅在实际创建音符时播放音频反馈
            /*
            // 检查是否应该播放音频反馈
            if (!ShouldPlayAudioFeedback()) 
                return;
                
            // 避免重复播放相同音高的音符
            if (_lastPreviewedPitch.HasValue && _lastPreviewedPitch.Value == pitch)
                return;
                
            _lastPreviewedPitch = pitch;
            
            try
            {
                // 播放短音符作为反馈
                _waveTableManager.PlayNote(pitch, 80, 100);
            }
            catch (Exception ex)
            {
                _logger.Error("PlayAudioFeedback", $"播放音频反馈失败: {ex.Message}");
            }
            */
        }

        /// <summary>
        /// 判断是否应播放音频反馈 - 已移除功能
        /// </summary>
        private bool ShouldPlayAudioFeedback()
        {
            // 已移除预览时的音频反馈功能
            // 根据用户需求，仅在实际创建音符时播放音频反馈
            return false;
            // 通过WaveTableManager访问全局设置中的音频反馈开关状态
            // return _waveTableManager.IsAudioFeedbackEnabled;
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