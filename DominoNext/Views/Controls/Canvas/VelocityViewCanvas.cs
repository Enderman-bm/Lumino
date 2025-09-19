using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Services.Interfaces;
using Lumino.Services.Implementation;
using Lumino.Views.Rendering.Utils;
using Lumino.Views.Rendering.Events;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace Lumino.Views.Controls.Canvas
{
    /// <summary>
    /// ������ͼ���� - ��ʾ�ͱ༭�������ȣ�֧�ֶ�̬����ͺ�̨Ԥ����
    /// </summary>
    public class VelocityViewCanvas : Control, IRenderSyncTarget
    {
        public static readonly StyledProperty<PianoRollViewModel?> ViewModelProperty =
            AvaloniaProperty.Register<VelocityViewCanvas, PianoRollViewModel?>(nameof(ViewModel));

        public PianoRollViewModel? ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        private readonly VelocityBarRenderer _velocityRenderer;
        private readonly IRenderSyncService _renderSyncService;

        // ���滭ˢʵ����ȷ����Ⱦһ����
        private readonly IBrush _backgroundBrush;
        private readonly IPen _gridLinePen;

        // �����Ż����
        private DateTime _lastPrecomputeTime = DateTime.MinValue;
        private readonly TimeSpan _precomputeInterval = TimeSpan.FromMilliseconds(500); // Ԥ������
        private volatile bool _precomputeScheduled = false;

        public VelocityViewCanvas()
        {
            _velocityRenderer = new VelocityBarRenderer();
            
            // ע�ᵽ��Ⱦͬ������
            _renderSyncService = RenderSyncService.Instance;
            _renderSyncService.RegisterTarget(this);
            
            // ��������¼�
            IsHitTestVisible = true;

            // ��ʼ�����滭ˢ
            _backgroundBrush = RenderingUtils.GetResourceBrush("VelocityViewBackgroundBrush", "#20000000");
            _gridLinePen = RenderingUtils.GetResourcePen("VelocityGridLineBrush", "#30808080", 1);

            // ���ú�̨Ԥ���㣨���ڴ����ݼ���
            _velocityRenderer.SetBackgroundPrecomputationEnabled(true);
            _velocityRenderer.SetPrecomputationThreshold(500); // ����500������ʱ����
        }

        static VelocityViewCanvas()
        {
            ViewModelProperty.Changed.AddClassHandler<VelocityViewCanvas>((canvas, e) =>
            {
                if (e.OldValue is PianoRollViewModel oldVm)
                {
                    canvas.UnsubscribeFromViewModel(oldVm);
                }

                if (e.NewValue is PianoRollViewModel newVm)
                {
                    canvas.SubscribeToViewModel(newVm);
                }

                // ���Ԥ���㻺�棬��ΪViewModel�ѱ��
                canvas._velocityRenderer.ClearPrecomputedCache();
                canvas.InvalidateVisual();
            });
        }

        private void SubscribeToViewModel(PianoRollViewModel viewModel)
        {
            // ����ViewModel���Ա仯
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            
            // �����������ϱ仯
            if (viewModel.Notes is INotifyCollectionChanged notesCollection)
            {
                notesCollection.CollectionChanged += OnNotesCollectionChanged;
            }
            
            // ������ǰ����������ϱ仯
            if (viewModel.CurrentTrackNotes is INotifyCollectionChanged currentTrackNotesCollection)
            {
                currentTrackNotesCollection.CollectionChanged += OnCurrentTrackNotesCollectionChanged;
            }

            // ����ÿ�����������Ա仯
            foreach (var note in viewModel.CurrentTrackNotes)
            {
                note.PropertyChanged += OnNotePropertyChanged;
            }

            // �������ȱ༭ģ���¼�
            if (viewModel.VelocityEditingModule != null)
            {
                viewModel.VelocityEditingModule.OnVelocityUpdated += OnVelocityUpdated;
            }

            // ������ʼԤ����
            SchedulePrecompute();
        }

        private void UnsubscribeFromViewModel(PianoRollViewModel viewModel)
        {
            // ȡ������ViewModel���Ա仯
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            
            // ȡ�������������ϱ仯
            if (viewModel.Notes is INotifyCollectionChanged notesCollection)
            {
                notesCollection.CollectionChanged -= OnNotesCollectionChanged;
            }
            
            // ȡ��������ǰ����������ϱ仯
            if (viewModel.CurrentTrackNotes is INotifyCollectionChanged currentTrackNotesCollection)
            {
                currentTrackNotesCollection.CollectionChanged -= OnCurrentTrackNotesCollectionChanged;
            }

            // ȡ������ÿ�����������Ա仯
            foreach (var note in viewModel.CurrentTrackNotes)
            {
                note.PropertyChanged -= OnNotePropertyChanged;
            }

            // ȡ���������ȱ༭ģ���¼�
            if (viewModel.VelocityEditingModule != null)
            {
                viewModel.VelocityEditingModule.OnVelocityUpdated -= OnVelocityUpdated;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PianoRollViewModel.Zoom) ||
                e.PropertyName == nameof(PianoRollViewModel.VerticalZoom))
            {
                // ���ű仯ʱ���Ԥ���㻺�沢����Ԥ����
                _velocityRenderer.ClearPrecomputedCache();
                SchedulePrecompute();
                _renderSyncService.SyncRefresh();
            }
            else if (e.PropertyName == nameof(PianoRollViewModel.TimelinePosition) ||
                     e.PropertyName == nameof(PianoRollViewModel.CurrentScrollOffset))
            {
                // ����ʱ���Ԥ���㻺�沢����Ԥ����
                _velocityRenderer.ClearPrecomputedCache();
                SchedulePrecompute();
                _renderSyncService.SyncRefresh();
            }
            else if (e.PropertyName == nameof(PianoRollViewModel.CurrentTrackIndex))
            {
                // ����л�ʱ�������
                _velocityRenderer.ClearPrecomputedCache();
                SchedulePrecompute();
                _renderSyncService.SyncRefresh();
            }
        }

        private void OnNotesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // �������Ϸ����仯ʱ��Ҫ�����¼�����
            if (e.OldItems != null)
            {
                foreach (NoteViewModel note in e.OldItems)
                {
                    note.PropertyChanged -= OnNotePropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (NoteViewModel note in e.NewItems)
                {
                    note.PropertyChanged += OnNotePropertyChanged;
                }
            }

            // ���Ԥ���㻺�沢����Ԥ����
            _velocityRenderer.ClearPrecomputedCache();
            SchedulePrecompute();
            _renderSyncService.SyncRefresh();
        }

        private void OnCurrentTrackNotesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // ��ǰ����������Ϸ����仯ʱ��Ҫ�����¼�����
            if (e.OldItems != null)
            {
                foreach (NoteViewModel note in e.OldItems)
                {
                    note.PropertyChanged -= OnNotePropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (NoteViewModel note in e.NewItems)
                {
                    note.PropertyChanged += OnNotePropertyChanged;
                }
            }

            // ���Ԥ���㻺�沢����Ԥ����
            _velocityRenderer.ClearPrecomputedCache();
            SchedulePrecompute();
            _renderSyncService.SyncRefresh();
        }

        private void OnNotePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // �κ��������Է����仯ʱ��ˢ��������ͼ
            if (e.PropertyName == nameof(NoteViewModel.Velocity) ||
                e.PropertyName == nameof(NoteViewModel.StartPosition) ||
                e.PropertyName == nameof(NoteViewModel.Duration) ||
                e.PropertyName == nameof(NoteViewModel.Pitch) ||
                e.PropertyName == nameof(NoteViewModel.IsSelected))
            {
                // ֻ��Ӱ����Ⱦ�����Ա仯ʱ���������
                if (e.PropertyName == nameof(NoteViewModel.Velocity) ||
                    e.PropertyName == nameof(NoteViewModel.StartPosition) ||
                    e.PropertyName == nameof(NoteViewModel.Duration))
                {
                    _velocityRenderer.ClearPrecomputedCache();
                    SchedulePrecompute();
                }
                
                _renderSyncService.SyncRefresh();
            }
        }

        private void OnVelocityUpdated()
        {
            // ���ȸ���ʱ����ˢ�£��������ȫ�����棨��Ϊ��������������£�
            _renderSyncService.SyncRefresh();
        }

        /// <summary>
        /// ���Ⱥ�̨Ԥ��������
        /// </summary>
        private void SchedulePrecompute()
        {
            if (_precomputeScheduled) return;
            
            var now = DateTime.Now;
            if (now - _lastPrecomputeTime < _precomputeInterval) return;

            _precomputeScheduled = true;
            
            // ʹ��Dispatcher�ӳ�ִ�У�������UI�����ڼ����
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    await Task.Delay(100); // �����ӳ�ȷ��UI�������
                    await PerformPrecompute();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ԥ��������ʧ��: {ex.Message}");
                }
                finally
                {
                    _precomputeScheduled = false;
                    _lastPrecomputeTime = DateTime.Now;
                }
            });
        }

        /// <summary>
        /// ִ�к�̨Ԥ����
        /// </summary>
        private async Task PerformPrecompute()
        {
            if (ViewModel?.CurrentTrackNotes == null || double.IsNaN(Bounds.Width) || double.IsNaN(Bounds.Height))
                return;

            var bounds = Bounds;
            var scrollOffset = ViewModel.CurrentScrollOffset;
            var timeToPixelScale = ViewModel.TimeToPixelScale;
            var notes = ViewModel.CurrentTrackNotes.ToList(); // �������ձ��⼯���޸�

            await _velocityRenderer.PrecomputeVelocityBarsAsync(notes, bounds, timeToPixelScale, scrollOffset);
        }

        public override void Render(DrawingContext context)
        {
            if (ViewModel == null) return;

            var bounds = Bounds;
            
            // �������ȱ༭ģ��Ļ����߶�
            if (ViewModel.VelocityEditingModule != null)
            {
                ViewModel.VelocityEditingModule.SetCanvasHeight(bounds.Height);
            }
            
            // ���Ʊ���
            context.DrawRectangle(_backgroundBrush, null, bounds);

            // ����������
            DrawVelocityBars(context, bounds);
            
            // ���������ߣ���ѡ��
            DrawGridLines(context, bounds);
        }

        private void DrawVelocityBars(DrawingContext context, Rect bounds)
        {
            if (ViewModel?.CurrentTrackNotes == null) return;

            var scrollOffset = ViewModel.CurrentScrollOffset;
            var noteCount = ViewModel.CurrentTrackNotes.Count;

            // ���ڴ���������ֻ��Ⱦ�ɼ������ڵ���������������
            var visibleNotes = noteCount > 1000 
                ? GetVisibleNotes(ViewModel.CurrentTrackNotes.AsEnumerable(), bounds, scrollOffset)
                : ViewModel.CurrentTrackNotes.AsEnumerable();

            foreach (var note in visibleNotes)
            {
                // ȷ����Ⱦ����
                var renderType = GetVelocityRenderType(note);
                
                _velocityRenderer.DrawVelocityBar(context, note, bounds, 
                    ViewModel.TimeToPixelScale, renderType, scrollOffset);
            }

            // ��Ⱦ���ڱ༭������Ԥ��
            if (ViewModel.VelocityEditingModule?.IsEditingVelocity == true)
            {
                _velocityRenderer.DrawEditingPreview(context, bounds, 
                    ViewModel.VelocityEditingModule, ViewModel.TimeToPixelScale, scrollOffset);
            }
        }

        /// <summary>
        /// ��ȡ�ɼ������ڵ������������Ż���
        /// </summary>
        private IEnumerable<NoteViewModel> GetVisibleNotes(IEnumerable<NoteViewModel> notes, Rect bounds, double scrollOffset)
        {
            var visibleTimeStart = scrollOffset / ViewModel!.TimeToPixelScale;
            var visibleTimeEnd = (scrollOffset + bounds.Width) / ViewModel.TimeToPixelScale;

            return notes.Where(note =>
            {
                var noteStart = note.StartPosition.ToDouble();
                var noteEnd = noteStart + note.Duration.ToDouble();
                return noteEnd >= visibleTimeStart && noteStart <= visibleTimeEnd;
            });
        }

        private VelocityRenderType GetVelocityRenderType(NoteViewModel note)
        {
            if (ViewModel?.VelocityEditingModule?.EditingNotes?.Contains(note) == true)
                return VelocityRenderType.Editing;
            
            if (note.IsSelected)
                return VelocityRenderType.Selected;
                
            if (ViewModel?.DragState?.DraggingNotes?.Contains(note) == true)
                return VelocityRenderType.Dragging;
                
            return VelocityRenderType.Normal;
        }

        private void DrawGridLines(DrawingContext context, Rect bounds)
        {
            if (ViewModel == null) return;

            // ����ˮƽ�ο��� (25%, 50%, 75%, 100%)
            var quarterHeight = bounds.Height / 4.0;
            for (int i = 1; i <= 3; i++)
            {
                var y = bounds.Height - (i * quarterHeight);
                context.DrawLine(_gridLinePen, new Point(0, y), new Point(bounds.Width, y));
            }
        }

        #region �û��¼�����

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (ViewModel?.VelocityEditingModule == null) return;

            var position = e.GetPosition(this);
            var properties = e.GetCurrentPoint(this).Properties;

            if (properties.IsLeftButtonPressed)
            {
                // ��Ļ����ת��Ϊ��������
                var worldPosition = new Point(
                    position.X + ViewModel.CurrentScrollOffset,
                    position.Y
                );
                
                ViewModel.VelocityEditingModule.StartEditing(worldPosition);
                e.Handled = true;
            }

            base.OnPointerPressed(e);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            if (ViewModel?.VelocityEditingModule == null) return;

            var position = e.GetPosition(this);
            
            // ֻ�ڱ༭ʱ�����ƶ��¼�
            if (ViewModel.VelocityEditingModule.IsEditingVelocity)
            {
                // ����λ���ڻ�����Χ��
                var clampedPosition = new Point(
                    Math.Max(0, Math.Min(Bounds.Width, position.X)),
                    Math.Max(0, Math.Min(Bounds.Height, position.Y))
                );
                
                // ��Ļ����ת��Ϊ��������
                var worldPosition = new Point(
                    clampedPosition.X + ViewModel.CurrentScrollOffset,
                    clampedPosition.Y
                );
                
                ViewModel.VelocityEditingModule.UpdateEditing(worldPosition);
            }

            base.OnPointerMoved(e);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (ViewModel?.VelocityEditingModule == null) return;
            
            ViewModel.VelocityEditingModule.EndEditing();
            e.Handled = true;

            base.OnPointerReleased(e);
        }

        #endregion

        #region IRenderSyncTarget�ӿ�ʵ��

        /// <summary>
        /// ʵ��IRenderSyncTarget�ӿ�
        /// </summary>
        public void RefreshRender()
        {
            InvalidateVisual();
        }

        #endregion

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            // ����Ⱦͬ������ע��
            _renderSyncService.UnregisterTarget(this);
            
            // ������л����ͷ��ڴ�
            _velocityRenderer.ClearAllCaches();
            
            base.OnDetachedFromVisualTree(e);
        }

        #region �������

        /// <summary>
        /// ��ȡ����ͳ����Ϣ�������ã�
        /// </summary>
        public string GetPerformanceStatistics()
        {
            var noteCount = ViewModel?.CurrentTrackNotes?.Count ?? 0;
            var cacheStats = _velocityRenderer.GetCacheStatistics();
            return $"��������: {noteCount}, {cacheStats}";
        }

        #endregion
    }
}