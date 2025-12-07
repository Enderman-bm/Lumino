using System;
using System.Collections.Generic;
using Avalonia;
using Lumino.Services.Interfaces;
using EnderDebugger;

namespace Lumino.Views.Rendering.Vulkan
{
    /// <summary>
    /// 优化的视口剔除系统 - 专门用于虚拟化音符数据
    /// 利用帧间一致性，避免每帧重新计算
    /// </summary>
    public class OptimizedViewportCuller
    {
        private readonly EnderLogger _logger = EnderLogger.Instance;

        // 当前视口参数
        private Rect _currentViewport;
        private double _baseQuarterNoteWidth;
        private double _keyHeight;
        private double _scrollX;
        private double _scrollY;
        private int _currentTrackIndex;

        // 缓存的结果
        private List<(NoteData note, Rect screenRect)> _cachedVisibleNotes = new();
        private bool _cacheValid;

        // 性能参数
        private const double ViewportChangeThreshold = 5.0; // 视口变化阈值（像素）
        private const double MinNoteWidth = 4.0;

        // 统计
        private int _totalProcessed;
        private int _visibleCount;
        private int _culledCount;

        /// <summary>
        /// 更新视口参数
        /// </summary>
        /// <returns>如果参数发生显著变化，返回true</returns>
        public bool UpdateViewport(
            Rect viewport,
            double baseQuarterNoteWidth,
            double keyHeight,
            double scrollX,
            double scrollY,
            int trackIndex)
        {
            // 检查参数是否发生显著变化
            bool changed = !_cacheValid ||
                trackIndex != _currentTrackIndex ||
                Math.Abs(viewport.Width - _currentViewport.Width) > ViewportChangeThreshold ||
                Math.Abs(viewport.Height - _currentViewport.Height) > ViewportChangeThreshold ||
                Math.Abs(scrollX - _scrollX) > ViewportChangeThreshold ||
                Math.Abs(scrollY - _scrollY) > ViewportChangeThreshold ||
                Math.Abs(baseQuarterNoteWidth - _baseQuarterNoteWidth) > 0.01 ||
                Math.Abs(keyHeight - _keyHeight) > 0.01;

            if (changed)
            {
                _currentViewport = viewport;
                _baseQuarterNoteWidth = baseQuarterNoteWidth;
                _keyHeight = keyHeight;
                _scrollX = scrollX;
                _scrollY = scrollY;
                _currentTrackIndex = trackIndex;
                _cacheValid = false;
            }

            return changed;
        }

        /// <summary>
        /// 执行剔除 - 返回可见音符及其屏幕坐标
        /// </summary>
        public IReadOnlyList<(NoteData note, Rect screenRect)> CullNotes(IReadOnlyList<NoteData> notes)
        {
            // 如果缓存仍然有效，直接返回
            if (_cacheValid)
            {
                return _cachedVisibleNotes;
            }

            _cachedVisibleNotes.Clear();
            _totalProcessed = notes.Count;
            _visibleCount = 0;
            _culledCount = 0;

            // 计算视口在音符坐标系中的范围（用于早期剔除）
            var viewportStartTime = _scrollX / _baseQuarterNoteWidth;
            var viewportEndTime = (_scrollX + _currentViewport.Width) / _baseQuarterNoteWidth;
            var viewportTopPitch = 127 - (int)(_scrollY / _keyHeight);
            var viewportBottomPitch = 127 - (int)((_scrollY + _currentViewport.Height) / _keyHeight);

            for (int i = 0; i < notes.Count; i++)
            {
                var note = notes[i];

                // 快速时间范围剔除
                if (note.EndPosition < viewportStartTime || note.StartPosition > viewportEndTime)
                {
                    _culledCount++;
                    continue;
                }

                // 快速音高范围剔除
                if (note.Pitch > viewportTopPitch + 1 || note.Pitch < viewportBottomPitch - 1)
                {
                    _culledCount++;
                    continue;
                }

                // 计算屏幕坐标
                var screenX = note.StartPosition * _baseQuarterNoteWidth - _scrollX;
                var screenY = (127 - note.Pitch) * _keyHeight - _scrollY;
                var screenWidth = Math.Max(MinNoteWidth, note.Duration * _baseQuarterNoteWidth);
                var screenHeight = _keyHeight;

                var screenRect = new Rect(screenX, screenY, screenWidth, screenHeight);

                // 最终视口相交检查
                if (screenRect.Intersects(_currentViewport))
                {
                    _cachedVisibleNotes.Add((note, screenRect));
                    _visibleCount++;
                }
                else
                {
                    _culledCount++;
                }
            }

            _cacheValid = true;

            _logger.Debug("OptimizedViewportCuller", 
                $"剔除完成: {_visibleCount}/{_totalProcessed} 可见, {_culledCount} 被剔除, 效率 {(_culledCount * 100.0 / Math.Max(1, _totalProcessed)):F1}%");

            return _cachedVisibleNotes;
        }

        /// <summary>
        /// 使缓存失效
        /// </summary>
        public void InvalidateCache()
        {
            _cacheValid = false;
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void ClearCache()
        {
            _cachedVisibleNotes.Clear();
            _cacheValid = false;
        }

        /// <summary>
        /// 获取剔除统计
        /// </summary>
        public (int total, int visible, int culled, double efficiency) GetStats()
        {
            var efficiency = _totalProcessed > 0 ? (double)_culledCount / _totalProcessed : 0;
            return (_totalProcessed, _visibleCount, _culledCount, efficiency);
        }
    }
}
