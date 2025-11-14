using Avalonia.Controls;
using Lumino.ViewModels.Dialogs;
using Lumino.Services.Interfaces;
using Avalonia.Threading;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using System;

namespace Lumino.Views.Dialogs
{
    public partial class ExportDialog : Window
    {
        public PreloadDialogResult ResultChoice { get; private set; } = PreloadDialogResult.Cancel;

        // View is intentionally lightweight: animation and visual state are driven by the ViewModel.

        public ExportDialog()
        {
            InitializeComponent();
        }

        public ExportDialog(ExportViewModel viewModel) : this()
        {
            DataContext = viewModel;

            // 当 ViewModel 请求关闭时，关闭窗口并保留结果
            viewModel.RequestClose += (res) =>
            {
                ResultChoice = res;
                Close();
            };

            // 订阅 ViewModel 的按钮隐藏指示以便触发平滑过渡动画（淡出/淡入）
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            // 初始化按钮视觉状态
            InitializeButtonsFromViewModel(viewModel);

            // Ensure buttons have RenderTransform/Origin ready for smooth animation
            try
            {
                ExportButton.RenderTransformOrigin = new Avalonia.RelativePoint(0.5, 0.5, Avalonia.RelativeUnit.Relative);
                CancelButton.RenderTransformOrigin = new Avalonia.RelativePoint(0.5, 0.5, Avalonia.RelativeUnit.Relative);
                if (!(ExportButton.RenderTransform is Avalonia.Media.ScaleTransform))
                    ExportButton.RenderTransform = new Avalonia.Media.ScaleTransform(1.0, 1.0);
                if (!(CancelButton.RenderTransform is Avalonia.Media.ScaleTransform))
                    CancelButton.RenderTransform = new Avalonia.Media.ScaleTransform(1.0, 1.0);
            }
            catch { }
        }

        private void InitializeButtonsFromViewModel(ExportViewModel vm)
        {
            try
            {
                var hide = vm.ButtonsHiddenExceptCancel;
                if (hide)
                {
                    ExportButton.Opacity = 0.0;
                    ExportButton.IsVisible = false;
                    ExportButton.IsEnabled = false;
                }
                else
                {
                    ExportButton.Opacity = 1.0;
                    ExportButton.IsVisible = true;
                    ExportButton.IsEnabled = true;
                }
            }
            catch { }
        }

        private Avalonia.Threading.DispatcherTimer? _fadeTimer;
        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is ExportViewModel vm && e.PropertyName == nameof(vm.ButtonsHiddenExceptCancel))
            {
                // 在 UI 线程执行动画
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        if (vm.ButtonsHiddenExceptCancel)
                            StartFadeOutButtons();
                        else
                            StartFadeInButtons();
                    }
                    catch { }
                });
            }
        }

        private void StartFadeOutButtons()
        {
            // stop any existing timer
            _fadeTimer?.Stop();
            double durationMs = 260.0;
            double steps = 15.0;
            double dt = durationMs / steps;
            double stepDelta = 1.0 / steps;
            int currentStep = 0;

            _fadeTimer = new Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(dt), Avalonia.Threading.DispatcherPriority.Render, (s, e) =>
            {
                currentStep++;
                // ease-out cubic: eased = 1 - (1-t)^3
                var t = Math.Min(1.0, currentStep * stepDelta);
                var eased = 1.0 - Math.Pow(1.0 - t, 3.0);
                var newOpacity = 1.0 - eased;
                var scale = 1.0 - 0.12 * eased; // scale down slightly when fading out

                // apply opacity and scale transform
                if (ExportButton.RenderTransform is Avalonia.Media.ScaleTransform rtf1)
                {
                    rtf1.ScaleX = scale;
                    rtf1.ScaleY = scale;
                }
                else
                {
                    ExportButton.RenderTransform = new Avalonia.Media.ScaleTransform(scale, scale);
                    ExportButton.RenderTransformOrigin = new Avalonia.RelativePoint(0.5, 0.5, Avalonia.RelativeUnit.Relative);
                }

                ExportButton.Opacity = newOpacity;
                if (t >= 1.0)
                {
                    _fadeTimer?.Stop();
                    ExportButton.IsVisible = false;
                    ExportButton.IsEnabled = false;
                }
            });
            // ensure visible at start
            ExportButton.IsVisible = true;
            _fadeTimer.Start();
        }

        private void StartFadeInButtons()
        {
            _fadeTimer?.Stop();
            double durationMs = 260.0;
            double steps = 15.0;
            double dt = durationMs / steps;
            double stepDelta = 1.0 / steps;
            int currentStep = 0;

            ExportButton.IsVisible = true;
            ExportButton.IsEnabled = true;

            _fadeTimer = new Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(dt), Avalonia.Threading.DispatcherPriority.Render, (s, e) =>
            {
                currentStep++;
                // ease-in cubic: eased = t^3
                var t = Math.Min(1.0, currentStep * stepDelta);
                var eased = Math.Pow(t, 3.0);
                var newOpacity = eased;
                var scale = 1.0 - 0.12 * (1.0 - eased); // start slightly smaller and grow to 1

                if (ExportButton.RenderTransform is Avalonia.Media.ScaleTransform rtf1)
                {
                    rtf1.ScaleX = scale;
                    rtf1.ScaleY = scale;
                }
                else
                {
                    ExportButton.RenderTransform = new Avalonia.Media.ScaleTransform(scale, scale);
                    ExportButton.RenderTransformOrigin = new Avalonia.RelativePoint(0.5, 0.5, Avalonia.RelativeUnit.Relative);
                }

                ExportButton.Opacity = newOpacity;
                if (t >= 1.0)
                {
                    _fadeTimer?.Stop();
                }
            });
            _fadeTimer.Start();
        }
    }
}