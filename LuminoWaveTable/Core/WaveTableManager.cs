using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LuminoWaveTable.Interfaces;
using LuminoWaveTable.Models;
using EnderDebugger;

namespace LuminoWaveTable.Core
{
    /// <summary>
    /// 播表管理器
    /// </summary>
    public class WaveTableManager
    {
        private readonly EnderLogger _logger;
        private readonly Dictionary<string, LuminoWaveTableInfo> _waveTables;
        private readonly string _waveTablesDirectory;
        private LuminoWaveTableInfo? _currentWaveTable;

        public WaveTableManager(string waveTablesDirectory)
        {
            _logger = EnderLogger.Instance;
            _waveTables = new Dictionary<string, LuminoWaveTableInfo>();
            _waveTablesDirectory = waveTablesDirectory;
            
            // 确保目录存在
            if (!Directory.Exists(_waveTablesDirectory))
            {
                Directory.CreateDirectory(_waveTablesDirectory);
            }
            
            InitializeBuiltinWaveTables();
            _logger.Info("WaveTableManager", $"播表管理器初始化完成，目录: {_waveTablesDirectory}");
        }

        /// <summary>
        /// 初始化内置播表
        /// </summary>
        private void InitializeBuiltinWaveTables()
        {
            // GM完整音色集播表
            var gmWaveTable = new LuminoWaveTableInfo
            {
                Id = "lumino_gm_complete",
                Name = "Lumino GM完整音色集",
                Description = "完整的General MIDI音色集，包含128种标准乐器",
                IsSystem = true,
                IsBuiltin = true,
                Provider = "LuminoWaveTable",
                Priority = 100,
                InstrumentMappings = CreateGmInstrumentMappings()
            };
            _waveTables[gmWaveTable.Id] = gmWaveTable;

            // 电子音乐播表
            var electronicWaveTable = new LuminoWaveTableInfo
            {
                Id = "lumino_electronic",
                Name = "Lumino 电子音乐",
                Description = "现代电子音乐音色集合",
                IsSystem = true,
                IsBuiltin = true,
                Provider = "LuminoWaveTable",
                Priority = 90,
                InstrumentMappings = CreateElectronicInstrumentMappings()
            };
            _waveTables[electronicWaveTable.Id] = electronicWaveTable;

            // 管弦乐播表
            var orchestralWaveTable = new LuminoWaveTableInfo
            {
                Id = "lumino_orchestral",
                Name = "Lumino 管弦乐",
                Description = "经典管弦乐音色集合",
                IsSystem = true,
                IsBuiltin = true,
                Provider = "LuminoWaveTable",
                Priority = 80,
                InstrumentMappings = CreateOrchestralInstrumentMappings()
            };
            _waveTables[orchestralWaveTable.Id] = orchestralWaveTable;

            // 民族乐器播表
            var ethnicWaveTable = new LuminoWaveTableInfo
            {
                Id = "lumino_ethnic",
                Name = "Lumino 民族乐器",
                Description = "世界各地民族乐器音色集合",
                IsSystem = true,
                IsBuiltin = true,
                Provider = "LuminoWaveTable",
                Priority = 70,
                InstrumentMappings = CreateEthnicInstrumentMappings()
            };
            _waveTables[ethnicWaveTable.Id] = ethnicWaveTable;

            // 默认播表
            _currentWaveTable = gmWaveTable;
            _logger.Info("WaveTableManager", $"内置播表初始化完成，共 {_waveTables.Count} 个播表");
        }

