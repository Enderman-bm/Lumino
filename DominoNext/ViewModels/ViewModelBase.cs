using CommunityToolkit.Mvvm.ComponentModel;
using System;
using DominoNext.Services.Interfaces;
using DominoNext.ViewModels.Base;

namespace DominoNext.ViewModels
{
    /// <summary>
    /// 增强的ViewModel基类 - 提供通用功能和设计时支持
    /// 继承自原有基类，添加设计时服务提供者支持
    /// </summary>
    public class ViewModelBase : ObservableObject, IDisposable
    {
        private bool _disposed;

        #region 设计时支持
        /// <summary>
        /// 检查是否在设计时模式
        /// </summary>
        public static bool IsInDesignMode => DesignTimeServiceProvider.IsInDesignMode();

        /// <summary>
        /// 获取设计时服务的便捷方法
        /// </summary>
        protected static T GetDesignTimeService<T>() where T : class
        {
            var serviceType = typeof(T);
            
            if (serviceType == typeof(ICoordinateService))
                return DesignTimeServiceProvider.GetCoordinateService() as T;
            if (serviceType == typeof(IEventCurveCalculationService))
                return DesignTimeServiceProvider.GetEventCurveCalculationService() as T;
            if (serviceType == typeof(IMidiConversionService))
                return DesignTimeServiceProvider.GetMidiConversionService() as T;
            if (serviceType == typeof(ISettingsService))
                return DesignTimeServiceProvider.GetSettingsService() as T;
            if (serviceType == typeof(ILoggingService))
                return DesignTimeServiceProvider.GetLoggingService() as T;
            if (serviceType == typeof(IDialogService))
                return DesignTimeServiceProvider.GetDialogService() as T;
            if (serviceType == typeof(IApplicationService))
                return DesignTimeServiceProvider.GetApplicationService() as T;
            if (serviceType == typeof(IProjectStorageService))
                return DesignTimeServiceProvider.GetProjectStorageService() as T;
                
            throw new NotSupportedException($"设计时服务类型 {serviceType.Name} 不受支持");
        }
        #endregion

        #region 资源释放
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 派生类可重写此方法来释放特定资源
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    try
                    {
                        DisposeCore();
                    }
                    catch (Exception ex)
                    {
                        // 记录错误但不抛出异常
                        System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] 释放资源时发生错误: {ex.Message}");
                    }
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// 派生类重写此方法来释放特定资源
        /// </summary>
        protected virtual void DisposeCore()
        {
            // 基类不需要释放特定资源
        }
        #endregion

        #region 通用辅助方法
        /// <summary>
        /// 安全地设置属性值，包含错误处理
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="field">字段引用</param>
        /// <param name="value">新值</param>
        /// <param name="propertyName">属性名称</param>
        /// <returns>是否成功设置</returns>
        protected bool SafeSetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            try
            {
                return SetProperty(ref field, value, propertyName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] 设置属性 {propertyName} 时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 安全地触发属性变更通知
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected void SafeOnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            try
            {
                OnPropertyChanged(propertyName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] 触发属性变更通知 {propertyName} 时发生错误: {ex.Message}");
            }
        }
        #endregion
    }
}
