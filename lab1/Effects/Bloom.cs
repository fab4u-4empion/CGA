using Rasterization;
using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace lab1.Effects
{
    public class Bloom
    {
        public static string KernelImg = null;
        public static int KernelCount = 1;

        public static Vector3[,] GetBoolmBuffer(int r, Vector3[,] src, int width, int height)
        {
            Vector3[,] tmp = new Vector3[width, height];
            Parallel.For(0, width, (x) =>
            {
                for (int y = 0; y < height; y++)
                {
                    Vector3 color = src[x, y];
                    float luminance = 0.299f * color.X + 0.587f * color.Y + 0.114f * color.Z;
                    float X = float.Clamp(luminance / 5f, 0, 1);
                    float factor = X * X * (3 - 2 * X);
                    tmp[x, y] = color * factor;
                }
            });

            if (KernelImg == null)
            {
                return GetGaussianClassicBlur(r, tmp, width, height);
            }

            return GetImageBasedBlur(tmp, width, height);
        }

        private static (int, int) GetRealSize(int w, int h) {
            int rW = w - 1;
            int rH = h - 1;

            rW = rW | (rW >> 1);
            rW = rW | (rW >> 2);
            rW = rW | (rW >> 4);
            rW = rW | (rW >> 8);
            rW = rW | (rW >> 16);
            rW = rW | (rW >> 32);

            rH = rH | (rH >> 1);
            rH = rH | (rH >> 2);
            rH = rH | (rH >> 4);
            rH = rH | (rH >> 8);
            rH = rH | (rH >> 16);
            rH = rH | (rH >> 32);

            return (rW + 1, rH + 1);
        }

        public static Vector3[,] GetGaussianClassicBlur(int r, Vector3[,] src, int width, int height)
        {
            Vector3[,] resultTMP = new Vector3[width, height];
            Vector3[,] result = new Vector3[width, height];

            float sigma;
            float factor = 1f;
            
            for (int a = 0; a < KernelCount; a++)
            {
                sigma = (r * 2f) / 6f;

                float[] g = new float[r * 2 + 1];

                for (int i = -r; i <= r; i++)
                {
                    g[i + r] = float.Exp(-1 * i * i / (2 * sigma * sigma)) / float.Sqrt(2 * float.Pi * sigma * sigma);
                }
                Parallel.For(0, width, (x) =>
                {
                    for (int y = 0; y < height; y++)
                    {
                        Vector3 sum = Vector3.Zero;
                        for (int i = x - r; i <= x + r; i++)
                        {
                            sum += src[
                                int.Min(int.Max(i, 0), width - 1),
                                y
                            ] * g[i + r - x];
                        }
                        resultTMP[x, y] = sum;
                    }
                });

                Parallel.For(0, width, (x) =>
                {
                    for (int y = 0; y < height; y++)
                    {
                        Vector3 sum = Vector3.Zero;
                        for (int i = y - r; i <= y + r; i++)
                        {
                            sum += resultTMP[
                            x,
                                int.Min(int.Max(i, 0), height - 1)
                            ] * g[i + r - y];
                        }
                        result[x, y] += sum * factor;
                    }
                });

                r *= 4;
                factor *= 0.75f;
            }

            return result;
        }

        public static Vector3[,] GetImageBasedBlur(Vector3[,] src, int width, int height)
        {
            (int w, int h) = GetRealSize(width, height);

            Pbgra32Bitmap kernel = new(new BitmapImage(new Uri($"{KernelImg}")));

            float[,] R = new float[w, h];
            float[,] G = new float[w, h];
            float[,] B = new float[w, h];
            float[,] K = new float[w, h];

            Parallel.For(0, w, (i) =>
            {
                for (int j = 0; j < h; j++)
                {
                    R[i, j] = i < width && j < height ? src[i, j].X : 0;
                    G[i, j] = i < width && j < height ? src[i, j].Y : 0;
                    B[i, j] = i < width && j < height ? src[i, j].Z : 0;
                }
            });



            for (int a = w / 2 - kernel.PixelWidth / 2, i = 0; i < kernel.PixelWidth; i++, a++)
            {
                for (int b = h / 2 - kernel.PixelHeight / 2, j = 0; j < kernel.PixelHeight; j++, b++)
                {
                    K[a, b] = kernel.GetPixel(i, j).X / kernel.PixelHeight;
                }
            }

            Complex[,] Rc = FFT.DFFT_2D(R, w, h);
            Complex[,] Gc = FFT.DFFT_2D(G, w, h);
            Complex[,] Bc = FFT.DFFT_2D(B, w, h);
            Complex[,] Kc = FFT.DFFT_2D(K, w, h);

            Parallel.For(0, w, (i) =>
            {
                for (int j = 0; j < h; j++)
                {
                    Rc[i, j] *= Kc[i, j];
                    Gc[i, j] *= Kc[i, j];
                    Bc[i, j] *= Kc[i, j];
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







//float[,] kernel = new float[2 * r + 1, 2 * r + 1];

//float sigma = (r * 2f) / 6f;

//for (int i = -r; i <= r; i++)
//{
//    for (int j = -r; j <= r; j++)
//    {
//        kernel[i + r, j + r] = float.Exp(-1 * (i * i + j * j) / (2 * sigma * sigma)) / (2 * float.Pi * sigma * sigma);
//    }
//}

//float[,] kernel = { { 1 } };

//loat[,] kernel = { { 1f, 2f, 1f }, { 2f, 4f, 2f }, { 1f, 2f, 1f } };





//for (int a = w / 2 - kernel.GetLength(0) / 2, i = 0; i < kernel.GetLength(0); i++, a++)
//{
//    for (int b = h / 2 - kernel.GetLength(1) / 2, j = 0; j < kernel.GetLength(1); j++, b++)
//    {
//        K[a, b] = kernel[i, j];
//    }
//}
