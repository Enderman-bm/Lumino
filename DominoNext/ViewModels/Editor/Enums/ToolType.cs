using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lumino.ViewModels.Editor.Enums
{
    /// <summary>
    /// 编辑器工具类型枚举
    /// </summary>
    public enum ToolType
    {
        /// <summary>
        /// 选择工具
        /// </summary>
        Select,
        
        /// <summary>
        /// 画笔工具
        /// </summary>
        Pen,
        
        /// <summary>
        /// 橡皮擦工具
        /// </summary>
        Eraser,
        
        /// <summary>
        /// 力度工具
        /// </summary>
        Velocity
    }
}