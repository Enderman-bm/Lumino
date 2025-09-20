using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lumino.Models.Project
{
    /// <summary>
    /// 项目模型类
    /// </summary>
    public class Project
    {
        /// <summary>
        /// 项目名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// 每小节拍数
        /// </summary>
        public int BeatsPerMeasure { get; set; } = 4;
        
        /// <summary>
        /// 拍值
        /// </summary>
        public int BeatValue { get; set; } = 4;
        
        /// <summary>
        /// 速度（BPM）
        /// </summary>
        public double Tempo { get; set; } = 120.0;
        
        /// <summary>
        /// 项目文件路径
        /// </summary>
        public string? FilePath { get; set; }
        
        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 修改时间
        /// </summary>
        public DateTime ModifiedAt { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 是否已保存
        /// </summary>
        public bool IsSaved { get; set; } = true;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public Project()
        {
        }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">项目名称</param>
        public Project(string name)
        {
            Name = name;
        }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">项目名称</param>
        /// <param name="tempo">速度</param>
        public Project(string name, double tempo)
        {
            Name = name;
            Tempo = tempo;
        }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">项目名称</param>
        /// <param name="tempo">速度</param>
        /// <param name="beatsPerMeasure">每小节拍数</param>
        /// <param name="beatValue">拍值</param>
        public Project(string name, double tempo, int beatsPerMeasure, int beatValue)
        {
            Name = name;
            Tempo = tempo;
            BeatsPerMeasure = beatsPerMeasure;
            BeatValue = beatValue;
        }
        
        /// <summary>
        /// 标记为已修改
        /// </summary>
        public void MarkAsModified()
        {
            ModifiedAt = DateTime.Now;
            IsSaved = false;
        }
        
        /// <summary>
        /// 标记为已保存
        /// </summary>
        public void MarkAsSaved()
        {
            IsSaved = true;
        }
    }
}