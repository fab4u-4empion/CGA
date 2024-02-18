using Rasterization;
using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace lab1.Effects
{
    using Smoothstep = (float A, float B);

    public struct Kernel
    {
        public int R;
        public float Intensity;
    }

    public class Bloom
    {
        public static string KernelImg = null;
        public static Kernel[] Kernels = [new(){ R = 1, Intensity = 1 }];
        public static Smoothstep Smoothstep = (0, 5);
        public static float Smoothing = 1;

        public static Buffer<Vector3> GetBoolmBuffer(Buffer<Vector3> src, int width, int height)
        {
            Buffer<Vector3> tmp = new(width, height);
            Parallel.For(0, width, (x) =>
            {
                for (int y = 0; y < height; y++)
                {
                    Vector3 color = src[x, y];
                    float luminance = 0.299f * color.X + 0.587f * color.Y + 0.114f * color.Z;
                    float X = float.Clamp((luminance - Smoothstep.A) / (Smoothstep.B - Smoothstep.A), 0, 1);
                    float factor = X * X * (3 - 2 * X);
                    tmp[x, y] = Vector3.Min(new(50f), color * factor);
                }
            });

            if (KernelImg == null)
            {
                return GetGaussianClassicBlur(tmp, width, height);
            }

            return GetImageBasedBlur(tmp, width, height);
        }

        private static (int, int) GetRealSize(int w, int h) {
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

        public static Buffer<Vector3> GetGaussianClassicBlur(Buffer<Vector3> src, int width, int height)
        {
            Buffer<Vector3> tmp1 = new(width, height);
            Buffer<Vector3> tmp2 = new(width, height);
            Buffer<Vector3> dest = new(width, height);
            
            foreach (Kernel kernel in Kernels)
            {
                int r = (int)(kernel.R * Smoothing);

                for (int b = 0; b < 4; b++)
                {
                    Buffer<Vector3> read = b == 0 ? src : tmp1;

                    Parallel.ForEach(Partitioner.Create(0, height), (range) =>
                    {
                        for (int y = range.Item1; y < range.Item2; y++)
                        {
                            tmp2[0, y] = Vector3.Zero;

                            for (int x = -r; x <= r; x++)
                                tmp2[0, y] += read[int.Clamp(x, 0, width - 1), y];

                            for (int x = 1; x < width; x++)
                                tmp2[x, y] = tmp2[x - 1, y] + read[int.Clamp(x + r, 0, width - 1), y] - read[int.Clamp(x - r - 1, 0, width - 1), y];
                        }
                    });

                    Parallel.ForEach(Partitioner.Create(0, width), (range) =>
                    {
                        for (int x = range.Item1; x < range.Item2; x++)
                        {
                            tmp1[x, 0] = Vector3.Zero;

                            for (int y = -r; y <= r; y++)
                                tmp1[x, 0] += tmp2[x, int.Clamp(y, 0, height - 1)];

                            for (int y = 1; y < height; y++)
                                tmp1[x, y] = tmp1[x, y - 1] + tmp2[x, int.Clamp(y + r, 0, height - 1)] - tmp2[x, int.Clamp(y - r - 1, 0, height - 1)];
                        }
                    });
                }

                float denom = kernel.Intensity / float.Pow(2 * r + 1, 2 * 4);

                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                        dest[x, y] += tmp1[x, y] * denom;
            }

            return dest;
        }

        public static Buffer<Vector3> GetImageBasedBlur(Buffer<Vector3> src, int width, int height)
        {
            (int w, int h) = GetRealSize(width, height);

            Pbgra32Bitmap kernel = new(new BitmapImage(new Uri($"{KernelImg}")));

            float[,] R = new float[w, h];
            float[,] G = new float[w, h];
            float[,] B = new float[w, h];

            float[,] Kr = new float[w, h];
            float[,] Kg = new float[w, h];
            float[,] Kb = new float[w, h];

            Vector3 sum = Vector3.Zero;

            Parallel.For(0, w, (i) =>
            {
                for (int j = 0; j < h; j++)
                {
                    R[i, j] = i < width && j < height ? src[i, j].X : 0;
                    G[i, j] = i < width && j < height ? src[i, j].Y : 0;
                    B[i, j] = i < width && j < height ? src[i, j].Z : 0;
                }
            });

            for (int i = 0; i < kernel.PixelWidth; i++)
                for (int j = 0; j <= kernel.PixelHeight; j++)
                    sum += kernel.GetPixel(i, j);

            for (int a = w / 2 - kernel.PixelWidth / 2, i = 0; i < kernel.PixelWidth; i++, a++)
            {
                for (int b = h / 2 - kernel.PixelHeight / 2, j = 0; j < kernel.PixelHeight; j++, b++)
                {
                    Vector3 color = kernel.GetPixel(i, j) / sum;
                    Kr[a, b] = color.X;
                    Kg[a, b] = color.Y;
                    Kb[a, b] = color.Z;
                }
            }

            Complex[,] Rc = FFT.DFFT_2D(R, w, h);
            Complex[,] Gc = FFT.DFFT_2D(G, w, h);
            Complex[,] Bc = FFT.DFFT_2D(B, w, h);

            Complex[,] Krc = FFT.DFFT_2D(Kr, w, h);
            Complex[,] Kgc = FFT.DFFT_2D(Kg, w, h);
            Complex[,] Kbc = FFT.DFFT_2D(Kb, w, h);

            Parallel.For(0, w, (i) =>
            {
                for (int j = 0; j < h; j++)
                {
                    Rc[i, j] *= Krc[i, j];
                    Gc[i, j] *= Kgc[i, j];
                    Bc[i, j] *= Kbc[i, j];
                }
            });

            Rc = FFT.IFFT_2D(Rc, w, h);
            Gc = FFT.IFFT_2D(Gc, w, h);
            Bc = FFT.IFFT_2D(Bc, w, h);

            Parallel.For(0, width, (i) =>
            {
                for (int j = 0; j < height; j++)
                {
                    src[i, j].X = float.Max((float)Rc[i, j].Magnitude, 0);
                    src[i, j].Y = float.Max((float)Gc[i, j].Magnitude, 0);
                    src[i, j].Z = float.Max((float)Bc[i, j].Magnitude, 0);
                }
            });

            return src;
        }
    }
}
