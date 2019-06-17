using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageToMidi
{
    static class Clusterisation
    {
        public static BitmapPalette Clusterise(BitmapPalette palette, byte[] image, int iterations)
        {
            Random rand = new Random();
            double rate = 0.3;
            double[][] positions = new double[palette.Colors.Count][];
            for (int i = 0; i < palette.Colors.Count; i++)
                positions[i] = new double[3];
            double[,] means = new double[palette.Colors.Count, 3];
            int[] pointCounts = new int[palette.Colors.Count];
            for (int i = 0; i < pointCounts.Length; i++)
            {
                positions[i][0] = palette.Colors[i].R;
                positions[i][1] = palette.Colors[i].G;
                positions[i][2] = palette.Colors[i].B;
            }
            for (int iter = 0; iter < iterations; iter++)
            {
                for (int i = 0; i < image.Length; i += 4)
                {
                    double min = 0;
                    bool first = true;
                    int minid = 0;
                    double r = image[i + 2];
                    double g = image[i + 1];
                    double b = image[i + 0];
                    if (image[i + 3] > 128)
                        for (int c = 0; c < pointCounts.Length; c++)
                        {
                            double _r = r - positions[c][0];
                            double _g = g - positions[c][1];
                            double _b = b - positions[c][2];
                            double distsqr = _r * _r + _g * _g + _b * _b;
                            if (distsqr < min || first)
                            {
                                min = distsqr;
                                first = false;
                                minid = c;
                            }
                        }
                    int count = pointCounts[minid];
                    means[minid, 0] = (means[minid, 0] * count + r) / (count + 1);
                    means[minid, 1] = (means[minid, 1] * count + g) / (count + 1);
                    means[minid, 2] = (means[minid, 2] * count + b) / (count + 1);
                    pointCounts[minid]++;
                }
                for (int i = 0; i < pointCounts.Length; i++)
                {
                    for (int c = 0; c < 3; c++)
                        positions[i][c] = positions[i][c] * (1 - rate) + means[i, c] * rate;
                }
                for (int i = 0; i < pointCounts.Length; i++)
                {
                    if (pointCounts[i] == 0)
                    {
                        int p = rand.Next(image.Length / 4);
                        positions[i][0] = image[p * 4 + 2];
                        positions[i][1] = image[p * 4 + 1];
                        positions[i][2] = image[p * 4 + 0];
                    }
                }
            }
            var newcol = new List<Color>();
            Array.Sort(pointCounts, positions);
            Array.Reverse(positions);
            for (int i = 0; i < pointCounts.Length; i++)
            {
                newcol.Add(Color.FromRgb((byte)positions[i][0], (byte)positions[i][1], (byte)positions[i][2]));
            }
            return new BitmapPalette(newcol);
        }
    }
}
