using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Lumino.ViewModels.Base
{
    /// <summary>
    /// ����֪ͨ�������� - �����Ա��֪ͨ���������Թ���
    /// ����ViewModel���ظ�������֪ͨ����
    /// </summary>
    public static class PropertyNotificationHelper
    {
        #region ����������ϵ����
        /// <summary>
        /// ����������ϵ�ֵ� - ��Ϊ�����ԣ�ֵΪ���������б�
        /// </summary>
        private static readonly Dictionary<string, List<PropertyDependency>> PropertyDependencies = new();

        /// <summary>
        /// ����������ϵ��Ϣ
        /// </summary>
        public class PropertyDependency
        {
            public string DependentProperty { get; set; } = string.Empty;
            public Type ViewModelType { get; set; } = typeof(object);
        }

        /// <summary>
        /// ע������������ϵ
        /// </summary>
        /// <param name="viewModelType">ViewModel����</param>
        /// <param name="sourceProperty">Դ������</param>
        /// <param name="dependentProperties">��������������</param>
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
        /// ��ȡ���Ե�������
        /// </summary>
        /// <param name="viewModelType">ViewModel����</param>
        /// <param name="sourceProperty">Դ������</param>
        /// <returns>���������б�</returns>
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

        #region ��������֪ͨ
        /// <summary>
        /// �����������Ա��֪ͨ
        /// </summary>
        /// <param name="notifier">���Ա��֪ͨ����</param>
        /// <param name="propertyNames">����������</param>
        public static void NotifyMultipleProperties(INotifyPropertyChanged notifier, params string[] propertyNames)
        {
            if (notifier is INotifyPropertyChanged notifyObject)
            {
                foreach (var propertyName in propertyNames)
                {
                    // ͨ���������OnPropertyChanged����
                    var method = notifier.GetType().GetMethod("OnPropertyChanged", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                        null, new[] { typeof(string) }, null);
                    
                    method?.Invoke(notifier, new object[] { propertyName });
                }
            }
        }

        /// <summary>
        /// �������Լ����������Եı��֪ͨ
        /// </summary>
        /// <param name="notifier">���Ա��֪ͨ����</param>
        /// <param name="propertyName">��������</param>
        public static void NotifyPropertyWithDependents(INotifyPropertyChanged notifier, [CallerMemberName] string? propertyName = null)
        {
            if (string.IsNullOrEmpty(propertyName)) return;

            // ֪ͨ������
            var method = notifier.GetType().GetMethod("OnPropertyChanged", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null, new[] { typeof(string) }, null);
            
            method?.Invoke(notifier, new object[] { propertyName });

            // ֪ͨ��������
            var dependentProperties = GetDependentProperties(notifier.GetType(), propertyName);
            foreach (var dependentProperty in dependentProperties)
            {
                method?.Invoke(notifier, new object[] { dependentProperty });
            }
        }
        #endregion
    }

    /// <summary>
    /// ֧����ǿ����֪ͨ��ViewModel����
    /// ���PropertyNotificationHelper�ṩ��ǿ�������֪ͨ����
    /// </summary>
    public abstract class PropertyNotificationViewModelBase : EnhancedViewModelBase
    {
        #region ���캯��
        protected PropertyNotificationViewModelBase() : base()
        {
            // ע�ᵱǰ���͵�����������ϵ
            RegisterPropertyDependencies();
        }
        #endregion

        #region �鷽��
        /// <summary>
        /// ע������������ϵ - ��������д�˷�����������������
        /// </summary>
        protected virtual void RegisterPropertyDependencies()
        {
            // ���಻��Ҫע��������ϵ
        }
        #endregion

        #region ��ǿ���������÷���
        /// <summary>
        /// ��������ֵ���Զ�֪ͨ��������
        /// </summary>
        /// <typeparam name="T">��������</typeparam>
        /// <param name="field">�����ֶ�����</param>
        /// <param name="value">��ֵ</param>
        /// <param name="propertyName">������</param>
        /// <returns>�Ƿ����˸ı�</returns>
        protected bool SetPropertyWithAutoDependents<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (SetProperty(ref field, value, propertyName))
            {
                // �Զ�֪ͨ��������
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
        /// �������Լ����������Եı��֪ͨ
        /// </summary>
        /// <param name="propertyName">������</param>
        protected void NotifyPropertyWithDependents([CallerMemberName] string? propertyName = null)
        {
            PropertyNotificationHelper.NotifyPropertyWithDependents(this, propertyName);
        }

        /// <summary>
        /// ����֪ͨ�������
        /// </summary>
        /// <param name="propertyNames">����������</param>
        protected void NotifyMultipleProperties(params string[] propertyNames)
        {
            PropertyNotificationHelper.NotifyMultipleProperties(this, propertyNames);
        }
        #endregion

        #region ��������
        /// <summary>
        /// ע�ᵥ�����Ե�������ϵ
        /// </summary>
        /// <param name="sourceProperty">Դ������</param>
        /// <param name="dependentProperties">��������������</param>
        protected void RegisterDependency(string sourceProperty, params string[] dependentProperties)
        {
            PropertyNotificationHelper.RegisterPropertyDependencies(GetType(), sourceProperty, dependentProperties);
        }
        #endregion
    }
}