namespace Lumino.ViewModels
// Lumino - 视图类型枚举，定义所有主视图类型。
// 全局注释：本文件为视图类型枚举，禁止随意更改枚举值。
{
    /// <summary>
    /// 视图类型枚举
    /// 定义应用程序中显示的不同视图类型
    /// </summary>
    public enum ViewType
    {
        /// <summary>钢琴卷帘视图 - 音符编辑器</summary>
        PianoRoll,
        
        /// <summary>音轨总览 - 显示所有音轨预览功能</summary>
        TrackOverview,

        /// <summary>事件列表 - 事件编辑器</summary>
        EventList,

        /// <summary>音频分析 - 音频解析和可视化</summary>
        AudioAnalysis
    }
}