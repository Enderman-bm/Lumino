namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// 编辑器工具类型
    /// </summary>
    public enum EditorTool
    {
        /// <summary>铅笔工具 - 添加/编辑音符或CC曲线</summary>
        Pencil,
        /// <summary>选择工具 - 选择和移动音符</summary>
        Select,
        /// <summary>橡皮工具 - 删除音符</summary>
        Eraser,
        /// <summary>切割工具 - 分割音符</summary>
        Cut,
        /// <summary>CC 点工具 - 单击添加/选择控制点</summary>
        CCPoint,
        /// <summary>CC 直线工具 - 绘制直线段</summary>
        CCLine,
        /// <summary>CC 曲线工具 - 绘制光滑曲线段</summary>
        CCCurve
    }
}