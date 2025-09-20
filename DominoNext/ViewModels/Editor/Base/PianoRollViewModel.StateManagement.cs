using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Lumino.Models;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Components;
using Lumino.ViewModels.Editor.Dialogs;
using Lumino.ViewModels.Editor.Enums;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel的状态管理和通知功能
    /// </summary>
    public partial class PianoRollViewModel
    {
        #region 状态管理属性
        /// <summary>
        /// 应用程序状态
        /// </summary>
        public ApplicationState CurrentState
        {
            get => _currentState;
            private set
            {
                if (SetProperty(ref _currentState, value))
                {
                    OnApplicationStateChanged(value);
                }
            }
        }
        private ApplicationState _currentState = ApplicationState.Idle;

        /// <summary>
        /// 编辑器状态
        /// </summary>
        public EditorState CurrentEditorState
        {
            get => _currentEditorState;
            private set
            {
                if (SetProperty(ref _currentEditorState, value))
                {
                    OnEditorStateChanged(value);
                }
            }
        }
        private EditorState _currentEditorState = EditorState.Ready;

        /// <summary>
        /// 播放状态
        /// </summary>
        public PlaybackState CurrentPlaybackState
        {
            get => _currentPlaybackState;
            private set
            {
                if (SetProperty(ref _currentPlaybackState, value))
                {
                    OnPlaybackStateChanged(value);
                }
            }
        }
        private PlaybackState _currentPlaybackState = PlaybackState.Stopped;




        /// <summary>
        /// 是否正在处理长时间操作
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnBusyStateChanged(value);
                }
            }
        }
        private bool _isBusy = false;

        /// <summary>
        /// 忙碌状态消息
        /// </summary>
        public string BusyMessage
        {
            get => _busyMessage;
            private set => SetProperty(ref _busyMessage, value ?? string.Empty);
        }
        private string _busyMessage = string.Empty;

        /// <summary>
        /// 操作进度（0-100）
        /// </summary>
        public double OperationProgress
        {
            get => _operationProgress;
            private set => SetProperty(ref _operationProgress, Math.Clamp(value, 0, 100));
        }
        private double _operationProgress = 0;
        #endregion

        #region 状态变化处理方法
        /// <summary>
        /// 应用程序状态变化处理
        /// </summary>
        private void OnApplicationStateChanged(ApplicationState newState)
        {
            try
            {
                // 发送状态变化通知
                WeakReferenceMessenger.Default.Send(new ApplicationStateChangedMessage(newState));
                
                // 更新UI状态
                UpdateUIForState(newState);
                
                // 记录状态变化
                LogStateChange("Application", newState.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用程序状态变化处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 编辑器状态变化处理
        /// </summary>
        private void OnEditorStateChanged(EditorState newState)
        {
            try
            {
                // 发送状态变化通知
                WeakReferenceMessenger.Default.Send(new EditorStateChangedMessage(newState));
                
                // 更新工具可用性
                UpdateToolAvailability(newState);
                
                // 记录状态变化
                LogStateChange("Editor", newState.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"编辑器状态变化处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 播放状态变化处理
        /// </summary>
        private void OnPlaybackStateChanged(PlaybackState newState)
        {
            try
            {
                // 发送状态变化通知
                WeakReferenceMessenger.Default.Send(new PlaybackStateChangedMessage(newState));
                
                // 更新播放控制UI
                UpdatePlaybackControls(newState);
                
                // 记录状态变化
                LogStateChange("Playback", newState.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"播放状态变化处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 未保存更改状态变化处理
        /// </summary>
        private void OnUnsavedChangesChanged(bool hasChanges)
        {
            try
            {
                // 发送状态变化通知
                WeakReferenceMessenger.Default.Send(new UnsavedChangesChangedMessage(hasChanges));
                
                // 更新标题栏
                UpdateTitleBar(hasChanges);
                
                // 记录状态变化
                LogStateChange("UnsavedChanges", hasChanges.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"未保存更改状态变化处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 忙碌状态变化处理
        /// </summary>
        private void OnBusyStateChanged(bool isBusy)
        {
            try
            {
                // 发送状态变化通知
                WeakReferenceMessenger.Default.Send(new BusyStateChangedMessage(isBusy, BusyMessage));
                
                // 更新UI状态
                SetUIState(!isBusy, isBusy ? BusyMessage : null);
                
                // 记录状态变化
                LogStateChange("Busy", isBusy.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"忙碌状态变化处理失败: {ex.Message}");
            }
        }
        #endregion

        #region 状态管理方法
        /// <summary>
        /// 设置应用程序状态
        /// </summary>
        private void SetApplicationState(ApplicationState state)
        {
            CurrentState = state;
        }

        /// <summary>
        /// 设置编辑器状态
        /// </summary>
        private void SetEditorState(EditorState state)
        {
            CurrentEditorState = state;
        }

        /// <summary>
        /// 设置播放状态
        /// </summary>
        private void SetPlaybackState(PlaybackState state)
        {
            CurrentPlaybackState = state;
        }

        /// <summary>
        /// 标记未保存的更改
        /// </summary>
        private void MarkAsUnsaved()
        {
            HasUnsavedChanges = true;
        }

        /// <summary>
        /// 清除未保存的更改标记
        /// </summary>
        private void ClearUnsavedChanges()
        {
            HasUnsavedChanges = false;
        }

        /// <summary>
        /// 开始长时间操作
        /// </summary>
        private void BeginLongOperation(string message, bool showProgress = false)
        {
            IsBusy = true;
            BusyMessage = message;
            OperationProgress = 0;
            
            if (showProgress)
            {
                _ = Task.Run(async () =>
                {
                    _currentProgressDialog = await ShowProgressDialogAsync("操作中", message);
                });
            }
        }

        /// <summary>
        /// 更新操作进度
        /// </summary>
        private void UpdateOperationProgress(double progress, string? message = null)
        {
            OperationProgress = progress;
            
            if (!string.IsNullOrEmpty(message))
            {
                BusyMessage = message;
            }
            
            if (_currentProgressDialog != null)
            {
                UpdateProgress(_currentProgressDialog, (int)progress, message);
            }
        }

        /// <summary>
        /// 结束长时间操作
        /// </summary>
        private void EndLongOperation()
        {
            IsBusy = false;
            BusyMessage = string.Empty;
            OperationProgress = 0;
            
            if (_currentProgressDialog != null)
            {
                CloseProgressDialog(_currentProgressDialog);
                _currentProgressDialog = null;
            }
        }

        /// <summary>
        /// 检查是否可以执行操作
        /// </summary>
        private bool CanExecuteOperation()
        {
            return !IsBusy && CurrentState != ApplicationState.Error;
        }

        /// <summary>
        /// 检查是否可以编辑
        /// </summary>
        private bool CanEdit()
        {
            return CanExecuteOperation() && 
                   CurrentEditorState != EditorState.Playing &&
                   CurrentEditorState != EditorState.Recording;
        }

        /// <summary>
        /// 检查是否可以播放
        /// </summary>
        private bool CanPlay()
        {
            return CanExecuteOperation() && 
                   CurrentEditorState != EditorState.Recording &&
                   CurrentPlaybackState != PlaybackState.Playing;
        }
        #endregion

        #region 通知管理方法
        /// <summary>
        /// 发送操作成功通知
        /// </summary>
        private void NotifySuccess(string message, string? actionText = null, Action? action = null)
        {
            ShowNotification(message, NotificationType.Success);
            
            if (!string.IsNullOrEmpty(actionText) && action != null)
            {
                // TODO: 实现可操作的通知识别
            }
        }

        /// <summary>
        /// 发送操作失败通知
        /// </summary>
        private void NotifyError(string message, Exception? exception = null)
        {
            var fullMessage = exception != null ? $"{message}: {exception.Message}" : message;
            ShowNotification(fullMessage, NotificationType.Error);
            
            // 记录错误
            LogError(message, exception);
        }

        /// <summary>
        /// 发送警告通知
        /// </summary>
        private void NotifyWarning(string message)
        {
            ShowNotification(message, NotificationType.Warning);
        }

        /// <summary>
        /// 发送信息通知
        /// </summary>
        private void NotifyInfo(string message)
        {
            ShowNotification(message, NotificationType.Info);
        }

        /// <summary>
        /// 发送进度通知
        /// </summary>
        private void NotifyProgress(string message, double progress)
        {
            UpdateOperationProgress(progress, message);
        }
        #endregion

        #region 日志记录方法
        /// <summary>
        /// 记录状态变化
        /// </summary>
        private void LogStateChange(string stateType, string newValue)
        {
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {stateType}状态变化: {newValue}");
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        private void LogError(string message, Exception? exception = null)
        {
            var errorMessage = $"[{DateTime.Now:HH:mm:ss.fff}] 错误: {message}";
            if (exception != null)
            {
                errorMessage += $"\n异常: {exception}";
            }
            
            System.Diagnostics.Debug.WriteLine(errorMessage);
        }

        /// <summary>
        /// 记录操作
        /// </summary>
        private void LogOperation(string operation, string details = "")
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] 操作: {operation}";
            if (!string.IsNullOrEmpty(details))
            {
                logMessage += $" - {details}";
            }
            
            System.Diagnostics.Debug.WriteLine(logMessage);
        }
        #endregion

        #region UI更新方法
        /// <summary>
        /// 根据状态更新UI
        /// </summary>
        private void UpdateUIForState(ApplicationState state)
        {
            try
            {
                switch (state)
                {
                    case ApplicationState.Idle:
                        // 应用程序空闲状态
                        SetUIState(true);
                        break;
                        
                    case ApplicationState.Loading:
                        // 应用程序加载状态
                        SetUIState(false, "正在加载...");
                        break;
                        
                    case ApplicationState.Error:
                        // 应用程序错误状态
                        SetUIState(false, "发生错误");
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新UI状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新工具可用性
        /// </summary>
        private void UpdateToolAvailability(EditorState state)
        {
            try
            {
                // TODO: 根据编辑器状态更新工具可用性
                // 这里应该通知工具栏和其他UI组件更新状态
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新工具可用性失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新播放控制
        /// </summary>
        private void UpdatePlaybackControls(PlaybackState state)
        {
            try
            {
                // TODO: 根据播放状态更新播放控制UI
                // 这里应该通知播放控制栏更新状态
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新播放控制失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新标题栏
        /// </summary>
        private void UpdateTitleBar(bool hasUnsavedChanges)
        {
            try
            {
                var title = ProjectName ?? "未命名项目";
                if (hasUnsavedChanges)
                {
                    title += " *";
                }
                
                // TODO: 更新窗口标题
                // 这里应该通知主窗口更新标题
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新标题栏失败: {ex.Message}");
            }
        }
        #endregion

        #region 状态消息类
        /// <summary>
        /// 应用程序状态变化消息
        /// </summary>
        public class ApplicationStateChangedMessage
        {
            public ApplicationState NewState { get; }
            
            public ApplicationStateChangedMessage(ApplicationState newState)
            {
                NewState = newState;
            }
        }

        /// <summary>
        /// 编辑器状态变化消息
        /// </summary>
        public class EditorStateChangedMessage
        {
            public EditorState NewState { get; }
            
            public EditorStateChangedMessage(EditorState newState)
            {
                NewState = newState;
            }
        }

        /// <summary>
        /// 播放状态变化消息
        /// </summary>
        public class PlaybackStateChangedMessage
        {
            public PlaybackState NewState { get; }
            
            public PlaybackStateChangedMessage(PlaybackState newState)
            {
                NewState = newState;
            }
        }

        /// <summary>
        /// 未保存更改状态变化消息
        /// </summary>
        public class UnsavedChangesChangedMessage
        {
            public bool HasChanges { get; }
            
            public UnsavedChangesChangedMessage(bool hasChanges)
            {
                HasChanges = hasChanges;
            }
        }

        /// <summary>
        /// 忙碌状态变化消息
        /// </summary>
        public class BusyStateChangedMessage
        {
            public bool IsBusy { get; }
            public string Message { get; }
            
            public BusyStateChangedMessage(bool isBusy, string message)
            {
                IsBusy = isBusy;
                Message = message;
            }
        }
        #endregion

        #region 私有字段
        /// <summary>
        /// 当前进度对话框
        /// </summary>
        private IProgressDialog? _currentProgressDialog;
        #endregion
    }
}