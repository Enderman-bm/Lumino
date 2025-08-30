using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using System;
using System.ComponentModel;
using DominoNext.Services.Interfaces;
using DominoNext.ViewModels.Editor;

namespace DominoNext.Views
{
    public partial class PianoRollView : UserControl
    {
        private bool _isUpdatingScroll = false;
        private ISettingsService? _settingsService;

        public PianoRollView()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
            
            // Subscribe to theme changes
            SubscribeToThemeChanges();
        }

        private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Subscribe to main scroll viewer scroll events
            if (this.FindControl<ScrollViewer>("MainScrollViewer") is ScrollViewer mainScrollViewer)
            {
                mainScrollViewer.ScrollChanged += OnMainScrollViewerScrollChanged;
            }

            // Subscribe to piano keys scroll viewer scroll events
            if (this.FindControl<ScrollViewer>("PianoKeysScrollViewer") is ScrollViewer pianoKeysScrollViewer)
            {
                pianoKeysScrollViewer.ScrollChanged += OnPianoKeysScrollViewerScrollChanged;
            }

            // Subscribe to event view scroll viewer scroll events
            if (this.FindControl<ScrollViewer>("EventViewScrollViewer") is ScrollViewer eventViewScrollViewer)
            {
                eventViewScrollViewer.ScrollChanged += OnEventViewScrollViewerScrollChanged;
            }

            // Subscribe to horizontal scroll bar value changed events
            if (this.FindControl<ScrollBar>("HorizontalScrollBar") is ScrollBar horizontalScrollBar)
            {
                horizontalScrollBar.ValueChanged += OnHorizontalScrollBarValueChanged;
            }

