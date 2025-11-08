using System;
using Avalonia.Controls;
using Lumino.ViewModels.Dialogs;
using Lumino.Services.Interfaces;
using Avalonia.Threading;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace Lumino.Views.Dialogs
{
    public partial class PreloadMidiDialog : Window
    {
        public PreloadDialogResult ResultChoice { get; private set; } = PreloadDialogResult.Cancel;

    // View is intentionally lightweight: animation and visual state are driven by the ViewModel.

        public PreloadMidiDialog()
        {
            InitializeComponent();
        }

        public PreloadMidiDialog(PreloadMidiViewModel viewModel) : this()
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
                ReselectButton.RenderTransformOrigin = new Avalonia.RelativePoint(0.5, 0.5, Avalonia.RelativeUnit.Relative);
                LoadButton.RenderTransformOrigin = new Avalonia.RelativePoint(0.5, 0.5, Avalonia.RelativeUnit.Relative);
                if (!(ReselectButton.RenderTransform is Avalonia.Media.ScaleTransform))
                    ReselectButton.RenderTransform = new Avalonia.Media.ScaleTransform(1.0, 1.0);
                if (!(LoadButton.RenderTransform is Avalonia.Media.ScaleTransform))
                    LoadButton.RenderTransform = new Avalonia.Media.ScaleTransform(1.0, 1.0);
            }
            catch { }
        }

        private void InitializeButtonsFromViewModel(PreloadMidiViewModel vm)
        {
            try
            {
                var hide = vm.ButtonsHiddenExceptCancel;
                if (hide)
                {
                    ReselectButton.Opacity = 0.0;
                    LoadButton.Opacity = 0.0;
                    ReselectButton.IsVisible = false;
                    LoadButton.IsVisible = false;
                    ReselectButton.IsEnabled = false;
                    LoadButton.IsEnabled = false;
                }
                else
                {
                    ReselectButton.Opacity = 1.0;
                    LoadButton.Opacity = 1.0;
                    ReselectButton.IsVisible = true;
                    LoadButton.IsVisible = true;
                    ReselectButton.IsEnabled = true;
                    LoadButton.IsEnabled = true;
                }
            }
            catch { }
        }

        private Avalonia.Threading.DispatcherTimer? _fadeTimer;
        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is PreloadMidiViewModel vm && e.PropertyName == nameof(vm.ButtonsHiddenExceptCancel))
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
                if (ReselectButton.RenderTransform is Avalonia.Media.ScaleTransform rtf1)
                {
                    rtf1.ScaleX = scale;
                    rtf1.ScaleY = scale;
                }
                else
                {
                    ReselectButton.RenderTransform = new Avalonia.Media.ScaleTransform(scale, scale);
                    ReselectButton.RenderTransformOrigin = new Avalonia.RelativePoint(0.5, 0.5, Avalonia.RelativeUnit.Relative);
                }

                if (LoadButton.RenderTransform is Avalonia.Media.ScaleTransform rtf2)
                {
                    rtf2.ScaleX = scale;
                    rtf2.ScaleY = scale;
                }
                else
                {
                    LoadButton.RenderTransform = new Avalonia.Media.ScaleTransform(scale, scale);
                    LoadButton.RenderTransformOrigin = new Avalonia.RelativePoint(0.5, 0.5, Avalonia.RelativeUnit.Relative);
                }

                ReselectButton.Opacity = newOpacity;
                LoadButton.Opacity = newOpacity;
                if (t >= 1.0)
                {
                    _fadeTimer?.Stop();
                    ReselectButton.IsVisible = false;
                    LoadButton.IsVisible = false;
                    ReselectButton.IsEnabled = false;
                    LoadButton.IsEnabled = false;
                }
            });
            // ensure visible at start
            ReselectButton.IsVisible = true;
            LoadButton.IsVisible = true;
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

            ReselectButton.IsVisible = true;
            LoadButton.IsVisible = true;
            ReselectButton.IsEnabled = true;
            LoadButton.IsEnabled = true;

            _fadeTimer = new Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(dt), Avalonia.Threading.DispatcherPriority.Render, (s, e) =>
            {
                currentStep++;
                // ease-in cubic: eased = t^3
                var t = Math.Min(1.0, currentStep * stepDelta);
                var eased = Math.Pow(t, 3.0);
                var newOpacity = eased;
                var scale = 1.0 - 0.12 * (1.0 - eased); // start slightly smaller and grow to 1

                if (ReselectButton.RenderTransform is Avalonia.Media.ScaleTransform rtf1)
                {
                    rtf1.ScaleX = scale;
                    rtf1.ScaleY = scale;
                }
                else
                {
                    ReselectButton.RenderTransform = new Avalonia.Media.ScaleTransform(scale, scale);
                    ReselectButton.RenderTransformOrigin = new Avalonia.RelativePoint(0.5, 0.5, Avalonia.RelativeUnit.Relative);
                }

                if (LoadButton.RenderTransform is Avalonia.Media.ScaleTransform rtf2)
                {
                    rtf2.ScaleX = scale;
                    rtf2.ScaleY = scale;
                }
                else
                {
                    LoadButton.RenderTransform = new Avalonia.Media.ScaleTransform(scale, scale);
                    LoadButton.RenderTransformOrigin = new Avalonia.RelativePoint(0.5, 0.5, Avalonia.RelativeUnit.Relative);
                }

                ReselectButton.Opacity = newOpacity;
                LoadButton.Opacity = newOpacity;
                if (t >= 1.0)
                {
                    _fadeTimer?.Stop();
                }
            });
            _fadeTimer.Start();
        }
        
    }
}
