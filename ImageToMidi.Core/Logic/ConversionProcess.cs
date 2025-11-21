using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using ImageToMidi.Logic.Clusterisation;
using ImageToMidi.Logic.Midi;
using ImageToMidi.Models;
using ImageToMidi.Core.Logic;

namespace ImageToMidi.Logic
{
    public class ConversionProcess : IDisposable
    {
        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Clean up managed resources
                    // Image is byte[] now, so no Dispose needed unless we wrap it.
                    // But we might have SKBitmap if we used it.
                    // In this port, we use byte[] mostly.

                    // Clean up large arrays
                    if (resizedImage != null)
                    {
                        // Array.Clear(resizedImage, 0, resizedImage.Length); // Not strictly necessary for GC
                        resizedImage = null;
                    }

                    if (imageData != null)
                    {
                        imageData = null;
                    }

                    // Clean up event buffers
                    if (EventBuffers != null)
                    {
                        for (int i = 0; i < EventBuffers.Length; i++)
                        {
                            if (EventBuffers[i] != null)
                            {
                                EventBuffers[i].Unlink();
                                EventBuffers[i] = null;
                            }
                        }
                    }

                    // Clean up other references
                    Palette = null;
                    keyList = null;
                    // paletteLabCache = null; // We don't use this cache class yet, or need to port it.
                    noteCountPerColor = null;

