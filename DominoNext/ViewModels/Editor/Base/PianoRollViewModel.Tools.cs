using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Enums;
using Lumino.ViewModels.Editor.State;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel的工具方法和公共接口
    /// </summary>
    public partial class PianoRollViewModel
    {
        #region 滚动条管理连接
        /// <summary>
        /// 连接滚动条管理器
        /// </summary>
        public void ConnectScrollBarManager()
        {
            // ScrollBarManager已废弃，使用Viewport替代
            // ScrollBarManager.ScrollOffsetChanged += OnScrollOffsetChanged;
            // ScrollBarManager.MaxScrollExtentChanged += OnMaxScrollExtentChanged;
        }

        /// <summary>
        /// 断开滚动条管理器连接
        /// </summary>
        public void DisconnectScrollBarManager()
        {
            // ScrollBarManager已废弃，使用Viewport替代
            // ScrollBarManager.ScrollOffsetChanged -= OnScrollOffsetChanged;
            // ScrollBarManager.MaxScrollExtentChanged -= OnMaxScrollExtentChanged;
        }

        /// <summary>
        /// 滚动偏移量变化处理
        /// </summary>
        private void OnScrollOffsetChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(HorizontalScrollOffset));
            OnPropertyChanged(nameof(VerticalScrollOffset));
        }

        /// <summary>
        /// 最大滚动范围变化处理
        /// </summary>
        private void OnMaxScrollExtentChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(MaxScrollExtent));
            OnPropertyChanged(nameof(MaxVerticalScrollExtent));
            OnPropertyChanged(nameof(HorizontalScrollBarVisible));
            OnPropertyChanged(nameof(VerticalScrollBarVisible));
        }
        #endregion

        #region 轻量级清理方法
        /// <summary>
        /// 清理内容（轻量级清理）
        /// </summary>
        public void ClearContent()
        {
            // 清理音符
            Notes?.Clear();
            SelectedNotes?.Clear();
            CurrentTrackNotes?.Clear();
            
            // 清理事件曲线
            EventCurves?.Clear();
            
            // 清理音轨
            Tracks?.Clear();
            
            // 重置状态
            CurrentTrackIndex = 0;
            
            // 清理撤销重做历史
            _undoRedoManager?.Clear();
            
            // 重置滚动位置
            if (Viewport != null)
            {
                Viewport.SetHorizontalScrollOffset(0);
                Viewport.SetVerticalScrollOffset(0, MaxVerticalScrollExtent);
            }
            
            // 重置缩放
            if (ZoomManager != null)
            {
                ZoomManager.Zoom = 1.0;
                ZoomManager.VerticalZoom = 1.0;
            }
            
            // 重置工具状态
            if (Configuration != null)
            {
                Configuration.CurrentTool = EditorTool.Select;
                Configuration.ShowVelocity = false;
            }
            
            // 重置配置
            if (Configuration != null)
            {
                Configuration.ShowGrid = true;
                Configuration.ShowNoteNames = true;
                Configuration.ShowVelocity = false;
                Configuration.ShowBlackKeys = true;
                Configuration.ShowWhiteKeys = true;
            }
        }
        #endregion

        #region 公共设置方法
        /// <summary>
        /// 设置当前工具
        /// </summary>
        public void SetCurrentTool(EditorTool tool)
        {
            CurrentTool = tool;
        }

        /// <summary>
        /// 设置当前音轨
        /// </summary>
        public void SetCurrentTrack(int trackIndex)
        {
            if (trackIndex >= 0 && trackIndex < Tracks.Count)
            {
                CurrentTrackIndex = trackIndex;
            }
        }

        /// <summary>
        /// 设置当前事件类型
        /// </summary>
        public void SetCurrentEventType(EventType eventType)
        {
            CurrentEventType = eventType;
        }

        /// <summary>
        /// 设置当前CC号
        /// </summary>
        public void SetCurrentCCNumber(int ccNumber)
        {
            CurrentCCNumber = ccNumber;
        }

        /// <summary>
        /// 设置吸附状态
        /// </summary>
        public void SetSnapToGrid(bool enabled)
        {
            SnapToGridEnabled = enabled;
        }

        /// <summary>
        /// 设置吸附值
        /// </summary>
        public void SetSnapValue(MusicalFraction snapValue)
        {
            SnapValue = snapValue;
        }

        /// <summary>
        /// 设置用户定义音符时长
        /// </summary>
        public void SetUserDefinedNoteDuration(MusicalFraction duration)
        {
            UserDefinedNoteDuration = duration;
        }

        /// <summary>
        /// 设置显示配置
        /// </summary>
        public void SetDisplayConfiguration(
            bool? showGrid = null,
            bool? showNoteNames = null,
            bool? showVelocity = null,
            bool? showBlackKeys = null,
            bool? showWhiteKeys = null)
        {
            if (showGrid.HasValue) ShowGrid = showGrid.Value;
            if (showNoteNames.HasValue) ShowNoteNames = showNoteNames.Value;
            if (showVelocity.HasValue) ShowVelocity = showVelocity.Value;
            if (showBlackKeys.HasValue) ShowBlackKeys = showBlackKeys.Value;
            if (showWhiteKeys.HasValue) ShowWhiteKeys = showWhiteKeys.Value;
        }

        /// <summary>
        /// 设置颜色配置
        /// </summary>
        public void SetColorConfiguration(
            Color? gridColor = null,
            Color? noteColor = null,
            Color? selectedNoteColor = null,
            Color? backgroundColor = null,
            Color? blackKeyColor = null,
            Color? whiteKeyColor = null,
            Color? blackKeyTextColor = null,
            Color? whiteKeyTextColor = null)
        {
            if (gridColor.HasValue) GridColor = gridColor.Value;
            if (noteColor.HasValue) NoteColor = noteColor.Value;
            if (selectedNoteColor.HasValue) SelectedNoteColor = selectedNoteColor.Value;
            if (backgroundColor.HasValue) BackgroundColor = backgroundColor.Value;
            if (blackKeyColor.HasValue) BlackKeyColor = blackKeyColor.Value;
            if (whiteKeyColor.HasValue) WhiteKeyColor = whiteKeyColor.Value;
            if (blackKeyTextColor.HasValue) BlackKeyTextColor = blackKeyTextColor.Value;
            if (whiteKeyTextColor.HasValue) WhiteKeyTextColor = whiteKeyTextColor.Value;
        }

        /// <summary>
        /// 设置缩放级别
        /// </summary>
        public void SetZoomLevels(double horizontalZoom, double verticalZoom)
        {
            if (ZoomManager != null)
            {
                ZoomManager.Zoom = horizontalZoom;
                ZoomManager.VerticalZoom = verticalZoom;
            }
        }

        /// <summary>
        /// 设置滚动位置
        /// </summary>
        public void SetScrollPosition(double horizontalOffset, double verticalOffset)
        {
            HorizontalScrollOffset = horizontalOffset;
            VerticalScrollOffset = verticalOffset;
        }

        /// <summary>
        /// 设置视口大小
        /// </summary>
        public void SetViewportSize(double width, double height)
        {
            ViewportWidth = width;
            ViewportHeight = height;
        }
        #endregion

        #region 批量操作优化
        /// <summary>
        /// 开始批量操作
        /// </summary>
        public IDisposable BeginBatchOperation()
        {
            return new BatchOperationScope(this);
        }

        /// <summary>
        /// 批量操作作用域
        /// </summary>
        private class BatchOperationScope : IDisposable
        {
            private readonly PianoRollViewModel _viewModel;
            private bool _disposed;

            public BatchOperationScope(PianoRollViewModel viewModel)
            {
                _viewModel = viewModel;
                _viewModel._isInBatchOperation = true;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _viewModel._isInBatchOperation = false;
                    _viewModel.OnBatchOperationCompleted();
                    _disposed = true;
                }
            }
        }

        /// <summary>
        /// 批量操作完成处理
        /// </summary>
        private void OnBatchOperationCompleted()
        {
            // 刷新所有相关属性
            OnPropertyChanged(nameof(SongLengthInSeconds));
            OnPropertyChanged(nameof(SongLengthInPixels));
            OnPropertyChanged(nameof(MaxScrollExtent));
            OnPropertyChanged(nameof(MaxVerticalScrollExtent));
            OnPropertyChanged(nameof(HorizontalScrollBarVisible));
            OnPropertyChanged(nameof(VerticalScrollBarVisible));
            OnPropertyChanged(nameof(HasNotes));
            OnPropertyChanged(nameof(HasSelectedNotes));
            OnPropertyChanged(nameof(SelectedNotesCount));
            OnPropertyChanged(nameof(CurrentTrackName));
            OnPropertyChanged(nameof(CurrentTrackColor));
        }

        private bool _isInBatchOperation;
        #endregion

        #region 项目初始化方法
        /// <summary>
        /// 初始化新项目
        /// </summary>
        public void InitializeNewProject()
        {
            ClearContent();
            
            // 创建默认音轨
            var defaultTrack = new TrackViewModel(0, "", "Track 1")
            {
                Color = Colors.Blue,
                Index = 0
            };
            
            Tracks.Add(defaultTrack);
            CurrentTrackIndex = 0;
            
            // 设置默认配置
            SetDisplayConfiguration(
                showGrid: true,
                showNoteNames: true,
                showVelocity: false,
                showBlackKeys: true,
                showWhiteKeys: true);
                
            SetColorConfiguration(
                gridColor: Colors.LightGray,
                noteColor: Colors.Blue,
                selectedNoteColor: Colors.Red,
                backgroundColor: Colors.White,
                blackKeyColor: Colors.Black,
                whiteKeyColor: Colors.White,
                blackKeyTextColor: Colors.White,
                whiteKeyTextColor: Colors.Black);
                
            SetZoomLevels(1.0, 1.0);
            SetScrollPosition(0, 0);
            SetCurrentTool(EditorTool.Select);
            SetSnapToGrid(true);
            SetSnapValue(new MusicalFraction(1, 4)); // 四分音符
            SetUserDefinedNoteDuration(new MusicalFraction(1, 4)); // 四分音符
            SetCurrentEventType(EventType.Velocity);
            SetCurrentCCNumber(1);
        }

        /// <summary>
        /// 从MIDI文件加载项目
        /// </summary>
        public void LoadProjectFromMidi(string filePath)
        {
            // TODO: 实现MIDI文件加载逻辑
            // 这里应该调用MIDI解析服务来加载文件
            throw new NotImplementedException("MIDI文件加载功能尚未实现");
        }

        /// <summary>
        /// 保存项目到MIDI文件
        /// </summary>
        public void SaveProjectToMidi(string filePath)
        {
            // TODO: 实现MIDI文件保存逻辑
            // 这里应该调用MIDI导出服务来保存文件
            throw new NotImplementedException("MIDI文件保存功能尚未实现");
        }

        /// <summary>
        /// 导出项目为音频文件
        /// </summary>
        public void ExportProjectToAudio(string filePath, string audioFormat = "wav")
        {
            // TODO: 实现音频导出逻辑
            // 这里应该调用音频合成服务来导出文件
            throw new NotImplementedException("音频导出功能尚未实现");
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 更新最大滚动范围
        /// </summary>
        public void UpdateMaxScrollExtent()
        {
            if (Viewport != null)
            {
                Viewport.UpdateMaxScrollExtent(MaxScrollExtent);
            }
        }

        /// <summary>
        /// 获取音符边界矩形
        /// </summary>
        public Rect GetNoteBounds(NoteViewModel note)
        {
            var x = note.StartPosition.ToDouble() * TimeToPixelScale;
            var y = (127 - note.Pitch) * KeyHeight;
            var width = note.Duration.ToDouble() * TimeToPixelScale;
            var height = KeyHeight;
            
            return new Rect(x, y, width, height);
        }

        /// <summary>
        /// 获取选择矩形内的音符
        /// </summary>
        public IEnumerable<NoteViewModel> GetNotesInSelectionRect(Rect selectionRect)
        {
            return CurrentTrackNotes.Where(note =>
            {
                var noteBounds = GetNoteBounds(note);
                return selectionRect.Intersects(noteBounds);
            });
        }

        /// <summary>
        /// 检查点是否在音符内
        /// </summary>
        public bool IsPointInNote(Point point, NoteViewModel note)
        {
            var noteBounds = GetNoteBounds(note);
            return noteBounds.Contains(point);
        }

        /// <summary>
        /// 获取音符的调整大小句柄位置
        /// </summary>
        public Point GetNoteResizeHandlePosition(NoteViewModel note, ResizeHandle handle)
        {
            var noteBounds = GetNoteBounds(note);
            
            return handle switch
            {
                ResizeHandle.StartEdge => new Point(noteBounds.X, noteBounds.Y + noteBounds.Height / 2),
                ResizeHandle.EndEdge => new Point(noteBounds.X + noteBounds.Width, noteBounds.Y + noteBounds.Height / 2),
                _ => new Point(noteBounds.X + noteBounds.Width / 2, noteBounds.Y + noteBounds.Height / 2)
            };
        }
        #endregion
    }
}