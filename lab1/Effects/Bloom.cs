using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading.Tasks;
using static System.Int32;
using static System.Numerics.Vector3;
using static System.Single;

namespace lab1.Effects
{
    public static class Bloom
    {
        private static Buffer<Vector3> tmp1 = null!;
        private static Buffer<Vector3> tmp2 = null!;
        private static Buffer<Vector3> dest = null!;

        public static void CreateBuffers(int width, int height)
        {
            tmp1 = new(width, height);
            tmp2 = new(width, height);
            dest = new(width, height);
        }

        public static Buffer<Vector3> GetBloomBuffer(Buffer<Vector3> src, int width, int height, float scaling)
        {
            Parallel.ForEach(Partitioner.Create(0, height), range =>
            {
                for (int y = range.Item1; y < range.Item2; y++)
                {
                    dest.GetRow(y).Clear();
                }
            });

            if (BloomConfig.Kernels.Count == 0 && BloomConfig.KernelImg == null)
                return dest;

            if (BloomConfig.KernelImg == null)
            {
                return GetGaussianClassicBlur(src, width, height, scaling);
            }

            return GetImageBasedBlur(src, width, height);
        }

        private static Buffer<Vector3> GetGaussianClassicBlur(Buffer<Vector3> src, int width, int height, float scaling, int passes = 4)
        {
            foreach (Kernel kernel in BloomConfig.Kernels)
            {
                int radius = (int)Ceiling(kernel.Radius * scaling);
                float factor = kernel.Intensity / Pow(2 * radius + 1, 2 * passes);

                Buffer<Vector3> read = src;
                Buffer<Vector3> write = tmp1;

                for (int pass = 0; pass < passes; pass++)
                {
                    Parallel.ForEach(Partitioner.Create(0, height), range =>
                    {
                        for (int y = range.Item1; y < range.Item2; y++)
                        {
                            Span<Vector3> readRow = read.GetRow(y);
                            Span<Vector3> writeRow = write.GetRow(y);

                            writeRow.Clear();

                            int xMax = Min(radius, width - 1);

                            for (int x = 0; x <= xMax; x++)
                            {
                                writeRow[0] += readRow[x];
                            }

                            for (int x = 1; x < width; x++)
                            {
                                int x1 = x - radius - 1;
                                int x2 = x + radius;

                                Vector3 left = x1 >= 0 ? readRow[x1] : Zero;
                                Vector3 right = x2 < width ? readRow[x2] : Zero;

                                writeRow[x] = writeRow[x - 1] - left + right;
                            }
                        }
                    });

                    read = write;
                    write = tmp2;

                    Parallel.ForEach(Partitioner.Create(0, width), range =>
                    {
                        write.GetRow(0)[range.Item1..range.Item2].Clear();

                        int yMax = Min(radius, height - 1);

                        for (int y = 0; y <= yMax; y++)
                        {
                            Span<Vector3> readRow = read.GetRow(y);

                            for (int x = range.Item1; x < range.Item2; x++)
                            {
                                write[x] += readRow[x];
                            }
                        }

                        for (int y = 1; y < height; y++)
                        {
                            int y1 = y - radius - 1;
                            int y2 = y + radius;

                            Span<Vector3> readRow1 = y1 >= 0 ? read.GetRow(y1) : [];
                            Span<Vector3> readRow2 = y2 < height ? read.GetRow(y2) : [];
                            Span<Vector3> writeRow1 = write.GetRow(y);
                            Span<Vector3> writeRow2 = write.GetRow(y - 1);

                            for (int x = range.Item1; x < range.Item2; x++)
                            {
                                Vector3 top = readRow1.IsEmpty ? Zero : readRow1[x];
                                Vector3 bottom = readRow2.IsEmpty ? Zero : readRow2[x];

                                writeRow1[x] = writeRow2[x] - top + bottom;
                            }
                        }
                    });

                    read = write;
                    write = tmp1;
                }

                Parallel.ForEach(Partitioner.Create(0, height), range =>
                {
                    for (int y = range.Item1; y < range.Item2; y++)
                    {
                        Span<Vector3> readRow = read.GetRow(y);
                        Span<Vector3> destRow = dest.GetRow(y);

                        for (int x = 0; x < width; x++)
                        {
                            destRow[x] += readRow[x] * factor;
                        }
                    }
                });
            }

            return dest;
        }

