using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Lumino.ViewModels.Dialogs;

namespace Lumino.Views.Dialogs
{
    /// <summary>
    /// 音轨选择对话框
    /// </summary>
    public partial class TrackSelectionDialog : Window
    {
        /// <summary>
        /// 选中的音轨索引结果
        /// </summary>
        public ObservableCollection<int> SelectedTrackIndices { get; } = new();

        public TrackSelectionDialog()
        {
            InitializeComponent();
        }

        public TrackSelectionDialog(TrackSelectionDialogViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        /// <summary>
        /// 确认按钮点击事件
        /// </summary>
        private void OnConfirmClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is TrackSelectionDialogViewModel viewModel)
            {
                // 复制选中的索引到结果
                SelectedTrackIndices.Clear();
                foreach (var index in viewModel.SelectedTrackIndices)
                {
                    SelectedTrackIndices.Add(index);
                }
            }

            Close(true);
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}