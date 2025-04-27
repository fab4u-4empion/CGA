using lab1.Effects;
using Rasterization;
using System;
using System.Collections.Concurrent;
using System.Globalization;
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

        private static float ReadSingle(StreamReader reader)
        {
            return BitConverter.ToSingle(
            [
                (byte)reader.Read(),
                (byte)reader.Read(),
                (byte)reader.Read(),
                (byte)reader.Read()
            ]);
        }

        private static unsafe float ChangeEndianness(float f)
        {
            uint i = *(uint*)&f;
            i = (i & 0x000000FFU) << 24 | (i & 0x0000FF00U) << 8 |
                (i & 0x00FF0000U) >> 8 | (i & 0xFF000000U) >> 24;
            return *(float*)&i;
        }

        public void Open(string filename)
        {
            using StreamReader reader = new(filename, Encoding.Latin1);

            reader.ReadLine();

            string[] size = reader.ReadLine()!.Split(" ");
            Width = int.Parse(size[0]);
            Height = int.Parse(size[1]);
            Source = new(Width, Height);

            float order = float.Parse(reader.ReadLine()!, CultureInfo.InvariantCulture);

            for (int y = Height - 1; y >= 0; y--)
            {
                for (int x = 0; x <= Width - 1; x++)
                {
                    float r = ReadSingle(reader);
                    float g = ReadSingle(reader);
                    float b = ReadSingle(reader);

                    if (order == 1)
                    {
                        r = ChangeEndianness(r);
                        g = ChangeEndianness(g);
                        b = ChangeEndianness(b);
                    }

                    Source[x, y] = new(r, g, b);
                }
            }
        }

        public Pbgra32Bitmap ToLDR()
        {
            Pbgra32Bitmap bmp = new(Width, Height);

            bmp.Source.Lock();

            Parallel.ForEach(Partitioner.Create(0, Width), range =>
            {
                for (int x = range.Item1; x < range.Item2; x++)
                    for (int y = 0; y < Height; y++)
                        bmp.SetPixel(x, y, ToneMapping.LinearToSrgb(ToneMapping.AgX(Source![x, y])));
            });

            bmp.Source.AddDirtyRect(new(0, 0, Width, Height));
            bmp.Source.Unlock();

            return bmp;
        }

        public Vector3 GetColor(Vector3 N)
        {
            float theta = Acos(Clamp(N.Y, -1, 1));
            float phi = Atan2(N.X, -N.Z) + float.Pi + Angle;

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
