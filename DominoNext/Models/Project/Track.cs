using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lumino.Models.Project
{
    /// <summary>
    /// 音轨模型类
    /// </summary>
    public class Track
    {
        /// <summary>
        /// 音轨名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// 音轨颜色
        /// </summary>
        public string Color { get; set; } = "#FF0000";
        
        /// <summary>
        /// MIDI通道
        /// </summary>
        public int Channel { get; set; }
        
        /// <summary>
        /// 音轨索引
        /// </summary>
        public int Index { get; set; }
        
        /// <summary>
        /// 是否静音
        /// </summary>
        public bool IsMuted { get; set; }
        
        /// <summary>
        /// 是否独奏
        /// </summary>
        public bool IsSolo { get; set; }
        
        /// <summary>
        /// 音量
        /// </summary>
        public int Volume { get; set; } = 100;
        
        /// <summary>
        /// 平衡
        /// </summary>
        public int Pan { get; set; } = 64;
        
        /// <summary>
        /// 乐器
        /// </summary>
        public int Instrument { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public Track()
        {
        }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">音轨名称</param>
        public Track(string name)
        {
            Name = name;
        }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">音轨名称</param>
        /// <param name="color">音轨颜色</param>
        public Track(string name, string color)
        {
            Name = name;
            Color = color;
        }
    }
}