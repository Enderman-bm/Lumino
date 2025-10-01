using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EnderDebugger;
using EnderWaveTableAccessingParty.Models;

namespace EnderWaveTableAccessingParty.Services
{
    /// <summary>
    /// 播表配置服务
    /// </summary>
    public interface IWaveTableConfigService
    {
        /// <summary>
        /// 获取播表设置
        /// </summary>
        Task<WaveTableSettings> GetSettingsAsync();

        /// <summary>
        /// 保存播表设置
        /// </summary>
        Task SaveSettingsAsync(WaveTableSettings settings);

        /// <summary>
        /// 重置为默认设置
        /// </summary>
        Task ResetToDefaultsAsync();

        /// <summary>
        /// 获取配置文件路径
        /// </summary>
        string GetConfigFilePath();

        /// <summary>
        /// 设置变更事件
        /// </summary>
        event EventHandler<WaveTableSettingsChangedEventArgs>? SettingsChanged;
    }

    /// <summary>
    /// 播表配置服务实现
    /// </summary>
    public class WaveTableConfigService : IWaveTableConfigService
    {
        private readonly string _configFilePath;
        private WaveTableSettings? _currentSettings;
        private readonly EnderLogger _logger;

        /// <summary>
        /// 设置变更事件
        /// </summary>
        public event EventHandler<WaveTableSettingsChangedEventArgs>? SettingsChanged;

        public WaveTableConfigService(string configDirectory)
        {
            _logger = EnderLogger.Instance;
            _configFilePath = Path.Combine(configDirectory, "wavetable_settings.json");
            
            _logger.Debug("WaveTableConfigService", $"播表配置服务初始化，配置文件路径: {_configFilePath}");
            
            // 确保目录存在
            var directory = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                _logger.Debug("WaveTableConfigService", $"创建配置目录: {directory}");
                Directory.CreateDirectory(directory);
            }
            
            _logger.Debug("WaveTableConfigService", "播表配置服务初始化完成");
        }

        /// <summary>
        /// 获取播表设置
        /// </summary>
        public async Task<WaveTableSettings> GetSettingsAsync()
        {
            if (_currentSettings != null)
            {
                _logger.Debug("WaveTableConfigService", "返回缓存的播表设置");
                return _currentSettings;
            }

            _logger.Debug("WaveTableConfigService", "开始获取播表设置");

            try
            {
                if (File.Exists(_configFilePath))
                {
                    _logger.Debug("WaveTableConfigService", $"配置文件存在，正在读取: {_configFilePath}");
                    var json = await File.ReadAllTextAsync(_configFilePath);
                    var settings = JsonSerializer.Deserialize<WaveTableSettings>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    _currentSettings = settings ?? CreateDefaultSettings();
                    _logger.Debug("WaveTableConfigService", "播表设置从配置文件加载成功");
                }
                else
                {
                    _logger.Debug("WaveTableConfigService", "配置文件不存在，创建默认设置");
                    _currentSettings = CreateDefaultSettings();
                    await SaveSettingsAsync(_currentSettings);
                    _logger.Debug("WaveTableConfigService", "默认设置已创建并保存");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableConfigService", $"加载播表设置失败: {ex.Message}");
                _logger.LogException(ex, "GetSettingsAsync");
                _currentSettings = CreateDefaultSettings();
            }

            // 订阅设置变更事件
            _currentSettings.SettingsChanged += OnSettingsChanged;
            _logger.Debug("WaveTableConfigService", "播表设置获取完成");

            return _currentSettings;
        }

        /// <summary>
        /// 保存播表设置
        /// </summary>
        public async Task SaveSettingsAsync(WaveTableSettings settings)
        {
            _logger.Debug("WaveTableConfigService", "开始保存播表设置");

            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                _logger.Debug("WaveTableConfigService", $"正在写入配置文件: {_configFilePath}");
                await File.WriteAllTextAsync(_configFilePath, json);
                _currentSettings = settings;
                _logger.Info("WaveTableConfigService", "播表设置保存成功");
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableConfigService", $"保存播表设置失败: {ex.Message}");
                _logger.LogException(ex, "SaveSettingsAsync");
                throw;
            }
        }

        /// <summary>
        /// 重置为默认设置
        /// </summary>
        public async Task ResetToDefaultsAsync()
        {
            _logger.Info("WaveTableConfigService", "开始重置为默认设置");
            var defaultSettings = CreateDefaultSettings();
            await SaveSettingsAsync(defaultSettings);
            _logger.Info("WaveTableConfigService", "已重置为默认设置");
        }

        /// <summary>
        /// 获取配置文件路径
        /// </summary>
        public string GetConfigFilePath()
        {
            return _configFilePath;
        }

        /// <summary>
        /// 创建默认设置
        /// </summary>
        private WaveTableSettings CreateDefaultSettings()
        {
            _logger.Debug("WaveTableConfigService", "创建默认播表设置");
            
            var defaultSettings = new WaveTableSettings
            {
                CurrentWaveTableId = "default",
                CurrentMidiDeviceId = -1,
                EnableAudioFeedback = true,
                DefaultNoteVelocity = 100,
                DefaultNoteDuration = 200,
                AutoPlayOnNotePlacement = true,
                EnableVelocitySensitivity = true,
                MasterVolume = 100,
                CustomSettings = new Dictionary<string, object>
                {
                    { "ReverbEnabled", true },
                    { "ChorusEnabled", false },
                    { "DefaultChannel", 0 }
                }
            };
            
            _logger.Debug("WaveTableConfigService", $"默认设置创建完成 - 播表ID: {defaultSettings.CurrentWaveTableId}, 设备ID: {defaultSettings.CurrentMidiDeviceId}");
            return defaultSettings;
        }

        /// <summary>
        /// 设置变更处理
        /// </summary>
        private void OnSettingsChanged(object? sender, WaveTableSettingsChangedEventArgs e)
        {
            _logger.Debug("WaveTableConfigService", $"播表设置已变更 - 变更类型: {e.PropertyName ?? "Unknown"}");
            SettingsChanged?.Invoke(this, e);
        }
    }
}