namespace DominoNext.Constants
{
    /// <summary>
    /// �Ի�����س�������
    /// ͳһ�����Ի���ı��⡢��Ϣ����Ϊ����������Ӳ����
    /// </summary>
    public static class DialogConstants
    {
        #region �ļ��Ի�����
        
        /// <summary>
        /// MIDI�ļ���չ��������
        /// ֧�ֱ�׼MIDI��ʽ��DominoNext��Ŀ��ʽ
        /// </summary>
        public static readonly string[] MidiFileFilters = { "*.mid", "*.midi", "*.dmn" };
        
        /// <summary>
        /// ��Ŀ�ļ���չ��������
        /// DominoNextר����Ŀ��ʽ
        /// </summary>
        public static readonly string[] ProjectFileFilters = { "*.dmn", "*.dmnx" };
        
        /// <summary>
        /// ����֧�ֵ��ļ���ʽ
        /// </summary>
        public static readonly string[] AllSupportedFilters = { "*.mid", "*.midi", "*.dmn", "*.dmnx" };
        
        #endregion

        #region �Ի�����ⳣ��
        
        public const string OPEN_FILE_TITLE = "��MIDI�ļ�";
        public const string SAVE_FILE_TITLE = "������Ŀ�ļ�";
        public const string ERROR_TITLE = "����";
        public const string WARNING_TITLE = "����";
        public const string INFO_TITLE = "��Ϣ";
        public const string CONFIRM_TITLE = "ȷ��";
        
        #endregion

        #region ȷ�϶Ի�����
        
        /// <summary>
        /// Ĭ��ȷ�Ͻ�� - ���޷���ʾȷ�϶Ի���ʱ�İ�ȫ����ֵ
        /// ѡ��false��Ϊ�˱���������ƻ��Բ���
        /// </summary>
        public const bool DEFAULT_CONFIRMATION_RESULT = false;
        
        /// <summary>
        /// �˳�ȷ����Ϣ
        /// </summary>
        public const string EXIT_CONFIRMATION_MESSAGE = "��δ����ĸ��ģ��Ƿ�ȷ���˳���";
        
        /// <summary>
        /// �½��ļ�ȷ����Ϣ
        /// </summary>
        public const string NEW_FILE_CONFIRMATION_MESSAGE = "��ǰ��Ŀ��δ����ĸ��ģ��Ƿ�����������ļ���";
        
        /// <summary>
        /// ���ļ�ȷ����Ϣ
        /// </summary>
        public const string OPEN_FILE_CONFIRMATION_MESSAGE = "��ǰ��Ŀ��δ����ĸ��ģ��Ƿ���������ļ���";
        
        #endregion

        #region ������Ϣ����
        
        public const string SETTINGS_DIALOG_ERROR = "������ʱ��������";
        public const string FILE_DIALOG_ERROR = "�ļ��Ի������ʧ��";
        public const string CONFIRMATION_DIALOG_ERROR = "��ʾȷ�϶Ի���ʱ��������";
        public const string ERROR_DIALOG_ERROR = "��ʾ����Ի���ʱ��������";
        public const string INFO_DIALOG_ERROR = "��ʾ��Ϣ�Ի���ʱ��������";
        public const string LOADING_DIALOG_ERROR = "��ʾ�����гԻ���ʱ��������";
        public const string PROGRESS_DIALOG_ERROR = "进度窗口操作时发生错误";
        
        #endregion

        #region ������ʾ��Ϣ
        
        public const string FEATURE_NOT_IMPLEMENTED = "�˹��ܽ��ں����汾��ʵ��";
        public const string NEW_FILE_FEATURE_MESSAGE = "�½��ļ����ܽ��ں����汾��ʵ��";
        public const string SAVE_FILE_FEATURE_MESSAGE = "�ļ����湦�ܽ��ں����汾��ʵ��";
        
        #endregion

        #region 进度窗口相关常量
        
        /// <summary>
        /// MIDI导入进度窗口标题
        /// </summary>
        public const string MIDI_IMPORT_PROGRESS_TITLE = "正在导入MIDI文件";
        
        /// <summary>
        /// MIDI导出进度窗口标题
        /// </summary>
        public const string MIDI_EXPORT_PROGRESS_TITLE = "正在导出MIDI文件";
        
        /// <summary>
        /// 项目保存进度窗口标题
        /// </summary>
        public const string PROJECT_SAVE_PROGRESS_TITLE = "正在保存项目";
        
        /// <summary>
        /// 项目加载进度窗口标题
        /// </summary>
        public const string PROJECT_LOAD_PROGRESS_TITLE = "正在加载项目";
        
        /// <summary>
        /// 音频渲染进度窗口标题
        /// </summary>
        public const string AUDIO_RENDER_PROGRESS_TITLE = "正在渲染音频";
        
        /// <summary>
        /// 默认进度窗口标题
        /// </summary>
        public const string DEFAULT_PROGRESS_TITLE = "正在处理...";
        
        #endregion
    }
}