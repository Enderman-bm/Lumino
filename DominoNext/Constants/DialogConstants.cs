namespace DominoNext.Constants
{
    /// <summary>
    /// 对话框常量管理器
    /// 统一管理对话框的标题、信息，避免硬编码
    /// </summary>
    public static class DialogConstants
    {
        #region 文件对话框常量
        
        /// <summary>
        /// MIDI文件扩展名过滤器
        /// 支持标准MIDI格式和DominoNext项目格式
        /// </summary>
        public static readonly string[] MidiFileFilters = { "*.mid", "*.midi", "*.dmn" };
        
        /// <summary>
        /// 项目文件扩展名过滤器
        /// DominoNext专用项目格式
        /// </summary>
        public static readonly string[] ProjectFileFilters = { "*.dmn", "*.dmnx" };
        
        /// <summary>
        /// 所有支持的文件格式
        /// </summary>
        public static readonly string[] AllSupportedFilters = { "*.mid", "*.midi", "*.dmn", "*.dmnx" };
        
        #endregion

        #region 对话框标题常量
        
        public const string OPEN_FILE_TITLE = "打开MIDI文件";
        public const string SAVE_FILE_TITLE = "保存项目文件";
        public const string ERROR_TITLE = "错误";
        public const string WARNING_TITLE = "警告";
        public const string INFO_TITLE = "信息";
        public const string CONFIRM_TITLE = "确认";
        
        #endregion

        #region 确认对话框常量
        
        /// <summary>
        /// 默认确认结果 - 当无法显示确认对话框时的安全默认值
        /// 选择false是为了避免意外的破坏性操作
        /// </summary>
        public const bool DEFAULT_CONFIRMATION_RESULT = false;
        
        /// <summary>
        /// 退出确认信息
        /// </summary>
        public const string EXIT_CONFIRMATION_MESSAGE = "有未保存的修改，是否确认退出？";
        
        /// <summary>
        /// 新建文件确认信息
        /// </summary>
        public const string NEW_FILE_CONFIRMATION_MESSAGE = "当前项目有未保存的修改，是否继续新建文件？";
        
        /// <summary>
        /// 打开文件确认信息
        /// </summary>
        public const string OPEN_FILE_CONFIRMATION_MESSAGE = "当前项目有未保存的修改，是否继续打开文件？";
        
        #endregion

        #region 错误信息常量
        
        public const string SETTINGS_DIALOG_ERROR = "设置窗口时发生错误";
        public const string FILE_DIALOG_ERROR = "文件对话框操作失败";
        public const string CONFIRMATION_DIALOG_ERROR = "显示确认对话框时发生错误";
        public const string ERROR_DIALOG_ERROR = "显示错误对话框时发生错误";
        public const string INFO_DIALOG_ERROR = "显示信息对话框时发生错误";
        public const string LOADING_DIALOG_ERROR = "显示加载中对话框时发生错误";
        public const string PROGRESS_DIALOG_ERROR = "进度窗口操作时发生错误";
        
        #endregion

        #region 功能提示信息
        
        public const string FEATURE_NOT_IMPLEMENTED = "此功能将在后续版本中实现";
        public const string NEW_FILE_FEATURE_MESSAGE = "新建文件功能将在后续版本中实现";
        public const string SAVE_FILE_FEATURE_MESSAGE = "文件保存功能将在后续版本中实现";
        
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