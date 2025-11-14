using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnderDebugger;
using EnderWaveTableAccessingParty.Services;
using Lumino.Services.Interfaces;
using LuminoWaveTable.Adapters;
using LuminoWaveTable.Interfaces;
using LuminoWaveTable.Services;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// 播表服务工厂 - 负责创建和管理播表服务实例
    /// </summary>
    public class WaveTableServiceFactory
    {
        private readonly ILoggingService _logger;
        private readonly Dictionary<string, IMidiPlaybackService> _services;
        private IMidiPlaybackService? _currentService;

        public WaveTableServiceFactory(ILoggingService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _services = new Dictionary<string, IMidiPlaybackService>();
            _currentService = null;
        }

        /// <summary>
        /// 获取所有可用的播表引擎
        /// </summary>
        public async Task<List<WaveTableEngineInfo>> GetAvailableEnginesAsync()
        {
            var engines = new List<WaveTableEngineInfo>();

            try
            {
                // 1. KDMAPI (默认选项)
                engines.Add(new WaveTableEngineInfo
                {
                    Id = "kdmapi",
                    Name = "KDMAPI",
                    Description = "现有的KDMAPI播表引擎",
                    IsAvailable = true,
                    IsRecommended = true,
                    Priority = 0
                });

                // 2. LuminoWaveTable
                var luminoAvailable = await IsLuminoWaveTableAvailableAsync();
                engines.Add(new WaveTableEngineInfo
                {
                    Id = "lumino",
                    Name = "Lumino播表",
                    Description = "高性能MIDI播表引擎，支持winmm接口",
                    IsAvailable = luminoAvailable,
                    IsRecommended = false,
                    Priority = 1
                });

                // 3. 自动检测winmm播表
                var winmmEngines = await DetectWinmmEnginesAsync();
                engines.AddRange(winmmEngines);

                _logger.LogInfo("WaveTableServiceFactory", $"发现 {engines.Count} 个播表引擎");
            }
            catch (Exception ex)
            {
                _logger.LogError($"WaveTableServiceFactory", $"检测播表引擎失败: {ex.Message}");
                
                // 确保至少有一个选项
                if (engines.Count == 0)
                {
                    engines.Add(new WaveTableEngineInfo
                    {
                        Id = "kdmapi",
                        Name = "KDMAPI",
                        Description = "默认播表引擎",
                        IsAvailable = true,
                        IsRecommended = true,
                        Priority = 0
                    });
                }
            }

            return engines.OrderBy(e => e.Priority).ToList();
        }

        /// <summary>
        /// 创建播表服务实例
        /// </summary>
        public IMidiPlaybackService CreateService(string engineId)
        {
            if (_services.TryGetValue(engineId, out var existingService))
            {
                return existingService;
            }

            IMidiPlaybackService service;
            
            switch (engineId.ToLower())
            {
                case "kdmapi":
                    // 创建KDMAPI服务（使用现有的MidiPlaybackService）
                    service = CreateKdmapiService();
                    break;
                    
                case "lumino":
                    // 创建LuminoWaveTable服务
                    service = CreateLuminoService();
                    break;
                    
                default:
                    // 尝试创建winmm播表服务
                    service = CreateWinmmService(engineId);
                    break;
            }

            _services[engineId] = service;
            return service;
        }

        /// <summary>
        /// 获取当前播表服务
        /// </summary>
        public IMidiPlaybackService? GetCurrentService()
        {
            return _currentService;
        }

        /// <summary>
        /// 设置当前播表服务
        /// </summary>
        public async Task SetCurrentServiceAsync(string engineId)
        {
            var newService = CreateService(engineId);
            
            // 清理旧服务
            if (_currentService != null)
            {
                try
                {
                    await _currentService.CleanupAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"WaveTableServiceFactory", $"清理旧播表服务失败: {ex.Message}");
                }
            }

            // 初始化新服务
            try
            {
                await newService.InitializeAsync();
                _currentService = newService;
                _logger.LogInfo("WaveTableServiceFactory", $"播表服务已切换到: {engineId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"WaveTableServiceFactory", $"初始化播表服务失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 创建KDMAPI服务
        /// </summary>
        private IMidiPlaybackService CreateKdmapiService()
        {
            // 使用现有的MidiPlaybackService实现
            var enderLogger = new EnderLogger("KDMAPI");
            return new MidiPlaybackService(enderLogger);
        }

        /// <summary>
        /// 创建LuminoWaveTable服务
        /// </summary>
        private IMidiPlaybackService CreateLuminoService()
        {
            var enderLogger = new EnderLogger("LuminoWaveTable");
            return new LuminoWaveTableAdapter(enderLogger);
        }

        /// <summary>
        /// 创建WinMM播表服务
        /// </summary>
        private IMidiPlaybackService CreateWinmmService(string engineId)
        {
            // 这里可以实现特定的winmm播表服务
            // 暂时返回LuminoWaveTable作为默认实现
            var enderLogger = new EnderLogger($"WinMM_{engineId}");
            return new LuminoWaveTableAdapter(enderLogger);
        }

        /// <summary>
        /// 检测LuminoWaveTable是否可用
        /// </summary>
        private async Task<bool> IsLuminoWaveTableAvailableAsync()
        {
            try
            {
                // 临时创建一个LuminoWaveTable服务来测试
                using var tempService = new LuminoWaveTableService();
                await tempService.InitializeAsync();
                
                // 检查是否能获取到MIDI设备
                var devices = await tempService.GetMidiDevicesAsync();
                var available = devices.Count > 0;
                
                await tempService.CleanupAsync();
                return available;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("WaveTableServiceFactory", $"LuminoWaveTable检测失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检测WinMM播表引擎
        /// </summary>
        private async Task<List<WaveTableEngineInfo>> DetectWinmmEnginesAsync()
        {
            var engines = new List<WaveTableEngineInfo>();

            try
            {
                // 使用winmm接口检测系统中的播表
                using var tempService = new LuminoWaveTableService();
                await tempService.InitializeAsync();
                
                var devices = await tempService.GetMidiDevicesAsync();
                
                foreach (var device in devices)
                {
                    if (device.Technology == 1) // MOD_MIDIPORT
                    {
                        engines.Add(new WaveTableEngineInfo
                        {
                            Id = $"winmm_{device.DeviceId}",
                            Name = $"WinMM - {device.Name}",
                            Description = $"Windows MIDI播表 - {device.Name}",
                            IsAvailable = true,
                            IsRecommended = false,
                            Priority = 2
                        });
                    }
                }
                
                await tempService.CleanupAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug("WaveTableServiceFactory", $"WinMM播表检测失败: {ex.Message}");
            }

            return engines;
        }

        /// <summary>
        /// 清理所有服务
        /// </summary>
        public async Task CleanupAllAsync()
        {
            foreach (var service in _services.Values)
            {
                try
                {
                    await service.CleanupAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"WaveTableServiceFactory", $"清理播表服务失败: {ex.Message}");
                }
            }
            
            _services.Clear();
            _currentService = null;
        }
    }

    /// <summary>
    /// 播表引擎信息
    /// </summary>
    public class WaveTableEngineInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public bool IsRecommended { get; set; }
        public int Priority { get; set; }
    }
}