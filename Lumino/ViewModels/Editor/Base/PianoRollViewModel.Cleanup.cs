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
    /// PianoRollViewModel清理方法
    /// 处理资源清理和状态重置
    /// </summary>
    public partial class PianoRollViewModel : ViewModelBase
    {
        #region 清理
        /// <summary>
        /// 完全清理所有状态和资源
        /// 用于应用程序关闭或切换项目时
        /// </summary>
        public void Cleanup()
        {
            _logger.Info("PianoRollViewModel", "[内存管理] 开始完全清理所有状态和资源");

            // 保存ScrollBarManager的连接状态，因为在MIDI导入后需要保持连接
            var scrollBarManagerWasConnected = ScrollBarManager != null;

            // 结束所有正在进行的操作
            DragModule.EndDrag();
            ResizeModule.EndResize();
            CreationModule.CancelCreating();
            SelectionModule.ClearSelection(CurrentTrackNotes);
            PreviewModule.ClearPreview();
            VelocityEditingModule.EndEditing();
            EventCurveDrawingModule.CancelDrawing();

            // 清理工具栏状态
            Toolbar.Cleanup();

            // 清空音符集合
            Notes.Clear();

            // 强制GC以释放内存
            GC.Collect();
            GC.WaitForPendingFinalizers();

            _logger.Info("PianoRollViewModel", "[内存管理] 完成资源清理，触发GC释放内存");

            // 如果ScrollBarManager之前是连接的，重新建立连接
            if (scrollBarManagerWasConnected)
            {
                EnsureScrollBarManagerConnection();
            }
        }

        /// <summary>
        /// 确保ScrollBarManager与PianoRollViewModel的连接
        /// </summary>
        private void EnsureScrollBarManagerConnection()
        {
            if (ScrollBarManager != null)
            {
                // 重新建立连接，确保滚动条功能正常
                ScrollBarManager.SetPianoRollViewModel(this);

                // 强制更新滚动条状态
                ScrollBarManager.ForceUpdateScrollBars();

                _logger.Info("PianoRollViewModel", "[内存管理] 重新建立ScrollBarManager连接");
            }
            else
            {
                _logger.Warn("PianoRollViewModel", "[内存管理] 警告：ScrollBarManager为null，无法建立连接");
            }
        }

        /// <summary>
        /// 轻量级清理方法，用于MIDI导入等场景，不断开ScrollBarManager连接
        /// </summary>
        public void ClearContent()
        {
            _logger.Info("PianoRollViewModel", "[内存管理] 开始轻量级清理内容");

            // 结束所有正在进行的操作
            DragModule.EndDrag();
            ResizeModule.EndResize();
            CreationModule.CancelCreating();
            SelectionModule.ClearSelection(CurrentTrackNotes);
            PreviewModule.ClearPreview();
            VelocityEditingModule.EndEditing();
            EventCurveDrawingModule.CancelDrawing();

            // 清空音符但保持ScrollBarManager连接
            Notes.Clear();

            // 重置MIDI文件时长
            ClearMidiFileDuration();

            _logger.Info("PianoRollViewModel", "[内存管理] 完成轻量级清理，保持ScrollBarManager连接");
        }
        #endregion
    }
}