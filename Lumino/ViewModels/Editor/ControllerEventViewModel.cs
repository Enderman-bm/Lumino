using CommunityToolkit.Mvvm.ComponentModel;
using Lumino.Models.Music;
using System;

namespace Lumino.ViewModels.Editor
{
    public partial class ControllerEventViewModel : ViewModelBase
    {
        [ObservableProperty]
        private Guid _id = Guid.NewGuid();

        [ObservableProperty]
        private int _trackIndex;

        [ObservableProperty]
        private int _controllerNumber;

        [ObservableProperty]
        private MusicalFraction _time;

        [ObservableProperty]
        private int _value;

        public ControllerEvent ToControllerEvent()
        {
            return new ControllerEvent
            {
                Id = Id,
                TrackIndex = TrackIndex,
                ControllerNumber = ControllerNumber,
                Time = Time,
                Value = Value
            };
        }

        public static ControllerEventViewModel FromModel(ControllerEvent model)
        {
            return new ControllerEventViewModel
            {
                Id = model.Id,
                TrackIndex = model.TrackIndex,
                ControllerNumber = model.ControllerNumber,
                Time = model.Time,
                Value = model.Value
            };
        }
    }
}
