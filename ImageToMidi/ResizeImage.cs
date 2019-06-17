using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageToMidi
{
    static class ResizeImage
    {
        public static byte[] MakeResizedImage(byte[] imageData, int imageStride, int newWidth)
        {
            int W = imageStride / 4;
            int H = imageData.Length / imageStride;
            int newW = newWidth;
            int newH = (int)((double)H / W * newW);

            byte[] resizedImage = new byte[newW * newH * 4];

            double relativeScale = 1 / 1;
            double relativePixelScale = relativeScale / newW * W;

            double left = 0;
            double top = 0;

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
                            if (i < 0 || i >= W || j < 0 || j >= H) continue;
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
            return resizedImage;
        }
    }
}
