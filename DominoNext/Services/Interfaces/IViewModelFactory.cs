using Lumino.ViewModels.Editor;
using Lumino.ViewModels.Settings;
using Lumino.Models.Music;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// ViewModel��������ӿ� - ���ڴ���ViewModelʵ��
    /// ͳһ����ViewModel�Ĵ���������ע�룬���ִ���Ŀɲ����ԺͿ�ά����
    /// ����MVVM���ʵ��������ע��ԭ��
    /// </summary>
    public interface IViewModelFactory
    {
        /// <summary>
        /// ����PianoRollViewModelʵ��
        /// </summary>
        /// <returns>���ú�������PianoRollViewModelʵ��</returns>
        PianoRollViewModel CreatePianoRollViewModel();

        /// <summary>
        /// ����SettingsWindowViewModelʵ��
        /// </summary>
        /// <returns>���ú�������SettingsWindowViewModelʵ��</returns>
        SettingsWindowViewModel CreateSettingsWindowViewModel();

        /// <summary>
        /// ����NoteViewModelʵ��
        /// </summary>
        /// <param name="note">��������ģ�ͣ����Ϊnull�򴴽�Ĭ������</param>
        /// <returns>���ú�������NoteViewModelʵ��</returns>
        NoteViewModel CreateNoteViewModel(Note? note = null);

        /// <summary>
        /// ����ViewModel��������������������չ
        /// ���磺CreateProjectViewModel, CreateMidiEditorViewModel��
        /// 
        /// ���ԭ��
        /// 1. ����ViewModel��Ӧ��ͨ������������ȷ��������ȷע��
        /// 2. ��������Ӧ�����ؾ������������ϸ��
        /// 3. ֧�ֵ�Ԫ����ʱ��Mock����ע��
        /// 4. ��ѭ��һְ��ԭ��ֻ���𴴽�ViewModel
        /// </summary>
    }
}