        private static (int, int) GetRealSize(int w, int h)
        {
            int rW = w - 1;
            int rH = h - 1;

            rW |= (rW >> 1);
            rW |= (rW >> 2);
            rW |= (rW >> 4);
            rW |= (rW >> 8);
            rW |= (rW >> 16);
            rW |= (rW >> 32);

            rH |= (rH >> 1);
            rH |= (rH >> 2);
            rH |= (rH >> 4);
            rH |= (rH >> 8);
            rH |= (rH >> 16);
            rH |= (rH >> 32);

            return (rW + 1, rH + 1);
        }

        private static Buffer<Vector3> GetImageBasedBlur(Buffer<Vector3> src, int width, int height)
        {
            Pbgra32Bitmap kernel = BloomConfig.KernelImg!;

            (int wp, int hp) = GetRealSize(width, height);
            (int wk, int hk) = GetRealSize(kernel.PixelWidth, kernel.PixelHeight);

            (int w, int h) = (Max(wp, wk), Max(hp, hk));

            float[,] R = new float[h, w];
            float[,] G = new float[h, w];
            float[,] B = new float[h, w];

            float[,] Kr = new float[h, w];
            float[,] Kg = new float[h, w];
            float[,] Kb = new float[h, w];

            Vector3 sum = Zero;

            Parallel.For(0, h, (j) =>
            {
                for (int i = 0; i < w; i++)
                {
                    R[j, i] = i < width && j < height ? src[i, j].X : 0;
                    G[j, i] = i < width && j < height ? src[i, j].Y : 0;
                    B[j, i] = i < width && j < height ? src[i, j].Z : 0;
                }
            });

            for (int j = 0; j < kernel.PixelHeight; j++)
                for (int i = 0; i < kernel.PixelWidth; i++)
                    sum += kernel.GetPixel(i, j);

            for (int b = h / 2 - kernel.PixelHeight / 2, j = 0; j < kernel.PixelHeight; j++, b++)
            {
                for (int a = w / 2 - kernel.PixelWidth / 2, i = 0; i < kernel.PixelWidth; i++, a++)
                {
                    Vector3 color = kernel.GetPixel(i, j) / sum;
                    Kr[b, a] = color.X;
                    Kg[b, a] = color.Y;
                    Kb[b, a] = color.Z;
                }
            }

            Complex[,] Rc = FFT.DFFT_2D(R, w, h);
            Complex[,] Gc = FFT.DFFT_2D(G, w, h);
            Complex[,] Bc = FFT.DFFT_2D(B, w, h);

            Complex[,] Krc = FFT.DFFT_2D(Kr, w, h);
            Complex[,] Kgc = FFT.DFFT_2D(Kg, w, h);
            Complex[,] Kbc = FFT.DFFT_2D(Kb, w, h);

            Parallel.For(0, h, (j) =>
            {
                for (int i = 0; i < w; i++)
                {
                    Rc[j, i] *= Krc[j, i];
                    Gc[j, i] *= Kgc[j, i];
                    Bc[j, i] *= Kbc[j, i];
                }
            });

            Rc = FFT.IFFT_2D(Rc, w, h);
            Gc = FFT.IFFT_2D(Gc, w, h);
            Bc = FFT.IFFT_2D(Bc, w, h);

            Parallel.For(0, height, (j) =>
            {
                for (int i = 0; i < width; i++)
                {
                    dest[i, j].X = Max((float)Rc[j, i].Magnitude, 0);
                    dest[i, j].Y = Max((float)Gc[j, i].Magnitude, 0);
                    dest[i, j].Z = Max((float)Bc[j, i].Magnitude, 0);
                }
            });

            return dest;
        }
    }
}