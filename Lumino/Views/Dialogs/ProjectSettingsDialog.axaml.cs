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
        private DateTime _originalCreated;
        private double _totalEditingTimeSeconds;

        public ProjectSettingsDialog()
        {
            InitializeComponent();

            OkButton.Click += OnOkClicked;
            CancelButton.Click += OnCancelClicked;

            // 设置默认值
            _originalCreated = DateTime.Now;
            _totalEditingTimeSeconds = 0;
            UpdateDisplayTexts();
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
            _originalCreated = metadata.Created;
            _totalEditingTimeSeconds = metadata.TotalEditingTimeSeconds;
            UpdateDisplayTexts();
        }

        /// <summary>
        /// 更新只读显示文本
        /// </summary>
        private void UpdateDisplayTexts()
        {
            // 创建日期显示
            CreatedDateText.Text = _originalCreated.ToString("yyyy年MM月dd日 HH:mm");

            // 累计创作时间显示（自适应单位）
            TotalEditingTimeText.Text = FormatTimeSpan(_totalEditingTimeSeconds);
        }

        /// <summary>
        /// 格式化时间（自适应单位）
        /// </summary>
        private static string FormatTimeSpan(double totalSeconds)
        {
            if (totalSeconds < 1)
                return "不足 1 秒";

            TimeSpan ts = TimeSpan.FromSeconds(totalSeconds);

            if (ts.TotalDays >= 1)
            {
                return $"{(int)ts.TotalDays} 天 {ts.Hours} 小时 {ts.Minutes} 分钟";
            }
            else if (ts.TotalHours >= 1)
            {
                return $"{(int)ts.TotalHours} 小时 {ts.Minutes} 分钟";
            }
            else if (ts.TotalMinutes >= 1)
            {
                return $"{(int)ts.TotalMinutes} 分钟 {ts.Seconds} 秒";
            }
            else
            {
                return $"{(int)ts.TotalSeconds} 秒";
            }
        }

        private void OnOkClicked(object? sender, RoutedEventArgs e)
        {
            Result = new ProjectMetadata
            {
                Title = TitleTextBox.Text ?? "",
                Tempo = (double)(TempoNumeric.Value ?? 120),
                Copyright = CopyrightTextBox.Text ?? "",
                Created = _originalCreated,
                TotalEditingTimeSeconds = _totalEditingTimeSeconds,
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
