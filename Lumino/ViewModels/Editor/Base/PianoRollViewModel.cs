using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models.Music;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor.Commands;
using Lumino.ViewModels.Editor.Modules;
using Lumino.ViewModels.Editor.State;
using Lumino.ViewModels.Editor.Components;
using Lumino.ViewModels.Editor.Enums;
using EnderDebugger;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// 重构后的钢琴卷帘ViewModel - 符合MVVM最佳实践和单一职责原则
    /// 主要负责协调各个组件和模块，业务逻辑委托给专门的组件处理
    ///
    /// 代码已拆分为多个文件以提高可维护性：
    /// - PianoRollViewModel.Core.cs: 核心字段和属性定义
    /// - PianoRollViewModel.Constructor.cs: 构造函数和初始化方法
    /// - PianoRollViewModel.Properties.cs: 计算属性和代理属性
    /// - PianoRollViewModel.Events.cs: 事件订阅和处理
    /// - PianoRollViewModel.Commands.cs: 命令定义
    /// - PianoRollViewModel.Methods.cs: 公共方法
    /// - PianoRollViewModel.Collections.cs: 集合管理
    /// - PianoRollViewModel.Coordinates.cs: 坐标转换（已合并到Methods.cs）
    /// - PianoRollViewModel.Midi.cs: MIDI文件管理
    /// - PianoRollViewModel.Viewport.cs: 视口管理
    /// - PianoRollViewModel.Batch.cs: 批量操作（已合并到Collections.cs）
    /// - PianoRollViewModel.Project.cs: 项目初始化
    /// - PianoRollViewModel.Cleanup.cs: 清理方法
    /// </summary>
    public partial class PianoRollViewModel : ViewModelBase
    {
        // 所有实现已拆分到对应的partial类文件中
    }
}
