using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace EnderDebugger.ViewModels
{
    public class ViewModelBase : ObservableObject, IDisposable
    {
        private bool _disposed;

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
                    EnderLogger.Instance.Info(this.GetType().Name, $"资源释放: Dispose({disposing})");
                }
                _disposed = true;
            }
        }
    }
}