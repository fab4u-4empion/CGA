using lab1.Effects;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Int32;
using static System.Numerics.Vector3;
using static System.Single;

namespace lab1
{
    public class HDRTexture
    {
        private Buffer<Vector3>? Source;

        public int Width { get; set; }
        public int Height { get; set; }

        public static float Angle { get; set; } = 0;

        public void Open(string filename)
        {
            static Vector3 RgbeToFloat(byte r, byte g, byte b, byte e)
            {
                float v = Pow(2, e - 128 - 8);
                float red = (r + 0.5f) * v;
                float green = (g + 0.5f) * v;
                float blue = (b + 0.5f) * v;
                return Create(red, green, blue);
            }

            Buffer<byte> rgbe = null!;

            using (StreamReader reader = new(filename, Encoding.Latin1))
            {
                while (!string.IsNullOrEmpty(reader.ReadLine())) ;
                string[] resolution = reader.ReadLine()!.Split(" ");
                Height = int.Parse(resolution[1]);
                Width = int.Parse(resolution[3]);
                rgbe = new(4 * Width, Height);

                for (int y = 0; y < rgbe.Height; y++)
                {
                    reader.Read();
                    reader.Read();
                    reader.Read();
                    reader.Read();

                    for (int x = 0; x < rgbe.Width;)
                    {
                        int length = reader.Read();

                        if (length > 128)
                        {
                            byte value = (byte)reader.Read();

                            while (length-- > 128)
                            {
                                rgbe[x++, y] = value;
                            }
                        }
                        else
                        {
                            while (length-- > 0)
                            {
                                rgbe[x++, y] = (byte)reader.Read();
                            }
                        }
                    }
                }
            }

            Source = new(Width, Height);

            Parallel.For(0, Height, y =>
            {
                for (int x = 0; x < Width; x++)
                {
                    byte r = rgbe[x, y];
                    byte g = rgbe[x + Width, y];
                    byte b = rgbe[x + 2 * Width, y];
                    byte e = rgbe[x + 3 * Width, y];
                    Source[x, y] = RgbeToFloat(r, g, b, e);
                }
            });
        }

        public Pbgra32Bitmap ToLDR()
        {
            Pbgra32Bitmap bmp = new(Width, Height);

            bmp.Source.Lock();

            Parallel.For(0, Height, y =>
            {
                for (int x = 0; x < Width; x++)
                    bmp.SetPixel(x, y, ToneMapping.LinearToSrgb(ToneMapping.AgX(Source![x, y])));
            });

            bmp.Source.AddDirtyRect(new(0, 0, Width, Height));
            bmp.Source.Unlock();

            return bmp;
        }

        public Vector3 GetColor(Vector3 N)
        {
            float theta = Acos(Clamp(N.Y, -1, 1));
            float phi = -Atan2(N.X, N.Z) + Angle;

            float x = phi / float.Tau * Width - 0.5f;
            float y = theta / float.Pi * Height - 0.5f;

            int x0 = (int)Floor(x);
            int y0 = (int)Floor(y);

            int x1 = x0 + 1;
            int y1 = y0 + 1;

            float x_ratio = x - x0;
            float y_ratio = y - y0;

            x0 &= (Source!.Width - 1);
            x1 &= (Source!.Width - 1);

            y0 = Max(0, y0);
            y1 = Min(Source.Height - 1, y1);

            return Lerp(
                Lerp(Source[x0, y0], Source[x1, y0], x_ratio),
                Lerp(Source[x0, y1], Source[x1, y1], x_ratio),
                y_ratio
            );
        }

        public Vector3 GetColor(float u, float v)
        {
            float x = u * Source!.Width - 0.5f;
            float y = v * Source!.Height - 0.5f;

            int x0 = (int)Floor(x);
            int y0 = (int)Floor(y);

            int x1 = x0 + 1;
            int y1 = y0 + 1;

            float x_ratio = x - x0;
            float y_ratio = y - y0;

            x0 = Max(0, x0); ;
            x1 = Min(Source.Width - 1, x1);

            y0 = Max(0, y0);
            y1 = Min(Source.Height - 1, y1);

            return Lerp(
                Lerp(Source[x0, y0], Source[x1, y0], x_ratio),
                Lerp(Source[x0, y1], Source[x1, y1], x_ratio),
                y_ratio
            );
        }
    }
}