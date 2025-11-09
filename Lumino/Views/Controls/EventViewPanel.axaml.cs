using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Lumino.ViewModels.Editor;
using Lumino.Services.Interfaces;
using Lumino.Services.Implementation;
using EnderDebugger;
using System;

namespace Lumino.Views.Controls
{
    /// <summary>
    /// �¼���ͼ��� - ������ʾMIDI�¼���������Ϣ��֧�ֶ�̬���޳���
    /// </summary>
    public partial class EventViewPanel : UserControl, IRenderSyncTarget
    {
        public static readonly StyledProperty<PianoRollViewModel?> ViewModelProperty =
            AvaloniaProperty.Register<EventViewPanel, PianoRollViewModel?>(nameof(ViewModel));

        public static readonly StyledProperty<bool> IsEventViewVisibleProperty =
            AvaloniaProperty.Register<EventViewPanel, bool>(nameof(IsEventViewVisible), true);

        private readonly IRenderSyncService _renderSyncService;
        private DateTime _lastRefreshTime = DateTime.MinValue;
        private const int REFRESH_THROTTLE_MS = 16; // ~60 FPS

        public PianoRollViewModel? ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

    public bool IsEventViewVisible
    {
        get => GetValue(IsEventViewVisibleProperty);
        set => SetValue(IsEventViewVisibleProperty, value);
    }        public EventViewPanel()
        {
            InitializeComponent();

            EnderLogger.Instance.Info("Initialization", "[EventViewPanel] 构造函数被调用，初始化组件。");

            // 注册到渲染同步服务
            _renderSyncService = RenderSyncService.Instance;
            _renderSyncService.RegisterTarget(this);
            EnderLogger.Instance.Info("RenderSync", "[EventViewPanel] 已注册到渲染同步服务。");

            // 监听属性变化
            this.PropertyChanged += OnPropertyChanged;
            EnderLogger.Instance.Info("PropertyChange", "[EventViewPanel] 已注册属性变化事件。");

            // 监听附加到可视树事件
            this.AttachedToVisualTree += OnAttachedToVisualTree;
            EnderLogger.Instance.Info("VisualTree", "[EventViewPanel] 已注册附加到可视树事件。");
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            EnderLogger.Instance.Info("VisualTree", "[EventViewPanel] 附加到可视树。");

            if (this.FindControl<TextBox>("CCNumberTextBox") is TextBox ccNumberTextBox)
            {
                ccNumberTextBox.LostFocus += OnCCNumberTextBoxLostFocus;
                ccNumberTextBox.KeyDown += OnCCNumberTextBoxKeyDown;
                EnderLogger.Instance.Info("EventRegistration", "[EventViewPanel] 已为 CCNumberTextBox 注册事件。");
            }
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            EnderLogger.Instance.Info("PropertyChange", $"[EventViewPanel] 属性变化：{e.Property.Name}，新值：{e.NewValue}");

            if (e.Property == ViewModelProperty && e.NewValue is PianoRollViewModel viewModel)
            {
                DataContext = viewModel;
                EnderLogger.Instance.Info("ViewModel", "[EventViewPanel] ViewModel 已更新。");
            }
            else if (e.Property == IsEventViewVisibleProperty && e.NewValue is bool isVisible)
            {
                // 恢复完整的可见性控制 - 不再强制隐藏整个面板
                this.IsVisible = isVisible;
                EnderLogger.Instance.Info("Visibility", $"[EventViewPanel] IsEventViewVisible 已更新为 {isVisible}。");
            }
        }

        /// <summary>
        /// CC�������ʧȥ�����¼�����
        /// </summary>
        private void OnCCNumberTextBoxLostFocus(object? sender, RoutedEventArgs e)
        {
            EnderLogger.Instance.Info("Focus", "[EventViewPanel] CCNumberTextBox 失去焦点。");

            if (sender is TextBox textBox)
            {
                ValidateCCNumber(textBox.Text!);
            }
        }

        /// <summary>
        /// CC������򰴼��¼�����
        /// </summary>
        private void OnCCNumberTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            EnderLogger.Instance.Info("KeyPress", $"[EventViewPanel] CCNumberTextBox 键盘按下：{e.Key}");

