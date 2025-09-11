using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DominoNext.ViewModels.Base
{
    /// <summary>
    /// 属性通知辅助工具 - 简化属性变更通知和依赖属性管理
    /// 减少ViewModel中重复的属性通知代码
    /// </summary>
    public static class PropertyNotificationHelper
    {
        #region 属性依赖关系管理
        /// <summary>
        /// 属性依赖关系字典 - 键为主属性，值为依赖属性列表
        /// </summary>
        private static readonly Dictionary<string, List<PropertyDependency>> PropertyDependencies = new();

        /// <summary>
        /// 属性依赖关系信息
        /// </summary>
        public class PropertyDependency
        {
            public string DependentProperty { get; set; } = string.Empty;
            public Type ViewModelType { get; set; } = typeof(object);
        }

        /// <summary>
        /// 注册属性依赖关系
        /// </summary>
        /// <param name="viewModelType">ViewModel类型</param>
        /// <param name="sourceProperty">源属性名</param>
        /// <param name="dependentProperties">依赖属性名数组</param>
        public static void RegisterPropertyDependencies(Type viewModelType, string sourceProperty, params string[] dependentProperties)
        {
            var key = $"{viewModelType.FullName}.{sourceProperty}";
            
            if (!PropertyDependencies.ContainsKey(key))
            {
                PropertyDependencies[key] = new List<PropertyDependency>();
            }

            foreach (var dependentProperty in dependentProperties)
            {
                PropertyDependencies[key].Add(new PropertyDependency
                {
                    DependentProperty = dependentProperty,
                    ViewModelType = viewModelType
                });
            }
        }

        /// <summary>
        /// 获取属性的依赖项
        /// </summary>
        /// <param name="viewModelType">ViewModel类型</param>
        /// <param name="sourceProperty">源属性名</param>
        /// <returns>依赖属性列表</returns>
        public static IEnumerable<string> GetDependentProperties(Type viewModelType, string sourceProperty)
        {
            var key = $"{viewModelType.FullName}.{sourceProperty}";
            
            if (PropertyDependencies.TryGetValue(key, out var dependencies))
            {
                foreach (var dependency in dependencies)
                {
                    yield return dependency.DependentProperty;
                }
            }
        }
        #endregion

        #region 批量属性通知
        /// <summary>
        /// 批量触发属性变更通知
        /// </summary>
        /// <param name="notifier">属性变更通知对象</param>
        /// <param name="propertyNames">属性名数组</param>
        public static void NotifyMultipleProperties(INotifyPropertyChanged notifier, params string[] propertyNames)
        {
            if (notifier is INotifyPropertyChanged notifyObject)
            {
                foreach (var propertyName in propertyNames)
                {
                    // 通过反射调用OnPropertyChanged方法
                    var method = notifier.GetType().GetMethod("OnPropertyChanged", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                        null, new[] { typeof(string) }, null);
                    
                    method?.Invoke(notifier, new object[] { propertyName });
                }
            }
        }

        /// <summary>
        /// 触发属性及其依赖属性的变更通知
        /// </summary>
        /// <param name="notifier">属性变更通知对象</param>
        /// <param name="propertyName">主属性名</param>
        public static void NotifyPropertyWithDependents(INotifyPropertyChanged notifier, [CallerMemberName] string? propertyName = null)
        {
            if (string.IsNullOrEmpty(propertyName)) return;

            // 通知主属性
            var method = notifier.GetType().GetMethod("OnPropertyChanged", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null, new[] { typeof(string) }, null);
            
            method?.Invoke(notifier, new object[] { propertyName });

            // 通知依赖属性
            var dependentProperties = GetDependentProperties(notifier.GetType(), propertyName);
            foreach (var dependentProperty in dependentProperties)
            {
                method?.Invoke(notifier, new object[] { dependentProperty });
            }
        }
        #endregion
    }

    /// <summary>
    /// 支持增强属性通知的ViewModel基类
    /// 结合PropertyNotificationHelper提供更强大的属性通知功能
    /// </summary>
    public abstract class PropertyNotificationViewModelBase : EnhancedViewModelBase
    {
        #region 构造函数
        protected PropertyNotificationViewModelBase() : base()
        {
            // 注册当前类型的属性依赖关系
            RegisterPropertyDependencies();
        }
        #endregion

        #region 虚方法
        /// <summary>
        /// 注册属性依赖关系 - 派生类重写此方法来定义属性依赖
        /// </summary>
        protected virtual void RegisterPropertyDependencies()
        {
            // 基类不需要注册依赖关系
        }
        #endregion

        #region 增强的属性设置方法
        /// <summary>
        /// 设置属性值并自动通知依赖属性
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="field">属性字段引用</param>
        /// <param name="value">新值</param>
        /// <param name="propertyName">属性名</param>
        /// <returns>是否发生了改变</returns>
        protected bool SetPropertyWithAutoDependents<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (SetProperty(ref field, value, propertyName))
            {
                // 自动通知依赖属性
                if (!string.IsNullOrEmpty(propertyName))
                {
                    var dependentProperties = PropertyNotificationHelper.GetDependentProperties(GetType(), propertyName);
                    foreach (var dependentProperty in dependentProperties)
                    {
                        OnPropertyChanged(dependentProperty);
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 触发属性及其依赖属性的变更通知
        /// </summary>
        /// <param name="propertyName">属性名</param>
        protected void NotifyPropertyWithDependents([CallerMemberName] string? propertyName = null)
        {
            PropertyNotificationHelper.NotifyPropertyWithDependents(this, propertyName);
        }

        /// <summary>
        /// 批量通知多个属性
        /// </summary>
        /// <param name="propertyNames">属性名数组</param>
        protected void NotifyMultipleProperties(params string[] propertyNames)
        {
            PropertyNotificationHelper.NotifyMultipleProperties(this, propertyNames);
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 注册单个属性的依赖关系
        /// </summary>
        /// <param name="sourceProperty">源属性名</param>
        /// <param name="dependentProperties">依赖属性名数组</param>
        protected void RegisterDependency(string sourceProperty, params string[] dependentProperties)
        {
            PropertyNotificationHelper.RegisterPropertyDependencies(GetType(), sourceProperty, dependentProperties);
        }
        #endregion
    }
}