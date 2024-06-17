using Rasterization;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using static System.Single;
using static System.Int32;
using static System.Numerics.Vector3;

namespace lab1
{
    public class HDRTexture
    {
        private Buffer<Vector3>? Source;

        public int Width { get; set; }
        public int Height { get; set; }

        public static float Angle { get; set; } = 0;

        private static string ReadLine(BinaryReader reader)
        {
            StringBuilder sb = new();

            while (reader.ReadChar() is char c && c != '\n')
                sb.Append(c);

            return sb.ToString();
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
            using Stream stream = File.OpenRead(filename);
            using BinaryReader reader = new(stream, Encoding.Default, true);

            ReadLine(reader);

            int[] size = ReadLine(reader)
                .Split(" ")
                .Select(e => int.Parse(e))
                .ToArray();
            Source = new(size[0], size[1]);
            (Width, Height) = (size[0], size[1]);

            float order = float.Parse(ReadLine(reader), CultureInfo.InvariantCulture);

            for (int y = Height - 1; y > -1; y--)
            {
                for (int x = 0; x < Width; x++)
                {
                    float r = reader.ReadSingle();
                    float g = reader.ReadSingle();
                    float b = reader.ReadSingle();

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

            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    bmp.SetPixel(x, y, ToneMapping.LinearToSrgb(ToneMapping.AcesFilmic(Source![x, y])));

            bmp.Source.AddDirtyRect(new(0, 0, Width, Height));
            bmp.Source.Unlock();

            return bmp;
        }

        public Vector3 GetColor(Vector3 N)
        {
            float theta = Acos(Clamp(N.Y, -1, 1));
            float phi = Atan2(N.X, -N.Z) + Pi + Angle;

            float x = phi / (2 * Pi) * Width - 0.5f;
            float y = theta / Pi * Height - 0.5f;

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
