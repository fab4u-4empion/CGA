using System.Numerics;
using System.Threading.Tasks;
using static System.Double;
using static System.Numerics.Complex;

namespace lab1
{
    public class FFT
    {
        private static Complex Fw(int k, int N)
        {
            if (k % N == 0) return 1;

            double arg = -2 * Pi * k / N;

            return new Complex(Cos(arg), Sin(arg));
        }

        public static Complex[] DFFT(Complex[] x)
        {
            Complex[] X;
            int N = x.Length;
            if (N == 2)
            {
                X = new Complex[2];
                X[0] = x[0] + x[1];
                X[1] = x[0] - x[1];
            }
            else
            {
                Complex[] x_even = new Complex[N / 2];
                Complex[] x_odd = new Complex[N / 2];

                for (int i = 0; i < N / 2; i++)
                {
                    x_even[i] = x[2 * i];
                    x_odd[i] = x[2 * i + 1];
                }

                Complex[] X_even = DFFT(x_even);
                Complex[] X_odd = DFFT(x_odd);

                X = new Complex[N];

                for (int i = 0; i < N / 2; i++)
                {
                    X[i] = X_even[i] + Fw(i, N) * X_odd[i];
                    X[i + N / 2] = X_even[i] - Fw(i, N) * X_odd[i];
                }
            }
            return X;
        }

        public static Complex[,] DFFT_2D(float[,] data, int W, int H)
        {
            Complex[,] X = new Complex[H, W];

            Parallel.For(0, W, (w) =>
            {
                Complex[] temp = new Complex[H];

                for (int h = 0; h < H; h++)
                    temp[h] = data[h, w];

                Complex[] tempFT = NFFT(DFFT(temp));

                for (int h = 0; h < H; h++)
                    X[h, w] = tempFT[h];
            });

            Parallel.For(0, H, (h) =>
            {
                Complex[] temp = new Complex[W];

                for (int w = 0; w < W; w++)
                    temp[w] = X[h, w];

                Complex[] tempFT = NFFT(DFFT(temp));

                for (int w = 0; w < W; w++)
                    X[h, w] = tempFT[w];
            });

            return X;
        }

        public static Complex[,] IFFT_2D(Complex[,] data, int W, int H)
        {
            Complex[,] x = new Complex[H, W];

            Parallel.For(0, W, (w) =>
            {
                Complex[] temp = new Complex[H];

                for (int h = 0; h < H; h++)
                    temp[h] = Conjugate(data[h, w]);

                Complex[] tempFT = NFFT(DFFT(temp));

                for (int h = 0; h < H; h++)
                    x[h, w] = Conjugate(tempFT[h]) / H;
            });

            Parallel.For(0, H, (h) =>
            {
                Complex[] temp = new Complex[W];

                for (int w = 0; w < W; w++)
                    temp[w] = Conjugate(x[h, w]);

                Complex[] tempFT = NFFT(DFFT(temp));

                for (int w = 0; w < W; w++)
                    x[h, w] = Conjugate(tempFT[w]) / W;
            });

            return x;
        }

        public static Complex[] NFFT(Complex[] X)
        {
            int N = X.Length;
            Complex[] X_n = new Complex[N];

            for (int i = 0; i < N / 2; i++)
            {
                X_n[i] = X[N / 2 + i];
                X_n[N / 2 + i] = X[i];
            }

            return X_n;
        }
    }
}
