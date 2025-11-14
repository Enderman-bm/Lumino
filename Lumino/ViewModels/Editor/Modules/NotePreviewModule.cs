using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Lumino.Services.Interfaces;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.State;
using Lumino.ViewModels.Editor.Modules.Base;
using Lumino.ViewModels.Editor.Services;
using System.Diagnostics;
using Lumino.Services.Implementation;

namespace Lumino.ViewModels.Editor.Modules
{
    /// <summary>
    /// 音符预览处理模块 - 用于处理音符预览
    /// 优化用户体验：使用防抖机制，避免重复创建；添加平滑移动动画
    /// </summary>
    public class NotePreviewModule : EditorModuleBase
    {
        public override string ModuleName => "NotePreview";
        
        private readonly WaveTableManager _waveTableManager;
        private NoteViewModel? _targetPreviewNote;
        private NoteViewModel? _animatedPreviewNote;
        private CancellationTokenSource? _animationCts;
        
        // 动画配置 - 优化跟手性
        private const int ANIMATION_DURATION_MS = 80;  // 缩短到80ms，提高响应速度
        private const int ANIMATION_FRAME_RATE_MS = 8; // 8ms帧间隔，约120fps，更流畅
        private const bool SKIP_ANIMATION_FOR_LARGE_JUMPS = false; // 大幅跳跃时不使用动画，直接显示

        public NoteViewModel? PreviewNote
        {
            get => _animatedPreviewNote;
            private set
            {
                if (_animatedPreviewNote != value)
                {
                    _animatedPreviewNote = value;
                    OnPreviewUpdated?.Invoke();
                }
            }
        }

        public NotePreviewModule(ICoordinateService coordinateService) : base(coordinateService)
        {
            // 初始化播表管理器
            _waveTableManager = new WaveTableManager();
        }

        /// <summary>
        /// 更新预览音符 - 使用用户监听和防抖机制
        /// </summary>
        public void UpdatePreview(Point position)
        {
            if (_pianoRollViewModel == null) return;

            // 在创建音符时不要显示通用预览
            if (_pianoRollViewModel.CreationModule?.IsCreatingNote ?? false)
            {
                ClearPreview();
                return;
            }

            // 在调整大小时不要显示通用预览
            if (_pianoRollViewModel.ResizeState?.IsResizing ?? false)
            {
                ClearPreview();
                return;
            }

            if (_pianoRollViewModel.CurrentTool != EditorTool.Pencil)
            {
                ClearPreview();
                return;
            }

            // 检查是否悬停在音符上，如果是则不显示预览（显示拖拽光标）
            var hoveredNote = _pianoRollViewModel.SelectionModule?.GetNoteAtPosition(position, _pianoRollViewModel.Notes, 
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
                
                if (_targetPreviewNote == null)
                {
                    shouldUpdate = true;
                }
                else if (_targetPreviewNote.Pitch != pitch)
                {
                    shouldUpdate = true;
                }
                else if (!_targetPreviewNote.StartPosition.Equals(quantizedPosition))
                {
                    shouldUpdate = true;
                }
                else if (!_targetPreviewNote.Duration.Equals(_pianoRollViewModel.UserDefinedNoteDuration))
                {
                    shouldUpdate = true;
                }

                if (shouldUpdate)
                {
                    _targetPreviewNote = new NoteViewModel
                    {
                        Pitch = pitch,
                        StartPosition = quantizedPosition,
                        Duration = _pianoRollViewModel.UserDefinedNoteDuration,
                        Velocity = 100,
                        IsPreview = true
                    };

                    // 启动平滑移动动画
                    AnimatePreviewNoteAsync();
                    
                    OnPreviewUpdated?.Invoke();
                }
            }
            else
            {
                ClearPreview();
            }
        }

        /// <summary>
        /// 启动预览音符的平滑移动动画（先加速后减速）
        /// 优化跟手性：立即显示、更快帧率、改进缓动
        /// </summary>
        private async void AnimatePreviewNoteAsync()
        {
            // 取消之前的动画
            _animationCts?.Cancel();
            _animationCts = new CancellationTokenSource();
            var token = _animationCts.Token;

            // 如果没有当前预览，直接设置目标
            if (PreviewNote == null)
            {
                PreviewNote = _targetPreviewNote;
                return;
            }

            var startNote = PreviewNote;
            var endNote = _targetPreviewNote;
            if (startNote == null || endNote == null) return;

            // 计算差值（使用double避免MusicalFraction计算中的分母问题）
            int pitchDiff = endNote.Pitch - startNote.Pitch;
            double startTime = startNote.StartPosition.ToDouble();
            double endTime = endNote.StartPosition.ToDouble();
            double timeDiff = endTime - startTime;

            // 立即显示初始预览以提高跟手性
            int initialPitch = startNote.Pitch;
            if (pitchDiff != 0)
            {
                initialPitch = endNote.Pitch; // 快速反应到目标音高
            }
            PreviewNote = new NoteViewModel
            {
                Pitch = initialPitch,
                StartPosition = _targetPreviewNote!.StartPosition,
                Duration = endNote.Duration,
                Velocity = endNote.Velocity,
                IsPreview = true
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                while (stopwatch.ElapsedMilliseconds < ANIMATION_DURATION_MS && !token.IsCancellationRequested)
                {
                    double progress = Math.Min(1.0, stopwatch.ElapsedMilliseconds / (double)ANIMATION_DURATION_MS);

                    // 改进的缓动函数：使用EaseOutQuad获得更快的初期响应
                    // EaseOutQuad: 快速开始，然后逐渐减速（1 - (1-t)^2）
                    double easeProgress = 1.0 - (1.0 - progress) * (1.0 - progress);

                    // 创建中间帧的预览音符
                    int currentPitch = (int)(startNote.Pitch + pitchDiff * easeProgress);
                    double currentTimeDouble = startTime + timeDiff * easeProgress;
                    var currentPosition = MusicalFraction.FromDouble(currentTimeDouble);

                    PreviewNote = new NoteViewModel
                    {
                        Pitch = currentPitch,
                        StartPosition = currentPosition,
                        Duration = endNote.Duration,
                        Velocity = endNote.Velocity,
                        IsPreview = true
                    };

                    await Task.Delay(ANIMATION_FRAME_RATE_MS, token);
                }

                // 确保最终状态是准确的目标值
                if (!token.IsCancellationRequested)
                {
                    PreviewNote = endNote;
                }
            }
            catch (OperationCanceledException)
            {
                // 动画被取消，这是正常的
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
                Debug.WriteLine($"播放音频反馈失败: {ex.Message}");
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
            if (PreviewNote != null || _targetPreviewNote != null)
            {
                _targetPreviewNote = null;
                PreviewNote = null;
                _animationCts?.Cancel();
                OnPreviewUpdated?.Invoke();
            }
        }

        // 事件
        public event Action? OnPreviewUpdated;
    }
}