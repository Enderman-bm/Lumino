using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Modules.Base;
using Lumino.ViewModels.Editor.Services;
using Lumino.ViewModels.Editor.State;
using Lumino.Services.Interfaces;

namespace Lumino.ViewModels.Editor.Modules
{
    /// <summary>
    /// 音符拖动动画模块 - 为拖动操作添加平滑动画效果
    /// 解决放置音符后拖动缺少动画反馈的问题
    /// </summary>
    public class NoteDragAnimationModule : EditorModuleBase
    {
        public override string ModuleName => "NoteDragAnimation";

        private readonly DragState _dragState;
        private readonly Dictionary<NoteViewModel, NoteViewModel> _animatedNotes;
        private readonly Dictionary<NoteViewModel, (MusicalFraction StartPosition, int StartPitch)> _animationStartPositions;
        private CancellationTokenSource? _animationCts;

        // 动画配置 - 平衡响应性和流畅性
        private const int DRAG_ANIMATION_DURATION_MS = 45;  // 45ms快速动画
        private const int DRAG_ANIMATION_FRAME_RATE_MS = 5; // 5ms帧间隔，200fps
        private const double MIN_ANIMATION_DISTANCE = 0.02; // 最小动画距离（时间单位）
        private const int MIN_PITCH_DISTANCE = 1; // 最小音高距离

        public NoteDragAnimationModule(DragState dragState, ICoordinateService coordinateService)
            : base(coordinateService)
        {
            _dragState = dragState;
            _animatedNotes = new Dictionary<NoteViewModel, NoteViewModel>();
            _animationStartPositions = new Dictionary<NoteViewModel, (MusicalFraction, int)>();
        }

        /// <summary>
        /// 开始拖动动画 - 创建动画副本
        /// </summary>
        public void StartDragAnimation(NoteViewModel note)
        {
            if (_dragState.OriginalDragPositions.TryGetValue(note, out var originalPos))
            {
                // 创建动画起始状态
                _animationStartPositions[note] = originalPos;

                // 创建动画音符副本（用于渲染）
                var animatedNote = new NoteViewModel
                {
                    Pitch = originalPos.OriginalPitch,
                    StartPosition = originalPos.OriginalStartPosition,
                    Duration = note.Duration,
                    Velocity = note.Velocity,
                    IsSelected = note.IsSelected,
                    IsPreview = true // 标记为预览状态
                };

                _animatedNotes[note] = animatedNote;
            }
        }

        /// <summary>
        /// 更新拖动动画 - 使用插值平滑过渡
        /// </summary>
        public async void UpdateDragAnimation(NoteViewModel note, MusicalFraction targetPosition, int targetPitch)
        {
            if (!_animatedNotes.ContainsKey(note) || !_animationStartPositions.ContainsKey(note))
                return;

            var animatedNote = _animatedNotes[note];
            var startPos = _animationStartPositions[note];
            var (originalStartPosition, originalPitch) = startPos;

            // 计算移动距离
            double timeDistance = Math.Abs(targetPosition.ToDouble() - originalStartPosition.ToDouble());
            int pitchDistance = Math.Abs(targetPitch - originalPitch);

            // 小距离移动直接更新，避免不必要的动画
            if (timeDistance < MIN_ANIMATION_DISTANCE && pitchDistance < MIN_PITCH_DISTANCE)
            {
                animatedNote.StartPosition = targetPosition;
                animatedNote.Pitch = targetPitch;
                note.StartPosition = targetPosition;
                note.Pitch = targetPitch;
                return;
            }

            // 取消之前的动画
            _animationCts?.Cancel();
            _animationCts = new CancellationTokenSource();
            var token = _animationCts.Token;

            var stopwatch = Stopwatch.StartNew();
            double startTime = originalStartPosition.ToDouble();
            double endTime = targetPosition.ToDouble();
            int startPitch = originalPitch;

            try
            {
                while (stopwatch.ElapsedMilliseconds < DRAG_ANIMATION_DURATION_MS && !token.IsCancellationRequested)
                {
                    double progress = Math.Min(1.0, stopwatch.ElapsedMilliseconds / (double)DRAG_ANIMATION_DURATION_MS);

                    // 使用EaseOutQuart缓动函数 - 极快响应，平滑结束
                    // EaseOutQuart: 1 - (1-t)^4
                    double easeProgress = 1.0 - Math.Pow(1.0 - progress, 4);

                    // 插值计算当前位置
                    double currentTime = startTime + (endTime - startTime) * easeProgress;
                    int currentPitch = startPitch + (targetPitch - startPitch) * (int)easeProgress;

                    animatedNote.StartPosition = MusicalFraction.FromDouble(currentTime);
                    animatedNote.Pitch = currentPitch;

                    // 同时更新实际音符位置（确保数据一致性）
                    note.StartPosition = MusicalFraction.FromDouble(currentTime);
                    note.Pitch = currentPitch;

                    // 触发重绘
                    OnDragAnimationUpdated?.Invoke();

                    await Task.Delay(DRAG_ANIMATION_FRAME_RATE_MS, token);
                }

                // 确保最终状态准确
                if (!token.IsCancellationRequested)
                {
                    animatedNote.StartPosition = targetPosition;
                    animatedNote.Pitch = targetPitch;
                    note.StartPosition = targetPosition;
                    note.Pitch = targetPitch;
                    OnDragAnimationUpdated?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                // 动画被取消，更新到目标位置
                animatedNote.StartPosition = targetPosition;
                animatedNote.Pitch = targetPitch;
                note.StartPosition = targetPosition;
                note.Pitch = targetPitch;
                OnDragAnimationUpdated?.Invoke();
            }
        }

        /// <summary>
        /// 结束拖动动画 - 清理资源
        /// </summary>
        public void EndDragAnimation(NoteViewModel note)
        {
            _animatedNotes.Remove(note);
            _animationStartPositions.Remove(note);
            _animationCts?.Cancel();
        }

        /// <summary>
        /// 获取动画音符（用于渲染）
        /// </summary>
        public NoteViewModel? GetAnimatedNote(NoteViewModel originalNote)
        {
            return _animatedNotes.TryGetValue(originalNote, out var animatedNote) ? animatedNote : null;
        }

        /// <summary>
        /// 获取所有动画音符
        /// </summary>
        public IEnumerable<NoteViewModel> GetAllAnimatedNotes()
        {
            return _animatedNotes.Values;
        }

        /// <summary>
        /// 清理所有动画
        /// </summary>
        public void ClearAllAnimations()
        {
            _animationCts?.Cancel();
            _animatedNotes.Clear();
            _animationStartPositions.Clear();
        }

        /// <summary>
        /// 检查音符是否有动画
        /// </summary>
        public bool HasAnimation(NoteViewModel note)
        {
            return _animatedNotes.ContainsKey(note);
        }

        // 事件
        public event Action? OnDragAnimationUpdated;
    }
}