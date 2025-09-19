using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Lumino.ViewModels.Editor;
using Lumino.Services.Interfaces;
using Lumino.Services.Implementation;
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

        public PianoRollViewModel? ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public bool IsEventViewVisible
        {
            get => GetValue(IsEventViewVisibleProperty);
            set => SetValue(IsEventViewVisibleProperty, value);
        }

        public EventViewPanel()
        {
            InitializeComponent();
            
            // ע�ᵽ��Ⱦͬ������
            _renderSyncService = RenderSyncService.Instance;
            _renderSyncService.RegisterTarget(this);
            
            // �������Ա仯 - ʹ����ȷ���¼����ķ�ʽ
            this.PropertyChanged += OnPropertyChanged;
            
            // �����ؼ��������¼�������CC���������¼�����
            this.AttachedToVisualTree += OnAttachedToVisualTree;
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // �ҵ�CC������򲢰��¼�
            if (this.FindControl<TextBox>("CCNumberTextBox") is TextBox ccNumberTextBox)
            {
                ccNumberTextBox.LostFocus += OnCCNumberTextBoxLostFocus;
                ccNumberTextBox.KeyDown += OnCCNumberTextBoxKeyDown;
            }
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ViewModelProperty && e.NewValue is PianoRollViewModel viewModel)
            {
                DataContext = viewModel;
            }
            else if (e.Property == IsEventViewVisibleProperty && e.NewValue is bool isVisible)
            {
                this.IsVisible = isVisible;
            }
        }

        /// <summary>
        /// CC�������ʧȥ�����¼�����
        /// </summary>
        private void OnCCNumberTextBoxLostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && ViewModel != null)
            {
                ValidateCCNumber(textBox.Text);
            }
        }

        /// <summary>
        /// CC������򰴼��¼�����
        /// </summary>
        private void OnCCNumberTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && ViewModel != null)
            {
                // ֻ������������
                if (e.Key >= Key.D0 && e.Key <= Key.D9 || 
                    e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9 ||
                    e.Key == Key.Back || e.Key == Key.Delete ||
                    e.Key == Key.Left || e.Key == Key.Right ||
                    e.Key == Key.Tab || e.Key == Key.Enter)
                {
                    if (e.Key == Key.Enter)
                    {
                        ValidateCCNumber(textBox.Text);
                        e.Handled = true;
                    }
                    // ������Щ����
                }
                else
                {
                    // ��ֹ��������
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// ��֤CC������
        /// </summary>
        private void ValidateCCNumber(string input)
        {
            if (ViewModel == null) return;

            if (int.TryParse(input, out int ccNumber))
            {
                ccNumber = Math.Max(0, Math.Min(127, ccNumber));
                ViewModel.CurrentCCNumber = ccNumber;
                
                // ����TextBox��ʾ��ȷ��ֵ
                if (this.FindControl<TextBox>("CCNumberTextBox") is TextBox textBox)
                {
                    textBox.Text = ccNumber.ToString();
                }
            }
            else
            {
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
            // ����¼�������
            if (this.FindControl<TextBox>("CCNumberTextBox") is TextBox ccNumberTextBox)
            {
                ccNumberTextBox.LostFocus -= OnCCNumberTextBoxLostFocus;
                ccNumberTextBox.KeyDown -= OnCCNumberTextBoxKeyDown;
            }
            
            // ����Ⱦͬ������ע��
            _renderSyncService.UnregisterTarget(this);
            base.OnDetachedFromVisualTree(e);
        }
    }
}