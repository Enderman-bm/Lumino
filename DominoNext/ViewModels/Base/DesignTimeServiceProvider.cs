using System;
using Lumino.Services.Interfaces;
using Lumino.Services.Implementation;

namespace Lumino.ViewModels.Base
{
    /// <summary>
    /// ���ʱ�����ṩ�� - ͳһ�������ʱ����Ĵ���
    /// �����ظ���CreateDesignTimeXXXService�������ṩ���л��ķ������
    /// </summary>
    public static class DesignTimeServiceProvider
    {
        #region ����ķ���ʵ��
        private static ICoordinateService? _coordinateService;
        private static IEventCurveCalculationService? _eventCurveCalculationService;
        private static IMidiConversionService? _midiConversionService;
        private static ISettingsService? _settingsService;
        private static ILoggingService? _loggingService;
        private static IDialogService? _dialogService;
        private static IApplicationService? _applicationService;
        private static IProjectStorageService? _projectStorageService;
        #endregion

        #region ���������ȡ����
        /// <summary>
        /// ��ȡ���ʱ�������
        /// </summary>
        public static ICoordinateService GetCoordinateService()
        {
            return _coordinateService ??= new CoordinateService();
        }

        /// <summary>
        /// ��ȡ���ʱ�¼����߼������
        /// </summary>
        public static IEventCurveCalculationService GetEventCurveCalculationService()
        {
            return _eventCurveCalculationService ??= new EventCurveCalculationService();
        }

        /// <summary>
        /// ��ȡ���ʱMIDIת������
        /// </summary>
        public static IMidiConversionService GetMidiConversionService()
        {
            return _midiConversionService ??= new MidiConversionService();
        }

        /// <summary>
        /// ��ȡ���ʱ���÷���
        /// </summary>
        public static ISettingsService GetSettingsService()
        {
            return _settingsService ??= new SettingsService();
        }

        /// <summary>
        /// ��ȡ���ʱ��־����
        /// </summary>
        public static ILoggingService GetLoggingService()
        {
            return _loggingService ??= new LoggingService(LogLevel.Debug);
        }

        /// <summary>
        /// ��ȡ���ʱ�Ի������
        /// </summary>
        public static IDialogService GetDialogService()
        {
            if (_dialogService == null)
            {
                var loggingService = GetLoggingService();
                // Ϊ���ʱ����һ���򻯵ĶԻ������
                _dialogService = new DialogService(null, loggingService);
            }
            return _dialogService;
        }

        /// <summary>
        /// ��ȡ���ʱӦ�ó������
        /// </summary>
        public static IApplicationService GetApplicationService()
        {
            if (_applicationService == null)
            {
                var settingsService = GetSettingsService();
                _applicationService = new ApplicationService(settingsService);
            }
            return _applicationService;
        }

        /// <summary>
        /// ��ȡ���ʱ��Ŀ�洢����
        /// </summary>
        public static IProjectStorageService GetProjectStorageService()
        {
            return _projectStorageService ??= new ProjectStorageService();
        }
        #endregion

        #region ��ݷ���
        /// <summary>
        /// ����Ƿ������ʱģʽ
        /// </summary>
        public static bool IsInDesignMode()
        {
            try
            {
                // �����ʱ����ЩAPIͨ�������û���Ϊ��ͬ
                return Avalonia.Controls.Design.IsDesignMode;
            }
            catch
            {
                // ����޷�ȷ�������費�����ʱ
                return false;
            }
        }

        /// <summary>
        /// ��ȡ�������ʱ���������񼯺�
        /// </summary>
        /// <returns>�����������ʱ�����Ԫ��</returns>
        public static (
            ICoordinateService coordinateService,
            IEventCurveCalculationService eventCurveCalculationService,
            IMidiConversionService midiConversionService,
            ISettingsService settingsService,
            ILoggingService loggingService,
            IDialogService dialogService,
            IApplicationService applicationService,
            IProjectStorageService projectStorageService
        ) GetAllDesignTimeServices()
        {
            return (
                GetCoordinateService(),
                GetEventCurveCalculationService(),
                GetMidiConversionService(),
                GetSettingsService(),
                GetLoggingService(),
                GetDialogService(),
                GetApplicationService(),
                GetProjectStorageService()
            );
        }
        #endregion

        #region ��������
        /// <summary>
        /// �������л���ķ���ʵ������Ҫ���ڲ��ԣ�
        /// </summary>
        public static void ClearCache()
        {
            _coordinateService = null;
            _eventCurveCalculationService = null;
            _midiConversionService = null;
            _settingsService = null;
            _loggingService = null;
            _dialogService = null;
            _applicationService = null;
            _projectStorageService = null;
        }
        #endregion
    }
}