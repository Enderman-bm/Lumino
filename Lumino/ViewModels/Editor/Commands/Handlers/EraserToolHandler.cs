using System.Diagnostics;

namespace Lumino.ViewModels.Editor.Commands
{
    /// <summary>
    /// 橡皮工具处理器
    /// </summary>
    public class EraserToolHandler
    {
        private PianoRollViewModel? _pianoRollViewModel;

        public void SetPianoRollViewModel(PianoRollViewModel viewModel)
        {
            _pianoRollViewModel = viewModel;
        }

        public void HandlePress(NoteViewModel? clickedNote)
        {
            if (clickedNote != null && _pianoRollViewModel != null)
            {
                Debug.WriteLine("橡皮工具: 删除音符");
                
                // 🎯 修复：同时从主集合和当前轨道集合中删除音符
                _pianoRollViewModel.Notes.Remove(clickedNote);
                
                // 同时从CurrentTrackNotes删除,确保渲染层立即更新
                if (_pianoRollViewModel.CurrentTrackNotes.Contains(clickedNote))
                {
                    _pianoRollViewModel.CurrentTrackNotes.Remove(clickedNote);
                }
                
                _pianoRollViewModel.UpdateMaxScrollExtent();
            }
        }
    }
}