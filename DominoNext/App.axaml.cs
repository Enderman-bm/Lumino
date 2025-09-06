using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using DominoNext.Services.Implementation;
using DominoNext.Services.Interfaces;
using DominoNext.ViewModels;
using DominoNext.Views;

namespace DominoNext
{
    public partial class App : Application
    {
        // �������� - �򵥵�����ע��ʵ��
        private ISettingsService? _settingsService;
        private IDialogService? _dialogService;
        private IApplicationService? _applicationService;
        private ICoordinateService? _coordinateService;
        private IViewModelFactory? _viewModelFactory;
        private IProjectStorageService? _projectStorageService;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            System.Diagnostics.Debug.WriteLine("App.Initialize() ���");
        }

        public override async void OnFrameworkInitializationCompleted()
        {
            System.Diagnostics.Debug.WriteLine("OnFrameworkInitializationCompleted ��ʼ");
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                System.Diagnostics.Debug.WriteLine("��⵽����Ӧ�ó�����������");
                
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();
                System.Diagnostics.Debug.WriteLine("������֤����ѽ���");

                try
                {
                    // ��ʼ����������
                    await InitializeServicesAsync();
                    System.Diagnostics.Debug.WriteLine("����������ʼ�����");

                    // �ȴ���ԴԤ�������
                    System.Diagnostics.Debug.WriteLine("��ʼ�ȴ���ԴԤ����...");
                    await ResourcePreloadService.Instance.PreloadResourcesAsync();
                    System.Diagnostics.Debug.WriteLine("��ԴԤ�������");

                    // ���ݵȴ�ȷ����Դϵͳ�ȶ�
                    await Task.Delay(100);

                    // ������ViewModel - ʹ������ע��
                    var viewModel = CreateMainWindowViewModel();
                    System.Diagnostics.Debug.WriteLine("MainWindowViewModel �Ѵ���");

                    var mainWindow = new MainWindow
                    {
                        DataContext = viewModel,
                    };
                    System.Diagnostics.Debug.WriteLine("MainWindow �Ѵ���");

                    desktop.MainWindow = mainWindow;
                    System.Diagnostics.Debug.WriteLine("MainWindow ����ΪӦ�ó���������");

                    // ��ʽ��ʾ����
                    mainWindow.Show();
                    System.Diagnostics.Debug.WriteLine("MainWindow.Show() �ѵ���");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ӧ�ó����ʼ��ʱ��������: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"��ջ����: {ex.StackTrace}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("δ��⵽����Ӧ�ó�����������");
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// ��ʼ���������� - �򵥵�����ע��ʵ��
        /// </summary>
        private async Task InitializeServicesAsync()
        {
            try
            {
                // ������˳���ʼ������

                // 1. �������� - ������
                _settingsService = new SettingsService();
                _coordinateService = new CoordinateService();
                
                // 2. ��־���� - ��������
                var loggingService = new LoggingService(LogLevel.Debug);

                // 3. MIDIת������ - ��������
                var midiConversionService = new MidiConversionService();

                // 4. ������������ķ���
                _applicationService = new ApplicationService(_settingsService);
                _viewModelFactory = new ViewModelFactory(_coordinateService, _settingsService, midiConversionService);
                
                // 5. �����������ĸ��Ϸ���
                _dialogService = new DialogService(_viewModelFactory, loggingService);
                
                // 6. �����洢����
                _projectStorageService = new ProjectStorageService();

                // 7. ��������
                await _settingsService.LoadSettingsAsync();
                System.Diagnostics.Debug.WriteLine("�����Ѽ���");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��ʼ����������ʱ��������: {ex.Message}");
                throw; // �����׳��쳣���õ����ߴ���
            }
        }

        /// <summary>
        /// ����������ViewModel - ʹ������ע��
        /// </summary>
        private MainWindowViewModel CreateMainWindowViewModel()
        {
            if (_settingsService == null || _dialogService == null || 
                _applicationService == null || _viewModelFactory == null ||
                _projectStorageService == null)
            {
                throw new InvalidOperationException("��������δ��ȷ��ʼ��");
            }

            return new MainWindowViewModel(
                _settingsService,
                _dialogService,
                _applicationService,
                _viewModelFactory,
                _projectStorageService);
        }

        private static void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}