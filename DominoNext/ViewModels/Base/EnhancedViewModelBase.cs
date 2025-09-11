using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DominoNext.Services.Interfaces;

namespace DominoNext.ViewModels.Base
{
    /// <summary>
    /// 增强的ViewModel基类 - 提供通用功能和模式复用
    /// 解决重复代码问题，提供高内聚低耦合的架构支持
    /// </summary>
    public abstract class EnhancedViewModelBase : ViewModelBase
    {
        #region 服务依赖保护字段
        /// <summary>
        /// 对话框服务 - 用于统一的错误处理和用户交互
        /// </summary>
        protected IDialogService? DialogService { get; private set; }

        /// <summary>
        /// 日志服务 - 用于统一的日志记录
        /// </summary>
        protected ILoggingService? LoggingService { get; private set; }
        #endregion

        #region 异步操作状态
        private bool _isBusy;

        /// <summary>
        /// 指示当前是否正在执行异步操作
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            protected set => SetProperty(ref _isBusy, value);
        }
        #endregion

        #region 构造函数
        /// <summary>
        /// 构造函数 - 可选择性注入服务依赖
        /// </summary>
        /// <param name="dialogService">对话框服务（可选）</param>
        /// <param name="loggingService">日志服务（可选）</param>
        protected EnhancedViewModelBase(IDialogService? dialogService = null, ILoggingService? loggingService = null)
        {
            DialogService = dialogService;
            LoggingService = loggingService;
        }
        #endregion

        #region 通用异常处理模式
        /// <summary>
        /// 执行带异常处理的异步操作
        /// 统一处理异常显示和日志记录，减少重复代码
        /// </summary>
        /// <param name="operation">要执行的异步操作</param>
        /// <param name="errorTitle">错误对话框标题</param>
        /// <param name="operationName">操作名称（用于日志）</param>
        /// <param name="showSuccessMessage">是否显示成功消息</param>
        /// <param name="successMessage">成功消息内容</param>
        protected async Task ExecuteWithExceptionHandlingAsync(
            Func<Task> operation,
            string errorTitle = "错误",
            string? operationName = null,
            bool showSuccessMessage = false,
            string? successMessage = null)
        {
            try
            {
                IsBusy = true;
                await operation();

                if (showSuccessMessage && !string.IsNullOrEmpty(successMessage) && DialogService != null)
                {
                    await DialogService.ShowInfoDialogAsync("成功", successMessage);
                }
            }
            catch (OperationCanceledException)
            {
                // 用户取消操作，不显示错误
                LoggingService?.LogInfo($"用户取消操作: {operationName ?? "未知操作"}", GetType().Name);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, errorTitle, operationName);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 执行带异常处理和返回值的异步操作
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="operation">要执行的异步操作</param>
        /// <param name="defaultValue">发生异常时的默认返回值</param>
        /// <param name="errorTitle">错误对话框标题</param>
        /// <param name="operationName">操作名称（用于日志）</param>
        /// <returns>操作结果或默认值</returns>
        protected async Task<T> ExecuteWithExceptionHandlingAsync<T>(
            Func<Task<T>> operation,
            T defaultValue,
            string errorTitle = "错误",
            string? operationName = null)
        {
            try
            {
                IsBusy = true;
                return await operation();
            }
            catch (OperationCanceledException)
            {
                LoggingService?.LogInfo($"用户取消操作: {operationName ?? "未知操作"}", GetType().Name);
                return defaultValue;
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, errorTitle, operationName);
                return defaultValue;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 统一的异常处理方法
        /// </summary>
        /// <param name="ex">异常对象</param>
        /// <param name="errorTitle">错误标题</param>
        /// <param name="operationName">操作名称</param>
        private async Task HandleExceptionAsync(Exception ex, string errorTitle, string? operationName)
        {
            var operation = operationName ?? "未知操作";
            var errorMessage = $"{operation}失败：{ex.Message}";

            // 记录日志
            LoggingService?.LogException(ex, $"{operation}时发生异常", GetType().Name);

            // 显示错误对话框
            if (DialogService != null)
            {
                await DialogService.ShowErrorDialogAsync(errorTitle, errorMessage);
            }

            // 调试输出（在调试模式下）
            System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] {operation}时发生错误: {ex.Message}");
        }
        #endregion

        #region 确认对话框辅助方法
        /// <summary>
        /// 显示确认对话框并执行操作
        /// </summary>
        /// <param name="operation">要执行的操作</param>
        /// <param name="confirmationTitle">确认对话框标题</param>
        /// <param name="confirmationMessage">确认消息</param>
        /// <param name="operationName">操作名称</param>
        protected async Task ExecuteWithConfirmationAsync(
            Func<Task> operation,
            string confirmationTitle,
            string confirmationMessage,
            string? operationName = null)
        {
            if (DialogService != null)
            {
                var confirmed = await DialogService.ShowConfirmationDialogAsync(confirmationTitle, confirmationMessage);
                if (!confirmed)
                {
                    return;
                }
            }

            await ExecuteWithExceptionHandlingAsync(operation, "错误", operationName);
        }
        #endregion

        #region 属性变更通知增强
        /// <summary>
        /// 设置属性值并触发相关属性的变更通知
        /// 支持依赖属性的自动通知
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="field">属性字段</param>
        /// <param name="value">新值</param>
        /// <param name="dependentProperties">依赖属性名称数组</param>
        /// <param name="propertyName">属性名称</param>
        /// <returns>是否发生了改变</returns>
        protected bool SetPropertyWithDependents<T>(
            ref T field, 
            T value, 
            string[]? dependentProperties = null,
            [CallerMemberName] string? propertyName = null)
        {
            if (SetProperty(ref field, value, propertyName))
            {
                // 通知依赖属性
                if (dependentProperties != null)
                {
                    foreach (var dependentProperty in dependentProperties)
                    {
                        OnPropertyChanged(dependentProperty);
                    }
                }
                return true;
            }
            return false;
        }
        #endregion

        #region 安全的资源清理
        /// <summary>
        /// 安全释放资源的模板方法
        /// 派生类可重写DisposeCore来释放特定资源
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    DisposeCore();
                }
                catch (Exception ex)
                {
                    // 释放资源时发生错误，记录但不抛出
                    LoggingService?.LogException(ex, "释放ViewModel资源时发生异常", GetType().Name);
                    System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] 释放资源时发生错误: {ex.Message}");
                }
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// 派生类重写此方法来释放特定资源
        /// </summary>
        protected virtual void DisposeCore()
        {
            // 基类不需要释放特定资源
        }
        #endregion
    }
}