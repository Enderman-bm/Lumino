using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Lumino.Services.Interfaces;

namespace Lumino.ViewModels.Base
{
    /// <summary>
    /// ��ǿ��ViewModel���� - �ṩͨ�ù��ܺ�ģʽ����
    /// ����ظ��������⣬�ṩ���ھ۵���ϵļܹ�֧��
    /// </summary>
    public abstract class EnhancedViewModelBase : ViewModelBase
    {
        #region �������������ֶ�
        /// <summary>
        /// �Ի������ - ����ͳһ�Ĵ��������û�����
        /// </summary>
        protected IDialogService? DialogService { get; private set; }

        /// <summary>
        /// ��־���� - ����ͳһ����־��¼
        /// </summary>
        protected ILoggingService? LoggingService { get; private set; }
        #endregion

        #region �첽����״̬
        private bool _isBusy;

        /// <summary>
        /// ָʾ��ǰ�Ƿ�����ִ���첽����
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            protected set => SetProperty(ref _isBusy, value);
        }
        #endregion

        #region ���캯��
        /// <summary>
        /// ���캯�� - ��ѡ����ע���������
        /// </summary>
        /// <param name="dialogService">�Ի�����񣨿�ѡ��</param>
        /// <param name="loggingService">��־���񣨿�ѡ��</param>
        protected EnhancedViewModelBase(IDialogService? dialogService = null, ILoggingService? loggingService = null)
        {
            DialogService = dialogService;
            LoggingService = loggingService;
        }
        #endregion

        #region ͨ���쳣����ģʽ
        /// <summary>
        /// ִ�д��쳣�������첽����
        /// ͳһ�����쳣��ʾ����־��¼�������ظ�����
        /// </summary>
        /// <param name="operation">Ҫִ�е��첽����</param>
        /// <param name="errorTitle">����Ի������</param>
        /// <param name="operationName">�������ƣ�������־��</param>
        /// <param name="showSuccessMessage">�Ƿ���ʾ�ɹ���Ϣ</param>
        /// <param name="successMessage">�ɹ���Ϣ����</param>
        protected async Task ExecuteWithExceptionHandlingAsync(
            Func<Task> operation,
            string errorTitle = "����",
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
                    await DialogService.ShowInfoDialogAsync("�ɹ�", successMessage);
                }
            }
            catch (OperationCanceledException)
            {
                // �û�ȡ������������ʾ����
                LoggingService?.LogInfo($"�û�ȡ������: {operationName ?? "δ֪����"}", GetType().Name);
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
        /// ִ�д��쳣�����ͷ���ֵ���첽����
        /// </summary>
        /// <typeparam name="T">����ֵ����</typeparam>
        /// <param name="operation">Ҫִ�е��첽����</param>
        /// <param name="defaultValue">�����쳣ʱ��Ĭ�Ϸ���ֵ</param>
        /// <param name="errorTitle">����Ի������</param>
        /// <param name="operationName">�������ƣ�������־��</param>
        /// <returns>���������Ĭ��ֵ</returns>
        protected async Task<T> ExecuteWithExceptionHandlingAsync<T>(
            Func<Task<T>> operation,
            T defaultValue,
            string errorTitle = "����",
            string? operationName = null)
        {
            try
            {
                IsBusy = true;
                return await operation();
            }
            catch (OperationCanceledException)
            {
                LoggingService?.LogInfo($"�û�ȡ������: {operationName ?? "δ֪����"}", GetType().Name);
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
        /// ͳһ���쳣��������
        /// </summary>
        /// <param name="ex">�쳣����</param>
        /// <param name="errorTitle">�������</param>
        /// <param name="operationName">��������</param>
        private async Task HandleExceptionAsync(Exception ex, string errorTitle, string? operationName)
        {
            var operation = operationName ?? "δ֪����";
            var errorMessage = $"{operation}ʧ�ܣ�{ex.Message}";

            // ��¼��־
            LoggingService?.LogException(ex, $"{operation}ʱ�����쳣", GetType().Name);

            // ��ʾ����Ի���
            if (DialogService != null)
            {
                await DialogService.ShowErrorDialogAsync(errorTitle, errorMessage);
            }

            // ����������ڵ���ģʽ�£�
            System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] {operation}ʱ��������: {ex.Message}");
        }
        #endregion

        #region ȷ�϶Ի���������
        /// <summary>
        /// ��ʾȷ�϶Ի���ִ�в���
        /// </summary>
        /// <param name="operation">Ҫִ�еĲ���</param>
        /// <param name="confirmationTitle">ȷ�϶Ի������</param>
        /// <param name="confirmationMessage">ȷ����Ϣ</param>
        /// <param name="operationName">��������</param>
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

            await ExecuteWithExceptionHandlingAsync(operation, "����", operationName);
        }
        #endregion

        #region ���Ա��֪ͨ��ǿ
        /// <summary>
        /// ��������ֵ������������Եı��֪ͨ
        /// ֧���������Ե��Զ�֪ͨ
        /// </summary>
        /// <typeparam name="T">��������</typeparam>
        /// <param name="field">�����ֶ�</param>
        /// <param name="value">��ֵ</param>
        /// <param name="dependentProperties">����������������</param>
        /// <param name="propertyName">��������</param>
        /// <returns>�Ƿ����˸ı�</returns>
        protected bool SetPropertyWithDependents<T>(
            ref T field, 
            T value, 
            string[]? dependentProperties = null,
            [CallerMemberName] string? propertyName = null)
        {
            if (SetProperty(ref field, value, propertyName))
            {
                // ֪ͨ��������
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

        #region ��ȫ����Դ����
        /// <summary>
        /// ��ȫ�ͷ���Դ��ģ�巽��
        /// ���������дDisposeCore���ͷ��ض���Դ
        /// </summary>
        /// <param name="disposing">�Ƿ������ͷ��й���Դ</param>
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
                    // �ͷ���Դʱ�������󣬼�¼�����׳�
                    LoggingService?.LogException(ex, "�ͷ�ViewModel��Դʱ�����쳣", GetType().Name);
                    System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] �ͷ���Դʱ��������: {ex.Message}");
                }
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// ��������д�˷������ͷ��ض���Դ
        /// </summary>
        protected virtual void DisposeCore()
        {
            // ���಻��Ҫ�ͷ��ض���Դ
        }
        #endregion
    }
}