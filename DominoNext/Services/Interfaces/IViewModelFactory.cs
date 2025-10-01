using DominoNext.ViewModels.Editor;
using DominoNext.ViewModels.Settings;
using DominoNext.Models.Music;

namespace DominoNext.Services.Interfaces
{
    /// <summary>
    /// ViewModel��������ӿ� - ���ڴ���ViewModelʵ��
    /// ͳһ����ViewModel�Ĵ���������ע�룬���ִ���Ŀɲ����ԺͿ�ά����
    /// ����MVVM���ʵ��������ע��ԭ��
    /// </summary>
    public interface IViewModelFactory
    {
        /// <summary>
        /// 创建PianoRollViewModel实例
        /// </summary>
        /// <returns>返回新创建的PianoRollViewModel实例</returns>
        PianoRollViewModel CreatePianoRollViewModel();

        /// <summary>
        /// 创建SettingsWindowViewModel实例
        /// </summary>
        /// <returns>返回新创建的SettingsWindowViewModel实例</returns>
        SettingsWindowViewModel CreateSettingsWindowViewModel();

        /// <summary>
        /// 创建NoteViewModel实例
        /// </summary>
        /// <param name="note">音符数据模型，如果为null则创建默认实例</param>
        /// <returns>返回新创建的NoteViewModel实例</returns>
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