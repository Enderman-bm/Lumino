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
    /// PianoRollViewModel视口管理
    /// 处理视口大小设置、滚动范围更新和滚动位置管理
    /// </summary>
    public partial class PianoRollViewModel : ViewModelBase
    {
        #region 视口管理方法
        /// <summary>
        /// 设置视口大小
        /// </summary>
        /// <param name="width">视口宽度</param>
        /// <param name="height">视口高度</param>
        public void SetViewportSize(double width, double height)
        {
            Viewport.SetViewportSize(width, height);
            UpdateMaxScrollExtent();

            // 更新滚动条轨道长度
            ScrollBarManager.SetScrollBarTrackLengths(width, height);
        }

        /// <summary>
        /// 更新最大滚动范围
        /// 根据音符位置和MIDI文件时长计算内容宽度
        /// </summary>
        public void UpdateMaxScrollExtent()
        {
            var noteEndPositions = Notes.Select(n => n.StartPosition + n.Duration);

            // 传递MIDI文件时长信息给计算组件
            var contentWidth = Calculations.CalculateContentWidth(noteEndPositions, HasMidiFileDuration ? MidiFileDuration : null);
            Viewport.UpdateMaxScrollExtent(contentWidth);

            // 添加调试信息
            EnderLogger.Instance.Debug("PianoRollViewModel", $"更新滚动范围: 内容宽度={contentWidth:F1}, 最大滚动={MaxScrollExtent:F1}, 恢复到上次滚动={Zoom:F2}");
        }

        /// <summary>
        /// 验证并修正滚动偏移量，确保在有效范围内
        /// </summary>
        public void ValidateAndClampScrollOffsets()
        {
            Viewport.ValidateAndClampScrollOffsets();
        }

        /// <summary>
        /// 获取有效的垂直滚动最大值
        /// </summary>
        /// <returns>垂直滚动最大值</returns>
        public double GetEffectiveVerticalScrollMax()
        {
            return Viewport.GetEffectiveVerticalScrollMax(TotalHeight);
        }

        /// <summary>
        /// 获取滚动范围的诊断信息
        /// 用于调试和监控滚动系统状态
        /// </summary>
        /// <returns>包含滚动系统详细信息的诊断字符串</returns>
        public string GetScrollDiagnostics()
        {
            var noteCount = Notes.Count;
            var maxNoteEnd = Notes.Any() ? Notes.Max(n => (n.StartPosition + n.Duration).ToDouble()) : 0;
            var contentWidth = Calculations.CalculateContentWidth(Notes.Select(n => n.StartPosition + n.Duration), HasMidiFileDuration ? MidiFileDuration : null);
            var scrollableRange = Viewport.GetHorizontalScrollableRange();
            var scrollPercentage = Viewport.GetScrollPercentage();

            return $"音符数量: {noteCount}\n" +
                   $"最远音符位置: {maxNoteEnd:F2} 四分音符\n" +
                   $"MIDI文件时长: {(HasMidiFileDuration ? MidiFileDuration.ToString("F2") : "未设置")}\n" +
                   $"内容宽度: {contentWidth:F1} 像素\n" +
                   $"最大滚动范围: {MaxScrollExtent:F1} 像素\n" +
                   $"可滚动范围: {scrollableRange:F1} 像素\n" +
                   $"当前滚动位置: {CurrentScrollOffset:F1} 像素 ({scrollPercentage:P1})\n" +
                   $"视口宽度: {ViewportWidth:F1} 像素\n" +
                   $"当前缩放: {Zoom:F2}x\n" +
                   $"基础四分音符宽度: {BaseQuarterNoteWidth:F1} 像素";
        }

        /// <summary>
        /// 强制重新计算并更新所有滚动相关的属性
        /// 用于解决滚动系统状态不一致的问题
        /// </summary>
        public void ForceRefreshScrollSystem()
        {
            // 强制重新计算内容宽度
            UpdateMaxScrollExtent();

            // 验证滚动位置
            ValidateAndClampScrollOffsets();

            // 强制更新滚动条
            ScrollBarManager.ForceUpdateScrollBars();

            // 通知所有相关属性变化
            OnPropertyChanged(nameof(MaxScrollExtent));
            OnPropertyChanged(nameof(CurrentScrollOffset));
            OnPropertyChanged(nameof(ViewportWidth));

            EnderLogger.Instance.Debug("PianoRollViewModel", "强制刷新滚动系统完成");
            EnderLogger.Instance.Debug("PianoRollViewModel", GetScrollDiagnostics());
        }
        #endregion

        #region 相对滚动位置管理
        /// <summary>
        /// 获取当前水平滚动相对位置（0.0-1.0）
        /// </summary>
        /// <returns>相对位置（0.0表示最左边，1.0表示最右边）</returns>
        public double GetRelativeScrollPosition()
        {
            var maxScroll = MaxScrollExtent;
            if (maxScroll <= 0)
                return 0.0;
            return Math.Max(0.0, Math.Min(1.0, CurrentScrollOffset / maxScroll));
        }

        /// <summary>
        /// 设置水平滚动相对位置（0.0-1.0）
        /// </summary>
        /// <param name="relativePosition">相对位置（0.0-1.0）</param>
        public void SetRelativeScrollPosition(double relativePosition)
        {
            var maxScroll = MaxScrollExtent;
            if (maxScroll > 0)
            {
                var newOffset = Math.Max(0.0, Math.Min(maxScroll, relativePosition * maxScroll));
                SetCurrentScrollOffset(newOffset);
            }
        }

        /// <summary>
        /// 获取当前垂直滚动相对位置（0.0-1.0）
        /// </summary>
        /// <returns>相对位置（0.0表示最顶部，1.0表示最底部）</returns>
        public double GetVerticalRelativeScrollPosition()
        {
            var maxVerticalScroll = GetEffectiveVerticalScrollMax();
            if (maxVerticalScroll <= 0)
                return 0.0;
            return Math.Max(0.0, Math.Min(1.0, VerticalScrollOffset / maxVerticalScroll));
        }

        /// <summary>
        /// 设置垂直滚动相对位置（0.0-1.0）
        /// </summary>
        /// <param name="relativePosition">相对位置（0.0-1.0）</param>
        public void SetVerticalRelativeScrollPosition(double relativePosition)
        {
            var maxVerticalScroll = GetEffectiveVerticalScrollMax();
            if (maxVerticalScroll > 0)
            {
                var newOffset = Math.Max(0.0, Math.Min(maxVerticalScroll, relativePosition * maxVerticalScroll));
                SetVerticalScrollOffset(newOffset);
            }
        }
        #endregion

        #region 公共设置方法 - 用于外部组件更新状态
        /// <summary>
        /// 设置当前工具
        /// </summary>
        /// <param name="tool">要设置的工具类型</param>
        public void SetCurrentTool(EditorTool tool)
        {
            Toolbar.SetCurrentTool(tool);
        }

        /// <summary>
        /// 设置用户定义的音符时长
        /// </summary>
        /// <param name="duration">音符时长</param>
        public void SetUserDefinedNoteDuration(MusicalFraction duration)
        {
            Toolbar.SetUserDefinedNoteDuration(duration);
        }

        /// <summary>
        /// 设置水平滚动偏移量
        /// </summary>
        /// <param name="offset">滚动偏移量</param>
        public void SetCurrentScrollOffset(double offset)
        {
            Viewport.SetHorizontalScrollOffset(offset);
        }

        /// <summary>
        /// 设置垂直滚动偏移量
        /// </summary>
        /// <param name="offset">垂直滚动偏移量</param>
        public void SetVerticalScrollOffset(double offset)
        {
            Viewport.SetVerticalScrollOffset(offset, TotalHeight);
        }

        /// <summary>
        /// 设置时间轴位置（播放头位置，单位：四分音符）
        /// </summary>
        /// <param name="position">时间轴位置</param>
        public void SetTimelinePosition(double position)
        {
            Viewport.TimelinePosition = Math.Max(0, position);
        }

        /// <summary>
        /// 设置缩放滑块值
        /// </summary>
        /// <param name="value">缩放滑块值（0.0-1.0）</param>
        public void SetZoomSliderValue(double value)
        {
            ZoomManager.SetZoomSliderValue(value);
        }

        /// <summary>
        /// 设置垂直缩放滑块值
        /// </summary>
        /// <param name="value">垂直缩放滑块值（0.0-1.0）</param>
        public void SetVerticalZoomSliderValue(double value)
        {
            ZoomManager.SetVerticalZoomSliderValue(value);
        }

        /// <summary>
        /// 获取有效的垂直滚动最大值（带参数重载）
        /// </summary>
        /// <param name="actualRenderHeight">实际渲染高度</param>
        /// <returns>垂直滚动最大值</returns>
        public double GetEffectiveVerticalScrollMax(double actualRenderHeight)
        {
            return Viewport.GetEffectiveScrollableHeight(TotalHeight, Toolbar.IsEventViewVisible);
        }
        #endregion
    }
}