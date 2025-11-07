using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Lumino.Services.Interfaces;
using EnderDebugger;

namespace Lumino.Views.Dialogs
{
    public partial class PreloadMidiDialog : Window
    {
        private readonly EnderLogger _logger;

        public string FileName { get; set; }
        public string FileSizeText { get; set; }

        // 供 DialogService 调用后读取的结果
        public PreloadDialogResult ResultChoice { get; private set; } = PreloadDialogResult.Cancel;

        private long _fileSizeBytes;

        public PreloadMidiDialog()
        {
            InitializeComponent();
            _logger = new EnderLogger("PreloadMidiDialog");
            DataContext = this;
        }

        public PreloadMidiDialog(string fileName, long fileSizeBytes) : this()
        {
            FileName = fileName;
            _fileSizeBytes = fileSizeBytes;
            FileSizeText = FormatFileSize(fileSizeBytes);
            DataContext = this;

            // 设置背景颜色根据文件大小和当前主题
            var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
            var brush = GetBackgroundBrushForSize(fileSizeBytes, isDark);

            // 应用到窗口背景和边框
            this.Background = brush;
            var root = this.FindControl<Border>("RootBorder");
            if (root != null)
            {
                root.Background = brush;
            }

            _logger.Info("Init", $"Initialized preload dialog for {fileName}, size={fileSizeBytes}");
        }

        private static string FormatFileSize(long bytes)
        {
            var kb = 1024.0;
            var mb = kb * 1024.0;
            var gb = mb * 1024.0;

            if (bytes >= gb)
            {
                return (bytes / gb).ToString("0.##", CultureInfo.InvariantCulture) + " GB";
            }
            else if (bytes >= mb)
            {
                return (bytes / mb).ToString("0.##", CultureInfo.InvariantCulture) + " MB";
            }
            else if (bytes >= kb)
            {
                return (bytes / kb).ToString("0.##", CultureInfo.InvariantCulture) + " KB";
            }
            else
            {
                return bytes + " B";
            }
        }

        private static IBrush GetBackgroundBrushForSize(long bytes, bool isDark)
        {
            const long MB = 1024 * 1024;
            const long GB = MB * 1024;

            // 颜色选择：浅色模式和深色模式使用不同的配色
            string hex = bytes switch
            {
                var b when b < 1 * MB => isDark ? "#263238" : "#FFFFFF",
                var b when b < 10 * MB => isDark ? "#1B5E20" : "#E8F5E9",    // 1-10MB 绿
                var b when b < 50 * MB => isDark ? "#F57F17" : "#FFF9C4",   // 10-50MB 黄
                var b when b < 200 * MB => isDark ? "#E65100" : "#FFE0B2",  // 50-200MB 橙
                var b when b < 1 * GB => isDark ? "#0D47A1" : "#E3F2FD",    // 200MB-1GB 蓝
                var b when b < 5 * GB => isDark ? "#6A1B9A" : "#F3E5F5",    // 1GB-5GB 紫
                _ => isDark ? "#B71C1C" : "#FFEBEE"                         // >5GB 红
            };

            try
            {
                return new SolidColorBrush(Color.Parse(hex));
            }
            catch
            {
                return Brushes.Transparent;
            }
        }

        private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _logger.Info("UserAction", "Preload dialog: Cancel");
            ResultChoice = PreloadDialogResult.Cancel;
            Close();
        }

        private void OnReselectClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _logger.Info("UserAction", "Preload dialog: Reselect");
            ResultChoice = PreloadDialogResult.Reselect;
            Close();
        }

        private void OnLoadClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _logger.Info("UserAction", "Preload dialog: Load");
            ResultChoice = PreloadDialogResult.Load;
            Close();
        }
    }
}
