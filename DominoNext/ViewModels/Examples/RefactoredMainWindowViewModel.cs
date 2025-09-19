using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Base;
using Lumino.ViewModels.Editor;
using Lumino.Models.Music;

namespace Lumino.ViewModels.Examples
{
    /// <summary>
    /// �ع����������ViewModel - ʹ����ǿ�Ļ�������ظ�����
    /// ��ʾ���ʹ���µļܹ�����ߴ��븴���ԺͿ�ά����
    /// </summary>
    public partial class RefactoredMainWindowViewModel : EnhancedViewModelBase
    {
        #region ��������
        private readonly ISettingsService _settingsService;
        private readonly IApplicationService _applicationService;
        private readonly IProjectStorageService _projectStorageService;
        #endregion

        #region ����
        [ObservableProperty]
        private string _greeting = "��ӭʹ�� DominoNext��";

        [ObservableProperty]
        private ViewType _currentView = ViewType.PianoRoll;

        [ObservableProperty]
        private PianoRollViewModel? _pianoRoll;

        [ObservableProperty]
        private TrackSelectorViewModel? _trackSelector;
        #endregion

        #region ���캯��
        /// <summary>
        /// �����������캯�� - ͨ������ע���ȡ�������
        /// </summary>
        public RefactoredMainWindowViewModel(
            ISettingsService settingsService,
            IDialogService dialogService,
            IApplicationService applicationService,
            IProjectStorageService projectStorageService)
            : base(dialogService, null) // ���ݶԻ�����������
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
            _projectStorageService = projectStorageService ?? throw new ArgumentNullException(nameof(projectStorageService));

            InitializeGreetingMessage();
        }

        /// <summary>
        /// ���ʱ���캯�� - ʹ��ͳһ�����ʱ�����ṩ��
        /// �����ظ���CreateDesignTimeXXXService����
        /// </summary>
        public RefactoredMainWindowViewModel() : this(
            DesignTimeServiceProvider.GetSettingsService(),
            DesignTimeServiceProvider.GetDialogService(),
            DesignTimeServiceProvider.GetApplicationService(),
            DesignTimeServiceProvider.GetProjectStorageService())
        {
            // ���ʱ�ض��ĳ�ʼ��
            InitializeDesignTimeData();
        }
        #endregion

        #region ����ʵ�� - ʹ�û�����쳣����ģʽ

        /// <summary>
        /// �½��ļ����� - չʾʹ�û����쳣�����ļ�д��
        /// </summary>
        [RelayCommand]
        private async Task NewFileAsync()
        {
            await ExecuteWithConfirmationAsync(
                operation: async () =>
                {
                    PianoRoll?.ClearContent();
                    TrackSelector?.ClearTracks();
                    TrackSelector?.AddTrack();
                },
                confirmationTitle: "ȷ��",
                confirmationMessage: "��ǰ��Ŀ��δ����ĸ��ģ��Ƿ�����������ļ���",
                operationName: "�½��ļ�"
            );
        }

        /// <summary>
        /// ���ļ����� - ʹ�û�����쳣����
        /// </summary>
        [RelayCommand]
        private async Task OpenFileAsync()
        {
            await ExecuteWithExceptionHandlingAsync(
                operation: async () =>
                {
                    if (!await _applicationService.CanShutdownSafelyAsync())
                    {
                        var shouldProceed = await DialogService!.ShowConfirmationDialogAsync(
                            "ȷ��", "��ǰ��Ŀ��δ����ĸ��ģ��Ƿ���������ļ���");
                        
                        if (!shouldProceed) return;
                    }

                    var filePath = await DialogService!.ShowOpenFileDialogAsync(
                        "��MIDI�ļ�", 
                        new[] { "*.mid", "*.midi", "*.dmn" });

                    if (!string.IsNullOrEmpty(filePath))
                    {
                        await ProcessFileAsync(filePath);
                    }
                },
                errorTitle: "���ļ�����",
                operationName: "���ļ�"
            );
        }

        /// <summary>
        /// �����ļ����� - ʹ�û�����쳣�����ͷ���ֵ
        /// </summary>
        [RelayCommand]
        private async Task SaveFileAsync()
        {
            var success = await ExecuteWithExceptionHandlingAsync(
                operation: async () =>
                {
                    if (PianoRoll == null) return false;
                    
                    var allNotes = PianoRoll.GetAllNotes();
                    var filePath = await DialogService!.ShowSaveFileDialogAsync(
                        "����MIDI�ļ�", null, new[] { "*.mid" });

                    if (string.IsNullOrEmpty(filePath)) return false;

                    return await _projectStorageService.ExportMidiAsync(
                        filePath, 
                        allNotes.Select(vm => vm.ToNoteModel()));
                },
                defaultValue: false,
                errorTitle: "�����ļ�����",
                operationName: "�����ļ�"
            );

            if (success && DialogService != null)
            {
                await DialogService.ShowInfoDialogAsync("�ɹ�", "�ļ�����ɹ���");
            }
        }

