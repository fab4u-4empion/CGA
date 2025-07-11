﻿using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading.Tasks;
using static System.Int32;
using static System.Numerics.Vector3;
using static System.Single;

namespace lab1.Effects
{
    public class Bloom
    {
        public static Buffer<Vector3> GetBloomBuffer(Buffer<Vector3> src, int width, int height, float scaling)
        {
            if (BloomConfig.Kernels.Count == 0 && BloomConfig.KernelImg == null)
                return new(width, height);

            Buffer<Vector3> tmp = new(width, height);
            Array.Copy(src, tmp, src.Length);

            if (BloomConfig.KernelImg == null)
            {
                return GetGaussianClassicBlur(tmp, width, height, scaling);
            }

            return GetImageBasedBlur(tmp, width, height);
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

        public static Buffer<Vector3> GetGaussianClassicBlur(Buffer<Vector3> src, int width, int height, float scaling)
        {
            Buffer<Vector3> tmp1 = new(width, height);
            Buffer<Vector3> tmp2 = new(width, height);
            Buffer<Vector3> dest = new(width, height);

            foreach (Kernel kernel in BloomConfig.Kernels)
            {
                int r = (int)(kernel.Radius * scaling);

                for (int b = 0; b < 4; b++)
                {
                    Buffer<Vector3> read = b == 0 ? src : tmp1;

                    Parallel.ForEach(Partitioner.Create(0, height), (range) =>
                    {
                        for (int y = range.Item1; y < range.Item2; y++)
                        {
                            tmp2[0, y] = Zero;

                            for (int x = -r; x <= r; x++)
                                tmp2[0, y] += read[Clamp(x, 0, width - 1), y];

                            for (int x = 1; x < width; x++)
                                tmp2[x, y] = tmp2[x - 1, y] + read[Clamp(x + r, 0, width - 1), y] - read[Clamp(x - r - 1, 0, width - 1), y];
                        }
                    });

                    Parallel.ForEach(Partitioner.Create(0, width), (range) =>
                    {
                        for (int x = range.Item1; x < range.Item2; x++)
                        {
                            tmp1[x, 0] = Zero;

                            for (int y = -r; y <= r; y++)
                                tmp1[x, 0] += tmp2[x, Clamp(y, 0, height - 1)];

                            for (int y = 1; y < height; y++)
                                tmp1[x, y] = tmp1[x, y - 1] + tmp2[x, Clamp(y + r, 0, height - 1)] - tmp2[x, Clamp(y - r - 1, 0, height - 1)];
                        }
                    });
                }

                float denom = kernel.Intensity / Pow(2 * r + 1, 2 * 4);

                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        dest[x, y] += tmp1[x, y] * denom;
            }

            return dest;
        }

        public static Buffer<Vector3> GetImageBasedBlur(Buffer<Vector3> src, int width, int height)
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

            for (int j = 0; j <= kernel.PixelHeight; j++)
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
                    src[i, j].X = Max((float)Rc[j, i].Magnitude, 0);
                    src[i, j].Y = Max((float)Gc[j, i].Magnitude, 0);
                    src[i, j].Z = Max((float)Bc[j, i].Magnitude, 0);
                }
            });

            return src;
        }
    }
}
