using System;

namespace Lumino.Models.Music
{
    public class ControllerEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int TrackIndex { get; set; }
        public int ControllerNumber { get; set; }
        public MusicalFraction Time { get; set; }
        public int Value { get; set; }
    }
}