        /// <summary>
        /// 创建GM完整音色映射
        /// </summary>
        private Dictionary<int, string> CreateGmInstrumentMappings()
        {
            return new Dictionary<int, string>
            {
                // 钢琴类 (0-7)
                { 0, "三角钢琴" }, { 1, "明亮钢琴" }, { 2, "电子三角钢琴" }, { 3, "酒吧钢琴" },
                { 4, "电钢琴1" }, { 5, "电钢琴2" }, { 6, "大键琴" }, { 7, "击弦古钢琴" },
                
                // 打击乐器 (8-15)
                { 8, "钢片琴" }, { 9, "钟琴" }, { 10, "音乐盒" }, { 11, "颤音琴" },
                { 12, "马林巴" }, { 13, "木琴" }, { 14, "管钟" }, { 15, "扬琴" },
                
                // 风琴类 (16-23)
                { 16, "拉杆风琴" }, { 17, "打击式风琴" }, { 18, "摇滚风琴" }, { 19, "教堂风琴" },
                { 20, "簧风琴" }, { 21, "手风琴" }, { 22, "口琴" }, { 23, "探戈手风琴" },
                
                // 吉他类 (24-31)
                { 24, "尼龙弦吉他" }, { 25, "钢弦吉他" }, { 26, "爵士电吉他" }, { 27, "清音电吉他" },
                { 28, "闷音电吉他" }, { 29, "过载吉他" }, { 30, "失真吉他" }, { 31, "吉他泛音" },
                
                // 贝斯类 (32-39)
                { 32, "原声贝斯" }, { 33, "指弹电贝斯" }, { 34, "拨片电贝斯" }, { 35, "无品贝斯" },
                { 36, "击弦贝斯1" }, { 37, "击弦贝斯2" }, { 38, "合成贝斯1" }, { 39, "合成贝斯2" },
                
                // 弦乐类 (40-47)
                { 40, "小提琴" }, { 41, "中提琴" }, { 42, "大提琴" }, { 43, "低音提琴" },
                { 44, "颤音弦乐" }, { 45, "拨弦弦乐" }, { 46, "竖琴" }, { 47, "定音鼓" },
                
                // 合奏/人声类 (48-55)
                { 48, "弦乐合奏1" }, { 49, "弦乐合奏2" }, { 50, "合成弦乐1" }, { 51, "合成弦乐2" },
                { 52, "合唱" }, { 53, "人声" }, { 54, "合成人声" }, { 55, "管弦乐齐奏" },
                
                // 铜管类 (56-63)
                { 56, "小号" }, { 57, "长号" }, { 58, "大号" }, { 59, "弱音小号" },
                { 60, "法国号" }, { 61, "铜管组" }, { 62, "合成铜管1" }, { 63, "合成铜管2" },
                
                // 木管类 (64-71)
                { 64, "高音萨克斯" }, { 65, "中音萨克斯" }, { 66, "次中音萨克斯" }, { 67, "上低音萨克斯" },
                { 68, "双簧管" }, { 69, "英国管" }, { 70, "巴松" }, { 71, "单簧管" },
                
                // 吹管类 (72-79)
                { 72, "短笛" }, { 73, "长笛" }, { 74, "竖笛" }, { 75, "排箫" },
                { 76, "吹瓶" }, { 77, "尺八" }, { 78, "口哨" }, { 79, "埙" },
                
                // 合成主音类 (80-87)
                { 80, "合成主音1" }, { 81, "合成主音2" }, { 82, "合成主音3" }, { 83, "合成主音4" },
                { 84, "合成主音5" }, { 85, "合成主音6" }, { 86, "合成主音7" }, { 87, "合成主音8" },
                
                // 合成柔音类 (88-95)
                { 88, "合成柔音1" }, { 89, "合成柔音2" }, { 90, "合成柔音3" }, { 91, "合成柔音4" },
                { 92, "合成柔音5" }, { 93, "合成柔音6" }, { 94, "合成柔音7" }, { 95, "合成柔音8" },
                
                // 合成特效类 (96-103)
                { 96, "合成特效1" }, { 97, "合成特效2" }, { 98, "合成特效3" }, { 99, "合成特效4" },
                { 100, "合成特效5" }, { 101, "合成特效6" }, { 102, "合成特效7" }, { 103, "合成特效8" },
                
                // 民族乐器类 (104-111)
                { 104, "锡塔尔" }, { 105, "班卓" }, { 106, "三味线" }, { 107, "十三弦筝" },
                { 108, "卡林巴" }, { 109, "风笛" }, { 110, "提琴" }, { 111, "山奈" },
                
                // 打击乐/音效类 (112-127)
                { 112, "叮当铃" }, { 113, "阿果果" }, { 114, "钢鼓" }, { 115, "木鱼" },
                { 116, "太鼓" }, { 117, "旋律鼓" }, { 118, "合成鼓" }, { 119, "反转钹" },
                { 120, "吉他换把噪音" }, { 121, "呼吸音" }, { 122, "海浪" }, { 123, "鸟鸣" },
                { 124, "电话铃" }, { 125, "直升机" }, { 126, "掌声" }, { 127, "枪声" }
            };
        }

