using CommunityToolkit.Mvvm.ComponentModel;
// DominoNext - ViewModel 基类，所有视图模型继承自此类。
// 全局注释：本文件为 MVVM 基础，包含资源释放等通用逻辑。
using System;

namespace DominoNext.ViewModels
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
                    EnderDebugger.EnderLogger.Instance.Info(this.GetType().Name, $"[EnderDebugger][2025-10-02 18:41:03.114][EnderLogger][{this.GetType().Name}]资源释放Dispose({disposing})");
                }
                _disposed = true;
            }
        }
    }
}