                    // Cancel tasks
                    ForceCancel();
                }
                disposed = true;
            }
        }

        ~ConversionProcess()
        {
            Dispose(false);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ConversionProcess));
        }

        public BitmapPalette Palette;
        byte[] imageData;
        int imageStride;
        private volatile bool cancelled = false;
        private readonly object cancellationLock = new object();
        private Task currentTask;
        private CancellationTokenSource cancellationTokenSource;

        int maxNoteLength;
        bool measureFromStart;
        bool useMaxNoteLength = false;

        public bool RandomColors = false;
        public int RandomColorSeed = 0;

        int startKey;
        int endKey;

        byte[] resizedImage;

        public int NoteCount { get; private set; }

        // public Bitmap Image { get; private set; } // Removed System.Drawing.Bitmap

        public int EffectiveWidth { get; set; }

        private FastList<MIDIEvent>[] EventBuffers;

        private ResizeAlgorithm resizeAlgorithm = ResizeAlgorithm.AreaResampling;
        private List<int> keyList = null;
        private bool useKeyList = false;
        private bool fixedWidth = false;
        private bool whiteKeyClipped = false;
        private bool blackKeyClipped = false;
        private bool whiteKeyFixed = false;
        private bool blackKeyFixed = false;

        public int targetHeight;
        private int[] noteCountPerColor;

        private ImageToMidi.Core.Logic.GetColorID.PaletteLabCache paletteLabCache;
        private ColorIdMethod colorIdMethod = ColorIdMethod.RGB;

        public bool IsCompleted { get; private set; } = false;

        private bool isProtected = false;
        private readonly object protectionLock = new object();

        public ConversionProcess(
            BitmapPalette palette,
            byte[] imageData,
            int imgStride,
            int startKey,
            int endKey,
            bool measureFromStart,
            int maxNoteLength,
            int targetHeight,
            ResizeAlgorithm resizeAlgorithm,
            List<int> keyList,
            bool whiteKeyFixed = false,
            bool blackKeyFixed = false,
            bool whiteKeyClipped = false,
            bool blackKeyClipped = false,
            ColorIdMethod colorIdMethod = ColorIdMethod.RGB
            )
        {
            if (palette == null)
                throw new ArgumentNullException(nameof(palette), "Palette cannot be null");
            if (palette.Colors == null)
                throw new ArgumentNullException(nameof(palette.Colors), "Palette.Colors cannot be null");
            if (palette.Colors.Count == 0)
                throw new ArgumentException("Palette.Colors cannot be 0");

            this.Palette = palette;
            this.imageData = imageData;
            this.imageStride = imgStride;
            this.startKey = startKey;
            this.endKey = endKey;
            this.measureFromStart = measureFromStart;
            this.maxNoteLength = maxNoteLength;
            this.useMaxNoteLength = maxNoteLength > 0;
            this.targetHeight = targetHeight;
            this.resizeAlgorithm = resizeAlgorithm;
            this.keyList = keyList;
            this.useKeyList = keyList != null;
            this.whiteKeyClipped = whiteKeyClipped;
            this.blackKeyClipped = blackKeyClipped;
            this.whiteKeyFixed = whiteKeyFixed;
            this.blackKeyFixed = blackKeyFixed;
            this.fixedWidth = whiteKeyFixed || blackKeyFixed;
            this.colorIdMethod = colorIdMethod;

            int tracks = Palette.Colors.Count;
            EventBuffers = new FastList<MIDIEvent>[tracks];
            for (int i = 0; i < tracks; i++)
                EventBuffers[i] = new FastList<MIDIEvent>();

            paletteLabCache = new ImageToMidi.Core.Logic.GetColorID.PaletteLabCache(Palette.Colors);
        }

        public Task RunProcessAsync(Action callback, Action<double> progressCallback = null, bool enableProtection = true)
        {
            if (useMaxNoteLength && maxNoteLength <= 0)
                throw new ArgumentException("maxNoteLength must be positive");

            if (keyList != null && keyList.Count < EffectiveWidth)
                throw new ArgumentException("keyList length insufficient");

            lock (cancellationLock)
            {
                cancellationTokenSource?.Cancel();
                cancellationTokenSource = new CancellationTokenSource();

                cancelled = false;
                IsCompleted = false;

                lock (protectionLock)
                {
                    isProtected = enableProtection;
                }
            }

            noteCountPerColor = new int[Palette.Colors.Count];

            var token = cancellationTokenSource.Token;
            currentTask = Task.Run(() =>
            {
                try
                {
                    if (token.IsCancellationRequested && !isProtected)
                        return;

                    int targetWidth = EffectiveWidth;
                    int height = targetHeight;
                    int width = targetWidth;

                    resizedImage = ResizeImage.MakeResizedImage(imageData, imageStride, targetWidth, height, resizeAlgorithm);

                    lock (protectionLock)
                    {
                        if (enableProtection)
                            isProtected = true;
                    }

                    long[] lastTimes = new long[Palette.Colors.Count];
                    long[] lastOnTimes = new long[width];
                    int[] colors = new int[width];
                    long time = 0;

                    for (int i = 0; i < width; i++)
                    {
                        colors[i] = -2;
                        lastOnTimes[i] = useMaxNoteLength ? -maxNoteLength - 1 : 0;
                    }

                    int[,] colorIndices = new int[height, width];

                    Parallel.For(0, height, i =>
                    {
                        if (token.IsCancellationRequested && !isProtected)
                            return;

                        int rowOffset = i * width * 4;
                        for (int j = 0; j < width; j++)
                        {
                            int pixel = rowOffset + j * 4;
                            int r = resizedImage[pixel + 2];
                            int g = resizedImage[pixel + 1];
                            int b = resizedImage[pixel + 0];
                            int a = resizedImage[pixel + 3];
                            if (a < 128)
                                colorIndices[i, j] = -2;
                            else
                            {
                                int id = GetColorID(r, g, b);
                                if (id < 0 || id >= Palette.Colors.Count)
                                    colorIndices[i, j] = -2;
                                else
                                    colorIndices[i, j] = id;
                            }
                        }
                    });

                    for (int i = height - 1; i >= 0; i--)
                    {
                        if (!isProtected && token.IsCancellationRequested)
                            return;

                        for (int j = 0; j < width; j++)
                        {
                            int midiKey;
                            if (fixedWidth)
                            {
                                midiKey = startKey + j;
                                if (whiteKeyFixed && !IsWhiteKey(midiKey)) { colors[j] = -2; continue; }
                                if (blackKeyFixed && IsWhiteKey(midiKey)) { colors[j] = -2; continue; }
                            }
                            else
                            {
                                if (keyList == null || j >= keyList.Count)
                                {
                                    colors[j] = -2;
                                    continue;
                                }
                                midiKey = keyList[j];
                                if (whiteKeyClipped && !IsWhiteKey(midiKey)) { colors[j] = -2; continue; }
                                if (blackKeyClipped && IsWhiteKey(midiKey)) { colors[j] = -2; continue; }
                            }

                            int c = colors[j];
                            int newc = colorIndices[i, j];
                            bool colorChanged = (newc != c);
                            bool newNote = false;

                            if (useMaxNoteLength)
                            {
                                if (measureFromStart)
                                {
                                    long rowFromBottom = height - 1 - i;
                                    newNote = (rowFromBottom > 0) && (rowFromBottom % maxNoteLength == 0);
                                }
                                else
                                {
                                    long timeSinceLastOn = time - lastOnTimes[j];
                                    newNote = timeSinceLastOn >= maxNoteLength;
                                }
                            }

                            if (colorChanged || newNote)
                            {
                                if (c >= 0 && c < EventBuffers.Length)
                                {
                                    EventBuffers[c].Add(new NoteOffEvent((uint)(time - lastTimes[c]), (byte)0, (byte)midiKey));
                                    lastTimes[c] = time;
                                }

                                if (newc >= 0 && newc < EventBuffers.Length)
                                {
                                    EventBuffers[newc].Add(new NoteOnEvent((uint)(time - lastTimes[newc]), (byte)0, (byte)midiKey, 100)); // Velocity 100
                                    lastTimes[newc] = time;
                                    noteCountPerColor[newc]++;
                                }

                                colors[j] = newc;
                                lastOnTimes[j] = time;
                            }
                        }
                        time++;

                        if (progressCallback != null && (i % 32 == 0 || i == 0))
                        {
                            double progress = 1.0 - (double)i / height;
                            progressCallback(progress);
                        }

                        if (!isProtected && (i & 1023) == 0 && token.IsCancellationRequested)
                            return;
                    }

                    for (int j = 0; j < width; j++)
                    {
                        int c = colors[j];
                        int midiKey;
                        if (fixedWidth)
                        {
                            midiKey = startKey + j;
                            if (whiteKeyFixed && !IsWhiteKey(midiKey)) continue;
                            if (blackKeyFixed && IsWhiteKey(midiKey)) continue;
                        }
                        else
                        {
                            if (keyList == null || j >= keyList.Count)
                                continue;
                            midiKey = keyList[j];
                        }
                        if (c >= 0 && c < EventBuffers.Length)
                        {
                            EventBuffers[c].Add(new NoteOffEvent((uint)(time - lastTimes[c]), (byte)0, (byte)midiKey));
                            lastTimes[c] = time;
                        }
                    }

                    CountNotes();
                    progressCallback?.Invoke(1.0);

                    IsCompleted = true;

                    lock (protectionLock)
                    {
                        isProtected = false;
                    }

                    if (!token.IsCancellationRequested && callback != null)
                        callback();
                }
                catch (OperationCanceledException)
                {
                    lock (protectionLock)
                    {
                        isProtected = false;
                    }
                    IsCompleted = false;
                }
                catch (Exception ex)
                {
                    lock (protectionLock)
                    {
                        isProtected = false;
                    }
                    IsCompleted = false;
                    Console.WriteLine($"RunProcessAsync Exception: {ex}");
                }
            }, token);

            return currentTask;
        }

        // Helper for IsWhiteKey
        public static bool IsWhiteKey(int key)
        {
            int k = key % 12;
            return (k == 0 || k == 2 || k == 4 || k == 5 || k == 7 || k == 9 || k == 11);
        }

        // Use the advanced GetColorID logic
        int GetColorID(int r, int g, int b)
        {
            return ImageToMidi.Core.Logic.GetColorID.FindColorID(colorIdMethod, r, g, b, paletteLabCache);
        }

        public void Cancel()
        {
            lock (protectionLock)
            {
                if (isProtected)
                    return;
            }

            lock (cancellationLock)
            {
                cancelled = true;
                cancellationTokenSource?.Cancel();
            }
        }

        public void ForceCancel()
        {
            lock (protectionLock)
            {
                isProtected = false;
            }

            lock (cancellationLock)
            {
                cancelled = true;
                cancellationTokenSource?.Cancel();
            }
        }

        public async Task WaitForCompletionAsync(int timeoutMs = 5000)
        {
            if (currentTask != null)
            {
                try
                {
                    await Task.WhenAny(currentTask, Task.Delay(timeoutMs));
                }
                catch
                {
                }
            }
        }

        private void CountNotes()
        {
            NoteCount = 0;
            for (int i = 0; i < EventBuffers.Length; i++)
            {
                foreach (Note n in new ExtractNotes(EventBuffers[i]))
                {
                    NoteCount++;
                }
            }
        }

        public int GetNoteCountForColor(int colorIndex)
        {
            if (noteCountPerColor == null || colorIndex < 0 || colorIndex >= noteCountPerColor.Length)
                return 0;
            return noteCountPerColor[colorIndex];
        }

        public static void WriteMidi(
            string filename,
            IEnumerable<ConversionProcess> processes,
            int ticksPerPixel,
            int ppq,
            int startOffset,
            int midiBPM,
            bool useColorEvents,
            Action<double> reportProgress = null)
        {
            var processList = processes.ToList();
            if (processList.Count == 0) return;

            int tracks = processList[0].Palette.Colors.Count;
            var palette = processList[0].Palette;

            int totalEvents = 0;
            foreach (var proc in processList)
            {
                for (int i = 0; i < tracks; i++)
                {
                    totalEvents += proc.EventBuffers[i].Count();
                    if (useColorEvents) totalEvents++;
                }
            }
            if (totalEvents == 0) totalEvents = 1;

            int writtenEvents = 0;

            using (var stream = new BufferedStream(File.Open(filename, FileMode.Create)))
            {
                MidiWriter writer = new MidiWriter(stream);
                writer.Init();
                writer.WriteFormat(1);
                writer.WritePPQ((ushort)ppq);
                writer.WriteNtrks((ushort)tracks);

                int tempo = 60000000 / midiBPM;

                for (int i = 0; i < tracks; i++)
                {
                    writer.InitTrack();
                    if (i == 0)
                    {
                        writer.Write(new TempoEvent(0, tempo));
                        writtenEvents++;
                    }

                    var absEvents = new List<(ulong absTick, MIDIEvent e)>();
                    ulong globalTick = (ulong)startOffset;
                    for (int frameIdx = 0; frameIdx < processList.Count; frameIdx++)
                    {
                        var proc = processList[frameIdx];
                        var eventBuffers = proc.EventBuffers;

                        if (useColorEvents)
                        {
                            var c = palette.Colors[i];
                            absEvents.Add((globalTick, new ColorEvent(0, 0, c.Red, c.Green, c.Blue, c.Alpha)));
                        }

                        ulong tick = globalTick;
                        foreach (MIDIEvent e in eventBuffers[i])
                        {
                            tick += e.DeltaTime * (ulong)ticksPerPixel;
                            absEvents.Add((tick, e.Clone()));
                        }

                        int frameHeight = proc.targetHeight;
                        globalTick += (ulong)(frameHeight * ticksPerPixel);
                    }

                    absEvents.Sort((a, b) => a.absTick.CompareTo(b.absTick));

                    ulong lastTick = 0;
                    foreach (var (absTick, e) in absEvents)
                    {
                        e.DeltaTime = (uint)(absTick - lastTick);
                        writer.Write(e);
                        lastTick = absTick;
                        writtenEvents++;
                        if ((writtenEvents & 0x3F) == 0 || writtenEvents == totalEvents)
                            reportProgress?.Invoke((double)writtenEvents / totalEvents);
                    }

                    writer.EndTrack();
                }
                writer.Close();
            }
            reportProgress?.Invoke(1.0);
        }
    }
}