        /// <summary>
        /// 创建电子音乐音色映射
        /// </summary>
        private Dictionary<int, string> CreateElectronicInstrumentMappings()
        {
            return new Dictionary<int, string>
            {
                { 25, "钢弦吉他" }, { 26, "爵士电吉他" }, { 27, "清音电吉他" }, { 28, "闷音电吉他" },
                { 29, "过载吉他" }, { 30, "失真吉他" }, { 80, "合成主音1" }, { 81, "合成主音2" },
                { 82, "合成主音3" }, { 83, "合成主音4" }, { 84, "合成主音5" }, { 85, "合成主音6" },
                { 86, "合成主音7" }, { 87, "合成主音8" }, { 88, "合成柔音1" }, { 89, "合成柔音2" },
                { 90, "合成柔音3" }, { 91, "合成柔音4" }, { 92, "合成柔音5" }, { 93, "合成柔音6" },
                { 94, "合成柔音7" }, { 95, "合成柔音8" }, { 96, "合成特效1" }, { 97, "合成特效2" },
                { 98, "合成特效3" }, { 99, "合成特效4" }, { 100, "合成特效5" }, { 101, "合成特效6" },
                { 102, "合成特效7" }, { 103, "合成特效8" }, { 120, "吉他换把噪音" }, { 121, "呼吸音" },
                { 122, "海浪" }, { 123, "鸟鸣" }, { 124, "电话铃" }, { 125, "直升机" }, { 126, "掌声" }, { 127, "枪声" }
            };
        }

        /// <summary>
        /// 创建管弦乐音色映射
        /// </summary>
        private Dictionary<int, string> CreateOrchestralInstrumentMappings()
        {
            return new Dictionary<int, string>
            {
                { 40, "小提琴" }, { 41, "中提琴" }, { 42, "大提琴" }, { 43, "低音提琴" },
                { 44, "颤音弦乐" }, { 45, "拨弦弦乐" }, { 46, "竖琴" }, { 47, "定音鼓" },
                { 48, "弦乐合奏1" }, { 49, "弦乐合奏2" }, { 50, "合成弦乐1" }, { 51, "合成弦乐2" },
                { 56, "小号" }, { 57, "长号" }, { 58, "大号" }, { 59, "弱音小号" },
                { 60, "法国号" }, { 61, "铜管组" }, { 62, "合成铜管1" }, { 63, "合成铜管2" },
                { 64, "高音萨克斯" }, { 65, "中音萨克斯" }, { 66, "次中音萨克斯" }, { 67, "上低音萨克斯" },
                { 68, "双簧管" }, { 69, "英国管" }, { 70, "巴松" }, { 71, "单簧管" },
                { 72, "短笛" }, { 73, "长笛" }, { 74, "竖笛" }, { 75, "排箫" }
            };
        }

        /// <summary>
        /// 创建民族乐器音色映射
        /// </summary>
        private Dictionary<int, string> CreateEthnicInstrumentMappings()
        {
            return new Dictionary<int, string>
            {
                { 104, "锡塔尔" }, { 105, "班卓" }, { 106, "三味线" }, { 107, "十三弦筝" },
                { 108, "卡林巴" }, { 109, "风笛" }, { 110, "提琴" }, { 111, "山奈" },
                { 24, "尼龙弦吉他" }, { 32, "原声贝斯" }, { 40, "小提琴" }, { 41, "中提琴" },
                { 42, "大提琴" }, { 43, "低音提琴" }, { 46, "竖琴" }, { 72, "短笛" },
                { 73, "长笛" }, { 74, "竖笛" }, { 75, "排箫" }, { 76, "吹瓶" }, { 77, "尺八" }
            };
        }

        /// <summary>
        /// 获取所有播表
        /// </summary>
        public List<LuminoWaveTableInfo> GetAllWaveTables()
        {
            lock (_waveTables)
            {
                return _waveTables.Values.OrderByDescending(wt => wt.Priority).ThenBy(wt => wt.Name).ToList();
            }
        }

        /// <summary>
        /// 获取播表
        /// </summary>
        public LuminoWaveTableInfo? GetWaveTable(string id)
        {
            lock (_waveTables)
            {
                return _waveTables.TryGetValue(id, out var waveTable) ? waveTable : null;
            }
        }

        /// <summary>
        /// 设置当前播表
        /// </summary>
        public bool SetCurrentWaveTable(string id)
        {
            lock (_waveTables)
            {
                if (_waveTables.TryGetValue(id, out var waveTable))
                {
                    _currentWaveTable = waveTable;
                    _logger.Info("WaveTableManager", $"播表已切换到: {waveTable.Name} ({id})");
                    return true;
                }
                
                _logger.Warn("WaveTableManager", $"未找到播表: {id}");
                return false;
            }
        }

        /// <summary>
        /// 获取当前播表
        /// </summary>
        public LuminoWaveTableInfo? GetCurrentWaveTable()
        {
            return _currentWaveTable;
        }

