using Avalonia;
using Avalonia.Controls;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using DominoNext.ViewModels;
using DominoNext.ViewModels.Editor;

namespace DominoNext.Views.Controls
{
    public partial class MidiEventView : UserControl
    {
        public static readonly StyledProperty<PianoRollViewModel?> PianoRollProperty =
            AvaloniaProperty.Register<MidiEventView, PianoRollViewModel?>(nameof(PianoRoll));

        public PianoRollViewModel? PianoRoll
        {
            get => GetValue(PianoRollProperty);
            set => SetValue(PianoRollProperty, value);
        }

        public MidiEventView()
        {
            InitializeComponent();
            
            // 监听PianoRoll属性变化
            PropertyChanged += MidiEventView_PropertyChanged;
        }

        private void MidiEventView_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == PianoRollProperty)
            {
                if (e.OldValue is PianoRollViewModel oldPianoRoll)
                {
                    oldPianoRoll.MidiEvents.CollectionChanged -= MidiEvents_CollectionChanged;
                }

                if (e.NewValue is PianoRollViewModel newPianoRoll)
                {
                    newPianoRoll.MidiEvents.CollectionChanged += MidiEvents_CollectionChanged;
                    EventsDataGrid.ItemsSource = newPianoRoll.MidiEvents;
                }
            }
        }

        private void MidiEvents_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 当MIDI事件集合发生变化时，刷新DataGrid
            if (PianoRoll != null)
            {
                EventsDataGrid.ItemsSource = null;
                EventsDataGrid.ItemsSource = PianoRoll.MidiEvents;
            }
        }
    }
}