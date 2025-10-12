using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Vulkan;

namespace Lumino.Views.Rendering.Notes
{
    /// <summary>
    /// 极端音符渲染器 - 100万音符性能优化
    /// </summary>
    public class ExtremeNoteRenderer : INoteRenderer
    {
        private readonly VulkanRenderContextExtreme _extremeContext;
        private readonly Dictionary<NoteViewModel, Rect> _noteCache = new();
        private readonly object _cacheLock = new object();
        
        public ExtremeNoteRenderer(VulkanRenderContextExtreme extremeContext)
        {
            _extremeContext = extremeContext;
        }
        
        /// <summary>
        /// 渲染音符 - 极端性能模式
        /// </summary>
        public async Task RenderNotesAsync(
            PianoRollViewModel viewModel,
            Dictionary<NoteViewModel, Rect> visibleNotes,
            Rect viewport,
            double zoomLevel)
        {
            if (visibleNotes.Count == 0) return;
            
            // 更新缓存
            UpdateNoteCache(visibleNotes);
            
            // 使用极端渲染器
            await _extremeContext.RenderNotesExtremeAsync(
                viewModel, 
                visibleNotes, 
                viewport, 
                zoomLevel
            );
        }
        
        /// <summary>
        /// 更新音符缓存
        /// </summary>
        private void UpdateNoteCache(Dictionary<NoteViewModel, Rect> notes)
        {
            lock (_cacheLock)
            {
                _noteCache.Clear();
                foreach (var kvp in notes)
                {
                    _noteCache[kvp.Key] = kvp.Value;
                }
            }
        }
        
        /// <summary>
        /// 获取极端性能统计
        /// </summary>
        public VulkanRenderContextExtreme.ExtremePerformanceStats GetPerformanceStats()
        {
            return _extremeContext.GetPerformanceStats();
        }
        
        /// <summary>
        /// 清理缓存
        /// </summary>
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _noteCache.Clear();
            }
        }
    }
    
    /// <summary>
    /// 音符渲染器接口
    /// </summary>
    public interface INoteRenderer
    {
        Task RenderNotesAsync(
            PianoRollViewModel viewModel,
            Dictionary<NoteViewModel, Rect> visibleNotes,
            Rect viewport,
            double zoomLevel);
    }
}