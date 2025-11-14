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
    /// PianoRollViewModel事件订阅和处理
    /// 包含所有事件订阅方法和事件处理逻辑
    /// </summary>
    public partial class PianoRollViewModel : ViewModelBase
    {
        #region 事件订阅
        /// <summary>
        /// 订阅所有必要的事件
        /// 包括模块事件和组件事件
        /// </summary>
        private void SubscribeToEvents()
        {
            // 订阅模块事件
            SubscribeToModuleEvents();

            // 订阅组件事件
            SubscribeToComponentEvents();
        }

        /// <summary>
        /// 处理事件类型相关属性变化
        /// 当CurrentEventType或CurrentCCNumber变化时更新相关显示属性
        /// </summary>
        private void OnEventTypePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CurrentEventType))
            {
                OnPropertyChanged(nameof(CurrentEventTypeText));
                OnPropertyChanged(nameof(CurrentEventValueRange));
                OnPropertyChanged(nameof(CurrentEventDescription));
            }
            else if (e.PropertyName == nameof(CurrentCCNumber))
            {
                OnPropertyChanged(nameof(CurrentEventTypeText));
                OnPropertyChanged(nameof(CurrentEventDescription));
            }
        }
        #endregion

        #region 模块事件订阅
        /// <summary>
        /// 订阅所有模块的事件
        /// 包括拖拽、调整大小、创建、选择、预览、力度编辑和曲线绘制模块
        /// </summary>
        private void SubscribeToModuleEvents()
        {
            // 拖拽模块事件
            DragModule?.OnDragUpdated += InvalidateVisual;
            DragModule?.OnDragEnded += InvalidateVisual;

            // 调整大小模块事件
            ResizeModule?.OnResizeUpdated += InvalidateVisual;
            ResizeModule?.OnResizeEnded += InvalidateVisual;

            // 创建模块事件
            CreationModule?.OnCreationUpdated += InvalidateVisual;
            CreationModule?.OnCreationCompleted += OnNoteCreated;

            // 选择模块事件
            SelectionModule?.OnSelectionUpdated += InvalidateVisual;

            // 力度编辑模块事件
            VelocityEditingModule?.OnVelocityUpdated += InvalidateVisual;

            // 事件曲线绘制模块事件
            EventCurveDrawingModule?.OnCurveUpdated += InvalidateVisual;
            EventCurveDrawingModule?.OnCurveCompleted += OnCurveDrawingCompleted;
            EventCurveDrawingModule?.OnCurveCancelled += InvalidateVisual;

            // 订阅选择状态变更事件
            SelectionState?.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(SelectionState.SelectionStart) ||
                    e.PropertyName == nameof(SelectionState.SelectionEnd) ||
                    e.PropertyName == nameof(SelectionState.IsSelecting))
                {
                    OnPropertyChanged(nameof(SelectionStart));
                    OnPropertyChanged(nameof(SelectionEnd));
                    OnPropertyChanged(nameof(IsSelecting));
                    InvalidateVisual();
                }
            };
        }
        #endregion

        #region 组件事件订阅
        /// <summary>
        /// 订阅所有组件的事件
        /// 包括配置、视口、缩放管理器和工具栏组件
        /// </summary>
        private void SubscribeToComponentEvents()
        {
            // 配置变更事件
            Configuration?.PropertyChanged += OnConfigurationPropertyChanged;

            // 视口变更事件
            Viewport?.PropertyChanged += OnViewportPropertyChanged;

            // 缩放管理器变更事件
            ZoomManager?.PropertyChanged += OnZoomManagerPropertyChanged;

            // 命令组件事件
            Commands?.SelectAllRequested += () => SelectionModule?.SelectAll(CurrentTrackNotes);
            Commands?.ConfigurationChanged += InvalidateVisual;
            Commands?.ViewportChanged += InvalidateVisual;

            // 工具栏事件 - 订阅工具变化事件以确保PianoRollViewModel能够正确响应
            Toolbar.ToolChanged += OnToolChanged;
            Toolbar.NoteDurationChanged += OnNoteDurationChanged;
            Toolbar.GridQuantizationChanged += OnGridQuantizationChanged;
            Toolbar.EventViewToggleRequested += OnEventViewToggleRequested;
        }
        #endregion

        #region 事件处理方法
        /// <summary>
        /// 处理工具栏工具变化事件
        /// 当工具栏的工具被切换时触发，确保PianoRollViewModel的CurrentTool属性得到更新
        /// </summary>
        private void OnToolChanged(EditorTool tool)
        {
            // 通知CurrentTool属性变化，确保所有订阅者（包括InputEventRouter）都能获取到最新的工具
            OnPropertyChanged(nameof(CurrentTool));
            _logger.Info("PianoRollViewModel", $"工具已切换到: {tool}");
            InvalidateVisual();
        }

        /// <summary>
        /// 处理工具栏音符时长变化事件
        /// </summary>
        private void OnNoteDurationChanged(MusicalFraction duration)
        {
            OnPropertyChanged(nameof(UserDefinedNoteDuration));
            OnPropertyChanged(nameof(CurrentNoteTimeValueText));
            _logger.Debug("PianoRollViewModel", $"音符时长已更改为: {duration}");
        }

        /// <summary>
        /// 处理工具栏网格量化变化事件
        /// </summary>
        private void OnGridQuantizationChanged(MusicalFraction quantization)
        {
            OnPropertyChanged(nameof(GridQuantization));
            OnPropertyChanged(nameof(CurrentNoteDurationText));
            _logger.Debug("PianoRollViewModel", $"网格量化已更改为: {quantization}");
            InvalidateVisual();
        }

        /// <summary>
        /// 处理工具栏事件视图切换请求事件
        /// </summary>
        private void OnEventViewToggleRequested(bool isVisible)
        {
            OnPropertyChanged(nameof(IsEventViewVisible));
            OnPropertyChanged(nameof(EffectiveScrollableHeight));
            OnPropertyChanged(nameof(ActualRenderHeight));
            _logger.Info("PianoRollViewModel", $"事件视图可见性已更改为: {isVisible}");
            InvalidateVisual();
        }

        /// <summary>
        /// 处理配置属性变化
        /// 根据不同的配置属性更新相应的UI状态和属性通知
        /// </summary>
        private void OnConfigurationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 将配置变更传播到主ViewModel的属性通知
            switch (e.PropertyName)
            {
                case nameof(Configuration.IsEventViewVisible):
                    OnPropertyChanged(nameof(IsEventViewVisible));
                    OnPropertyChanged(nameof(EffectiveScrollableHeight));
                    OnPropertyChanged(nameof(ActualRenderHeight));
                    // 事件视图可见性变化时，更新视口设置
                    Viewport.UpdateViewportForEventView(Configuration.IsEventViewVisible);
                    break;
                case nameof(Configuration.CurrentTool):
                    OnPropertyChanged(nameof(CurrentTool));
                    break;
                case nameof(Configuration.GridQuantization):
                    OnPropertyChanged(nameof(GridQuantization));
                    OnPropertyChanged(nameof(CurrentNoteDurationText));
                    break;
                case nameof(Configuration.UserDefinedNoteDuration):
                    OnPropertyChanged(nameof(UserDefinedNoteDuration));
                    OnPropertyChanged(nameof(CurrentNoteTimeValueText));
                    break;
                case nameof(Configuration.IsNoteDurationDropDownOpen):
                    OnPropertyChanged(nameof(IsNoteDurationDropDownOpen));
                    break;
                case nameof(Configuration.CustomFractionInput):
                    OnPropertyChanged(nameof(CustomFractionInput));
                    break;
                case nameof(Configuration.OnionSkinMode):
                    // 洋葱皮模式改变时，需要重新渲染
                    OnPropertyChanged(nameof(Configuration.OnionSkinMode));
                    InvalidateVisual();
                    break;
                case nameof(Configuration.IsOnionSkinEnabled):
                    // 洋葱皮启用状态改变时，需要重新渲染
                    OnPropertyChanged(nameof(Configuration.IsOnionSkinEnabled));
                    InvalidateVisual();
                    break;
                case nameof(Configuration.SelectedOnionTrackIndices):
                    // 选中的洋葱皮音轨改变时，需要重新渲染
                    OnPropertyChanged(nameof(Configuration.SelectedOnionTrackIndices));
                    InvalidateVisual();
                    break;
                    // 其他配置属性的处理...
            }

            InvalidateVisual();
        }

        /// <summary>
        /// 处理缩放管理器属性变化
        /// 处理水平和垂直缩放变化，更新相关属性和滚动位置
        /// </summary>
        private void OnZoomManagerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 将缩放管理器变更传播到主ViewModel的属性通知
            switch (e.PropertyName)
            {
                case nameof(ZoomManager.Zoom):
                    // 在缩放变化前保存相对位置
                    var oldRelativePosition = GetRelativeScrollPosition();

                    OnPropertyChanged(nameof(Zoom));
                    OnPropertyChanged(nameof(BaseQuarterNoteWidth));
                    OnPropertyChanged(nameof(TimeToPixelScale));
                    OnPropertyChanged(nameof(MeasureWidth));
                    OnPropertyChanged(nameof(BeatWidth));
                    OnPropertyChanged(nameof(EighthNoteWidth));
                    OnPropertyChanged(nameof(SixteenthNoteWidth));
                    // 缩放变化时必须更新滚动范围
                    UpdateMaxScrollExtent();
                    // 同时更新最大滚动范围的属性通知
                    OnPropertyChanged(nameof(MaxScrollExtent));
                    // 更新歌曲长度相关属性
                    OnPropertyChanged(nameof(EffectiveSongLength));
                    OnPropertyChanged(nameof(ScrollbarTotalLength));
                    OnPropertyChanged(nameof(CurrentViewportRatio));
                    OnPropertyChanged(nameof(CurrentScrollPositionRatio));
                    InvalidateNoteCache();

                    // 在缩放变化后恢复相对位置
                    SetRelativeScrollPosition(oldRelativePosition);
                    break;
                case nameof(ZoomManager.VerticalZoom):
                    // 在垂直缩放变化前保存相对位置
                    var oldVerticalRelativePosition = GetVerticalRelativeScrollPosition();

                    OnPropertyChanged(nameof(VerticalZoom));
                    OnPropertyChanged(nameof(KeyHeight));
                    OnPropertyChanged(nameof(TotalHeight));
                    OnPropertyChanged(nameof(EffectiveScrollableHeight));
                    OnPropertyChanged(nameof(ActualRenderHeight));
                    InvalidateNoteCache();

                    // 在垂直缩放变化后恢复相对位置
                    SetVerticalRelativeScrollPosition(oldVerticalRelativePosition);
                    break;
                case nameof(ZoomManager.ZoomSliderValue):
                    OnPropertyChanged(nameof(ZoomSliderValue));
                    // 滑块值变化也要更新滚动范围
                    UpdateMaxScrollExtent();
                    OnPropertyChanged(nameof(MaxScrollExtent));
                    // 更新歌曲长度相关属性
                    OnPropertyChanged(nameof(EffectiveSongLength));
                    OnPropertyChanged(nameof(ScrollbarTotalLength));
                    OnPropertyChanged(nameof(CurrentViewportRatio));
                    OnPropertyChanged(nameof(CurrentScrollPositionRatio));
                    break;
                case nameof(ZoomManager.VerticalZoomSliderValue):
                    OnPropertyChanged(nameof(VerticalZoomSliderValue));
                    break;
            }

            InvalidateVisual();
        }

        /// <summary>
        /// 处理视口属性变化
        /// 更新视口相关的属性通知
        /// </summary>
        private void OnViewportPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 将视口变更传播到主ViewModel的属性通知
            switch (e.PropertyName)
            {
                case nameof(Viewport.CurrentScrollOffset):
                    OnPropertyChanged(nameof(CurrentScrollOffset));
                    OnPropertyChanged(nameof(CurrentScrollPositionRatio));
                    break;
                case nameof(Viewport.VerticalScrollOffset):
                    OnPropertyChanged(nameof(VerticalScrollOffset));
                    break;
                case nameof(Viewport.ViewportWidth):
                case nameof(Viewport.ViewportHeight):
                    OnPropertyChanged(nameof(ViewportWidth));
                    OnPropertyChanged(nameof(ViewportHeight));
                    OnPropertyChanged(nameof(EffectiveScrollableHeight));
                    OnPropertyChanged(nameof(ActualRenderHeight));
                    // 视口大小变化影响比例计算
                    OnPropertyChanged(nameof(CurrentViewportRatio));
                    OnPropertyChanged(nameof(CurrentScrollPositionRatio));
                    break;
            }

            InvalidateVisual();
        }

        /// <summary>
        /// 触发UI更新的方法，由View层实现
        /// </summary>
        private void InvalidateVisual()
        {
            // 触发UI更新的方法，由View层实现
        }

        /// <summary>
        /// 处理音符创建完成事件
        /// 更新UI并同步最新音符的时值到工具栏
        /// </summary>
        private void OnNoteCreated()
        {
            InvalidateVisual();

            // 同步最新创建音符的时值到UI显示
            if (Notes.Count > 0)
            {
                var lastNote = Notes.Last();
                if (!lastNote.Duration.Equals(Configuration.UserDefinedNoteDuration))
                {
                    // 这里需要通过Configuration组件来更新
                    // Configuration.UserDefinedNoteDuration = lastNote.Duration;
                    OnPropertyChanged(nameof(CurrentNoteTimeValueText));
                }
            };

            UpdateMaxScrollExtent();
        }

        /// <summary>
        /// 使所有音符的缓存失效
        /// 在缩放变化时调用以确保音符正确重绘
        /// </summary>
        private void InvalidateNoteCache()
        {
            foreach (var note in Notes)
            {
                note.InvalidateCache();
            }
        }

        /// <summary>
        /// 处理曲线绘制完成事件
        /// 将曲线点转换为MIDI事件并保存到项目中
        /// </summary>
        /// <param name="curvePoints">绘制的曲线点集合</param>
        private void OnCurveDrawingCompleted(List<CurvePoint> curvePoints)
        {
            _logger.Info("PianoRollViewModel", $"曲线绘制完成，事件类型: {CurrentEventType}, 点数: {curvePoints.Count}");
            ApplyCurveDrawingResult(curvePoints);
            InvalidateVisual();
        }

        /// <summary>
        /// 处理撤销重做状态变化
        /// 当撤销重做状态改变时通知UI更新
        /// </summary>
        private void OnUndoRedoStateChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(UndoDescription));
            OnPropertyChanged(nameof(RedoDescription));
        }
        #endregion
    }
}