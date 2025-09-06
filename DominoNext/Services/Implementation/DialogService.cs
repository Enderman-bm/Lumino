using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using DominoNext.Constants;
using DominoNext.Services.Interfaces;
using DominoNext.Views.Dialogs;
using DominoNext.Views.Settings;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// �Ի������ʵ�� - ����MVVMԭ��Ϳ����淶�ĶԻ��������װ
    /// ����ͳһ�������ֶԻ������ʾ��ȷ������������־��¼��һ����
    /// </summary>
    public class DialogService : IDialogService
    {
        #region ��������
        
        private readonly IViewModelFactory _viewModelFactory;
        private readonly ILoggingService _loggingService;
        
        #endregion

        #region ���캯��
        
        /// <summary>
        /// ��ʼ���Ի������
        /// </summary>
        /// <param name="viewModelFactory">ViewModel�����������ڴ����Ի����ViewModel</param>
        /// <param name="loggingService">��־�������ڼ�¼����͵�����Ϣ</param>
        public DialogService(IViewModelFactory viewModelFactory, ILoggingService loggingService)
        {
            _viewModelFactory = viewModelFactory ?? throw new ArgumentNullException(nameof(viewModelFactory));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }
        
        #endregion

        #region ��������ʵ��

        public async Task<bool> ShowSettingsDialogAsync()
        {
            try
            {
                _loggingService.LogInfo("��ʼ��ʾ���öԻ���", "DialogService");
                
                // ͨ����������ViewModel�����ֵ�һְ��
                var settingsViewModel = _viewModelFactory.CreateSettingsWindowViewModel();
                var settingsWindow = new SettingsWindow
                {
                    DataContext = settingsViewModel
                };

                // ��ȫ����ʾ�Ի���
                var result = await ShowDialogWithParentAsync(settingsWindow);
                
                // ��������Ƿ��б��
                var hasChanges = settingsViewModel.HasUnsavedChanges == false;
                
                _loggingService.LogInfo($"���öԻ���رգ��б����{hasChanges}", "DialogService");
                return hasChanges;
            }
            catch (Exception ex)
            {
                // ͳһ���쳣���� - ��¼��ϸ������Ϣ�����ذ�ȫ��Ĭ��ֵ
                _loggingService.LogException(ex, DialogConstants.SETTINGS_DIALOG_ERROR, "DialogService");
                return false;
            }
        }

        public async Task<string?> ShowOpenFileDialogAsync(string title, string[]? filters = null)
        {
            try
            {
                _loggingService.LogInfo($"��ʾ�ļ��򿪶Ի���{title}", "DialogService");
                
                var window = GetMainWindow();
                if (window?.StorageProvider == null)
                {
                    _loggingService.LogWarning("�޷���ȡ�����ڻ�洢�ṩ����", "DialogService");
                    return null;
                }

                var options = CreateFilePickerOpenOptions(title, filters);
                var result = await window.StorageProvider.OpenFilePickerAsync(options);
                var selectedPath = result?.FirstOrDefault()?.Path.LocalPath;
                
                _loggingService.LogInfo($"�ļ�ѡ������{selectedPath ?? "��ѡ��"}", "DialogService");
                return selectedPath;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, DialogConstants.FILE_DIALOG_ERROR, "DialogService");
                return null;
            }
        }

        public async Task<string?> ShowSaveFileDialogAsync(string title, string? defaultFileName = null, string[]? filters = null)
        {
            try
            {
                _loggingService.LogInfo($"��ʾ�ļ�����Ի���{title}", "DialogService");
                
                var window = GetMainWindow();
                if (window?.StorageProvider == null)
                {
                    _loggingService.LogWarning("�޷���ȡ�����ڻ�洢�ṩ����", "DialogService");
                    return null;
                }

                var options = CreateFilePickerSaveOptions(title, defaultFileName, filters);
                var result = await window.StorageProvider.SaveFilePickerAsync(options);
                var selectedPath = result?.Path.LocalPath;
                
                _loggingService.LogInfo($"�����ļ�·����{selectedPath ?? "��ѡ��"}", "DialogService");
                return selectedPath;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, DialogConstants.FILE_DIALOG_ERROR, "DialogService");
                return null;
            }
        }

        public async Task<bool> ShowConfirmationDialogAsync(string title, string message)
        {
            try
            {
                _loggingService.LogInfo($"��ʾȷ�϶Ի���{title} - {message}", "DialogService");
                
                var confirmationDialog = new ConfirmationDialog
                {
                    Title = title,
                    Message = message
                };

                var result = await ShowDialogWithParentAsync(confirmationDialog);
                
                // ���result��bool���ͣ�ֱ�ӷ��أ�����ʹ�öԻ����Result����
                var confirmationResult = result is bool boolResult ? boolResult : confirmationDialog.Result;
                
                _loggingService.LogInfo($"ȷ�϶Ի�������{confirmationResult}", "DialogService");
                return confirmationResult;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, DialogConstants.CONFIRMATION_DIALOG_ERROR, "DialogService");
                
                // ��������ʱ���ذ�ȫ��Ĭ��ֵ - ����������ƻ��Բ���
                return DialogConstants.DEFAULT_CONFIRMATION_RESULT;
            }
        }

        public async Task ShowErrorDialogAsync(string title, string message)
        {
            try
            {
                _loggingService.LogError($"����Ի��� - {title}: {message}", "DialogService");
                
                // TODO: ʵ���Զ������Ի���UI
                // Ŀǰʹ����־��¼���������Դ���ר�ŵĴ���Ի���View
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, DialogConstants.ERROR_DIALOG_ERROR, "DialogService");
            }
        }

        public async Task ShowInfoDialogAsync(string title, string message)
        {
            try
            {
                _loggingService.LogInfo($"��Ϣ�Ի��� - {title}: {message}", "DialogService");
                
                // TODO: ʵ���Զ�����Ϣ�Ի���UI
                // Ŀǰʹ����־��¼���������Դ���ר�ŵ���Ϣ�Ի���View
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, DialogConstants.INFO_DIALOG_ERROR, "DialogService");
            }
        }

        public async Task ShowLoadingDialogAsync(string message)
        {
            try
            {
                _loggingService.LogInfo($"显示加载中对话框: {message}", "DialogService");
                
                // TODO: 实现自定义加载中对话框UI
                // 当前使用日志记录，未来需要开发专门的加载中对话框View
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, DialogConstants.LOADING_DIALOG_ERROR, "DialogService");
            }
        }

        public async Task CloseLoadingDialogAsync()
        {
            try
            {
                _loggingService.LogInfo($"关闭加载中对话框", "DialogService");
                
                // TODO: 实现关闭加载中对话框UI
                // 当前使用日志记录，未来需要开发专门的加载中对话框View
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, DialogConstants.LOADING_DIALOG_ERROR, "DialogService");
            }
        }

        #endregion

        #region ˽�и�������

        /// <summary>
        /// ��ȡ������
        /// �ṩͳһ�������ڻ�ȡ�߼�����������ظ�
        /// </summary>
        /// <returns>������ʵ��������޷���ȡ�򷵻�null</returns>
        private Window? GetMainWindow()
        {
            try
            {
                if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    return desktop.MainWindow;
                }
                return null;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "��ȡ������ʱ��������", "DialogService");
                return null;
            }
        }

        /// <summary>
        /// ��������Ϊ��������ʾ�Ի���
        /// ͳһ�ĶԻ�����ʾ�߼������������ڹ������쳣���
        /// </summary>
        /// <param name="dialog">Ҫ��ʾ�ĶԻ���</param>
        /// <returns>�Ի���ķ��ؽ��</returns>
        private async Task<object?> ShowDialogWithParentAsync(Window dialog)
        {
            try
            {
                var parentWindow = GetMainWindow();
                if (parentWindow != null)
                {
                    // ��ģ̬��ʽ��ʾ�Ի���
                    await dialog.ShowDialog(parentWindow);
                    
                    // ����ȷ�϶Ի��򣬷�����Result����
                    if (dialog is ConfirmationDialog confirmDialog)
                    {
                        return confirmDialog.Result;
                    }
                    
                    // ���������Ի��򣬷���DataContext
                    return dialog.DataContext;
                }
                else
                {
                    // ���û�������ڣ���Ϊ����������ʾ
                    _loggingService.LogWarning("û�������ڣ��Ի�����Ϊ����������ʾ", "DialogService");
                    dialog.Show();
                    return null;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "��ʾ�Ի���ʱ��������", "DialogService");
                return null;
            }
        }

        /// <summary>
        /// �����ļ���ѡ��
        /// ��װ�ļ��򿪶Ի���������߼�����ߴ��븴����
        /// </summary>
        /// <param name="title">�Ի������</param>
        /// <param name="filters">�ļ�������</param>
        /// <returns>���úõ��ļ�ѡ��ѡ��</returns>
        private FilePickerOpenOptions CreateFilePickerOpenOptions(string title, string[]? filters)
        {
            var options = new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            };

            // ʹ�ó����ж����Ĭ�Ϲ�����������Ӳ����
            var actualFilters = filters ?? DialogConstants.AllSupportedFilters;
            
            if (actualFilters.Length > 0)
            {
                options.FileTypeFilter = actualFilters.Select(filter => new FilePickerFileType(filter)
                {
                    Patterns = new[] { filter }
                }).ToArray();
            }

            return options;
        }

        /// <summary>
        /// �����ļ�����ѡ��
        /// ��װ�ļ�����Ի���������߼�����ߴ��븴����
        /// </summary>
        /// <param name="title">�Ի������</param>
        /// <param name="defaultFileName">Ĭ���ļ���</param>
        /// <param name="filters">�ļ�������</param>
        /// <returns>���úõ��ļ�����ѡ��</returns>
        private FilePickerSaveOptions CreateFilePickerSaveOptions(string title, string? defaultFileName, string[]? filters)
        {
            var options = new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = defaultFileName
            };

            // ʹ�ó����ж����Ĭ�Ϲ�����������Ӳ����
            var actualFilters = filters ?? DialogConstants.AllSupportedFilters;
            
            if (actualFilters.Length > 0)
            {
                options.FileTypeChoices = actualFilters.Select(filter => new FilePickerFileType(filter)
                {
                    Patterns = new[] { filter }
                }).ToArray();
            }

            return options;
        }

        #endregion
    }
}