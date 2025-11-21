using System;
using System.Collections.Generic;

namespace ImageToMidi.Logic.Midi
{
    public struct Note
    {
        public uint Start;
        public uint Length;
        public byte Key;
        public byte Velocity;
        public byte Channel;

        public uint End => Start + Length;
    }

    public class ExtractNotes : IEnumerable<Note>
    {
        private readonly FastList<MIDIEvent> _events;

        public ExtractNotes(FastList<MIDIEvent> events)
        {
            _events = events;
        }

        public IEnumerator<Note> GetEnumerator()
        {
            // This is a simplified extractor. 
            // It assumes events are sorted by time (which they are in ConversionProcess)
            // But ConversionProcess stores DeltaTime.
            // Wait, ConversionProcess stores DeltaTime relative to previous event in the same track?
            // Yes: EventBuffers[newc].Add(new NoteOnEvent((uint)(time - lastTimes[newc]), ...));
            // So we need to accumulate time.
            
            // Also, we need to pair NoteOn and NoteOff.
            // Since it's a single track per color, we can track active notes.
            // But wait, multiple keys can be active in the same track (same color)?
            // Yes, different keys.
            
            var activeNotes = new Dictionary<byte, uint>(); // Key -> StartTime
            uint currentTime = 0;

            foreach (var evt in _events)
            {
                currentTime += evt.DeltaTime;

                if (evt is NoteOnEvent on)
                {
                    if (on.Velocity > 0)
                    {
                        // Note On
                        if (activeNotes.ContainsKey(on.Key))
                        {
                            // Already on? Maybe retrigger or error. 
                            // Let's close previous note and start new one.
                            uint start = activeNotes[on.Key];
                            yield return new Note
                            {
                                Start = start,
                                Length = currentTime - start,
                                Key = on.Key,
                                Velocity = on.Velocity, // Use previous velocity? Or current?
                                Channel = on.Channel
                            };
                            activeNotes[on.Key] = currentTime;
                        }
                        else
                        {
                            activeNotes[on.Key] = currentTime;
                        }
                    }
                    else
                    {
                        // Note On with velocity 0 = Note Off
                        if (activeNotes.TryGetValue(on.Key, out uint start))
                        {
                            yield return new Note
                            {
                                Start = start,
                                Length = currentTime - start,
                                Key = on.Key,
                                Velocity = 0, // Off
                                Channel = on.Channel
                            };
                            activeNotes.Remove(on.Key);
                        }
                    }
                }
                else if (evt is NoteOffEvent off)
                {
                    if (activeNotes.TryGetValue(off.Key, out uint start))
                    {
                        yield return new Note
                        {
                            Start = start,
                            Length = currentTime - start,
                            Key = off.Key,
                            Velocity = off.Velocity,
                            Channel = off.Channel
                        };
                        activeNotes.Remove(off.Key);
                    }
                }
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
