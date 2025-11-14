using System;
using System.Collections.Generic;

namespace LuminoWaveTable.Models
{
    /// <summary>
    /// 播表信息
    /// </summary>
    public class LuminoWaveTableInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSystem { get; set; }
        public bool IsBuiltin { get; set; }
        public DateTime CreatedTime { get; set; } = DateTime.Now;
        public DateTime ModifiedTime { get; set; } = DateTime.Now;
        public Dictionary<int, string> InstrumentMappings { get; set; } = new();
        public string Provider { get; set; } = "LuminoWaveTable";
        public int Priority { get; set; } = 0;
    }
}