using MIDIModificationFramework;
using MIDIModificationFramework.MIDI_Events;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageToMidi
{
    class ConversionProcess
    {
        BitmapPalette Palette;
        byte[] imageData;
        int imageStride;
        bool cancelled = false;
        int maxNoteLength;
        bool measureFromStart;
        bool useMaxNoteLength = false;

        int startKey;
        int endKey;

        byte[] resizedImage;

        public Bitmap Image { get; private set; }

        FastList<MIDIEvent>[] EventBuffers;

        public ConversionProcess(BitmapPalette palette, byte[] imageData, int imgStride, int startKey, int endKey)
        {
            Palette = palette;
            this.imageData = imageData;
            int colors = palette.Colors.Count + 15;
            int tracks = (colors - (colors % 16)) / 16;
            this.startKey = startKey;
            this.endKey = endKey;
            imageStride = imgStride;
            EventBuffers = new FastList<MIDIEvent>[tracks];
            for (int i = 0; i < tracks; i++)
                EventBuffers[i] = new FastList<MIDIEvent>();
        }

        public ConversionProcess(BitmapPalette palette, byte[] imageData, int imgStride, int startKey, int endKey, bool measureFromStart, int maxNoteLength)
        {
            Palette = palette;
            this.imageData = imageData;
            int colors = palette.Colors.Count + 15;
            int tracks = (colors - (colors % 16)) / 16;
            this.startKey = startKey;
            this.endKey = endKey;
            imageStride = imgStride;
            EventBuffers = new FastList<MIDIEvent>[tracks];
            for (int i = 0; i < tracks; i++)
                EventBuffers[i] = new FastList<MIDIEvent>();
            useMaxNoteLength = true;
            this.measureFromStart = measureFromStart;
            this.maxNoteLength = maxNoteLength;
        }

        public Task RunProcessAsync(Action callback)
        {
            return Task.Run(() =>
            {
                MakeResizedImage();
                RunProcess();
                Image = GenerateImage();
                if (!cancelled) callback();
            });
        }

        void MakeResizedImage()
        {
            int W = imageStride / 4;
            int H = imageData.Length / imageStride;
            int newW = endKey - startKey;
            int newH = (int)((double)H / W * newW);

            resizedImage = new byte[newW * newH * 4];

            double relativeScale = 1 / 1;
            double relativePixelScale = relativeScale / newW * W;

            double left = 0;//(x2 - x1) * z1 / 5 * Size1 + Size1 / 2.0 - Size1 * relativeScale / 2.0;
            double top = 0;//(y2 - y1) * z1 / 5 * Size1 + Size1 / 2.0 - Size1 * relativeScale / 2.0;

            double divisor = 1 / relativeScale / relativeScale / W * newW / W * newW;

            Parallel.For(0, newW * newH, p =>
            {
                int x = p % newW;
                int y = (p - x) / newW;

                int x4 = x * 4;
                double pixelx = left + x * relativePixelScale;
                double pixely = top + y * relativePixelScale;

                double pixelx2 = pixelx + relativePixelScale;
                double pixely2 = pixely + relativePixelScale;

                int startx = (int)Math.Floor(pixelx);
                int starty = (int)Math.Floor(pixely);
                int endx = (int)Math.Floor(pixelx2);
                int endy = (int)Math.Floor(pixely2);

                if (pixelx2 == Math.Floor(pixelx2)) endx--;
                if (pixely2 == Math.Floor(pixely2)) endy--;

                {
                    double r = 0;
                    double g = 0;
                    double b = 0;
                    double a = 0;
                    for (int i = startx; i <= endx; i++)
                    {
                        int i4 = i * 4;
                        for (int j = starty; j <= endy; j++)
                        {
                            if (i < 0 || i >= W || j < 0 || j >= W) continue;
                            double footprintWidth = Math.Max(i, pixelx) - Math.Min(i + 1, pixelx2);
                            double footprintHeight = Math.Max(j, pixely) - Math.Min(j + 1, pixely2);

                            double effect = footprintHeight * footprintWidth * divisor;

                            int I1index = i4 + j * W * 4;
                            r += imageData[I1index] * effect;
                            g += imageData[I1index + 1] * effect;
                            b += imageData[I1index + 2] * effect;
                            a += imageData[I1index + 3] * effect;
                        }
                    }
                    int I2index = x4 + y * newW * 4;
                    resizedImage[I2index] = (byte)r;
                    resizedImage[I2index + 1] = (byte)g;
                    resizedImage[I2index + 2] = (byte)b;
                    resizedImage[I2index + 3] = (byte)a;
                }
            });
        }

        int GetColorID(int r, int g, int b)
        {
            int smallest = 0;
            bool first = true;
            int id = 0;
            for (int i = 0; i < Palette.Colors.Count; i++)
            {
                var col = Palette.Colors[i];
                int _r = col.R - r;
                int _g = col.G - g;
                int _b = col.B - b;
                int dist = _r * _r + _g * _g + _b * _b;
                if (dist < smallest || first)
                {
                    first = false;
                    smallest = dist;
                    id = i;
                }
            }
            return id;
        }

        public void Cancel()
        {
            cancelled = true;
            try
            {
                Image.Dispose();
            }
            catch { }
        }

        public void RunProcess()
        {
            int width = endKey - startKey;
            int height = resizedImage.Length / 4 / width;
            int tracks = (Palette.Colors.Count + 15 - ((Palette.Colors.Count + 15) % 16)) / 16;
            long[] lastTimes = new long[tracks];
            long[] lastOnTimes = new long[width];
            int[] colors = new int[width];
            long time = 0;
            for (int i = 0; i < width; i++) colors[i] = -1;
            for (int i = height - 1; i >= 0 && !cancelled; i--)
            {
                for (int j = 0; j < width; j++)
                {
                    int pixel = (i * width + j) * 4;
                    int c = colors[j];
                    int newc = GetColorID(resizedImage[pixel + 2], resizedImage[pixel + 1], resizedImage[pixel + 0]);
                    if (resizedImage[pixel + 3] < 128) newc = -2;
                    bool newNote = false;
                    if (useMaxNoteLength)
                    {
                        if (measureFromStart) newNote = (i % maxNoteLength == 0) && c != -1;
                        else newNote = (time - lastOnTimes[j]) >= maxNoteLength && c != -1;
                    }
                    if (newc != c || newNote)
                    {
                        int track, channel;
                        if (c != -1 && c != -2)
                        {
                            channel = c % 16;
                            track = (c - channel) / 16;
                            EventBuffers[track].Add(new NoteOffEvent((uint)(time - lastTimes[track]), (byte)channel, (byte)(j + startKey)));
                            lastTimes[track] = time;
                        }
                        colors[j] = newc;
                        c = newc;
                        if (c != -2)
                        {
                            channel = c % 16;
                            track = (c - channel) / 16;
                            EventBuffers[track].Add(new NoteOnEvent((uint)(time - lastTimes[track]), (byte)channel, (byte)(j + startKey), 1));
                            lastTimes[track] = time;
                            lastOnTimes[j] = time;
                        }
                    }
                }
                time++;
            }
            if (cancelled) return;
            for (int j = 0; j < width; j++)
            {
                int c = colors[j];
                if (c != -1 && c != -2)
                {
                    int channel = c % 16;
                    int track = (c - channel) / 16;
                    EventBuffers[track].Add(new NoteOffEvent((uint)(time - lastTimes[track]), (byte)channel, (byte)(j + startKey)));
                    lastTimes[track] = time;
                }
            }
            if (cancelled) return;
        }

        public Bitmap GenerateImage()
        {
            int width = endKey - startKey;
            int height = resizedImage.Length / 4 / width;
            int scale = 5;
            Bitmap img = new Bitmap(width * scale + 1, height * scale + 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics dg = Graphics.FromImage(img))
            {
                for (int i = 0; i < EventBuffers.Length; i++)
                {
                    foreach (Note n in new ExtractNotes(EventBuffers[i]))
                    {
                        var c = Palette.Colors[i * 16 + n.Channel];
                        var _c = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
                        using (var brush = new System.Drawing.SolidBrush(_c))
                            dg.FillRectangle(brush, (n.Key - startKey) * scale, height * scale - (int)n.End * scale, scale, (int)n.Length * scale);
                        using (var pen = new System.Drawing.Pen(System.Drawing.Color.Black))
                            dg.DrawRectangle(pen, (n.Key - startKey) * scale, height * scale - (int)n.End * scale, scale, (int)n.Length * scale);
                        if (cancelled) break;
                    }
                    if (cancelled) break;
                }
            }

            var src = img;
            return src;
        }

        public void WriteMidi(string filename, int ticksPerPixel, int ppq, int startOffset, bool useColorEvents)
        {
            int tracks = (Palette.Colors.Count + 15 - ((Palette.Colors.Count + 15) % 16)) / 16;
            MidiWriter writer = new MidiWriter(new BufferedStream(File.Open(filename, FileMode.Create)));
            writer.Init();
            writer.WriteFormat(1);
            writer.WritePPQ((ushort)ppq);
            writer.WriteNtrks((ushort)tracks);
            for (int i = 0; i < tracks; i++)
            {
                writer.InitTrack();
                if (useColorEvents)
                    for (byte j = 0; j < 16; j++)
                        if (i * 16 + j < Palette.Colors.Count)
                        {
                            var c = Palette.Colors[i * 16 + j];
                            writer.Write(new ColorEvent(0, j, c.R, c.G, c.B, c.A));
                        }

                uint o = (uint)startOffset;
                foreach (MIDIEvent e in EventBuffers[i])
                {
                    var _e = e.Clone();
                    _e.DeltaTime *= (uint)ticksPerPixel;
                    _e.DeltaTime += o;
                    o = 0;
                    writer.Write(_e);
                }
                writer.EndTrack();
            }
            writer.Close();
        }
    }
}
