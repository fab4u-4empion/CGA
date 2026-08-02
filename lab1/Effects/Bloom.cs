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
        public static Buffer<Vector3> GetBloomBuffer(Buffer<Vector3> src, int width, int height, float scaling)
        {
            if (BloomConfig.Kernels.Count == 0 && BloomConfig.KernelImg == null)
                return new(width, height);

            if (BloomConfig.KernelImg == null)
            {
                return GetGaussianClassicBlur(src, width, height, scaling);
            }

            return GetImageBasedBlur(src, width, height);
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

        private static Buffer<Vector3> GetGaussianClassicBlur(Buffer<Vector3> src, int width, int height, float scaling)
        {
            Buffer<Vector3> tmp1 = new(width, height);
            Buffer<Vector3> tmp2 = new(width, height);
            Buffer<Vector3> dest = new(width, height);

            foreach (Kernel kernel in BloomConfig.Kernels)
            {
                int r = (int)(kernel.Radius * scaling + 0.5f);

                Buffer<Vector3> read = src;
                Buffer<Vector3> write = tmp1;

                for (int pass = 0; pass < 4; pass++)
                {
                    Parallel.ForEach(Partitioner.Create(0, height), range =>
                    {
                        for (int y = range.Item1; y < range.Item2; y++)
                        {
                            Span<Vector3> readRow = read.GetRow(y);
                            Span<Vector3> writeRow = write.GetRow(y);

                            writeRow[0] = (r + 1) * readRow[0];

                            for (int x = 1; x <= r; x++)
                                writeRow[0] += readRow[Min(x, width - 1)];

                            for (int x = 1; x < width; x++)
                                writeRow[x] = writeRow[x - 1] + readRow[Min(x + r, width - 1)] - readRow[Max(x - r - 1, 0)];
                        }
                    });

                    read = write;
                    write = tmp2;

                    Parallel.ForEach(Partitioner.Create(0, width), range =>
                    {
                        Span<Vector3> readRow0 = read.GetRow(0);
                        Span<Vector3> writeRow0 = write.GetRow(0);

                        for (int x = range.Item1; x < range.Item2; x++)
                        {
                            writeRow0[x] = (r + 1) * readRow0[x];
                        }

                        for (int y = 1; y <= r; y++)
                        {
                            Span<Vector3> readRow = read.GetRow(Min(y, height - 1));

                            for (int x = range.Item1; x < range.Item2; x++)
                            {
                                writeRow0[x] += readRow[x];
                            }
                        }

                        for (int y = 1; y < height; y++)
                        {
                            Span<Vector3> readRow1 = read.GetRow(Min(y + r, height - 1));
                            Span<Vector3> readRow2 = read.GetRow(Max(y - r - 1, 0));
                            Span<Vector3> writeRow1 = write.GetRow(y);
                            Span<Vector3> writeRow2 = write.GetRow(y - 1);

                            for (int x = range.Item1; x < range.Item2; x++)
                            {
                                writeRow1[x] = writeRow2[x] + readRow1[x] - readRow2[x];
                            }
                        }
                    });

                    read = write;
                    write = tmp1;
                }

                float factor = kernel.Intensity / Pow(2 * r + 1, 2 * 4);

                Parallel.ForEach(Partitioner.Create(0, src.Length), range =>
                {
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        dest[i] += tmp2[i] * factor;
                    }
                });
            }

            return dest;
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

            Buffer<Vector3> dest = new(width, height);

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