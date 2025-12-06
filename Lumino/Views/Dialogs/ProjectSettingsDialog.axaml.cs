using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Lumino.Services.Interfaces;
using System;

namespace Lumino.Views.Dialogs
{
    public partial class ProjectSettingsDialog : Window
    {
        public ProjectMetadata? Result { get; private set; }

        public ProjectSettingsDialog()
        {
            InitializeComponent();

            OkButton.Click += OnOkClicked;
            CancelButton.Click += OnCancelClicked;

            // 设置默认日期
            CreatedDatePicker.SelectedDate = DateTimeOffset.Now;
        }

        /// <summary>
        /// 使用现有的工程信息初始化对话框
        /// </summary>
        public void SetProjectMetadata(ProjectMetadata metadata)
        {
            if (metadata == null) return;

            TitleTextBox.Text = metadata.Title;
            TempoNumeric.Value = (decimal)metadata.Tempo;
            CopyrightTextBox.Text = metadata.Copyright;
            CreatedDatePicker.SelectedDate = new DateTimeOffset(metadata.Created);
        }

        private void OnOkClicked(object? sender, RoutedEventArgs e)
        {
            Result = new ProjectMetadata
            {
                Title = TitleTextBox.Text ?? "",
                Tempo = (double)(TempoNumeric.Value ?? 120),
                Copyright = CopyrightTextBox.Text ?? "",
                Created = CreatedDatePicker.SelectedDate?.DateTime ?? DateTime.Now,
                LastModified = DateTime.Now
            };

            Close(Result);
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e)
        {
            Result = null;
            Close(null);
        }
    }
}
