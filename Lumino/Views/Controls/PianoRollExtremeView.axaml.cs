using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Vulkan;
using Lumino.Services.Interfaces;

namespace Lumino.Views.Controls
{
    /// <summary>
    /// 极端性能钢琴卷帘视图 - 100万音符渲染优化
    /// </summary>
    public class PianoRollExtremeView : UserControl
    {
        private readonly VulkanRenderContextExtreme _extremeContext;
        private readonly ISettingsService _settingsService;
        
        public PianoRollExtremeView(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _extremeContext = new VulkanRenderContextExtreme(
                new Lumino.Services.Implementation.VulkanRenderService()
            );
        }
        
        /// <summary>
        /// 极端模式渲染 - 支持100万音符
        /// </summary>
        public async Task RenderNotesExtremeAsync(
            PianoRollViewModel viewModel,
            Dictionary<NoteViewModel, Rect> visibleNotes,
            Rect viewport,
            double zoomLevel)
        {
            if (visibleNotes.Count == 0) return;
            
            await _extremeContext.RenderNotesExtremeAsync(
                viewModel, 
                visibleNotes, 
                viewport, 
                zoomLevel
            );
        }
        
        /// <summary>
        /// 获取极端性能统计
        /// </summary>
        public VulkanRenderContextExtreme.ExtremePerformanceStats GetPerformanceStats()
        {
            return _extremeContext.GetPerformanceStats();
        }
    }
}