        /// <summary>
        /// 添加自定义播表
        /// </summary>
        public bool AddCustomWaveTable(LuminoWaveTableInfo waveTable)
        {
            if (waveTable == null || string.IsNullOrEmpty(waveTable.Id))
            {
                _logger.Error("WaveTableManager", "无效的播表信息");
                return false;
            }

            lock (_waveTables)
            {
                if (_waveTables.ContainsKey(waveTable.Id))
                {
                    _logger.Warn("WaveTableManager", $"播表ID已存在: {waveTable.Id}");
                    return false;
                }

                waveTable.IsSystem = false;
                waveTable.IsBuiltin = false;
                waveTable.CreatedTime = DateTime.Now;
                waveTable.ModifiedTime = DateTime.Now;
                
                _waveTables[waveTable.Id] = waveTable;
                _logger.Info("WaveTableManager", $"添加自定义播表: {waveTable.Name} ({waveTable.Id})");
                return true;
            }
        }

        /// <summary>
        /// 删除播表
        /// </summary>
        public bool RemoveWaveTable(string id)
        {
            lock (_waveTables)
            {
                if (!_waveTables.TryGetValue(id, out var waveTable) || waveTable.IsSystem)
                {
                    _logger.Warn("WaveTableManager", $"无法删除系统播表: {id}");
                    return false;
                }

                _waveTables.Remove(id);
                
                // 如果删除的是当前播表，切换到默认播表
                if (_currentWaveTable?.Id == id)
                {
                    var defaultWaveTable = _waveTables.Values.FirstOrDefault(wt => wt.IsBuiltin);
                    _currentWaveTable = defaultWaveTable ?? _waveTables.Values.FirstOrDefault();
                }

                _logger.Info("WaveTableManager", $"删除播表: {id}");
                return true;
            }
        }

        /// <summary>
        /// 保存播表到文件
        /// </summary>
        public async Task<bool> SaveWaveTableAsync(LuminoWaveTableInfo waveTable)
        {
            try
            {
                if (waveTable == null || string.IsNullOrEmpty(waveTable.Id))
                {
                    _logger.Error("WaveTableManager", "无效的播表信息");
                    return false;
                }

                var fileName = $"wavetable_{waveTable.Id}.json";
                var filePath = Path.Combine(_waveTablesDirectory, fileName);
                
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(waveTable, options);
                await File.WriteAllTextAsync(filePath, json);
                
                _logger.Info("WaveTableManager", $"播表已保存到文件: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableManager", $"保存播表失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从文件加载播表
        /// </summary>
        public async Task<LuminoWaveTableInfo?> LoadWaveTableAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.Warn("WaveTableManager", $"播表文件不存在: {filePath}");
                    return null;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var json = await File.ReadAllTextAsync(filePath);
                var waveTable = JsonSerializer.Deserialize<LuminoWaveTableInfo>(json, options);
                
                if (waveTable != null)
                {
                    _logger.Info("WaveTableManager", $"从文件加载播表: {waveTable.Name} ({filePath})");
                }
                
                return waveTable;
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableManager", $"加载播表失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 加载所有自定义播表
        /// </summary>
        public async Task<int> LoadCustomWaveTablesAsync()
        {
            try
            {
                var files = Directory.GetFiles(_waveTablesDirectory, "wavetable_*.json");
                int loadedCount = 0;

                foreach (var file in files)
                {
                    var waveTable = await LoadWaveTableAsync(file);
                    if (waveTable != null && AddCustomWaveTable(waveTable))
                    {
                        loadedCount++;
                    }
                }

                _logger.Info("WaveTableManager", $"加载自定义播表完成，共 {loadedCount} 个");
                return loadedCount;
            }
            catch (Exception ex)
            {
                _logger.Error("WaveTableManager", $"加载自定义播表失败: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 搜索播表
        /// </summary>
        public List<LuminoWaveTableInfo> SearchWaveTables(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return GetAllWaveTables();
            }

            lock (_waveTables)
            {
                var term = searchTerm.ToLower();
                return _waveTables.Values
                    .Where(wt => 
                        wt.Name.ToLower().Contains(term) || 
                        wt.Description.ToLower().Contains(term) ||
                        wt.InstrumentMappings.Values.Any(instr => instr.ToLower().Contains(term)))
                    .OrderByDescending(wt => wt.Priority)
                    .ThenBy(wt => wt.Name)
                    .ToList();
            }
        }

        /// <summary>
        /// 获取播表统计信息
        /// </summary>
        public (int TotalCount, int BuiltinCount, int CustomCount, int SystemCount) GetStatistics()
        {
            lock (_waveTables)
            {
                int totalCount = _waveTables.Count;
                int builtinCount = _waveTables.Values.Count(wt => wt.IsBuiltin);
                int customCount = _waveTables.Values.Count(wt => !wt.IsBuiltin);
                int systemCount = _waveTables.Values.Count(wt => wt.IsSystem);

                return (totalCount, builtinCount, customCount, systemCount);
            }
        }
    }
}