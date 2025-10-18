using Avalonia;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// 编辑器状态管理服务实现
    /// </summary>
    public class EditorStateService : IEditorStateService
    {
        public bool IsDragging { get; private set; }
        public bool IsSelecting { get; private set; }
        public bool IsResizing { get; private set; }

        // 频谱背景相关属性
        public double[,]? SpectrogramData { get; private set; }
        public int SpectrogramSampleRate { get; private set; }
        public double SpectrogramDuration { get; private set; }
        public double SpectrogramMaxFrequency { get; private set; }
        public bool IsSpectrogramVisible { get; private set; }

        public void StartDrag(NoteViewModel note, Point startPosition)
        {
            IsDragging = true;
            // 拖动逻辑实现
        }

        public void UpdateDrag(Point currentPosition)
        {
            if (IsDragging)
            {
                // 更新拖动位置逻辑
            }
        }

        public void EndDrag()
        {
            IsDragging = false;
        }

        public void StartSelection(Point startPosition)
        {
            IsSelecting = true;
            // 选择逻辑实现
        }

        public void UpdateSelection(Point currentPosition)
        {
            if (IsSelecting)
            {
                // 更新选择区域逻辑
            }
        }

        public void EndSelection()
        {
            IsSelecting = false;
        }

        public void LoadSpectrogramBackground(double[,] spectrogramData, int sampleRate, double duration, double maxFrequency)
        {
            SpectrogramData = spectrogramData;
            SpectrogramSampleRate = sampleRate;
            SpectrogramDuration = duration;
            SpectrogramMaxFrequency = maxFrequency;
            IsSpectrogramVisible = true;
        }

        public void ClearSpectrogramBackground()
        {
            SpectrogramData = null;
            SpectrogramSampleRate = 0;
            SpectrogramDuration = 0;
            SpectrogramMaxFrequency = 0;
            IsSpectrogramVisible = false;
        }

        public void SetSpectrogramVisibility(bool isVisible)
        {
            IsSpectrogramVisible = isVisible && SpectrogramData != null;
        }
    }
}