using System;
using DominoNext.Services.Interfaces;
using DominoNext.Services.Implementation;

namespace DominoNext.ViewModels.Base
{
    /// <summary>
    /// 设计时服务提供者 - 统一管理设计时服务的创建
    /// 消除重复的CreateDesignTimeXXXService方法，提供集中化的服务管理
    /// </summary>
    public static class DesignTimeServiceProvider
    {
        #region 缓存的服务实例
        private static ICoordinateService? _coordinateService;
        private static IEventCurveCalculationService? _eventCurveCalculationService;
        private static IMidiConversionService? _midiConversionService;
        private static ISettingsService? _settingsService;
        private static ILoggingService? _loggingService;
        private static IDialogService? _dialogService;
        private static IApplicationService? _applicationService;
        private static IProjectStorageService? _projectStorageService;
        #endregion

        #region 公共服务获取方法
        /// <summary>
        /// 获取设计时坐标服务
        /// </summary>
        public static ICoordinateService GetCoordinateService()
        {
            return _coordinateService ??= new CoordinateService();
        }

        /// <summary>
        /// 获取设计时事件曲线计算服务
        /// </summary>
        public static IEventCurveCalculationService GetEventCurveCalculationService()
        {
            return _eventCurveCalculationService ??= new EventCurveCalculationService();
        }

        /// <summary>
        /// 获取设计时MIDI转换服务
        /// </summary>
        public static IMidiConversionService GetMidiConversionService()
        {
            return _midiConversionService ??= new MidiConversionService();
        }

        /// <summary>
        /// 获取设计时设置服务
        /// </summary>
        public static ISettingsService GetSettingsService()
        {
            return _settingsService ??= new SettingsService();
        }

        /// <summary>
        /// 获取设计时日志服务
        /// </summary>
        public static ILoggingService GetLoggingService()
        {
            return _loggingService ??= new LoggingService(LogLevel.Debug);
        }

        /// <summary>
        /// 获取设计时对话框服务
        /// </summary>
        public static IDialogService GetDialogService()
        {
            if (_dialogService == null)
            {
                var loggingService = GetLoggingService();
                // 为设计时创建一个简化的对话框服务
                _dialogService = new DialogService(null, loggingService);
            }
            return _dialogService;
        }

        /// <summary>
        /// 获取设计时应用程序服务
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
        /// 获取设计时项目存储服务
        /// </summary>
        public static IProjectStorageService GetProjectStorageService()
        {
            return _projectStorageService ??= new ProjectStorageService();
        }
        #endregion

        #region 便捷方法
        /// <summary>
        /// 检查是否在设计时模式
        /// </summary>
        public static bool IsInDesignMode()
        {
            try
            {
                // 在设计时，这些API通常不可用或行为不同
                return Avalonia.Controls.Design.IsDesignMode;
            }
            catch
            {
                // 如果无法确定，假设不在设计时
                return false;
            }
        }

        /// <summary>
        /// 获取用于设计时的完整服务集合
        /// </summary>
        /// <returns>包含所有设计时服务的元组</returns>
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

        #region 清理方法
        /// <summary>
        /// 清理所有缓存的服务实例（主要用于测试）
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