        /// <summary>
        /// ����MIDI�ļ����� - չʾ���Ӳ����ļ򻯴���
        /// </summary>
        [RelayCommand]
        private async Task ImportMidiFileAsync()
        {
            await ExecuteWithExceptionHandlingAsync(
                operation: async () =>
                {
                    var filePath = await DialogService!.ShowOpenFileDialogAsync(
                        "ѡ��MIDI�ļ�", new[] { "*.mid", "*.midi" });

                    if (!string.IsNullOrEmpty(filePath))
                    {
                        await ProcessMidiImportAsync(filePath);
                    }
                },
                errorTitle: "����MIDI�ļ�����",
                operationName: "����MIDI�ļ�",
                showSuccessMessage: true,
                successMessage: "MIDI�ļ�������ɣ�"
            );
        }

        /// <summary>
        /// �����öԻ�������
        /// </summary>
        [RelayCommand]
        private async Task OpenSettingsAsync()
        {
            var result = await ExecuteWithExceptionHandlingAsync(
                operation: async () => await DialogService!.ShowSettingsDialogAsync(),
                defaultValue: false,
                errorTitle: "���ô���",
                operationName: "������"
            );

            if (result)
            {
                await RefreshUIAfterSettingsChangeAsync();
            }
        }

        /// <summary>
        /// �˳�Ӧ�ó�������
        /// </summary>
        [RelayCommand]
        private async Task ExitApplicationAsync()
        {
            await ExecuteWithConfirmationAsync(
                operation: async () =>
                {
                    if (await _applicationService.CanShutdownSafelyAsync())
                    {
                        _applicationService.Shutdown();
                    }
                    else
                    {
                        _applicationService.Shutdown(); // ǿ���˳�
                    }
                },
                confirmationTitle: "ȷ���˳�",
                confirmationMessage: "��δ����ĸ��ģ��Ƿ�ȷ���˳���",
                operationName: "�˳�Ӧ�ó���"
            );
        }
        #endregion

        #region ˽�и�������
        /// <summary>
        /// ��ʼ����ӭ��Ϣ
        /// </summary>
        private void InitializeGreetingMessage()
        {
            try
            {
                var appInfo = _applicationService.GetApplicationInfo();
                Greeting = $"��ӭʹ�� {appInfo.Name} v{appInfo.Version}��";
            }
            catch (Exception ex)
            {
                LoggingService?.LogException(ex, "��ʼ����ӭ��Ϣʧ��", GetType().Name);
                Greeting = "��ӭʹ�� DominoNext��";
            }
        }

        /// <summary>
        /// ��ʼ�����ʱ����
        /// </summary>
        private void InitializeDesignTimeData()
        {
            if (DesignTimeServiceProvider.IsInDesignMode())
            {
                PianoRoll = new PianoRollViewModel();
                TrackSelector = new TrackSelectorViewModel();
            }
        }

        /// <summary>
        /// �����ļ���������չ���ж����ͣ�
        /// </summary>
        private async Task ProcessFileAsync(string filePath)
        {
            var extension = System.IO.Path.GetExtension(filePath).ToLower();
            
            switch (extension)
            {
                case ".mid":
                case ".midi":
                    await ProcessMidiImportAsync(filePath);
                    break;
                case ".dmn":
                    await DialogService!.ShowInfoDialogAsync("��Ϣ", "DominoNext��Ŀ�ļ����ع��ܽ��ں����汾��ʵ��");
                    break;
                default:
                    await DialogService!.ShowErrorDialogAsync("����", "��֧�ֵ��ļ���ʽ");
                    break;
            }
        }

        /// <summary>
        /// ����MIDI�ļ�����
        /// </summary>
        private async Task ProcessMidiImportAsync(string filePath)
        {
            // ʹ�öԻ������Ľ�����ʾ����
            await DialogService!.RunWithProgressAsync("����MIDI�ļ�", async (progress, cancellationToken) =>
            {
                var notes = await _projectStorageService.ImportMidiWithProgressAsync(filePath, progress, cancellationToken);
                
                // ����UI����UI�߳���ִ�У�
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateUIAfterMidiImport(notes, filePath);
                });
            });
        }

        /// <summary>
        /// MIDI��������UI
        /// </summary>
        private void UpdateUIAfterMidiImport(IEnumerable<Note> notes, string filePath)
        {
            if (PianoRoll == null || TrackSelector == null) return;
            
            PianoRoll.ClearContent();
            
            // ����������Ϣ��...
            // ������Լ���ʵ�־����UI�����߼�
        }

        /// <summary>
        /// ���ø��ĺ�ˢ��UI
        /// </summary>
        private async Task RefreshUIAfterSettingsChangeAsync()
        {
            InitializeGreetingMessage();
            // ����UIˢ���߼�...
            await Task.CompletedTask;
        }
        #endregion

        #region ��Դ����
        /// <summary>
        /// �ͷ��ض���Դ
        /// </summary>
        protected override void DisposeCore()
        {
            // �����ض���MainWindow����Դ
            PianoRoll?.Dispose();
            TrackSelector = null;
        }
        #endregion
    }
}