            if (sender is TextBox textBox)
            {
                if (e.Key == Key.Enter)
                {
                    ValidateCCNumber(textBox.Text!);
                    e.Handled = true;
                    EnderLogger.Instance.Info("Validation", "[EventViewPanel] CCNumberTextBox 按下回车键，验证 CCNumber。");
                }
                else if (e.Handled)
                {
                    EnderLogger.Instance.Warn("KeyPress", "[EventViewPanel] 非法按键被阻止。");
                }
            }
        }

        /// <summary>
        /// ��֤CC������
        /// </summary>
        private void ValidateCCNumber(string? input)
        {
            if (string.IsNullOrEmpty(input))
            {
                EnderLogger.Instance.Warn("Validation", "[EventViewPanel] 输入的 CCNumber 为空或无效。");
                return;
            }

            EnderLogger.Instance.Info("Validation", $"[EventViewPanel] 验证 CCNumber：{input}");

            if (ViewModel == null)
            {
                EnderLogger.Instance.Warn("Validation", "[EventViewPanel] ViewModel 为 null，无法验证 CCNumber。");
                return;
            }

            if (int.TryParse(input, out int ccNumber))
            {
                ccNumber = Math.Max(0, Math.Min(127, ccNumber));
                ViewModel.CurrentCCNumber = ccNumber;
                EnderLogger.Instance.Info("Validation", $"[EventViewPanel] CCNumber 验证通过，设置为 {ccNumber}。");

                // ����TextBox��ʾ��ȷ��ֵ
                if (this.FindControl<TextBox>("CCNumberTextBox") is TextBox textBox)
                {
                    textBox.Text = ccNumber.ToString();
                }
            }
            else
            {
                EnderLogger.Instance.Warn("Validation", "[EventViewPanel] CCNumber 验证失败，输入无效。");

                // ���������Ч���ָ�Ϊ��ǰֵ
                if (this.FindControl<TextBox>("CCNumberTextBox") is TextBox textBox)
                {
                    textBox.Text = ViewModel.CurrentCCNumber.ToString();
                }
            }
        }

        /// <summary>
        /// ��ȡ�ڲ��Ĺ�����ͼ��������������ͼ���й���ͬ�����ѷ��������������ԣ�
        /// </summary>
        [Obsolete("����ʹ��ScrollViewer����ΪCanvas������Ⱦ")]
        public ScrollViewer? GetEventViewScrollViewer()
        {
            return null; // ����ʹ��ScrollViewer
        }

        /// <summary>
        /// ͬ��ˮƽ����λ�� - ��ʵ�֣�ͨ����Ⱦͬ������
        /// </summary>
        /// <param name="offset">����ƫ����</param>
        public void SyncHorizontalScroll(double offset)
        {
            // ͨ����Ⱦͬ������ͬ��ˢ��
            _renderSyncService.SyncRefresh();
        }

        /// <summary>
        /// ʵ��IRenderSyncTarget�ӿ�
        /// </summary>
        public void RefreshRender()
        {
            // 节流机制：限制刷新频率，避免过于频繁的渲染
            var now = DateTime.Now;
            if ((now - _lastRefreshTime).TotalMilliseconds < REFRESH_THROTTLE_MS)
            {
                return;
            }
            _lastRefreshTime = now;

            InvalidateVisual();

            // Ҳˢ����Canvas
            if (this.FindControl<Canvas.EventViewCanvas>("EventViewCanvas") is Canvas.EventViewCanvas eventViewCanvas)
            {
                eventViewCanvas.InvalidateVisual();
            }

            if (this.FindControl<Canvas.VelocityViewCanvas>("VelocityViewCanvas") is Canvas.VelocityViewCanvas velocityViewCanvas)
            {
                velocityViewCanvas.InvalidateVisual();
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            EnderLogger.Instance.Info("VisualTree", "[EventViewPanel] 从可视树分离。");

            if (this.FindControl<TextBox>("CCNumberTextBox") is TextBox ccNumberTextBox)
            {
                ccNumberTextBox.LostFocus -= OnCCNumberTextBoxLostFocus;
                ccNumberTextBox.KeyDown -= OnCCNumberTextBoxKeyDown;
                EnderLogger.Instance.Info("EventUnregistration", "[EventViewPanel] 已注销 CCNumberTextBox 的事件。");
            }

            // ����Ⱦͬ������ע��
            _renderSyncService.UnregisterTarget(this);
            EnderLogger.Instance.Info("RenderSync", "[EventViewPanel] 已从渲染同步服务注销。");

            base.OnDetachedFromVisualTree(e);
        }
    }
}