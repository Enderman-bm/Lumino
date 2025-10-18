using Avalonia;
using Lumino.ViewModels.Editor;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// 编辑器状态管理服务
    /// </summary>
    public interface IEditorStateService
    {
        bool IsDragging { get; }
        bool IsSelecting { get; }
        bool IsResizing { get; }

        void StartDrag(NoteViewModel note, Point startPosition);
        void UpdateDrag(Point currentPosition);
        void EndDrag();

        void StartSelection(Point startPosition);
        void UpdateSelection(Point currentPosition);
        void EndSelection();

        /// <summary>
        /// 加载频谱背景数据
        /// </summary>
        /// <param name="spectrogramData">频谱数据</param>
        /// <param name="sampleRate">采样率</param>
        /// <param name="duration">音频时长（秒）</param>
        /// <param name="maxFrequency">最大频率（Hz）</param>
        void LoadSpectrogramBackground(double[,] spectrogramData, int sampleRate, double duration, double maxFrequency);

        /// <summary>
        /// 清除频谱背景
        /// </summary>
        void ClearSpectrogramBackground();

        /// <summary>
        /// 设置频谱背景可见性
        /// </summary>
        /// <param name="isVisible">是否可见</param>
        void SetSpectrogramVisibility(bool isVisible);
    }
}