            // Subscribe to vertical scroll bar value changed events
            if (this.FindControl<ScrollBar>("VerticalScrollBar") is ScrollBar verticalScrollBar)
            {
                verticalScrollBar.ValueChanged += OnVerticalScrollBarValueChanged;
            }
        }

        /// <summary>
        /// Subscribe to theme changes
        /// </summary>
        private void SubscribeToThemeChanges()
        {
            try
            {
                // Get settings service instance from resource dictionary
                _settingsService = GetSettingsService();
                
                if (_settingsService != null)
                {
                    _settingsService.SettingsChanged += OnSettingsChanged;
                }

                // Subscribe to application property changed events
                if (Application.Current != null)
                {
                    Application.Current.PropertyChanged += OnApplicationPropertyChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to subscribe to theme changes: {ex.Message}");
            }
        }

        /// <summary>
        /// Get settings service instance
        /// </summary>
        private ISettingsService? GetSettingsService()
        {
            try
            {
                // Get settings service instance from resource dictionary
                // Use default implementation if it doesn't exist
                return new DominoNext.Services.Implementation.SettingsService();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Handle settings changed event
        /// </summary>
        private void OnSettingsChanged(object? sender, DominoNext.Services.Interfaces.SettingsChangedEventArgs e)
        {
            try
            {
                // Force refresh view when color settings or theme changes
                if (e.PropertyName?.EndsWith("Color") == true || e.PropertyName == "Theme")
                {
                    Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ForceRefreshTheme();
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to handle settings change: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle application property changed event
        /// </summary>
        private void OnApplicationPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            try
            {
                if (e.Property.Name == nameof(Application.RequestedThemeVariant))
                {
                    Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ForceRefreshTheme();
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to handle application property change: {ex.Message}");
            }
        }

        /// <summary>
        /// Force refresh view - redraw all controls
        /// </summary>
        private void ForceRefreshTheme()
        {
            try
            {
                // Force redraw
                this.InvalidateVisual();
                
                // Force re-measure and arrange
                this.InvalidateMeasure();
                this.InvalidateArrange();

                // Refresh custom canvas controls
                RefreshCustomCanvasControls();
                
                // Refresh all child controls
                RefreshChildControls(this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to force refresh view: {ex.Message}");
            }
        }

        /// <summary>
        /// Refresh custom canvas controls
        /// </summary>
        private void RefreshCustomCanvasControls()
        {
            try
            {
                // Refresh piano roll canvas
                if (this.FindControl<DominoNext.Views.Controls.Canvas.PianoRollCanvas>("PianoRollCanvas") is var pianoRollCanvas && pianoRollCanvas != null)
                {
                    pianoRollCanvas.InvalidateVisual();
                }

                // Refresh measure header canvas
                if (this.FindControl<DominoNext.Views.Controls.Canvas.MeasureHeaderCanvas>("MeasureHeaderCanvas") is var measureHeaderCanvas && measureHeaderCanvas != null)
                {
                    measureHeaderCanvas.InvalidateVisual();
                }

                // Refresh event view canvas
                if (this.FindControl<DominoNext.Views.Controls.Canvas.EventViewCanvas>("EventViewCanvas") is var eventViewCanvas && eventViewCanvas != null)
                {
                    eventViewCanvas.InvalidateVisual();
                }

                // Refresh velocity view canvas
                if (this.FindControl<DominoNext.Views.Controls.Canvas.VelocityViewCanvas>("VelocityViewCanvas") is var velocityViewCanvas && velocityViewCanvas != null)
                {
                    velocityViewCanvas.InvalidateVisual();
                }

                // Refresh piano keys control
                if (this.FindControl<DominoNext.Views.Controls.PianoKeysControl>("PianoKeysControl") is var pianoKeysControl && pianoKeysControl != null)
                {
                    pianoKeysControl.InvalidateVisual();
                }

                // Refresh note editing layer
                if (this.FindControl<DominoNext.Views.Controls.Editing.NoteEditingLayer>("NoteEditingLayer") is var noteEditingLayer && noteEditingLayer != null)
                {
                    noteEditingLayer.InvalidateVisual();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to refresh custom canvas controls: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively refresh all child controls
        /// </summary>
        private void RefreshChildControls(Control control)
        {
            try
            {
                // Force redraw
                control.InvalidateVisual();
                
                // Force re-measure and arrange
                control.InvalidateMeasure();
                control.InvalidateArrange();

                // Recursively refresh child controls
                if (control is Panel panel)
                {
                    foreach (Control child in panel.Children)
                    {
                        RefreshChildControls(child);
                    }
                }
                else if (control is ContentControl contentControl && contentControl.Content is Control childControl)
                {
                    RefreshChildControls(childControl);
                }
                else if (control is ScrollViewer scrollViewer && scrollViewer.Content is Control scrollContent)
                {
                    RefreshChildControls(scrollContent);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to refresh child controls: {ex.Message}");
            }
        }

        private void OnMainScrollViewerScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            if (_isUpdatingScroll) return;

            try
            {
                _isUpdatingScroll = true;

                // Check if near end, auto extend measures if so
                if (sender is ScrollViewer mainScrollViewer)
                {
                    var horizontalOffset = mainScrollViewer.Offset.X;
                    var viewportWidth = mainScrollViewer.Viewport.Width;
                    var extentWidth = mainScrollViewer.Extent.Width;

                    // Auto add 10 measures when scrolled to 90% end
                    if (horizontalOffset + viewportWidth >= extentWidth * 0.9)
                    {
                        if (DataContext is PianoRollViewModel pianoRoll)
                        {
                            pianoRoll.TotalMeasures += 10;
                        }
                    }
                }

                // Sync measure header horizontal scroll
                SyncMeasureHeaderScroll();

                // Sync event view horizontal scroll
                SyncEventViewScroll();

                // Sync piano keys vertical scroll
                SyncPianoKeysScroll();
            }
            finally
            {
                _isUpdatingScroll = false;
            }
        }

        private void OnPianoKeysScrollViewerScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            if (_isUpdatingScroll) return;

            try
            {
                _isUpdatingScroll = true;

                // Sync main view vertical scroll
                if (this.FindControl<ScrollViewer>("MainScrollViewer") is ScrollViewer mainScrollViewer &&
                    sender is ScrollViewer pianoKeysScrollViewer)
                {
                    var newOffset = new Avalonia.Vector(mainScrollViewer.Offset.X, pianoKeysScrollViewer.Offset.Y);
                    mainScrollViewer.Offset = newOffset;
                }
            }
            finally
            {
                _isUpdatingScroll = false;
            }
        }

        private void OnEventViewScrollViewerScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            if (_isUpdatingScroll) return;

            try
            {
                _isUpdatingScroll = true;

                // Sync main view horizontal scroll
                if (this.FindControl<ScrollViewer>("MainScrollViewer") is ScrollViewer mainScrollViewer &&
                    sender is ScrollViewer eventViewScrollViewer)
                {
                    var newOffset = new Avalonia.Vector(eventViewScrollViewer.Offset.X, mainScrollViewer.Offset.Y);
                    mainScrollViewer.Offset = newOffset;
                }

                // Sync measure header horizontal scroll
                SyncMeasureHeaderScroll();
            }
            finally
            {
                _isUpdatingScroll = false;
            }
        }

        private void OnHorizontalScrollBarValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdatingScroll) return;

            try
            {
                _isUpdatingScroll = true;

                // Update main view horizontal offset when horizontal scroll bar value changes
                if (this.FindControl<ScrollViewer>("MainScrollViewer") is ScrollViewer mainScrollViewer &&
                    sender is ScrollBar scrollBar)
                {
                    var newOffset = new Avalonia.Vector(scrollBar.Value, mainScrollViewer.Offset.Y);
                    mainScrollViewer.Offset = newOffset;
                }

                SyncMeasureHeaderScroll();
                SyncEventViewScroll();
            }
            finally
            {
                _isUpdatingScroll = false;
            }
        }

        private void OnVerticalScrollBarValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdatingScroll) return;

            try
            {
                _isUpdatingScroll = true;

                // Update main view vertical offset when vertical scroll bar value changes
                if (this.FindControl<ScrollViewer>("MainScrollViewer") is ScrollViewer mainScrollViewer &&
                    sender is ScrollBar scrollBar)
                {
                    var newOffset = new Avalonia.Vector(mainScrollViewer.Offset.X, scrollBar.Value);
                    mainScrollViewer.Offset = newOffset;
                }

                SyncPianoKeysScroll();
            }
            finally
            {
                _isUpdatingScroll = false;
            }
        }

        private void SyncMeasureHeaderScroll()
        {
            if (this.FindControl<ScrollViewer>("MainScrollViewer") is ScrollViewer mainScrollViewer &&
                this.FindControl<ScrollViewer>("MeasureHeaderScrollViewer") is ScrollViewer measureHeaderScrollViewer)
            {
                measureHeaderScrollViewer.Offset = new Avalonia.Vector(mainScrollViewer.Offset.X, 0);
            }
        }

        private void SyncEventViewScroll()
        {
            if (this.FindControl<ScrollViewer>("MainScrollViewer") is ScrollViewer mainScrollViewer &&
                this.FindControl<ScrollViewer>("EventViewScrollViewer") is ScrollViewer eventViewScrollViewer)
            {
                eventViewScrollViewer.Offset = new Avalonia.Vector(mainScrollViewer.Offset.X, 0);
            }
        }

        private void SyncPianoKeysScroll()
        {
            if (this.FindControl<ScrollViewer>("MainScrollViewer") is ScrollViewer mainScrollViewer &&
                this.FindControl<ScrollViewer>("PianoKeysScrollViewer") is ScrollViewer pianoKeysScrollViewer)
            {
                pianoKeysScrollViewer.Offset = new Avalonia.Vector(0, mainScrollViewer.Offset.Y);
            }
        }

        /// <summary>
        /// Detach resources
        /// </summary>
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            try
            {
                // Unsubscribe from settings changes
                if (_settingsService != null)
                {
                    _settingsService.SettingsChanged -= OnSettingsChanged;
                }

                if (Application.Current != null)
                {
                    Application.Current.PropertyChanged -= OnApplicationPropertyChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to detach resources: {ex.Message}");
            }

            base.OnDetachedFromVisualTree(e);
        }
    }
}