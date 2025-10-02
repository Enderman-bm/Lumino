// 文件用途：
// ConfirmationDialog 是一个确认对话框类，允许用户确认或取消操作。
// 使用限制：
// 1. 仅供 DominoNext 项目使用。
// 2. 修改此文件需经过代码审查。

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using EnderDebugger;

namespace DominoNext.Views.Dialogs
{
    /// <summary>
    /// ȷ�϶Ի��� - ������ʾȷ����Ϣ����ȡ�û�ѡ��
    /// �򵥵�ģ̬�Ի���֧��ȷ��/ȡ������
    /// </summary>
    public partial class ConfirmationDialog : Window
    {
        private readonly EnderLogger _logger;

        /// <summary>
        /// �Ի����� - �û��Ƿ�ȷ�ϲ���
        /// </summary>
        public bool Result { get; private set; } = false;

        /// <summary>
        /// ��ʾ����Ϣ����
        /// </summary>
        public string Message { get; set; } = string.Empty;

        public ConfirmationDialog()
        {
            InitializeComponent();
            _logger = new EnderLogger("ConfirmationDialog");
            _logger.Info("Initialization", "[EnderDebugger][{DateTime.Now}][EnderLogger][ConfirmationDialog] 确认对话框已初始化。");
            
            // ����DataContextΪ�Լ����Ա��Message����
            DataContext = this;
        }

        /// <summary>
        /// ȷ����ť����¼�
        /// </summary>
        private void OnConfirmClick(object? sender, RoutedEventArgs e)
        {
            _logger.Info("UserAction", "[EnderDebugger][{DateTime.Now}][EnderLogger][ConfirmationDialog] 用户点击了确认按钮。");
            Result = true;
            Close(Result);
        }

        /// <summary>
        /// ȡ����ť����¼�
        /// </summary>
        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            _logger.Info("UserAction", "[EnderDebugger][{DateTime.Now}][EnderLogger][ConfirmationDialog] 用户点击了取消按钮。");
            Result = false;
            Close(Result);
        }
    }
}