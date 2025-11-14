using System;
using System.Globalization;
using System.Windows.Input;
using Avalonia.Media;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumino.Services.Interfaces;

namespace Lumino.ViewModels.Dialogs
{
    /// <summary>
    /// Export dialog ViewModel - drives the export dialog UI and actions.
    /// </summary>
    public partial class ExportViewModel : ObservableObject
    {
        public event Action<PreloadDialogResult>? RequestClose;
        /// <summary>
        /// 当用户点击"导出"但对话框不应立即关闭时触发（用于在对话框内部开始进度任务）
        /// </summary>
        public event Action? ExportRequested;

        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private string _fileSizeText = string.Empty;

        private long _fileSizeBytes;

        [ObservableProperty]
        private IBrush? _backgroundBrush;

        [ObservableProperty]
        private double _opacity = 0.0;

        [ObservableProperty]
        private double _rotationAngle = 0.0;

        [ObservableProperty]
        private bool _isStarting = false;

        [ObservableProperty]
        private double _progress = 0.0;

        [ObservableProperty]
        private string _statusText = string.Empty;

        // 当导出完成并进入"写入文件"阶段时, 切换为横向不确定性加载动画
        [ObservableProperty]
        private bool _isIndeterminate = false;

        // 当按下导出后，除了取消以外隐藏/禁用其他按钮（触发平滑过渡）
        [ObservableProperty]
        private bool _buttonsHiddenExceptCancel = false;

        // 若对话框在运行任务时允许取消，则此字段会被赋值
        private System.Threading.CancellationTokenSource? _cancellationSource;

        public ExportViewModel(string fileName, long fileSizeBytes)
        {
            _fileName = fileName;
            _fileSizeBytes = fileSizeBytes;
            _fileSizeText = FormatFileSize(fileSizeBytes);
            var isDark = Avalonia.Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
            _backgroundBrush = GetBackgroundBrushForSize(fileSizeBytes, isDark == true);

            // 初始动画状态
            _opacity = 0.0;
            _rotationAngle = 0.0;
            _progress = 0.0;
            _statusText = "准备导出...";
        }

        [RelayCommand]
        private void Cancel()
        {
            // 如果任务已经开始且存在取消令牌源，则触发取消；否则直接关闭对话框并返回取消
            if (IsStarting && _cancellationSource != null)
            {
                try
                {
                    _cancellationSource.Cancel();
                    StatusText = "取消中...";
                }
                catch { }
                return;
            }
            RequestClose?.Invoke(PreloadDialogResult.Cancel);
        }

        [RelayCommand]
        private async Task Export()
        {
            // 切换到启动进度的视觉状态并短暂显示启动动画，随后通知调用者开始在对话框中执行任务
            IsStarting = true;
            StartIntroAnimation();

            // 短暂视觉反馈
            await Task.Delay(420);

            // 隐藏除了取消以外的按钮（平滑过渡）
            ButtonsHiddenExceptCancel = true;

            // 不再关闭对话框，这里通知调用者（DialogService）开始执行任务并在对话框内显示进度
            ExportRequested?.Invoke();
        }

        /// <summary>
        /// 为后续的任务执行设置可取消令牌源（由DialogService创建并传入）
        /// </summary>
        /// <param name="cts"></param>
        public void SetCancellationSource(System.Threading.CancellationTokenSource? cts)
        {
            _cancellationSource = cts;
        }

        public void StartIntroAnimation()
        {
            // 使用 Avalonia 的调度定时器在 ViewModel 中更新可绑定属性（纯 MVVM 驱动的动画）
            try
            {
                Avalonia.Threading.DispatcherTimer? rotateTimer = null;
                Avalonia.Threading.DispatcherTimer? fadeTimer = null;

                // 旋转计时器
                rotateTimer = new Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(30), Avalonia.Threading.DispatcherPriority.Render, (s, e) =>
                {
                    RotationAngle = (RotationAngle + 6.0) % 360.0;
                });
                rotateTimer.Start();

                // 淡入计时器
                fadeTimer = new Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(16), Avalonia.Threading.DispatcherPriority.Render, (s, e) =>
                {
                    if (Opacity >= 1.0)
                    {
                        fadeTimer?.Stop();
                        return;
                    }
                    Opacity = Math.Min(1.0, Opacity + 0.06);
                });
                fadeTimer.Start();

                // 将计时器引用保存在本实例的私有字段，以便 Stop 时可以停止它们
                _animationRotateTimer = rotateTimer;
                _animationFadeTimer = fadeTimer;
            }
            catch { }
        }

        public void StopIntroAnimation()
        {
            try
            {
                _animationRotateTimer?.Stop();
                _animationFadeTimer?.Stop();
            }
            catch { }
            finally
            {
                _animationRotateTimer = null;
                _animationFadeTimer = null;
            }
        }

        // 私有计时器引用
        private Avalonia.Threading.DispatcherTimer? _animationRotateTimer;
        private Avalonia.Threading.DispatcherTimer? _animationFadeTimer;

        private static string FormatFileSize(long bytes)
        {
            var kb = 1024.0;
            var mb = kb * 1024.0;
            var gb = mb * 1024.0;

            if (bytes >= gb) return (bytes / gb).ToString("0.##", CultureInfo.InvariantCulture) + " GB";
            if (bytes >= mb) return (bytes / mb).ToString("0.##", CultureInfo.InvariantCulture) + " MB";
            if (bytes >= kb) return (bytes / kb).ToString("0.##", CultureInfo.InvariantCulture) + " KB";
            return bytes + " B";
        }

        private static IBrush GetBackgroundBrushForSize(long bytes, bool isDark)
        {
            const long MB = 1024 * 1024;
            const long GB = MB * 1024;

            string category = bytes switch
            {
                var b when b < 1 * MB => "Tiny",
                var b when b < 10 * MB => "Small",
                var b when b < 50 * MB => "Medium",
                var b when b < 200 * MB => "Large",
                var b when b < 1 * GB => "Huge",
                var b when b < 5 * GB => "Massive",
                _ => "Insane",
            };

            var variant = isDark ? "Dark" : "Light";
            var key = $"PreloadBg_{category}_{variant}";

            try
            {
                // 尝试从资源字典中获取已定义的渐变画刷
                var brush = Lumino.Views.Rendering.Utils.RenderingUtils.GetResourceBrush(key, "#FFFFFF");
                return brush;
            }
            catch
            {
                return Brushes.Transparent;
            }
        }
    }
}