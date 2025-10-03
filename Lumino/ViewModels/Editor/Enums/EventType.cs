namespace Lumino.ViewModels.Editor.Enums
{
    /// <summary>
    /// 事件视图中可选择的事件类型
    /// </summary>
    public enum EventType
    {
        /// <summary>
        /// 力度（范围1-127）
        /// </summary>
        Velocity,
        
        /// <summary>
        /// 弯音（范围-8192-8191）
        /// </summary>
        PitchBend,
        
        /// <summary>
        /// 控制器变化（CC，范围0-127）
        /// </summary>
        ControlChange,
        
        /// <summary>
        /// 速度（BPM），范围20-300
        /// </summary>
        Tempo
    }
}