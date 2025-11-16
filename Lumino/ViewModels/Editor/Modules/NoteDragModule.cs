using System;
using System.Linq;
using Avalonia;
using Lumino.ViewModels.Editor.State;
using Lumino.Services.Interfaces;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Modules.Base;
using Lumino.ViewModels.Editor.Services;
using System.Diagnostics;

namespace Lumino.ViewModels.Editor.Modules
{
    /// <summary>
    /// 音符拖拽功能模块 - 基于分数的新实现
    /// 重构后使用基类和通用服务，减少重复代码
    /// </summary>
    public class NoteDragModule : EditorModuleBase
    {
        private readonly DragState _dragState;
        private readonly AntiShakeService _antiShakeService;
        private readonly NoteDragAnimationModule _animationModule;

        public override string ModuleName => "NoteDrag";

        public NoteDragModule(DragState dragState, ICoordinateService coordinateService)
            : base(coordinateService)
        {
            _dragState = dragState;
            // 使用极简防抖配置，只过滤真正微小的移动
            _antiShakeService = new AntiShakeService(AntiShakeConfig.Minimal);
            // 初始化动画模块
            _animationModule = new NoteDragAnimationModule(dragState, coordinateService);
        }

        /// <summary>
        /// 开始拖拽音符
        /// </summary>
        public void StartDrag(NoteViewModel note, Point startPosition)
        {
            if (_pianoRollViewModel == null) return;

            _dragState.StartDrag(note, startPosition);
            
            // 获取所有选中的音符进行拖拽
            _dragState.DraggingNotes = _pianoRollViewModel.Notes.Where(n => n.IsSelected).ToList();

            // 记录所有被拖拽音符的原始位置
            _dragState.OriginalDragPositions.Clear();
            foreach (var dragNote in _dragState.DraggingNotes)
            {
                _dragState.OriginalDragPositions[dragNote] = (dragNote.StartPosition, dragNote.Pitch);
                // 为每个音符启动拖动动画
                _animationModule.StartDragAnimation(dragNote);
            }

            Debug.WriteLine($"开始拖拽 {_dragState.DraggingNotes.Count} 个音符");
        }

        /// <summary>
        /// 更新拖拽 - 使用统一的防抖服务
        /// </summary>
        public void UpdateDrag(Point currentPosition)
        {
            if (!_dragState.IsDragging || _pianoRollViewModel == null) return;

            // 使用统一的防抖检查
            if (_antiShakeService.ShouldIgnoreMovement(_dragState.DragStartPosition, currentPosition))
            {
                return; // 忽略微小移动
            }

            var deltaX = currentPosition.X - _dragState.DragStartPosition.X;
            var deltaY = currentPosition.Y - _dragState.DragStartPosition.Y;

            // 计算时间偏移（基于分数）
            var timeDelta = deltaX / _pianoRollViewModel.BaseQuarterNoteWidth; // 以四分音符为单位
            var pitchDelta = -(int)(deltaY / _pianoRollViewModel.KeyHeight);

            // 直接更新所有被拖拽的音符
            foreach (var note in _dragState.DraggingNotes)
            {
                if (_dragState.OriginalDragPositions.TryGetValue(note, out var originalPos))
                {
                    var originalTimeValue = originalPos.OriginalStartPosition.ToDouble();
                    var newTimeValue = Math.Max(0, originalTimeValue + timeDelta);
                    var newPitch = EditorValidationService.ClampPitch(originalPos.OriginalPitch + pitchDelta);

                    // 转换为分数并量化
                    var newTimeFraction = MusicalFraction.FromDouble(newTimeValue);
                    var quantizedPosition = _pianoRollViewModel.SnapToGrid(newTimeFraction);

                    // 使用动画更新替代直接更新
                    _animationModule.UpdateDragAnimation(note, quantizedPosition, newPitch);

                    // 注意：实际音符位置在动画模块中更新，这里只更新动画状态
                    SafeInvalidateNoteCache(note);
                }
            }

            // 触发更新通知
            OnDragUpdated?.Invoke();
        }

        /// <summary>
        /// 结束拖拽
        /// </summary>
        public void EndDrag()
        {
            if (_dragState.IsDragging)
            {
                Debug.WriteLine($"结束拖拽 {_dragState.DraggingNotes.Count} 个音符");

                // 结束所有动画
                foreach (var note in _dragState.DraggingNotes.ToList())
                {
                    _animationModule.EndDragAnimation(note);
                }

                // 拖拽结束后重新计算滚动范围，因为音符位置可能已经改变
                _pianoRollViewModel?.UpdateMaxScrollExtent();
            }

            _dragState.EndDrag();
            OnDragEnded?.Invoke();
        }

        /// <summary>
        /// 取消拖拽，恢复原始位置
        /// </summary>
        public void CancelDrag()
        {
            if (_dragState.IsDragging && _dragState.DraggingNotes.Count > 0)
            {
                // 清理动画
                _animationModule.ClearAllAnimations();

                foreach (var note in _dragState.DraggingNotes)
                {
                    if (_dragState.OriginalDragPositions.TryGetValue(note, out var originalPos))
                    {
                        note.StartPosition = originalPos.OriginalStartPosition;
                        note.Pitch = originalPos.OriginalPitch;
                        SafeInvalidateNoteCache(note);
                    }
                }
                Debug.WriteLine($"取消拖拽，恢复 {_dragState.DraggingNotes.Count} 个音符的原始位置");
            }

            EndDrag();
        }

        // 事件
        public event Action? OnDragUpdated;
        public event Action? OnDragEnded;

        // 只读属性
        public bool IsDragging => _dragState.IsDragging;
        public NoteViewModel? DraggingNote => _dragState.DraggingNote;
        public System.Collections.Generic.List<NoteViewModel> DraggingNotes => _dragState.DraggingNotes;
        public NoteDragAnimationModule AnimationModule => _animationModule;
    }
}