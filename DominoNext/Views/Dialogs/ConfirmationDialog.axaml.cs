using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Lumino.Views.Dialogs
{
    /// <summary>
    /// ȷ�϶Ի��� - ������ʾȷ����Ϣ����ȡ�û�ѡ��
    /// �򵥵�ģ̬�Ի���֧��ȷ��/ȡ������
    /// </summary>
    public partial class ConfirmationDialog : Window
    {
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
            
            // ����DataContextΪ�Լ����Ա��Message����
            DataContext = this;
        }

        /// <summary>
        /// ȷ����ť����¼�
        /// </summary>
        private void OnConfirmClick(object? sender, RoutedEventArgs e)
        {
            Result = true;
            Close(Result);
        }

        /// <summary>
        /// ȡ����ť����¼�
        /// </summary>
        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Result = false;
            Close(Result);
        }
    }
}