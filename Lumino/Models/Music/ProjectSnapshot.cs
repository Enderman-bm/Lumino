using System.Collections.Generic;
using System.Linq;

namespace Lumino.Models.Music
{
    /// <summary>
    /// 序列化时使用的项目快照，包含音符与控制器事件集合。
    /// </summary>
    public class ProjectSnapshot
    {
        public List<Note> Notes { get; set; } = new List<Note>();
        public List<ControllerEvent> ControllerEvents { get; set; } = new List<ControllerEvent>();

        public static ProjectSnapshot FromNotesOnly(IEnumerable<Note> notes)
        {
            return new ProjectSnapshot
            {
                Notes = notes?.ToList() ?? new List<Note>()
            };
        }
    }
}
